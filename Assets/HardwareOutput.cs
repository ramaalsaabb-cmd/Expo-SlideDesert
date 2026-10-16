using UnityEngine;

/// <summary>Two normalized signals, sampled before the WebSocket sender.</summary>
[DefaultExecutionOrder(900)]
public class HardwareOutput : MonoBehaviour
{
    [Header("References")]
    public SledMotionController sledMotion;
    public CarPathFollower carMovement;
    [Header("Roll: left -10, straight 0, right +10")]
    [Range(-10f,10f)] public float hardwareRoll;
    [Header("Bump strength: none 0, maximum 10")]
    [Range(0f,10f)] public float hardwareBump;
    void OnEnable() { ResetReadings(); }
    void OnDisable() { ResetReadings(); }
    void ResetReadings() { hardwareRoll=hardwareBump=0f; }
    void LateUpdate()
    {
        if(!sledMotion || !sledMotion.isActiveAndEnabled || !sledMotion.sledRoot)
        { ResetReadings();return; }
        // Positive yaw/roll is a right turn; do not invert this signal.
        float roll=Mathf.Abs(sledMotion.currentRoll)<.02f?0f:sledMotion.currentRoll;
        hardwareRoll=HardwareSignalFormat.Roll(roll,sledMotion.maxRoll);
        bool terrainValid=sledMotion.currentTerrain && sledMotion.currentTerrain.terrainData;
        float bump=Mathf.Abs(sledMotion.currentBump)<.01f?0f:sledMotion.currentBump;
        hardwareBump=terrainValid?HardwareSignalFormat.Bump(bump,sledMotion.maxBumpVelocity):0f;
    }
}
