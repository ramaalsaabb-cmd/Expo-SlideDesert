using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class DesertRestorePlacedCamera {
 const string Marker="work/camera-original-angle-restored-v1.txt";
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=Apply;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Apply;};}
 static void Apply(){if(EditorApplication.isPlayingOrWillChangePlaymode||File.Exists(Marker))return;
 var report=new System.Text.StringBuilder();foreach(string branch in new[]{"Mufleh","Saqeera"}){
 var asset=AssetDatabase.LoadAllAssetsAtPath("Assets/Cinematics/"+branch+" Selected.playable").OfType<DesertSelectedCameraClip>().Single();
 asset.usePlacedAngle=true;EditorUtility.SetDirty(asset);report.AppendLine(branch+": restored placed camera angle; offset="+asset.placedRigOffset+" rotation="+asset.placedRigRotation.eulerAngles+"; seated orbit retained.");
 }AssetDatabase.SaveAssets();File.WriteAllText(Marker,report.ToString());}
}
