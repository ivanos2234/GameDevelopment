using UnityEngine;

/// <summary>Restores hearts when the player walks over it. Stays on the floor while the player is at full health.</summary>
[RequireComponent(typeof(Collider2D))]
public sealed class HealthPickup2D : MonoBehaviour
{
    [SerializeField, Min(1)] private int heal = 1;

    [Header("Idle animation")]
    [SerializeField] private Transform visual;
    [SerializeField, Min(0f)] private float pulseAmount = 0.12f;
    [SerializeField, Min(0f)] private float pulseSpeed = 4f;

    private Vector3 visualBaseScale;

    private void Awake()
    {
        if (visual != null)
            visualBaseScale = visual.localScale;
    }

    private void Update()
    {
        if (visual != null)
            visual.localScale = visualBaseScale * (1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryPickUp(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryPickUp(other);
    }

    private void TryPickUp(Collider2D other)
    {
        PlayerHealth2D player = other.GetComponentInParent<PlayerHealth2D>();
        if (player != null && player.Heal(heal))
            Destroy(gameObject);
    }
}
