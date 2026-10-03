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

    void LateUpdate()
    {
        if (terrain == null)
            return;

        // ===== ارتفاع السيارة =====

        Vector3 carPosition = transform.position;

        float groundHeight =
            terrain.SampleHeight(carPosition)
            + terrain.transform.position.y;

        carPosition.y = Mathf.Lerp(
            carPosition.y,
            groundHeight + groundOffset,
            heightSmooth * Time.deltaTime
        );

        transform.position = carPosition;


        // ===== ميل السيارة مع الجبل =====

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

        frontPoint.y = frontHeight;
        backPoint.y = backHeight;

        Vector3 slopeDirection =
            (frontPoint - backPoint).normalized;

        if (slopeDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(
                    slopeDirection,
                    Vector3.up
                );

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSmooth * Time.deltaTime
                );
        }
    }
}