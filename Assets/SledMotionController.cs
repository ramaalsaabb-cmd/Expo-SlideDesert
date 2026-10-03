using UnityEngine;

public class SledMotionController : MonoBehaviour
{
    [Header("References")]
    public Transform sledRoot;
    public Terrain terrain;

    [Header("Fixed Front Lift")]
    public float fixedPitch = -10f;

    [Header("Turning")]
    public float maxRoll = 6f;
    public float rollSensitivity = 0.08f;
    public float rollSmoothTime = 0.15f;
    public float turnDeadZone = 1f;

    [Header("Terrain / Bumps")]
    public float rideHeight = 0.3f;
    public float sampleDistance = 1.5f;
    public float verticalSmoothTime = 0.1f;

    public float bumpDeadZone = 0.02f;
    public float maxBumpVelocity = 2f;
    public float bumpSmoothTime = 0.12f;

    [Header("OUTPUT VALUES")]
    public float currentRoll;
    public float currentBump;
    public float currentVertical;

    [Header("Debug")]
    public float sledTurnRate;
    public float rawBump;

    public float frontTerrainHeight;
    public float centerTerrainHeight;
    public float backTerrainHeight;

    private float previousSledYaw;
    private float previousAverageTerrainHeight;

    private float rollVelocity;
    private float verticalVelocity;
    private float bumpSmoothVelocity;

    void Start()
    {
        if (sledRoot != null)
        {
            previousSledYaw = sledRoot.eulerAngles.y;
        }

        if (terrain != null)
        {
            ReadTerrainHeights();

            previousAverageTerrainHeight =
                GetAverageTerrainHeight();

            currentVertical =
                previousAverageTerrainHeight + rideHeight;
        }
    }

    void Update()
    {
        CalculateRoll();
        CalculateTerrainMotion();
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

        // Sled turning speed in degrees per second
        sledTurnRate =
            yawDifference /
            Mathf.Max(Time.deltaTime, 0.0001f);

        // Ignore tiny rotation noise
        if (Mathf.Abs(sledTurnRate) < turnDeadZone)
        {
            sledTurnRate = 0f;
        }

        // Convert turn rate to roll angle
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
        if (Mathf.Abs(currentRoll) < 0.05f &&
            sledTurnRate == 0f)
        {
            currentRoll = 0f;
            rollVelocity = 0f;
        }

        previousSledYaw = currentYaw;
    }

    // =========================================
    // TERRAIN SAMPLING
    // =========================================

    void ReadTerrainHeights()
    {
        if (terrain == null)
            return;

        Vector3 centerPoint =
            transform.position;

        Vector3 forwardDirection =
            sledRoot != null
            ? sledRoot.forward
            : transform.forward;

        Vector3 frontPoint =
            centerPoint +
            forwardDirection * sampleDistance;

        Vector3 backPoint =
            centerPoint -
            forwardDirection * sampleDistance;

        centerTerrainHeight =
            terrain.SampleHeight(centerPoint)
            + terrain.transform.position.y;

        frontTerrainHeight =
            terrain.SampleHeight(frontPoint)
            + terrain.transform.position.y;

        backTerrainHeight =
            terrain.SampleHeight(backPoint)
            + terrain.transform.position.y;
    }

    float GetAverageTerrainHeight()
    {
        return (
            frontTerrainHeight +
            centerTerrainHeight +
            backTerrainHeight
        ) / 3f;
    }

    // =========================================
    // TERRAIN / BUMP
    // =========================================

    void CalculateTerrainMotion()
    {
        if (terrain == null)
            return;

        ReadTerrainHeights();

        float averageTerrainHeight =
            GetAverageTerrainHeight();

        // -------------------------
        // Vertical sled position
        // -------------------------

        float targetVertical =
            averageTerrainHeight + rideHeight;

        currentVertical =
            Mathf.SmoothDamp(
                currentVertical,
                targetVertical,
                ref verticalVelocity,
                verticalSmoothTime
            );

        // -------------------------
        // Raw bump
        // -------------------------

        rawBump =
            (averageTerrainHeight -
             previousAverageTerrainHeight)
            /
            Mathf.Max(Time.deltaTime, 0.0001f);

        // Ignore tiny terrain noise
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

        // -------------------------
        // Smooth hardware output
        // -------------------------

        currentBump =
            Mathf.SmoothDamp(
                currentBump,
                rawBump,
                ref bumpSmoothVelocity,
                bumpSmoothTime
            );

        // Return exactly to zero
        if (rawBump == 0f &&
            Mathf.Abs(currentBump) < 0.02f)
        {
            currentBump = 0f;
            bumpSmoothVelocity = 0f;
        }

        previousAverageTerrainHeight =
            averageTerrainHeight;
    }

    // =========================================
    // APPLY MOTION
    // =========================================

    void ApplyMotion()
{
    float yaw =
        sledRoot != null
        ? sledRoot.eulerAngles.y
        : transform.eulerAngles.y;

    transform.rotation =
        Quaternion.Euler(
            fixedPitch,
            yaw,
            -currentRoll
        );
}
}