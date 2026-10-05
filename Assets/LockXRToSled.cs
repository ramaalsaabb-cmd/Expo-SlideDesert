using UnityEngine;

public class LockXRToSled : MonoBehaviour
{
    [Header("Sled Reference")]
    public Transform sledMotion;

    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;

    void Start()
    {
        // Save XR Origin position relative to the sled
        startLocalPosition = transform.localPosition;
        startLocalRotation = transform.localRotation;
    }

    void LateUpdate()
    {
        if (sledMotion == null)
            return;

        // Keep XR Origin locked to the sled
        transform.localPosition = startLocalPosition;
        transform.localRotation = startLocalRotation;
    }
}