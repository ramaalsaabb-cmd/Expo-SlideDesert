using UnityEngine;

public class XRSeatController : MonoBehaviour
{
    [Header("References")]
    public Transform seatPoint;
    public Transform headCamera;

    [Header("Start Delay")]
    public float resetDelay = 0.5f;

    private Vector3 lockedLocalPosition;
    private Quaternion lockedLocalRotation;

    private bool isLocked = false;

    void Start()
    {
        Invoke(nameof(ResetAndLock), resetDelay);
    }

    void LateUpdate()
    {
        if (!isLocked)
            return;

        // Keep XR Origin attached to the sled
        transform.localPosition = lockedLocalPosition;
        transform.localRotation = lockedLocalRotation;
    }

    public void ResetAndLock()
    {
        if (seatPoint == null || headCamera == null)
        {
            Debug.LogWarning("XRSeatController: Missing references.");
            return;
        }

        // =========================================
        // 1. FIX DIRECTION
        // Make the player's current view face
        // the same direction as SeatPoint
        // =========================================

        Vector3 headForward = headCamera.forward;
        headForward.y = 0f;
        headForward.Normalize();

        Vector3 targetForward = seatPoint.forward;
        targetForward.y = 0f;
        targetForward.Normalize();

        float angleDifference =
            Vector3.SignedAngle(
                headForward,
                targetForward,
                Vector3.up
            );

        transform.Rotate(
            0f,
            angleDifference,
            0f,
            Space.World
        );

        // =========================================
        // 2. FIX POSITION
        // Put the player's HEAD at SeatPoint
        // =========================================

        Vector3 positionDifference =
            seatPoint.position - headCamera.position;

        transform.position += positionDifference;

        // =========================================
        // 3. SAVE CORRECT POSITION + ROTATION
        // =========================================

        lockedLocalPosition = transform.localPosition;
        lockedLocalRotation = transform.localRotation;

        isLocked = true;

        Debug.Log("XR Player reset and locked to seat.");
    }
}