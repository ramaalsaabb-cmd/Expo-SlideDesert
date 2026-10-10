using UnityEngine;

public static class SledDustSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnPlay()
    {
        foreach(var follow in Object.FindObjectsByType<SledFollowCar>())
        {
            if(follow.gameObject.scene.path!="Assets/Scenes/Demo.unity") continue;
            var dust=Configure(follow);
            dust.enabled=true;
            dust.Initialize();
            Debug.Log("[Sled Dust] Connected: " + (dust.sledModel ? dust.sledModel.name : "missing sled model")
                + "; material: " + (dust.dustMaterial ? dust.dustMaterial.name : "missing"), dust);
        }
    }
    public static SledDustController Configure(SledFollowCar follow)
    {
        var dust=follow.GetComponent<SledDustController>();
        if(!dust) dust=follow.gameObject.AddComponent<SledDustController>();
        if(!dust.dustMaterial) dust.dustMaterial=Resources.Load<Material>("SledDust");
        return dust;
    }
}

