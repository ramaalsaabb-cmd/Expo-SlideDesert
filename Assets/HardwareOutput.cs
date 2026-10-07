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

[Header("Bump")]
public bool bumpDidHappen;

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
    hardwareRoll = -sledMotion.currentRoll;
}

void CalculateBump()
{
    hardwareBump = sledMotion.currentBump;
    bumpDidHappen = sledMotion.bumpActive;
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