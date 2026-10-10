using UnityEngine;

/// <summary>SeatPoint is the neutral eye position. Recenter the origin, never the tracked head.</summary>
[DefaultExecutionOrder(200)]
public class XRSeatController : MonoBehaviour
{
    public Transform seatPoint;
    public Transform headCamera;
    public float resetDelay = 0.5f;
    Vector3 lockedLocalPosition;
    Quaternion lockedLocalRotation;
    bool isLocked;

    void OnEnable() { BeginSeatCalibration(); }

    public void BeginSeatCalibration()
    {
        CancelInvoke(nameof(ResetAndLock));
        isLocked = false;
        Invoke(nameof(ResetAndLock), Mathf.Max(0.1f, resetDelay));
    }

    void LateUpdate()
    {
        if (!isLocked) return;
        transform.localPosition = lockedLocalPosition;
        transform.localRotation = lockedLocalRotation;
    }

    [ContextMenu("Recenter player in seat")]
    public void ResetAndLock()
    {
        if (!seatPoint || !headCamera)
        {
            Debug.LogWarning("XRSeatController: Missing seat or head reference.", this);
            return;
        }
        if (seatPoint.IsChildOf(transform))
        {
            Debug.LogWarning("SeatPoint must be outside the XR Origin hierarchy.", this);
            return;
        }
        // Match the seat's neutral orientation, then compensate only tracked yaw.
        transform.rotation = seatPoint.rotation;
        Vector3 up = seatPoint.up;
        Vector3 headForward = Vector3.ProjectOnPlane(headCamera.forward, up);
        Vector3 seatForward = Vector3.ProjectOnPlane(seatPoint.forward, up);
        if (headForward.sqrMagnitude > 0.001f && seatForward.sqrMagnitude > 0.001f)
            transform.Rotate(up, Vector3.SignedAngle(headForward, seatForward, up), Space.World);
        transform.position += seatPoint.position - headCamera.position;
        lockedLocalPosition = transform.localPosition;
        lockedLocalRotation = transform.localRotation;
        isLocked = true;
        Debug.Log("[XR Seat] Head aligned to SeatPoint; tracked head movement remains active.", this);
    }

    void OnDisable() { CancelInvoke(); isLocked = false; }
}
