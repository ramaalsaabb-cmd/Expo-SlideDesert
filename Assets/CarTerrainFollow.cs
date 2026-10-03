using UnityEngine;

public class CarTerrainFollow : MonoBehaviour
{
    public Terrain terrain;

    [Header("Car Height")]
    public float groundOffset = 0.5f;
    public float heightSmooth = 10f;

    [Header("Hill Tilt")]
    public float sampleDistance = 1.5f;
    public float rotationSmooth = 8f;

    private float currentPitch = 0f;

    void LateUpdate()
    {
        if (terrain == null)
            return;

        FollowTerrainHeight();
        FollowTerrainSlope();
    }

    void FollowTerrainHeight()
    {
        Vector3 carPosition = transform.position;

        float groundHeight =
            terrain.SampleHeight(carPosition)
            + terrain.transform.position.y;

        float targetY =
            groundHeight + groundOffset;

        carPosition.y = Mathf.Lerp(
            carPosition.y,
            targetY,
            heightSmooth * Time.deltaTime
        );

        transform.position = carPosition;
    }

    void FollowTerrainSlope()
    {
        Vector3 frontPoint =
            transform.position +
            transform.forward * sampleDistance;

        Vector3 backPoint =
            transform.position -
            transform.forward * sampleDistance;

        float frontHeight =
            terrain.SampleHeight(frontPoint)
            + terrain.transform.position.y;

        float backHeight =
            terrain.SampleHeight(backPoint)
            + terrain.transform.position.y;

        float heightDifference =
            frontHeight - backHeight;

        float targetPitch =
            -Mathf.Atan2(
                heightDifference,
                sampleDistance * 2f
            ) * Mathf.Rad2Deg;

        currentPitch = Mathf.Lerp(
            currentPitch,
            targetPitch,
            rotationSmooth * Time.deltaTime
        );

        // Y stays controlled by CarPathFollower
        float currentYaw =
            transform.eulerAngles.y;

        transform.rotation =
            Quaternion.Euler(
                currentPitch,
                currentYaw,
                0f
            );
    }
}