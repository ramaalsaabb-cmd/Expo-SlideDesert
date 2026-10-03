using UnityEngine;

public class SledMotionController : MonoBehaviour
{
    [Header("References")]
    public Transform sledRoot;

    [Header("Terrains")]
    public Terrain[] terrains;

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
    public float bumpResetThreshold = 0.10f;

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

    public Terrain currentTerrain;

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
        pivotStartLocalPosition =
            transform.localPosition;

        bumpReady = true;

        if (sledRoot != null)
        {
            previousSledYaw =
                sledRoot.eulerAngles.y;

            currentTerrain =
                GetTerrainAtPosition(sledRoot.position);
        }

        if (currentTerrain != null &&
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
        if (sledRoot != null)
        {
            currentTerrain =
                GetTerrainAtPosition(sledRoot.position);
        }

        CalculateRoll();
        CalculateTerrainMotion();
        UpdateBumpImpact();
        ApplyMotion();
    }

    // =========================================
    // ROLL
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

        sledTurnRate =
            yawDifference /
            Mathf.Max(
                Time.deltaTime,
                0.0001f
            );

        if (Mathf.Abs(sledTurnRate)
            < turnDeadZone)
        {
            sledTurnRate = 0f;
        }

        float targetRoll =
            Mathf.Clamp(
                sledTurnRate * rollSensitivity,
                -maxRoll,
                maxRoll
            );

        currentRoll =
            Mathf.SmoothDamp(
                currentRoll,
                targetRoll,
                ref rollVelocity,
                rollSmoothTime
            );

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
        if (sledRoot == null)
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

        // Each point can be on a different Terrain
        Terrain centerTerrain =
            GetTerrainAtPosition(centerPoint);

        Terrain frontTerrain =
            GetTerrainAtPosition(frontPoint);

        Terrain backTerrain =
            GetTerrainAtPosition(backPoint);

        // CENTER
        if (centerTerrain != null)
        {
            centerTerrainHeight =
                centerTerrain.SampleHeight(centerPoint)
                + centerTerrain.transform.position.y;
        }

        // FRONT
        if (frontTerrain != null)
        {
            frontTerrainHeight =
                frontTerrain.SampleHeight(frontPoint)
                + frontTerrain.transform.position.y;
        }
        else
        {
            frontTerrainHeight =
                centerTerrainHeight;
        }

        // BACK
        if (backTerrain != null)
        {
            backTerrainHeight =
                backTerrain.SampleHeight(backPoint)
                + backTerrain.transform.position.y;
        }
        else
        {
            backTerrainHeight =
                centerTerrainHeight;
        }
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
    // TERRAIN / BUMP
    // =========================================

    void CalculateTerrainMotion()
    {
        if (sledRoot == null ||
            currentTerrain == null)
            return;

        ReadTerrainHeights();

        float averageTerrainHeight =
            GetAverageTerrainHeight();

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

        if (Mathf.Abs(rawBump)
            < bumpDeadZone)
        {
            rawBump = 0f;
        }

        rawBump =
            Mathf.Clamp(
                rawBump,
                -maxBumpVelocity,
                maxBumpVelocity
            );

        // =====================================
        // HARDWARE BUMP
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
        // ONE IMPACT PER BUMP
        // =====================================

        if (rawBump > bumpTrigger &&
            bumpReady)
        {
            bumpActive = true;
            bumpGoingUp = true;

            bumpReady = false;
        }

        if (!bumpActive &&
            Mathf.Abs(rawBump) < bumpResetThreshold)
        {
            bumpReady = true;
        }

        previousAverageTerrainHeight =
            averageTerrainHeight;
    }

    // =========================================
    // BUMP IMPACT
    // =========================================

    void UpdateBumpImpact()
    {
        if (!bumpActive)
        {
            visualBumpOffset = 0f;
            return;
        }

        // GO UP
        if (bumpGoingUp)
        {
            visualBumpOffset =
                Mathf.MoveTowards(
                    visualBumpOffset,
                    bumpJumpHeight,
                    bumpUpSpeed * Time.deltaTime
                );

            if (visualBumpOffset >=
                bumpJumpHeight - 0.001f)
            {
                visualBumpOffset =
                    bumpJumpHeight;

                bumpGoingUp = false;
            }
        }

        // COME DOWN
        else
        {
            visualBumpOffset =
                Mathf.MoveTowards(
                    visualBumpOffset,
                    0f,
                    bumpDownSpeed * Time.deltaTime
                );

            if (visualBumpOffset <= 0.001f)
            {
                visualBumpOffset = 0f;
                bumpActive = false;
            }
        }
    }

    // =========================================
    // APPLY MOTION
    // =========================================

    void ApplyMotion()
    {
        transform.localRotation =
            Quaternion.Euler(
                fixedPitch,
                0f,
                -currentRoll
            );

        Vector3 targetPosition =
            pivotStartLocalPosition;

        targetPosition.y +=
            visualBumpOffset;

        transform.localPosition =
            targetPosition;
    }

    // =========================================
    // FIND TERRAIN AT POSITION
    // =========================================

    Terrain GetTerrainAtPosition(Vector3 position)
    {
        if (terrains == null)
            return null;

        foreach (Terrain terrain in terrains)
        {
            if (terrain == null)
                continue;

            Vector3 terrainPosition =
                terrain.transform.position;

            Vector3 terrainSize =
                terrain.terrainData.size;

            bool insideX =
                position.x >= terrainPosition.x &&
                position.x <=
                terrainPosition.x + terrainSize.x;

            bool insideZ =
                position.z >= terrainPosition.z &&
                position.z <=
                terrainPosition.z + terrainSize.z;

            if (insideX && insideZ)
            {
                return terrain;
            }
        }

        return null;
    }
}