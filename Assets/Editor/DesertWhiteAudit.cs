using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
public static class DesertWhiteAudit {
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Run;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Run;};}
 static void Run(){if(EditorApplication.isPlayingOrWillChangePlaymode||File.Exists("work/white-final-audit.txt"))return;var prev=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath("Assets/Scenes/DesertOverview.unity");bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/DesertOverview.unity",OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);try{
 var seq=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DesertCinematicSequence>(true)).Single();var report=new System.Text.StringBuilder();foreach(var d in new[]{seq.muflehTimeline,seq.saqeeraTimeline}){var tl=(TimelineAsset)d.playableAsset;report.AppendLine(d.name+" duration="+tl.duration+" directorDuration="+d.duration+" mode="+tl.durationMode+" fixed="+tl.fixedDuration);
 var g=tl.GetRootTracks().OfType<GroupTrack>().Single(x=>x.name.StartsWith("White Thobe"));var body=g.GetChildTracks().OfType<AnimationTrack>().First(x=>x.name.StartsWith("Body"));foreach(var c in body.GetClips())report.AppendLine(c.displayName+" "+c.start+".."+c.end+" clip="+((AnimationPlayableAsset)c.asset).clip.name);
 var move=g.GetChildTracks().OfType<AnimationTrack>().First(x=>x.name.StartsWith("Position"));var item=move.GetClips().Single();var animation=((AnimationPlayableAsset)item.asset).clip;report.AppendLine("Movement "+item.start+".."+item.end+" length="+animation.length);
 var b=AnimationUtility.GetCurveBindings(animation);foreach(var prop in new[]{"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z"}){var c=AnimationUtility.GetEditorCurve(animation,b.First(x=>x.propertyName==prop));report.AppendLine(prop+" last="+c.keys.Last().time+"/"+c.keys.Last().value);}
 foreach(float time in new[]{10f,18f,24f,30f,(float)tl.duration-.1f}){d.time=time;d.RebuildGraph();d.Evaluate();d.Evaluate();var a=(Animator)d.GetGenericBinding(body);var hip=a.GetBoneTransform(HumanBodyBones.Hips);var model=a.transform;var target=model.parent;report.AppendLine("t="+time+" root="+target.position+" hip="+hip.position+" facing="+target.forward);}
 d.Stop();}File.WriteAllText("work/white-final-audit.txt",report.ToString());
 }finally{SceneManager.SetActiveScene(prev);if(opened)EditorSceneManager.CloseScene(scene,true);}}
}
