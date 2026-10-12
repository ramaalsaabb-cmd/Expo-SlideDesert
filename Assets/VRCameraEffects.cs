using UnityEngine;

public class VRCameraEffects : MonoBehaviour
{
    [Header("References")]
    public SledMotionController sledMotion;
    public Transform sledRoot;

    [Header("Turn Roll")]
    public float turnTiltMultiplier = 0.7f;
    public float maxTurnTilt = 4f;
    public float turnSmooth = 8f;

    [Header("Hill Pitch")]
    public float pitchMultiplier = 0.45f;
    public float maxPitch = 5f;
    public float pitchSmooth = 6f;

    [Header("Bump Kick")]
    public float bumpMoveMultiplier = 0.05f;
    public float maxBumpMove = 0.08f;
    public float bumpSmooth = 14f;

    [Header("Impact Kick")]
    public float impactKick = 0.04f;
    public float impactReturnSpeed = 8f;

    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;

    private float currentRoll;
    private float currentPitch;
    private float currentBump;

    private float impactOffset;
    private bool previousBumpActive;

    // Expose only authored translation, so seat locking does not erase bump kicks.
    public Vector3 WorldPositionOffset => transform.parent
        ? transform.parent.TransformVector(Vector3.up * (currentBump + impactOffset))
        : Vector3.up * (currentBump + impactOffset);

    void Awake()
    {
        startLocalPosition = transform.localPosition;
        startLocalRotation = transform.localRotation;
    }

    void LateUpdate()
    {
        if (sledMotion == null || sledRoot == null)
            return;

        CalculateTurn();
        CalculatePitch();
        CalculateBump();

        ApplyEffects();
    }

    // =====================================
    // TURN / ROLL
    // =====================================

    void CalculateTurn()
    {
        float targetRoll =
            Mathf.Clamp(
                -sledMotion.currentRoll * turnTiltMultiplier,
                -maxTurnTilt,
                maxTurnTilt
            );

        currentRoll = Mathf.Lerp(
            currentRoll,
            targetRoll,
            turnSmooth * Time.deltaTime
        );
    }

    // =====================================
    // HILL / PITCH
    // =====================================

    void CalculatePitch()
    {
        // Get the sled's actual forward slope
        float sledPitch = sledRoot.eulerAngles.x;

        if (sledPitch > 180f)
            sledPitch -= 360f;

        float targetPitch =
            Mathf.Clamp(
                sledPitch * pitchMultiplier,
                -maxPitch,
                maxPitch
            );

        currentPitch = Mathf.Lerp(
            currentPitch,
            targetPitch,
            pitchSmooth * Time.deltaTime
        );
    }

    // =====================================
    // BUMP
    // =====================================

    void CalculateBump()
    {
        float targetBump =
            Mathf.Clamp(
                sledMotion.currentBump * bumpMoveMultiplier,
                -maxBumpMove,
                maxBumpMove
            );

        currentBump = Mathf.Lerp(
            currentBump,
            targetBump,
            bumpSmooth * Time.deltaTime
        );

        // Strong kick only when a new impact begins
        if (sledMotion.bumpActive && !previousBumpActive)
        {
            impactOffset = impactKick;
        }

        impactOffset = Mathf.MoveTowards(
            impactOffset,
            0f,
            impactReturnSpeed * Time.deltaTime
        );

        previousBumpActive = sledMotion.bumpActive;
    }

    // =====================================
    // APPLY TO CAMERA EFFECTS PIVOT
    // =====================================

    void ApplyEffects()
    {
        // Vertical movement from terrain + impact
        transform.localPosition =
            startLocalPosition +
            Vector3.up * (currentBump + impactOffset);

        // Pitch + Roll
        transform.localRotation =
            startLocalRotation *
            Quaternion.Euler(
                currentPitch,
                0f,
                currentRoll
            );
    }
}