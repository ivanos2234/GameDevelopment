using UnityEngine;

public sealed class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float followSpeed = 8f;
    private Vector3 offset;

    private void Awake()
    {
        if (target != null)
            offset = transform.position - target.position;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        offset = transform.position - target.position;
    }

    private void LateUpdate()
    {
        if (target == null) return;
        Vector3 destination = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, destination, followSpeed * Time.deltaTime);
    }
}
