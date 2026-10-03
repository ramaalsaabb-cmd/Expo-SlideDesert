using UnityEngine;

public class CarPathFollower : MonoBehaviour
{
    [Header("Path")]
    public Transform[] points;
    public float speed = 8f;
    public float turnSpeed = 120f;
    public float reachDistance = 1.5f;

    [Header("Wheels")]
    public Transform[] wheels;
    public float wheelRadius = 0.35f;

    private int currentPoint = 0;

    void Update()
    {
        MoveCar();
        SpinWheels();
    }

    void MoveCar()
    {
        if (points == null || points.Length == 0)
            return;

        if (currentPoint >= points.Length)
            return;

        Transform target = points[currentPoint];

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        // تلف السيارة للنقطة القادمة
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime
            );
        }

        // تحريك السيارة للأمام
        transform.position +=
            transform.forward * speed * Time.deltaTime;

        // الوصول للنقطة
        Vector3 carPosition = transform.position;
        Vector3 targetPosition = target.position;

        carPosition.y = 0f;
        targetPosition.y = 0f;

        if (Vector3.Distance(carPosition, targetPosition) < reachDistance)
        {
            currentPoint++;
        }
    }

    void SpinWheels()
    {
        if (wheelRadius <= 0f)
            return;

        float rotationSpeed =
            (speed / wheelRadius) * Mathf.Rad2Deg;

        foreach (Transform wheel in wheels)
        {
            if (wheel != null)
            {
                wheel.Rotate(
                    Vector3.right,
                    rotationSpeed * Time.deltaTime,
                    Space.Self
                );
            }
        }
    }
}
