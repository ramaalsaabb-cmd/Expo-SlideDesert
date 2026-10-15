using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEditor;

// Editor-only geometry queries; no colliders or navigation objects are saved in the scene.
public sealed class DesertEntranceRouting : IDisposable
{
    bool committed;
    readonly Dictionary<AnimationClip,List<(EditorCurveBinding binding,AnimationCurve curve)>> originals=new Dictionary<AnimationClip,List<(EditorCurveBinding,AnimationCurve)>>();
    public void Commit(){committed=true;}
    readonly List<GameObject> temporary=new List<GameObject>();
    readonly Dictionary<MeshCollider,MeshFilter> sources=new Dictionary<MeshCollider,MeshFilter>();
    readonly List<Collider> obstacles=new List<Collider>();
    readonly Bounds tentBounds;
    readonly List<Vector3> assignedSeats=new List<Vector3>();
    readonly List<Vector3> occupiedSeats=new List<Vector3>();
    readonly Vector3 carCentre;
    readonly Transform shellTransform;
    readonly Bounds shellLocalBounds;
    readonly float floor;
    readonly System.Diagnostics.Stopwatch timer=System.Diagnostics.Stopwatch.StartNew();
    const float Grid=.30f;
    float Radius=.34f;
    public DesertEntranceRouting(Transform tent,Transform car,Transform sled,Scene scene)
    {
        // The imported Tent object is a container for ten separately named architecture prefabs.
        var shell=tent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t!=tent&&t.name=="Tent");
        var shellRenderers=(shell?shell:tent).GetComponentsInChildren<MeshRenderer>(false)
            .Where(r=>r.enabled&&r.GetComponent<MeshFilter>()&&r.GetComponent<MeshFilter>().sharedMesh).ToArray();
        if(shellRenderers.Length==0)throw new Exception("No renderable tent architecture found under "+tent.name);
        var shellBounds=shellRenderers[0].bounds;
        foreach(var renderer in shellRenderers.Skip(1))shellBounds.Encapsulate(renderer.bounds);
        tentBounds=shellBounds;floor=tentBounds.min.y;
        shellTransform=shell?shell:tent;
        bool firstVertex=true;var localBounds=new Bounds();
        foreach(var r in shellRenderers)
        {
            var filter=r.GetComponent<MeshFilter>();var b=filter.sharedMesh.bounds;
            for(int corner=0;corner<8;corner++)
            {
                var v=b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                v=shellTransform.InverseTransformPoint(filter.transform.TransformPoint(v));
                if(firstVertex){localBounds=new Bounds(v,Vector3.zero);firstVertex=false;}else localBounds.Encapsulate(v);
            }
        }
        shellLocalBounds=localBounds;
        var carRenderers=car.GetComponentsInChildren<MeshRenderer>(false).Where(r=>r.enabled).ToArray();
        if(carRenderers.Length==0)throw new Exception("No visible car geometry found.");
        var carBounds=carRenderers[0].bounds;
        foreach(var renderer in carRenderers.Skip(1))carBounds.Encapsulate(renderer.bounds);
        carCentre=carBounds.center;
        foreach(var root in new[]{tent,car,sled})
        {
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(false))
            {
                string meshName=filter.name.ToLowerInvariant();
                if(!filter.sharedMesh||meshName.Contains("lod1")||meshName.Contains("lod2")||meshName.Contains("lod3")||meshName.Contains("decal")||meshName.Contains("carpets_ground")||meshName.Contains("carpets_big")||meshName.Contains("carpets_thin")||meshName.Contains("carpets_thick"))continue;
                var renderer=filter.GetComponent<MeshRenderer>();if(!renderer||!renderer.enabled)continue;
                var go=new GameObject("Route check: "+filter.name);go.hideFlags=HideFlags.HideAndDontSave;
                SceneManager.MoveGameObjectToScene(go,scene);temporary.Add(go);
                go.transform.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);go.transform.localScale=filter.transform.lossyScale;
                var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;obstacles.Add(collider);sources[collider]=filter;
            }
        }
        // Existing obstacle colliders outside the imported mesh hierarchies also participate.
        foreach(var root in scene.GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<Collider>(false))
        {
            if(c.transform.IsChildOf(tent)||c.transform.IsChildOf(car)||c.transform.IsChildOf(sled))continue;
            if(c.isTrigger||c is TerrainCollider||c.GetComponentInParent<Animator>()||temporary.Contains(c.gameObject))continue;
            string label=c.name.ToLowerInvariant();
            if(label.StartsWith("camera clearance:"))continue;
            if(label.Contains("terrain")||label.Contains("ground")||label.Contains("floor")||label.Contains("sand"))continue;
            if(c.bounds.size.x*c.bounds.size.z>400f||c.bounds.max.y<=floor+.12f)continue;
            obstacles.Add(c);
        }
        Physics.SyncTransforms();
    }
    bool Clear(Vector3 a,Vector3 b)
    {
        if(timer.Elapsed.TotalSeconds>20)throw new Exception("Route search exceeded 20 seconds; stopped safely. Adjust entrance/seat clearance and retry.");
        Vector3 d=b-a;d.y=0;float length=d.magnitude;
        var bottom=a+Vector3.up*(Radius+.12f);var top=a+Vector3.up*(1.72f-Radius);
        // A swept capsule checks the whole body width, including bends and the segment's start.
        foreach(var collider in Physics.OverlapCapsule(bottom,top,Radius,~0,QueryTriggerInteraction.Ignore))
            if(obstacles.Contains(collider))return false;
        if(length<.001f)return true;
        foreach(var hit in Physics.CapsuleCastAll(bottom,top,Radius,d/length,length,~0,QueryTriggerInteraction.Ignore))
            if(obstacles.Contains(hit.collider))return false;
        return true;
    }
    Vector3[] Gate(Vector3 start)
    {
        var candidates=new List<Vector3[]>();
        // Use the broad front opening, in the tent's own coordinates (it is rotated in this scene).
        // Only the middle portion is eligible; the closed short sides are never used as doors.
        bool acrossX=shellLocalBounds.size.x>=shellLocalBounds.size.z;
        for(int face=-1;face<=1;face+=2)for(float u=.38f;u<=.62f;u+=.02f)
        {
            var p=shellLocalBounds.center;
            if(acrossX){p.x=Mathf.Lerp(shellLocalBounds.min.x,shellLocalBounds.max.x,u);p.z=face<0?shellLocalBounds.min.z:shellLocalBounds.max.z;}
            else{p.z=Mathf.Lerp(shellLocalBounds.min.z,shellLocalBounds.max.z,u);p.x=face<0?shellLocalBounds.min.x:shellLocalBounds.max.x;}
            var outward=shellTransform.TransformDirection(acrossX?new Vector3(0,0,face):new Vector3(face,0,0));
            outward=Vector3.ProjectOnPlane(outward,Vector3.up).normalized;
            p=shellTransform.TransformPoint(p);p.y=start.y;
            // The roof overhang extends beyond the actual doorway: probe well past the eaves.
            var outside=p+outward*1.4f;var inside=p-outward*2.0f;
            if(Clear(outside,inside))candidates.Add(new[]{outside,inside});
        }
        if(candidates.Count==0)throw new Exception("No person-width tent entrance found. Routes were not marked complete.");
        return candidates.OrderByDescending(g=>Clear(start,g[1])).ThenBy(g=>Vector3.Distance(g[1],start)).First();
    }
    Vector3 ClearStart(Vector3 start)
    {
        // Account for the dress width rather than allowing the root to stand against a bumper.
        foreach(float radius in new[]{0f,.35f,.7f,1.05f,1.4f,1.8f,2.2f,2.6f})
        {
            for(int angle=0;angle<16;angle++)
            {
                float radians=angle*Mathf.PI/8;
                var p=start+new Vector3(Mathf.Cos(radians),0,Mathf.Sin(radians))*radius;
                bool free=true;
                for(int d=0;d<8&&free;d++)
                {
                    float a=d*Mathf.PI/4;
                    free=Clear(p,p+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.48f);
                }
                if(free)return p;
            }
        }
        var blockers=Physics.OverlapCapsule(start+Vector3.up*(Radius+.12f),start+Vector3.up*(1.72f-Radius),Radius,~0,QueryTriggerInteraction.Ignore)
            .Where(c=>obstacles.Contains(c)).Select(c=>c.name);
        throw new Exception("No clear starting space at "+start+". Blocking geometry: "+string.Join(", ",blockers));
    }
    List<Vector3> Path(Vector3 start,Vector3 end,bool seating=false)
    {
        if(Clear(start,end))return new List<Vector3>{start,end};
        var min=Vector3.Min(start,end)-Vector3.one*10;var max=Vector3.Max(start,end)+Vector3.one*10;
        int width=Mathf.CeilToInt((max.x-min.x)/Grid)+1,depth=Mathf.CeilToInt((max.z-min.z)/Grid)+1;
        Func<Vector3,Vector2Int> cell=p=>new Vector2Int(Mathf.RoundToInt((p.x-min.x)/Grid),Mathf.RoundToInt((p.z-min.z)/Grid));
        Func<Vector2Int,Vector3> point=k=>new Vector3(min.x+k.x*Grid,start.y,min.z+k.y*Grid);
        var first=cell(start);var goal=cell(end);var nearest=first;float nearestDistance=Vector3.Distance(start,end);var open=new List<Vector2Int>{first};
        var score=new Dictionary<Vector2Int,float>{{first,0}};var previous=new Dictionary<Vector2Int,Vector2Int>();var closed=new HashSet<Vector2Int>();
        for(int iteration=0;open.Count>0&&iteration<16000;iteration++)
        {
            var current=open.OrderBy(k=>score[k]+Vector2Int.Distance(k,goal)*Grid).First();open.Remove(current);
            var p=current==first?start:point(current);
            float goalDistance=Vector3.Distance(p,end);
            if(goalDistance<nearestDistance){nearest=current;nearestDistance=goalDistance;}
            if(seating&&iteration>=450)break;
            if((Vector3.Distance(p,end)<Grid*1.6f&&Clear(p,end)) || (seating&&Vector3.Distance(p,end)<2.4f))
            {
                var result=new List<Vector3>{seating&&!Clear(p,end)?p:end,p};
                while(previous.ContainsKey(current)){current=previous[current];result.Add(current==first?start:point(current));}
                result.Reverse();
                for(int i=0;i<result.Count-2;i++){int j=result.Count-1;while(j>i+1&&!Clear(result[i],result[j]))j--;if(j>i+1)result.RemoveRange(i+1,j-i-1);}
                return result;
            }
            closed.Add(current);
            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
            {
                if(x==0&&z==0)continue;var next=current+new Vector2Int(x,z);
                if(next.x<0||next.y<0||next.x>=width||next.y>=depth||closed.Contains(next))continue;
                var q=point(next);float cost=score[current]+Vector3.Distance(p,q);
                if(score.TryGetValue(next,out float old)&&old<=cost)continue;
                if(!Clear(p,q))continue;score[next]=cost;previous[next]=current;if(!open.Contains(next))open.Add(next);
            }
        }
        if(seating)
        {
            // Seating may be enclosed by low furnishings: finish at the closest reachable place.
            // Never force the final step through the tea set or firepit to reach the old seat.
            var result=new List<Vector3>();var current=nearest;
            result.Add(current==first?start:point(current));
            while(previous.ContainsKey(current)){current=previous[current];result.Add(current==first?start:point(current));}
            result.Reverse();return result;
        }
        throw new Exception("No obstacle-free walking path to "+end+". Check destination seat clearance; no successful route marker was saved.");
    }
    public void Bake(PlayableDirector director,bool onlyWhiteThobe=false)
    {
        assignedSeats.Clear();occupiedSeats.Clear();
        var timeline=(TimelineAsset)director.playableAsset;
        foreach(var group in timeline.GetRootTracks().OfType<GroupTrack>())
        {
            var body=group.GetChildTracks().OfType<AnimationTrack>().FirstOrDefault(t=>t.name.StartsWith("Body"));
            if(body==null||body.GetClips().Any(c=>Math.Abs(c.start-1)<.001))continue;
            var movement=group.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Position"));
            var animation=((AnimationPlayableAsset)movement.GetClips().Single().asset).clip;
            var bindings=AnimationUtility.GetCurveBindings(animation);
            var animator=(Animator)director.GetGenericBinding(movement);
            var target=animator.transform.Find(bindings.First(b=>b.propertyName=="m_LocalPosition.x").path);
            Func<string,float> at=n=>AnimationUtility.GetEditorCurve(animation,bindings.First(b=>b.propertyName==n)).Evaluate(0);
            occupiedSeats.Add(target.parent.TransformPoint(new Vector3(at("m_LocalPosition.x"),at("m_LocalPosition.y"),at("m_LocalPosition.z"))));
        }
        foreach(var group in timeline.GetRootTracks().OfType<GroupTrack>())
        {
            if(onlyWhiteThobe&&!group.name.StartsWith("White Thobe"))continue;
            var pose=group.GetChildTracks().OfType<AnimationTrack>().FirstOrDefault(t=>t.name.StartsWith("Body"));
            var walk=pose?.GetClips().FirstOrDefault(c=>Math.Abs(c.start-1)<.001);if(walk==null)continue;
            var track=group.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Position"));
            var clip=((AnimationPlayableAsset)track.GetClips().Single().asset).clip;
            if(!originals.ContainsKey(clip))originals[clip]=AnimationUtility.GetCurveBindings(clip).Select(b=>(b,AnimationUtility.GetEditorCurve(clip,b))).ToList();
            var bindings=AnimationUtility.GetCurveBindings(clip);var animator=(Animator)director.GetGenericBinding(track);
            var target=animator.transform.Find(bindings.First(b=>b.propertyName=="m_LocalPosition.x").path);
            if(!target)throw new Exception("Missing character motion target: "+group.name);
            string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
            var curves=properties.Select(n=>AnimationUtility.GetEditorCurve(clip,bindings.First(b=>b.propertyName==n))).ToArray();
            float arrival=(float)(walk.start+walk.duration);
            Func<float,Vector3> world=t=>target.parent.TransformPoint(new Vector3(curves[0].Evaluate(t),curves[1].Evaluate(t),curves[2].Evaluate(t)));
            Radius=group.name.StartsWith("White Thobe")?.22f:.34f;
            var start=world(0);var end=world(arrival);var gate=Gate(start);
            if(!Clear(start,start))
            {
                var overlapping=Physics.OverlapCapsule(start+Vector3.up*(Radius+.12f),start+Vector3.up*(1.72f-Radius),Radius,~0,QueryTriggerInteraction.Ignore).Where(c=>obstacles.Contains(c)).Select(c=>c.name);
                throw new Exception("User start intersects "+string.Join(", ",overlapping)+": "+group.name+" at "+start+". Starting position preserved.");
            }
            // A distinct interior destination is required before a sit clip may be scheduled.
            // Keep a single straight segment, testing actual furniture and occupied seating clearance.
            var inward=Vector3.ProjectOnPlane(gate[1]-gate[0],Vector3.up).normalized;
            var across=Vector3.Cross(Vector3.up,inward);
            var gathering=occupiedSeats.Aggregate(Vector3.zero,(sum,p)=>sum+p)/Mathf.Max(1,occupiedSeats.Count);
            float frontRowDepth=occupiedSeats.Min(p=>Vector3.Dot(p-gate[1],inward));
            var candidates=new List<Vector3>();
            var directlyReachable=new List<Vector3>();
            for(float depth=2.5f;depth<=7.5f;depth+=.30f)for(float lateral=-5.0f;lateral<=5.0f;lateral+=.30f)
            {
                var candidate=gate[1]+inward*depth+across*lateral;candidate.y=start.y;
                var localSeat=shellTransform.InverseTransformPoint(candidate);
                if(localSeat.x<shellLocalBounds.min.x+.6f||localSeat.x>shellLocalBounds.max.x-.6f||localSeat.z<shellLocalBounds.min.z+.6f||localSeat.z>shellLocalBounds.max.z-.6f)continue;
                if(assignedSeats.Concat(occupiedSeats).Any(p=>Vector3.ProjectOnPlane(p-candidate,Vector3.up).magnitude<.95f))continue;
                if(Vector3.Dot(candidate-gate[1],inward)<frontRowDepth-.15f)continue;
                if(Vector3.ProjectOnPlane(candidate-gathering,Vector3.up).magnitude>3.8f)continue;
                if(occupiedSeats.Min(p=>Vector3.ProjectOnPlane(p-candidate,Vector3.up).magnitude)>2.8f)continue;
                if(Clear(candidate,candidate))
                {
                    candidates.Add(candidate);
                    if(Clear(start,candidate))directlyReachable.Add(candidate);
                }
            }
            if(candidates.Count==0)throw new Exception("No clear seat beside the existing seated group: "+group.name);
            Vector3 straightEnd;
            List<Vector3> route;
            if(directlyReachable.Count>0)
            {
                straightEnd=directlyReachable.OrderBy(p=>Vector3.Distance(p,gathering)).First();
                route=new List<Vector3>{start,straightEnd};
            }
            else
            {
                // Keep the approach straight through the front door; only the interior aisle may bend.
                if(!Clear(start,gate[1]))throw new Exception("Straight front-door approach blocked: "+group.name);
                route=null;straightEnd=Vector3.zero;
                foreach(var candidate in candidates.OrderBy(p=>Vector3.Distance(p,gathering)).Take(12))
                {
                    try
                    {
                        var insidePath=Path(gate[1],candidate);
                        route=new List<Vector3>{start,gate[1]};route.AddRange(insidePath.Skip(1));straightEnd=candidate;break;
                    }
                    catch(Exception ex){if(ex.Message.Contains("20 seconds"))throw;}
                }
                if(route==null)throw new Exception("No interior aisle to the seated group: "+group.name);
            }
            if(onlyWhiteThobe)
            {
                // Keep the approved seat. Remove the forced sideways door waypoint from his approach.
                // Restore the approved gathering seat from before the straight-route leg-room clamp.
                var seatMarker=director.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t=>t.name=="White Thobe Gathering Seat");
                if(!seatMarker)
                {
                    var marker=new GameObject("White Thobe Gathering Seat");SceneManager.MoveGameObjectToScene(marker,director.gameObject.scene);
                    marker.transform.position=new Vector3(77.49f,start.y,33.15f);seatMarker=marker.transform;
                }
                straightEnd=seatMarker.position;
                route=new List<Vector3>{start,straightEnd};
                if(!Clear(start,straightEnd))
                {
                    var delta=straightEnd-start;
                    var blocker=Physics.CapsuleCastAll(start+Vector3.up*(Radius+.12f),start+Vector3.up*(1.72f-Radius),Radius,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore)
                        .Where(h=>obstacles.Contains(h.collider)).OrderBy(h=>h.distance).FirstOrDefault();
                    if(blocker.collider is MeshCollider mesh&&sources.TryGetValue(mesh,out var source)&&source.name.ToLowerInvariant().Contains("cushion"))
                    {
                        var cushion=source.transform;
                        while(cushion.parent&&cushion.parent.name!="Props"&&cushion.parent.name!="Carpets"&&cushion.parent.name!="Tent")cushion=cushion.parent;
                        if(cushion.parent)
                        {
                            var side=Vector3.Cross(Vector3.up,delta.normalized);
                            if(Vector3.Dot(cushion.position-start,side)<0)side=-side;
                            Undo.RecordObject(cushion,"Clear cushion from straight walking aisle");
                            var bounds=mesh.bounds;
                            float halfWidth=Mathf.Abs(side.x)*bounds.extents.x+Mathf.Abs(side.z)*bounds.extents.z;
                            float lateral=Mathf.Abs(Vector3.Dot(bounds.center-start,side));
                            cushion.position+=side*Mathf.Max(.15f,halfWidth+Radius+.25f-lateral);EditorUtility.SetDirty(cushion);
                            foreach(var pair in sources)
                            {
                                if(!pair.Value.transform.IsChildOf(cushion)&&pair.Value.transform!=cushion)continue;
                                pair.Key.transform.SetPositionAndRotation(pair.Value.transform.position,pair.Value.transform.rotation);
                                pair.Key.transform.localScale=pair.Value.transform.lossyScale;
                            }
                            Physics.SyncTransforms();
                            Debug.Log("Moved only blocking cushion 0.85 m beside the walking aisle. Car, sled, tent and character starts retained.");
                        }
                    }
                    if(!Clear(start,straightEnd))
                    {
                        var nearest=Physics.CapsuleCastAll(start+Vector3.up*(Radius+.12f),start+Vector3.up*(1.72f-Radius),Radius,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore)
                            .Where(h=>obstacles.Contains(h.collider)).OrderBy(h=>h.distance).FirstOrDefault();
                        if(nearest.collider is MeshCollider trayMesh&&sources.TryGetValue(trayMesh,out var traySource)&&traySource.name.ToLowerInvariant().Contains("teaset_plate"))
                        {
                            var lod=traySource.GetComponentInParent<LODGroup>();
                            var tray=lod?lod.transform:traySource.transform.parent;
                            if(tray&&tray.name!="Tent"&&tray.name!="Props")
                            {
                                var side=Vector3.Cross(Vector3.up,delta.normalized);
                                if(Vector3.Dot(trayMesh.bounds.center-start,side)<0)side=-side;
                                var bounds=trayMesh.bounds;
                                float halfWidth=Mathf.Abs(side.x)*bounds.extents.x+Mathf.Abs(side.z)*bounds.extents.z;
                                float lateral=Mathf.Abs(Vector3.Dot(bounds.center-start,side));
                                Undo.RecordObject(tray,"Clear tray from man's approved gathering seat");
                                tray.position+=side*Mathf.Max(.2f,halfWidth+.9f-lateral);EditorUtility.SetDirty(tray);
                                foreach(var pair in sources)
                                {
                                    if(!pair.Value.transform.IsChildOf(tray)&&pair.Value.transform!=tray)continue;
                                    pair.Key.transform.SetPositionAndRotation(pair.Value.transform.position,pair.Value.transform.rotation);
                                    pair.Key.transform.localScale=pair.Value.transform.lossyScale;
                                }
                                Physics.SyncTransforms();
                                Debug.Log("White Thobe gathering seat restored; blocking tea tray placed beside the aisle.");
                            }
                        }
                    }
                    if(!Clear(start,straightEnd))
                    throw new Exception("STRAIGHT MALE ROUTE BLOCKED: "+(blocker.collider?blocker.collider.name:"geometry")+" at "+blocker.point+"; start "+start+"; seat "+straightEnd+". A straight line requires moving the departure point or obstacle. Existing layout preserved.");
                }
                Debug.Log("White Thobe: exactly one straight segment, no sideways waypoints.");
            }
            assignedSeats.Add(straightEnd);
            Debug.Log(group.name+" straight route: retained start="+start+", end="+straightEnd+"; seat beside existing seated characters.");
            float length=0;for(int i=1;i<route.Count;i++)length+=Vector3.Distance(route[i-1],route[i]);
            var walkingAsset=(AnimationPlayableAsset)walk.asset;
            float originalSpeed=walkingAsset.clip.averageSpeed.magnitude;
            if(originalSpeed<.3f||originalSpeed>2f)originalSpeed=1.15f;
            walk.timeScale=1;
            float travelSeconds=Mathf.Max(.1f,length/originalSpeed);
            arrival=(float)walk.start+travelSeconds;
            walk.duration=travelSeconds+.35f;
            var sitting=pose.GetClips().FirstOrDefault(c=>c.displayName.StartsWith("Sit"));
            if(sitting!=null)
            {
                sitting.start=arrival;sitting.duration=Math.Max(.1,timeline.duration-sitting.start);
                walk.blendOutDuration=.35;sitting.blendInDuration=.35;
            }
            EditorUtility.SetDirty(pose);EditorUtility.SetDirty(timeline);
            Debug.Log(group.name+": restored original walking speed "+originalSpeed.ToString("F2")+" m/s; body clip speed 1x; travel "+length.ToString("F2")+" m in "+walk.duration.ToString("F2")+" s.");
            var output=Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray();
            int samples=Mathf.CeilToInt((float)timeline.duration*30);
            for(int sample=0;sample<=samples;sample++)
            {
                float time=(float)timeline.duration*sample/samples;
                float remaining=Mathf.Clamp01((time-1)/Mathf.Max(.1f,arrival-1))*length;
                int segment=1;while(segment<route.Count-1&&remaining>Vector3.Distance(route[segment-1],route[segment])){remaining-=Vector3.Distance(route[segment-1],route[segment]);segment++;}
                var direction=route[segment]-route[segment-1];var p=Vector3.Lerp(route[segment-1],route[segment],remaining/Mathf.Max(.001f,direction.magnitude));
                var local=target.parent.InverseTransformPoint(p);

                var walkRotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(direction,Vector3.up));
                var faceGroup=Vector3.ProjectOnPlane(gathering-straightEnd,Vector3.up);
                var seatRotation=faceGroup.sqrMagnitude>.001f?Quaternion.LookRotation(faceGroup):walkRotation;
                var rotation=Quaternion.Inverse(target.parent.rotation)*Quaternion.Slerp(walkRotation,seatRotation,Mathf.SmoothStep(0,1,(time-arrival)/.35f));
                for(int axis=0;axis<3;axis++)output[axis].AddKey(time,local[axis]);for(int axis=0;axis<4;axis++)output[axis+3].AddKey(time,rotation[axis]);
            }
            for(int axis=0;axis<7;axis++)
            {
                for(int key=0;key<output[axis].length;key++){AnimationUtility.SetKeyLeftTangentMode(output[axis],key,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(output[axis],key,AnimationUtility.TangentMode.Linear);}
                AnimationUtility.SetEditorCurve(clip,bindings.First(b=>b.propertyName==properties[axis]),output[axis]);
            }
            clip.EnsureQuaternionContinuity();EditorUtility.SetDirty(clip);
            // Check the final baked movement rather than relying only on the pathfinder nodes.
            for(int sample=1;sample<=samples;sample++)
            {
                float ta=(float)timeline.duration*(sample-1)/samples,tb=(float)timeline.duration*sample/samples;
                Func<float,Vector3> at=t=>target.parent.TransformPoint(new Vector3(output[0].Evaluate(t),output[1].Evaluate(t),output[2].Evaluate(t)));
                if(!Clear(at(ta),at(tb)))throw new Exception("Baked route intersects geometry: "+group.name+" at "+tb);
            }
            Debug.Log(group.name+": straight route checked against geometry, "+route.Count+" waypoints, "+length.ToString("F1")+" m.");
        }
    }
    public void Dispose()
    {
        if(!committed)foreach(var pair in originals)
        {
            foreach(var binding in AnimationUtility.GetCurveBindings(pair.Key))AnimationUtility.SetEditorCurve(pair.Key,binding,null);
            foreach(var original in pair.Value)AnimationUtility.SetEditorCurve(pair.Key,original.binding,original.curve);
            EditorUtility.SetDirty(pair.Key);
        }
        foreach(var go in temporary)if(go)UnityEngine.Object.DestroyImmediate(go);
    }
}
