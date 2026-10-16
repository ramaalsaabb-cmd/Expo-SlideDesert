using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

// Repairs authored seating curves, retaining the user's raised characters and walking heights.
public static class DesertSeatHeightRepair
{
    const string Marker="work/seat-height-repair-complete.txt";
    [InitializeOnLoadMethod] static void Initialize(){EditorApplication.delayCall+=ApplyOnce;}
    static void ApplyOnce(){if(!File.Exists(Marker)&&!EditorApplication.isPlayingOrWillChangePlaymode)Repair();}
    [MenuItem("Tools/Desert VR/Repair Sunken Seated Characters")]
    public static void Repair()
    {
        var previous=SceneManager.GetActiveScene();
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/DesertOverview.unity");
        bool opened=!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/DesertOverview.unity",OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var report=new System.Text.StringBuilder();
        var backup="work/SeatHeightBackup";Directory.CreateDirectory(backup);
        try
        {
            var seq=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<DesertCinematicSequence>(true)).Single();
            var tent=scene.GetRootGameObjects().Single(x=>x.name=="Tent");
            string scenePath=scene.path;
            if(!File.Exists(backup+"/DesertOverview.unity"))File.Copy(scenePath,backup+"/DesertOverview.unity");
            foreach(var director in new[]{seq.muflehTimeline,seq.saqeeraTimeline})
            {
                director.time=40;director.RebuildGraph();director.Evaluate();director.Evaluate();
                foreach(var group in ((TimelineAsset)director.playableAsset).GetRootTracks().OfType<GroupTrack>())
                {
                    if(!(group.name.StartsWith("White Thobe")||group.name.StartsWith("Traditional Dress")))continue;
                    var body=group.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Body"));
                    var model=(Animator)director.GetGenericBinding(body);
                    var hip=model.GetBoneTransform(HumanBodyBones.Hips);
                    float surface=float.NegativeInfinity;
                    foreach(var filter in tent.GetComponentsInChildren<MeshFilter>())
                    {
                        // Ground and seat carpets, excluding cushion backrests and tent roof.
                        if(!filter.sharedMesh||!filter.name.StartsWith("Carpets_"))continue;
                        var probe=new GameObject("Temporary seat surface probe");
                        try
                        {
                            probe.transform.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);
                            probe.transform.localScale=filter.transform.lossyScale;
                            var collider=probe.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;
                            Physics.SyncTransforms();
                            if(collider.Raycast(new Ray(hip.position+Vector3.up*.5f,Vector3.down),out var hit,2f))surface=Mathf.Max(surface,hit.point.y);
                        }
                        finally{UnityEngine.Object.DestroyImmediate(probe);}
                    }
                    if(float.IsNegativeInfinity(surface))throw new Exception("Seat carpet missing: "+group.name);
                    float lift=surface+.015f-model.transform.position.y;
                    if(lift<=.02f){report.AppendLine(director.name+" / "+group.name+": existing lift retained.");continue;}
                    var movement=group.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Position"));
                    var clip=((AnimationPlayableAsset)movement.GetClips().Single().asset).clip;
                    string path=AssetDatabase.GetAssetPath(clip);
                    string copy=backup+"/"+Path.GetFileName(path);if(!File.Exists(copy))File.Copy(path,copy);
                    var binding=AnimationUtility.GetCurveBindings(clip).Single(b=>b.propertyName=="m_LocalPosition.y");
                    var driver=(Animator)director.GetGenericBinding(movement);
                    float localLift=driver.transform.InverseTransformVector(Vector3.up*lift).y;
                    var curve=AnimationUtility.GetEditorCurve(clip,binding);
                    var walk=body.GetClips().FirstOrDefault(c=>c.displayName.StartsWith("Walk"));
                    float arrival=walk==null?0:(float)(walk.start+walk.duration);
                    var keys=curve.keys;
                    for(int i=0;i<keys.Length;i++)keys[i].value+=localLift*(walk==null?1:Mathf.SmoothStep(0,1,(keys[i].time-arrival)/.35f));
                    curve.keys=keys;
                    for(int i=0;i<keys.Length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}
                    AnimationUtility.SetEditorCurve(clip,binding,curve);EditorUtility.SetDirty(clip);
                    director.time=40;director.RebuildGraph();director.Evaluate();director.Evaluate();
                    if(model.transform.position.y<surface+.01f)throw new Exception("Seat height verification failed: "+group.name);
                    report.AppendLine(director.name+" / "+group.name+": lifted "+lift.ToString("F3")+"m; seat root "+model.transform.position.y.ToString("F3")+"m; carpet "+surface.ToString("F3")+"m.");
                }
                director.Stop();
            }
            AssetDatabase.SaveAssets();
            var selected=DesertCharacterSelection.MuflehSelected?seq.muflehTimeline:seq.saqeeraTimeline;
            selected.time=0;selected.RebuildGraph();selected.Evaluate();selected.Evaluate();selected.Pause();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Marker,report.ToString());Debug.Log("Seat height repair complete: "+report);
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            if(opened)EditorSceneManager.CloseScene(scene,true);
        }
    }
}
