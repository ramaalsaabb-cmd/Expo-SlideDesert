using UnityEngine;

public class CarTerrainFollow : MonoBehaviour
{
    [Header("Terrains")]
    public Terrain[] terrains;

    [Header("Car Height")]
    public float groundOffset = 0.5f;
    public float heightSmooth = 10f;

    [Header("Hill Tilt")]
    public float sampleDistance = 1.5f;
    public float rotationSmooth = 8f;

    private Terrain currentTerrain;

    void LateUpdate()
    {
        // Find terrain under the car
        currentTerrain = GetTerrainAtPosition(transform.position);

        if (currentTerrain == null)
            return;

        Vector3 carPosition = transform.position;

        // Get ground height
        float groundHeight =
            currentTerrain.SampleHeight(carPosition)
            + currentTerrain.transform.position.y;

        // Follow terrain height
        carPosition.y = Mathf.Lerp(
            carPosition.y,
            groundHeight + groundOffset,
            heightSmooth * Time.deltaTime
        );

        transform.position = carPosition;

        // =====================================
        // HILL TILT
        // =====================================

        Vector3 frontPoint =
            transform.position +
            transform.forward * sampleDistance;

        Vector3 backPoint =
            transform.position -
            transform.forward * sampleDistance;

        Terrain frontTerrain =
            GetTerrainAtPosition(frontPoint);

        Terrain backTerrain =
            GetTerrainAtPosition(backPoint);

        if (frontTerrain == null)
            frontTerrain = currentTerrain;

        if (backTerrain == null)
            backTerrain = currentTerrain;

        float frontHeight =
            frontTerrain.SampleHeight(frontPoint)
            + frontTerrain.transform.position.y;

        float backHeight =
            backTerrain.SampleHeight(backPoint)
            + backTerrain.transform.position.y;

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

    // =========================================
    // FIND TERRAIN UNDER POSITION
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
                position.x <= terrainPosition.x + terrainSize.x;

            bool insideZ =
                position.z >= terrainPosition.z &&
                position.z <= terrainPosition.z + terrainSize.z;

            if (insideX && insideZ)
            {
                return terrain;
            }
        }

        return null;
    }
}