using UnityEngine;

public class SledMotionController : MonoBehaviour
{
    [Header("References")]
    public Transform sledRoot;
    public Terrain terrain;

    [Header("Fixed Front Lift")]
    public float fixedPitch = 0f;

    // =========================================
    // TURNING / ROLL
    // =========================================

    [Header("Turning")]
    public float maxRoll = 6f;
    public float rollSensitivity = 0.08f;
    public float rollSmoothTime = 0.15f;
    public float turnDeadZone = 1f;

    // =========================================
    // TERRAIN / BUMP DETECTION
    // =========================================

    [Header("Terrain / Bumps")]
    public float rideHeight = 0.3f;
    public float sampleDistance = 1.5f;
    public float verticalSmoothTime = 0.1f;

    public float bumpDeadZone = 0.02f;
    public float maxBumpVelocity = 2f;
    public float bumpSmoothTime = 0.12f;

    // =========================================
    // BUMP IMPACT
    // =========================================

    [Header("Bump Impact")]
    public float bumpTrigger = 0.35f;
    public float bumpJumpHeight = 0.15f;
    public float bumpUpSpeed = 10f;
    public float bumpDownSpeed = 6f;

    // =========================================
    // OUTPUT VALUES
    // =========================================

    [Header("OUTPUT VALUES")]
    public float currentRoll;
    public float currentBump;
    public float currentVertical;

    // =========================================
    // DEBUG
    // =========================================

    [Header("Debug")]
    public float sledTurnRate;
    public float rawBump;

    public float frontTerrainHeight;
    public float centerTerrainHeight;
    public float backTerrainHeight;

    public bool bumpActive;
    public bool bumpReady;
    public float visualBumpOffset;

    // =========================================
    // PRIVATE
    // =========================================

    private float previousSledYaw;
    private float previousAverageTerrainHeight;

    private float rollVelocity;
    private float verticalVelocity;
    private float bumpSmoothVelocity;

    private Vector3 pivotStartLocalPosition;

    private bool bumpGoingUp = false;

    // =========================================
    // START
    // =========================================

    void Start()
    {
        // Save original SledTiltPivot position
        pivotStartLocalPosition =
            transform.localPosition;

        // Bump is ready at start
        bumpReady = true;

        // Save starting sled direction
        if (sledRoot != null)
        {
            previousSledYaw =
                sledRoot.eulerAngles.y;
        }

        // Read starting terrain
        if (terrain != null &&
            sledRoot != null)
        {
            ReadTerrainHeights();

            previousAverageTerrainHeight =
                GetAverageTerrainHeight();

            currentVertical =
                previousAverageTerrainHeight
                + rideHeight;
        }
    }

    // =========================================
    // UPDATE
    // =========================================

    void Update()
    {
        CalculateRoll();
        CalculateTerrainMotion();
        UpdateBumpImpact();
        ApplyMotion();
    }

    // =========================================
    // SLED TURN -> ROLL
    // =========================================

    void CalculateRoll()
    {
        if (sledRoot == null)
            return;

        float currentYaw =
            sledRoot.eulerAngles.y;

        float yawDifference =
            Mathf.DeltaAngle(
                previousSledYaw,
                currentYaw
            );

        // Degrees per second
        sledTurnRate =
            yawDifference /
            Mathf.Max(
                Time.deltaTime,
                0.0001f
            );

        // Remove tiny rotation noise
        if (Mathf.Abs(sledTurnRate) < turnDeadZone)
        {
            sledTurnRate = 0f;
        }

        // Convert turning speed into roll
        float targetRoll =
            Mathf.Clamp(
                sledTurnRate * rollSensitivity,
                -maxRoll,
                maxRoll
            );

        // Smooth roll
        currentRoll =
            Mathf.SmoothDamp(
                currentRoll,
                targetRoll,
                ref rollVelocity,
                rollSmoothTime
            );

        // Return exactly to zero
        if (sledTurnRate == 0f &&
            Mathf.Abs(currentRoll) < 0.05f)
        {
            currentRoll = 0f;
            rollVelocity = 0f;
        }

        previousSledYaw =
            currentYaw;
    }

    // =========================================
    // TERRAIN SAMPLING
    // =========================================

    void ReadTerrainHeights()
    {
        if (terrain == null ||
            sledRoot == null)
            return;

        Vector3 centerPoint =
            sledRoot.position;

        Vector3 forwardDirection =
            sledRoot.forward;

        Vector3 frontPoint =
            centerPoint +
            forwardDirection * sampleDistance;

        Vector3 backPoint =
            centerPoint -
            forwardDirection * sampleDistance;

        // Center
        centerTerrainHeight =
            terrain.SampleHeight(centerPoint)
            + terrain.transform.position.y;

        // Front
        frontTerrainHeight =
            terrain.SampleHeight(frontPoint)
            + terrain.transform.position.y;

        // Back
        backTerrainHeight =
            terrain.SampleHeight(backPoint)
            + terrain.transform.position.y;
    }

    float GetAverageTerrainHeight()
    {
        return
            (
                frontTerrainHeight +
                centerTerrainHeight +
                backTerrainHeight
            )
            / 3f;
    }

    // =========================================
    // TERRAIN / BUMP DETECTION
    // =========================================

    void CalculateTerrainMotion()
    {
        if (terrain == null ||
            sledRoot == null)
            return;

        ReadTerrainHeights();

        float averageTerrainHeight =
            GetAverageTerrainHeight();

        // Terrain height information
        float targetVertical =
            averageTerrainHeight + rideHeight;

        currentVertical =
            Mathf.SmoothDamp(
                currentVertical,
                targetVertical,
                ref verticalVelocity,
                verticalSmoothTime
            );

        // =====================================
        // RAW BUMP
        // =====================================

        rawBump =
            (
                averageTerrainHeight -
                previousAverageTerrainHeight
            )
            /
            Mathf.Max(
                Time.deltaTime,
                0.0001f
            );

        // Remove tiny terrain noise
        if (Mathf.Abs(rawBump) < bumpDeadZone)
        {
            rawBump = 0f;
        }

        // Limit extreme values
        rawBump =
            Mathf.Clamp(
                rawBump,
                -maxBumpVelocity,
                maxBumpVelocity
            );

        // =====================================
        // HARDWARE BUMP OUTPUT
        // =====================================

        currentBump =
            Mathf.SmoothDamp(
                currentBump,
                rawBump,
                ref bumpSmoothVelocity,
                bumpSmoothTime
            );

        if (rawBump == 0f &&
            Mathf.Abs(currentBump) < 0.02f)
        {
            currentBump = 0f;
            bumpSmoothVelocity = 0f;
        }

        // =====================================
        // BUMP IMPACT TRIGGER
        // =====================================

        // Trigger ONE jump
        if (rawBump > bumpTrigger &&
            !bumpActive &&
            bumpReady)
        {
            bumpActive = true;
            bumpGoingUp = true;

            // Lock this bump
            bumpReady = false;
        }

        // Re-arm only after leaving the bump
        if (rawBump < bumpTrigger * 0.4f &&
            !bumpActive)
        {
            bumpReady = true;
        }

        previousAverageTerrainHeight =
            averageTerrainHeight;
    }

    // =========================================
    // BUMP IMPACT / JUMP
    // =========================================

    void UpdateBumpImpact()
    {
        if (!bumpActive)
        {
            visualBumpOffset = 0f;
            return;
        }

        // =====================================
        // GO UP
        // =====================================

        if (bumpGoingUp)
        {
            visualBumpOffset =
                Mathf.MoveTowards(
                    visualBumpOffset,
                    bumpJumpHeight,
                    bumpUpSpeed * Time.deltaTime
                );

            // Reached top
            if (visualBumpOffset >=
                bumpJumpHeight - 0.001f)
            {
                visualBumpOffset =
                    bumpJumpHeight;

                bumpGoingUp = false;
            }
        }

        // =====================================
        // COME DOWN
        // =====================================

        else
        {
            visualBumpOffset =
                Mathf.MoveTowards(
                    visualBumpOffset,
                    0f,
                    bumpDownSpeed * Time.deltaTime
                );

            // Finished jump
            if (visualBumpOffset <= 0.001f)
            {
                visualBumpOffset = 0f;
                bumpActive = false;
            }
        }
    }

    // =========================================
    // APPLY MOTION TO SledTiltPivot
    // =========================================

    void ApplyMotion()
    {
        // =====================================
        // PITCH + ROLL
        // =====================================

        transform.localRotation =
            Quaternion.Euler(
                fixedPitch,
                0f,
                -currentRoll
            );

        // =====================================
        // BUMP JUMP
        // =====================================

        Vector3 targetPosition =
            pivotStartLocalPosition;

        targetPosition.y +=
            visualBumpOffset;

        transform.localPosition =
            targetPosition;
    }
}