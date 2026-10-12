using UnityEngine;

/// <summary>Also installs on an already-open scene when Unity skips scene reload.</summary>
public static class DustPlayModeSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ConnectLoadedScene()
    {
        foreach (var terrain in Terrain.activeTerrains)
        {
            if (terrain.gameObject.scene.path != "Assets/Scenes/Demo.unity" || !terrain.terrainData) continue;
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider && collider.terrainData != terrain.terrainData)
            {
                collider.terrainData = terrain.terrainData;
                Debug.Log("[Wheel Dust] Repaired terrain collider data: " + terrain.name, terrain);
            }
        }
        foreach (var seatController in Object.FindObjectsByType<XRSeatController>())
        {
            if (seatController.gameObject.scene.path != "Assets/Scenes/Demo.unity" || !seatController.seatPoint) continue;
            // Preserve the authored seat pose; dust setup must not move the viewpoint.
            // Disable the legacy competing transform lock if it is present.
            var oldLock = seatController.GetComponent<LockXRToSled>();
            if (oldLock) oldLock.enabled = false;
            var follow = seatController.GetComponentInParent<SledFollowCar>();
            var camera = seatController.headCamera ? seatController.headCamera.GetComponent<Camera>() : null;
            if (follow && camera)
            {
                foreach (var filter in follow.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.name != "Green_Dress_Woman_Seated") continue;
                    var view = filter.GetComponent<SeatedRiderView>();
                    if (!view) view = filter.gameObject.AddComponent<SeatedRiderView>();
                    view.Configure(camera);
                }
            }
            seatController.BeginSeatCalibration();
        }
        Physics.SyncTransforms();
        foreach (var car in Object.FindObjectsByType<CarPathFollower>())
        {
            if (car.gameObject.scene.path != "Assets/Scenes/Demo.unity") continue;
            var dust = car.GetComponent<WheelDustController>();
            if (!dust) dust = car.gameObject.AddComponent<WheelDustController>();
            dust.playerCamera = Camera.main;
            if (!dust.legacyDust)
            {
                foreach (var ps in Object.FindObjectsByType<ParticleSystem>())
                {
                    if (ps.gameObject.scene == car.gameObject.scene && ps.name == "Particle System")
                    {
                        dust.legacyDust = ps;
                        break;
                    }
                }
            }
            if (dust.legacyDust)
                dust.dustMaterial = dust.legacyDust.GetComponent<ParticleSystemRenderer>().sharedMaterial;
            if (dust.playerCamera && !dust.playerCamera.GetComponent<VRDustVision>())
                dust.playerCamera.gameObject.AddComponent<VRDustVision>();
            dust.ResetForPlay();
            if (dust.playerCamera)
            {
                var vision = dust.playerCamera.GetComponent<VRDustVision>();
                if (vision)
                {
                    vision.blinkingEnabled = false;
                    vision.blinkThreshold = 0.65f;
                    vision.blinkInterval = 12f;
                    vision.maximumVeil = 0.01f;
                    vision.recoverySeconds = 0.8f;
                    vision.ResetForPlay();
                }
            }
            Debug.Log("[Wheel Dust] Connected to " + car.name + "; wheels: "
                + (car.wheels == null ? 0 : car.wheels.Length)
                + "; camera: " + (dust.playerCamera ? dust.playerCamera.name : "missing"), car);
        }
    }
}


