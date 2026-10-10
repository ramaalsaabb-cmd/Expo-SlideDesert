using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SeatedExperienceSetup
{
    static SeatedExperienceSetup()
    {
        EditorApplication.delayCall += Apply;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Apply;
        };
    }

    [MenuItem("Tools/Desert VR/Apply Seated Setup")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach (var seat in Object.FindObjectsByType<XRSeatController>())
        {
            if (seat.gameObject.scene.path != "Assets/Scenes/Demo.unity" || !seat.seatPoint) continue;
            Undo.RecordObjects(new Object[] { seat.transform, seat.seatPoint }, "Align seated XR rig");
            seat.seatPoint.localPosition = new Vector3(10.56f, 2.2525f, 0.8696f);
            seat.seatPoint.localRotation = Quaternion.Euler(7.103f, 0f, 0f);
            seat.transform.localRotation = seat.seatPoint.localRotation;
            seat.transform.localPosition = seat.seatPoint.localPosition
                - seat.transform.localRotation * (Vector3.up * 1.2f);
            var offset = seat.transform.Find("Camera Offset");
            if (offset)
            {
                Undo.RecordObject(offset, "Set seated camera offset");
                offset.localPosition = Vector3.up * 1.2f;
            }
            foreach (var component in seat.GetComponents<Component>())
            {
                if (!component) continue;
                var serialized = new SerializedObject(component);
                var height = serialized.FindProperty("m_CameraYOffset");
                if (height == null) continue;
                height.floatValue = 1.2f;
                var tracking = serialized.FindProperty("m_RequestedTrackingOriginMode");
                if (tracking != null) tracking.intValue = 1;
                serialized.ApplyModifiedProperties();
            }
            var oldLock = seat.GetComponent<LockXRToSled>();
            if (oldLock) { Undo.RecordObject(oldLock, "Disable competing XR lock"); oldLock.enabled = false; }
            var vision = seat.headCamera ? seat.headCamera.GetComponent<VRDustVision>() : null;
            if (vision)
            {
                Undo.RecordObject(vision, "Make dust reaction subtle");
                vision.blinkThreshold = 0.65f;
                vision.blinkInterval = 12f;
                vision.maximumVeil = 0.06f;
                vision.recoverySeconds = 0.8f;
            }
            EditorSceneManager.MarkSceneDirty(seat.gameObject.scene);
        }
    }
}
