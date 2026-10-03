using UnityEngine;

public class SledMotionController : MonoBehaviour
{
    [Header("References")]
    public Transform car;
    public Terrain terrain;

    [Header("Turning")]
    public float maxRoll = 15f;
    public float rollSensitivity = 0.4f;
    public float rollSmooth = 4f;

    [Header("Bumps")]
    public float sledLength = 2f;
    public float rideHeight = 0.3f;
    public float bumpSmooth = 6f;

    [Header("OUTPUT VALUES")]
    public float currentRoll;
    public float currentPitch;
    public float currentVertical;

    private float previousCarYaw;
    private float baseY;

    void Start()
    {
        previousCarYaw = car.eulerAngles.y;
        baseY = transform.position.y;
    }

    void Update()
    {
        CalculateTurn();
        CalculateTerrain();
        ApplyMotion();
    }

    void CalculateTurn()
    {
        float currentYaw = car.eulerAngles.y;

        float yawDifference =
            Mathf.DeltaAngle(previousCarYaw, currentYaw);

        float turnRate = yawDifference / Time.deltaTime;

        float targetRoll =
            Mathf.Clamp(
                turnRate * rollSensitivity,
                -maxRoll,
                maxRoll
            );

        currentRoll = Mathf.Lerp(
            currentRoll,
            targetRoll,
            rollSmooth * Time.deltaTime
        );

        previousCarYaw = currentYaw;
    }

    void CalculateTerrain()
    {
        Vector3 frontPoint =
            transform.position +
            transform.forward * (sledLength / 2f);

        Vector3 backPoint =
            transform.position -
            transform.forward * (sledLength / 2f);

        float frontHeight =
            terrain.SampleHeight(frontPoint)
            + terrain.transform.position.y;

        float backHeight =
            terrain.SampleHeight(backPoint)
            + terrain.transform.position.y;

        float averageHeight =
            (frontHeight + backHeight) / 2f;

        float targetVertical =
            averageHeight + rideHeight;

        currentVertical = Mathf.Lerp(
            currentVertical,
            targetVertical,
            bumpSmooth * Time.deltaTime
        );

        float heightDifference =
            frontHeight - backHeight;

        float targetPitch =
            Mathf.Atan2(
                heightDifference,
                sledLength
            ) * Mathf.Rad2Deg;

        currentPitch = Mathf.Lerp(
            currentPitch,
            targetPitch,
            bumpSmooth * Time.deltaTime
        );
    }

    void ApplyMotion()
    {
        // ارتفاع السليد
        Vector3 position = transform.position;
        position.y = currentVertical;
        transform.position = position;

        // الميلان
        Vector3 rotation = transform.eulerAngles;

        transform.rotation = Quaternion.Euler(
            currentPitch,
            rotation.y,
            -currentRoll
        );
    }
}
