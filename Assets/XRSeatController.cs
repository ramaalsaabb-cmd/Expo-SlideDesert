using UnityEngine;
using UnityEngine.XR;

/// <summary>Keeps the viewpoint attached to the seat while preserving tracked head rotation.</summary>
[DefaultExecutionOrder(10000)]
public class XRSeatController : MonoBehaviour
{
    public Transform seatPoint;
    public Transform headCamera;
    public Transform viewTarget;
    public float targetHeight = 1.2f;
    VRCameraEffects cameraEffects;
    Quaternion seatRotationOffset;
    bool isLocked;
    bool calibrationPending;
    bool headsetWasReady;

    void OnEnable()
    {
        cameraEffects = headCamera ? headCamera.GetComponentInParent<VRCameraEffects>() : null;
        Application.onBeforeRender += BeforeRender;
        InputDevices.deviceConnected += OnHeadsetConnected;
        headsetWasReady = false;
        BeginSeatCalibration();
    }

    public void BeginSeatCalibration()
    {
        // OnEnable and scene setup can both call this; initialize only once.
        if (isLocked || calibrationPending) return;
        calibrationPending = true;
    }

    bool HasValidReferences()
    {
        return seatPoint && headCamera && headCamera.IsChildOf(transform)
            && seatPoint != transform && !seatPoint.IsChildOf(transform);
    }

    void RequestResumeCalibration()
    {
        if (!isActiveAndEnabled) return;
        isLocked = false;
        calibrationPending = true;
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused) RequestResumeCalibration();
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused) RequestResumeCalibration();
    }

    void OnHeadsetConnected(InputDevice device)
    {
        if ((device.characteristics & InputDeviceCharacteristics.HeadMounted) != 0)
            RequestResumeCalibration();
    }

    bool HeadsetReady()
    {
        if (!XRSettings.isDeviceActive) return false;
        var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        bool tracked;
        Quaternion rotation;
        if (!device.isValid
            || !device.TryGetFeatureValue(CommonUsages.isTracked, out tracked) || !tracked
            || !device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation)) return false;
        bool present;
        return !device.TryGetFeatureValue(CommonUsages.userPresence, out present) || present;
    }

    void MonitorHeadsetReturn()
    {
        bool ready = HeadsetReady();
        if (ready && !headsetWasReady) RequestResumeCalibration();
        headsetWasReady = ready;
    }

    void LateUpdate()
    {
        MonitorHeadsetReturn();
        // Keep position anchored while waiting for the final tracked pose before rendering.
        if (calibrationPending)
        {
            if (HasValidReferences()) transform.position += seatPoint.position - headCamera.position;
            return;
        }
        KeepInSeat();
    }

    [BeforeRenderOrder(10000)]
    void BeforeRender()
    {
        MonitorHeadsetReturn();
        KeepInSeat();
    }

    // Run after the XR pose update as well, so tracking cannot lower the view before rendering.
    void KeepInSeat()
    {
        // Initialize after the first pose/update, before presenting the first frame.
        if (calibrationPending)
        {
            if (!HasValidReferences()) return;
            // Use a real headset pose, never the placeholder pose before XR starts.
            if (XRSettings.isDeviceActive && !HeadsetReady())
            {
                transform.position += seatPoint.position - headCamera.position;
                return;
            }
            calibrationPending = false;
            ResetAndLock();
            return;
        }
        if (!isLocked || !HasValidReferences()) return;
        transform.rotation = seatPoint.rotation * seatRotationOffset;
        // Cancel tracking/locomotion displacement, but retain intentional bump movement.
        Vector3 bumpOffset = cameraEffects && cameraEffects.isActiveAndEnabled
            ? cameraEffects.WorldPositionOffset : Vector3.zero;
        transform.position += seatPoint.position + bumpOffset - headCamera.position;
    }

    [ContextMenu("Recenter player in seat")]
    public void ResetAndLock()
    {
        calibrationPending = false;
        if (!HasValidReferences())
        {
            isLocked = false;
            Debug.LogWarning("XRSeatController: Head must be inside the rig and SeatPoint outside it.", this);
            return;
        }
        transform.rotation = seatPoint.rotation;
        var target = viewTarget;
        if (!target)
        {
            var follow = GetComponentInParent<SledFollowCar>();
            if (follow) target = follow.car;
        }
        Vector3 forward = target
            ? target.position + Vector3.up * targetHeight - seatPoint.position
            : seatPoint.forward;
        if (forward.sqrMagnitude > 0.001f)
        {
            // Recenter toward the car once; subsequent head turns remain fully tracked.
            Quaternion headRelativeRotation = Quaternion.Inverse(transform.rotation) * headCamera.rotation;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up)
                * Quaternion.Inverse(headRelativeRotation);
        }
        seatRotationOffset = Quaternion.Inverse(seatPoint.rotation) * transform.rotation;
        isLocked = true;
        KeepInSeat();
    }

    void OnDisable()
    {
        Application.onBeforeRender -= BeforeRender;
        InputDevices.deviceConnected -= OnHeadsetConnected;
        headsetWasReady = false;
        calibrationPending = false;
        isLocked = false;
    }
}
