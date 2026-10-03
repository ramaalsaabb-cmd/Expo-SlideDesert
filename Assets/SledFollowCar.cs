using UnityEngine;

public class SledFollowCar : MonoBehaviour
{
    [Header("Car")]
    public Transform car;

    [Header("Follow")]
    public float followDistance = 5f;
    public float followSpeed = 4f;
    public float turnSpeed = 5f;

    void Update()
    {
        if (car == null)
            return;

        // المكان المطلوب خلف السيارة
        Vector3 targetPosition =
            car.position - car.forward * followDistance;

        // نخلي الارتفاع مثل ما هو
        targetPosition.y = transform.position.y;

        // حركة ناعمة خلف السيارة
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSpeed * Time.deltaTime
        );

        // اتجاه السليد نحو السيارة
        Vector3 direction =
            car.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime
            );
        }
    }
}
