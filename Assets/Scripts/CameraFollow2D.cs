using UnityEngine;

public sealed class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float followSpeed = 8f;
    private Vector3 offset;
    private Vector3 followPosition;
    private float shakeStrength;
    private float shakeDuration = 1f;
    private float shakeStartedAt = float.NegativeInfinity;

    private void Awake()
    {
        followPosition = transform.position;
        if (target != null)
            offset = transform.position - target.position;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        offset = followPosition - target.position;
    }

    /// <summary>Shakes the camera, fading out over the duration. Runs on unscaled time so it also settles while paused.</summary>
    public void Shake(float strength, float duration)
    {
        shakeStrength = strength;
        shakeDuration = Mathf.Max(0.01f, duration);
        shakeStartedAt = Time.unscaledTime;
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            Vector3 destination = target.position + offset;
            followPosition = Vector3.Lerp(followPosition, destination, followSpeed * Time.deltaTime);
        }

        float shakeLeft = 1f - (Time.unscaledTime - shakeStartedAt) / shakeDuration;
        Vector3 shakeOffset = shakeLeft > 0f ? (Vector3)(Random.insideUnitCircle * shakeStrength * shakeLeft) : Vector3.zero;
        transform.position = followPosition + shakeOffset;
    }
}
