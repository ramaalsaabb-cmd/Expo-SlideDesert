using UnityEngine;

public class HardwareOutput : MonoBehaviour
{
    [Header("References")]
    public SledMotionController sledMotion;
    public CarPathFollower carMovement;

    [Header("Hardware Output")]
    public float hardwareRoll;
    public float hardwareBump;
    public float hardwareSpeed;

    // True while the visual bump/jump is happening
    public bool bumpImpact;

    [Header("Direction State")]
    public string turnDirection = "STRAIGHT";

    [Header("Settings")]
    public float turnDeadZone = 1f;

    void Update()
    {
        if (sledMotion == null)
            return;

        CalculateHardwareRoll();
        CalculateBump();
        GetSpeed();
        DetectDirection();
    }

    void CalculateHardwareRoll()
    {
        // Hardware tilts opposite to sled roll
        hardwareRoll = -sledMotion.currentRoll;
    }

    void CalculateBump()
    {
        // Strength / terrain bump value
        hardwareBump = sledMotion.currentBump;

        // Is a jump currently happening?
        bumpImpact = sledMotion.bumpActive;
    }

    void GetSpeed()
    {
        if (carMovement != null)
        {
            hardwareSpeed = carMovement.speed;
        }
    }

    void DetectDirection()
    {
        if (hardwareRoll > turnDeadZone)
        {
            turnDirection = "RIGHT";
        }
        else if (hardwareRoll < -turnDeadZone)
        {
            turnDirection = "LEFT";
        }
        else
        {
            turnDirection = "STRAIGHT";
        }
    }
}