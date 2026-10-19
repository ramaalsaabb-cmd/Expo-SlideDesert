using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class HardwareRideCoverageAudit
{
    [InitializeOnLoadMethod] static void Queue(){EditorApplication.delayCall+=Run;}
    [MenuItem("Tools/Desert VR/Audit Hardware Ride Coverage")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var car=UnityEngine.Object.FindFirstObjectByType<CarPathFollower>();
        var output=UnityEngine.Object.FindFirstObjectByType<HardwareOutput>();
        if(!car||!output)return;
        var report=new StringBuilder("Read-only ride coverage audit\n");
        var motion=output.sledMotion;
        report.AppendLine("Motion assigned/enabled: "+(motion&&motion.isActiveAndEnabled));
        var sender=UnityEngine.Object.FindFirstObjectByType<WebSocketSender>();
        report.AppendLine("Sender assigned/enabled: "+(sender&&sender.isActiveAndEnabled));
        report.AppendLine("Sender points at this output: "+(sender&&sender.hardwareOutput==output));
        if(motion&&car.points!=null)
        {
            var anchors=new[]{car.transform.position}.Concat(car.points.Where(p=>p).Select(p=>p.position)).ToArray();
            int covered=0,missing=0;Vector3 firstMissing=default;
            for(int i=0;i<anchors.Length-1;i++)
            {
                var a=anchors[Mathf.Max(0,i-1)];var b=anchors[i];var c=anchors[i+1];var d=anchors[Mathf.Min(anchors.Length-1,i+2)];
                int count=Mathf.Max(12,Mathf.CeilToInt(Vector3.Distance(b,c)/1.5f));
                for(int j=0;j<=count;j++)
                {
                    float t=j/(float)count;
                    var p=.5f*(2*b+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);
                    bool found=false;
                    foreach(var terrain in motion.terrains??Array.Empty<Terrain>())
                    {
                        if(!terrain||!terrain.terrainData)continue;
                        var local=p-terrain.transform.position;var size=terrain.terrainData.size;
                        if(local.x>=0&&local.z>=0&&local.x<=size.x&&local.z<=size.z){found=true;break;}
                    }
                    if(found)covered++;else{if(missing==0)firstMissing=p;missing++;}
                }
            }
            report.AppendLine("Route samples inside sensor terrain: "+covered+"; outside: "+missing);
            if(missing>0)report.AppendLine("First uncovered sample: "+firstMissing);
            foreach(var terrain in motion.terrains??Array.Empty<Terrain>())
                if(terrain&&terrain.terrainData)report.AppendLine("Terrain "+terrain.name+": origin "+terrain.transform.position+" size "+terrain.terrainData.size);
        }
        report.AppendLine("HardwareOutput, WebSocket and all scene objects were read only.");
        File.WriteAllText("Assets/Editor/HardwareRideCoverageAudit.txt",report.ToString());
        Debug.Log(report.ToString());
    }
}
