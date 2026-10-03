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
        // IMPORTANT:
        // Unity sled direction is reversed for the physical hardware.
        //
        // Sled rolls right -> Hardware moves left
        // Sled rolls left  -> Hardware moves right

        hardwareRoll = -sledMotion.currentRoll;
    }

    void CalculateBump()
    {
        // Bump is a separate output channel
        hardwareBump = sledMotion.currentBump;
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