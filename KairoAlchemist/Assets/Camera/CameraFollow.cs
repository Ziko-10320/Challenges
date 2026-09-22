using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target;

    [Header("Follow Settings")]
    [Range(0.01f, 1.0f)]
    [SerializeField] private float smoothSpeed = 0.125f;
    [SerializeField] private Vector3 baseOffset = new Vector3(0, 0, -10);

    [Header("Axis Locking")]
    [SerializeField] private bool lockYAxis = false;

    void LateUpdate()
    {
        if (target == null)
        {
            Debug.LogWarning("CameraFollow script has no target assigned!");
            return;
        }

        // --- 1. CALCULATE DESIRED POSITION ---
        Vector3 desiredPosition = target.position + baseOffset;

        // --- 2. APPLY Y-AXIS LOCK (IF ENABLED) ---
        if (lockYAxis)
        {
            desiredPosition.y = transform.position.y;
        }

        // --- 3. SMOOTHLY MOVE THE CAMERA ---
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

        // --- 4. APPLY THE FINAL POSITION ---
        transform.position = smoothedPosition;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}