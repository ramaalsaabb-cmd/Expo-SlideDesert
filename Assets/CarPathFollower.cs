using System.Collections.Generic;
using UnityEngine;

public class CarPathFollower : MonoBehaviour
{
    [Header("Path")]
    public Transform[] points;
    public float speed = 25f;
    public float turnSpeed = 120f;
    public float reachDistance = 1.5f;
    [Header("Smooth driving / drift")]
    public float acceleration = 14f;
    public float maxDriftAngle = 24f;
    public float driftSmoothTime = 0.35f;
    public float driftLookAhead = 12f;
    [Header("Wheels")]
    public Transform[] wheels;
    public float wheelRadius = 0.35f;
    readonly List<Vector3> path = new List<Vector3>();
    readonly List<float> distances = new List<float>();
    float pathDistance, actualSpeed, driftAngle, driftVelocity;
    bool routeCompleted;
    public float CurrentSpeed => actualSpeed;

    void Start()
    {
        var anchors = new List<Vector3> { transform.position };
        if (points != null) foreach (var point in points)
            if (point && Vector3.ProjectOnPlane(point.position - anchors[anchors.Count - 1], Vector3.up).sqrMagnitude > 0.01f)
                anchors.Add(point.position);
        if (anchors.Count < 2) return;
        path.Add(anchors[0]); distances.Add(0f);
        for (int i = 0; i < anchors.Count - 1; i++)
        {
            Vector3 a = anchors[Mathf.Max(0, i - 1)], b = anchors[i];
            Vector3 c = anchors[i + 1], d = anchors[Mathf.Min(anchors.Count - 1, i + 2)];
            int samples = Mathf.Max(12, Mathf.CeilToInt(Vector3.Distance(b, c) / 1.5f));
            for (int j = 1; j <= samples; j++)
            {
                float t = j / (float)samples;
                Vector3 position = 0.5f * ((2f * b) + (-a + c) * t
                    + (2f * a - 5f * b + 4f * c - d) * t * t
                    + (-a + 3f * b - 3f * c + d) * t * t * t);
                float length = Vector3.ProjectOnPlane(position - path[path.Count - 1], Vector3.up).magnitude;
                if (length < 0.001f) continue;
                distances.Add(distances[distances.Count - 1] + length);
                path.Add(position);
            }
        }
    }

    Vector3 Sample(float distance)
    {
        distance = Mathf.Clamp(distance, 0f, distances[distances.Count - 1]);
        int index = distances.BinarySearch(distance);
        if (index >= 0) return path[index];
        index = ~index;
        if (index <= 0) return path[0];
        if (index >= path.Count) return path[path.Count - 1];
        float fraction = (distance - distances[index - 1]) / (distances[index] - distances[index - 1]);
        return Vector3.Lerp(path[index - 1], path[index], fraction);
    }

    void Update()
    {
        if (path.Count < 2) return;
        float remaining = distances[distances.Count - 1] - pathDistance;
        // Maintain speed through intermediate points; brake only at the end of the route.
        float targetSpeed = Mathf.Min(Mathf.Max(0f, speed), Mathf.Sqrt(2f * Mathf.Max(1f, acceleration) * remaining));
        actualSpeed = Mathf.MoveTowards(actualSpeed, targetSpeed, Mathf.Max(1f, acceleration) * Time.deltaTime);
        pathDistance = Mathf.Min(distances[distances.Count - 1], pathDistance + actualSpeed * Time.deltaTime);
        Vector3 position = Sample(pathDistance);
        position.y = transform.position.y;
        transform.position = position;
        Vector3 tangent = Vector3.ProjectOnPlane(Sample(pathDistance + 2f) - Sample(pathDistance - 2f), Vector3.up);
        Vector3 future = Vector3.ProjectOnPlane(Sample(pathDistance + driftLookAhead) - Sample(pathDistance), Vector3.up);
        float corner = tangent.sqrMagnitude > 0.001f && future.sqrMagnitude > 0.001f
            ? Vector3.SignedAngle(tangent, future, Vector3.up) : 0f;
        float targetDrift = Mathf.Clamp(corner * 1.4f, -maxDriftAngle, maxDriftAngle)
            * Mathf.Clamp01(actualSpeed / 10f);
        driftAngle = Mathf.SmoothDamp(driftAngle, targetDrift, ref driftVelocity, Mathf.Max(0.05f, driftSmoothTime));
        if (tangent.sqrMagnitude > 0.001f)
        {
            // Heading and travel direction differ on turns, producing a controlled slide.
            Quaternion rotation = Quaternion.LookRotation(tangent) * Quaternion.Euler(0f, driftAngle, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation,
                1f - Mathf.Exp(-Mathf.Max(1f, turnSpeed / 12f) * Time.deltaTime));
        }
        if (pathDistance >= distances[distances.Count - 1] - 0.05f)
        {
            actualSpeed = 0f;
            if (!routeCompleted)
            {
                var transition = FindAnyObjectByType<DesertSceneTransition>();
                if (!transition) transition = new GameObject("End Of Route Transition").AddComponent<DesertSceneTransition>();
                routeCompleted = transition.TryBegin();
            }
        }
        if (wheels != null && wheelRadius > 0f)
            foreach (var wheel in wheels)
                if (wheel) wheel.Rotate(Vector3.right, actualSpeed / wheelRadius * Mathf.Rad2Deg * Time.deltaTime, Space.Self);
    }
}


