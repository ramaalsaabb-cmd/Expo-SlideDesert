using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
public static class DesertWhiteFinalSeatHeight {
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Apply;}
 static void Apply(){if(EditorApplication.isPlayingOrWillChangePlaymode||File.Exists("work/white-final-height-v1.txt"))return;
 foreach(var label in new[]{"Mufleh","Saqeera"}){var timeline=AssetDatabase.LoadAssetAtPath<TimelineAsset>("Assets/Cinematics/"+label+" Selected.playable");var group=timeline.GetRootTracks().OfType<GroupTrack>().Single(g=>g.name.StartsWith("White Thobe"));var body=group.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Body"));float arrival=(float)body.GetClips().First(c=>c.displayName.StartsWith("Sit")).start;var motion=group.GetChildTracks().OfType<AnimationTrack>().First(t=>t.name.StartsWith("Position"));var clip=((AnimationPlayableAsset)motion.GetClips().Single().asset).clip;var binding=AnimationUtility.GetCurveBindings(clip).Single(b=>b.propertyName=="m_LocalPosition.y");var curve=AnimationUtility.GetEditorCurve(clip,binding);var keys=curve.keys;float floor=curve.Evaluate(0);for(int i=0;i<keys.Length;i++){if(keys[i].time>=arrival)keys[i].value=Mathf.Lerp(floor,.16688856f,Mathf.SmoothStep(0,1,(keys[i].time-arrival)/.35f));}curve.keys=keys;for(int i=0;i<keys.Length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}AnimationUtility.SetEditorCurve(clip,binding,curve);EditorUtility.SetDirty(clip);}AssetDatabase.SaveAssets();File.WriteAllText("work/white-final-height-v1.txt","Original White Thobe seated height retained in both timelines: 0.16688856m. XZ routes and camera unchanged.");}
}
