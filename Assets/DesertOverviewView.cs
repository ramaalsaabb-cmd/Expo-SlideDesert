using UnityEngine;
using UnityEngine.XR;

public class DesertOverviewView : MonoBehaviour
{
    Quaternion basis, initialHead;
    bool calibrated;
    void Awake() { basis = transform.rotation; }
    void OnEnable() { Application.onBeforeRender += FollowHead; }
    void OnDisable() { Application.onBeforeRender -= FollowHead; }
    void LateUpdate() { FollowHead(); }
    void FollowHead()
    {
        if (!XRSettings.isDeviceActive) return;
        var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation)) return;
        if (!calibrated) { initialHead = rotation; calibrated = true; }
        transform.rotation = basis * Quaternion.Inverse(initialHead) * rotation;
    }
}
