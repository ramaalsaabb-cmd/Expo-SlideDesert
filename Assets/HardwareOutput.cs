using UnityEngine;

/// <summary>Sample finished sled motion before the WebSocket sender's LateUpdate.</summary>
[DefaultExecutionOrder(900)]
public class HardwareOutput : MonoBehaviour
{
    [Header("References")]
    public SledMotionController sledMotion;
    public CarPathFollower carMovement;
    [Header("Hardware Output - degrees and metres per second")]
    public float hardwareRoll;
    public float hardwareBump;
    public float hardwareSpeed;
    public bool bumpImpact;
    public string turnDirection = "STRAIGHT";
    [Header("Telemetry Settings")]
    public float turnDeadZone = 1f;
    [Range(0f,1f)] public float directionReleaseRatio = 0.6f;
    [Min(0f)] public float speedSmoothSeconds = 0.12f;
    [Min(0f)] public float stoppedSpeed = 0.05f;
    [Min(0.05f)] public float impactRetentionSeconds = 0.25f;
    [Min(1f)] public float maximumValidSpeed = 100f;
    public bool readingsValid;
    readonly SledImpactLatch impacts = new SledImpactLatch();
    Vector3 previousPosition;
    Transform speedSource;
    uint observedImpact;
    bool positionReady;
    public uint ImpactSequence { get { return impacts.Sequence; } }
    void OnEnable() { ResetReadings(); }
    void OnDisable() { ResetReadings(); }
    void ResetReadings()
    {
        hardwareRoll=hardwareBump=hardwareSpeed=0f;
        bumpImpact=false;turnDirection="STRAIGHT";readingsValid=false;
        impacts.Clear();positionReady=false;speedSource=null;
        observedImpact=sledMotion ? sledMotion.ImpactSequence : 0;
    }
    void LateUpdate()
    {
        if (!sledMotion || !sledMotion.isActiveAndEnabled || !sledMotion.sledRoot)
        { ResetReadings();return; }
        float dt=Time.deltaTime;
        if(dt<=0f || !SledTelemetryMath.IsFinite(dt)) return;
        readingsValid = sledMotion.currentTerrain && sledMotion.currentTerrain.terrainData;
        hardwareRoll = SledTelemetryMath.ClampFinite(-sledMotion.currentRoll,sledMotion.maxRoll);
        if(Mathf.Abs(hardwareRoll)<0.02f) hardwareRoll=0f;
        hardwareBump = readingsValid ? SledTelemetryMath.ClampFinite(sledMotion.currentBump,sledMotion.maxBumpVelocity) : 0f;
        if(Mathf.Abs(hardwareBump)<0.01f) hardwareBump=0f;
        // Measure the sled's actual horizontal travel, not the car's requested speed.
        Transform source=sledMotion.sledRoot;
        Vector3 position=source.position;
        if(positionReady && speedSource==source)
        {
            Vector3 movement=position-previousPosition;movement.y=0f;
            float measured=movement.magnitude/dt;
            if(!SledTelemetryMath.IsFinite(measured) || measured>Mathf.Max(1f,maximumValidSpeed))
                hardwareSpeed=0f; // Re-seed after a teleport rather than report a speed spike.
            else
            {
                hardwareSpeed=SledTelemetryMath.Smooth(hardwareSpeed,measured,speedSmoothSeconds,dt);
                if(measured<stoppedSpeed && hardwareSpeed<stoppedSpeed) hardwareSpeed=0f;
            }
        }
        else hardwareSpeed=0f;
        speedSource=source;previousPosition=position;positionReady=true;
        if(readingsValid && sledMotion.ImpactSequence!=observedImpact)
            impacts.Trigger(Time.unscaledTimeAsDouble,impactRetentionSeconds);
        observedImpact=sledMotion.ImpactSequence;
        if(!readingsValid) impacts.Clear();
        bumpImpact=readingsValid && (sledMotion.bumpActive || impacts.IsPending(Time.unscaledTimeAsDouble));
        turnDirection=SledTelemetryMath.Direction(hardwareRoll,turnDirection,
            turnDeadZone,turnDeadZone*Mathf.Clamp01(directionReleaseRatio));
    }
    public void AcknowledgeImpact(uint sequence) { impacts.Acknowledge(sequence); }
}
