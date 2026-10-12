using System.Collections.Generic;
using UnityEngine;

public class SledFollowCar : MonoBehaviour
{
    public Transform car;
    public float followDistance = 8f;
    public float followSpeed = 4f;
    public float turnSpeed = 5f;
    public float groundFollowSpeed = 12f;
    readonly List<Vector3> trail = new List<Vector3>();
    Vector3 followVelocity;
    float groundOffset;
    bool groundOffsetReady;
    Terrain[] terrains;

    void Start()
    {
        terrains = Terrain.activeTerrains;
        if (car)
        {
            trail.Add(transform.position);
            trail.Add(car.position);
        }
        if (TryGroundHeight(transform.position, out float height))
        {
            groundOffset = transform.position.y - height;
            groundOffsetReady = true;
        }
    }

    void Update()
    {
        if (!car) return;
        if (trail.Count == 0 || (car.position - trail[trail.Count - 1]).sqrMagnitude >= 0.25f)
            trail.Add(car.position);
        Vector3 target = car.position;
        float remaining = Mathf.Max(0f, followDistance);
        int oldestNeeded = 0;
        // Follow the car's actual traveled path instead of cutting across corners.
        for (int i = trail.Count - 1; i > 0; i--)
        {
            float length = Vector3.ProjectOnPlane(trail[i] - trail[i - 1], Vector3.up).magnitude;
            if (length < 0.001f) continue;
            if (remaining <= length)
            {
                target = Vector3.Lerp(trail[i], trail[i - 1], remaining / length);
                oldestNeeded = Mathf.Max(0, i - 2);
                break;
            }
            remaining -= length;
            target = trail[i - 1];
        }
        if (oldestNeeded > 0) trail.RemoveRange(0, oldestNeeded);
        target.y = transform.position.y;
        transform.position = Vector3.SmoothDamp(transform.position, target, ref followVelocity,
            Mathf.Max(0.08f, 1f / Mathf.Max(0.1f, followSpeed)));
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction),
                1f - Mathf.Exp(-Mathf.Max(0f, turnSpeed) * Time.deltaTime));
    }

    void LateUpdate()
    {
        if (!TryGroundHeight(transform.position, out float height)) return;
        if (!groundOffsetReady)
        {
            groundOffset = transform.position.y - height;
            groundOffsetReady = true;
        }
        Vector3 position = transform.position;
        position.y = Mathf.Lerp(position.y, height + groundOffset,
            1f - Mathf.Exp(-Mathf.Max(0f, groundFollowSpeed) * Time.deltaTime));
        transform.position = position;
    }

    bool TryGroundHeight(Vector3 position, out float height)
    {
        height = 0f;
        if (terrains == null) return false;
        foreach (var terrain in terrains)
        {
            if (!terrain || !terrain.terrainData) continue;
            Vector3 local = position - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z) continue;
            height = terrain.SampleHeight(position) + terrain.transform.position.y;
            return true;
        }
        return false;
    }
}
