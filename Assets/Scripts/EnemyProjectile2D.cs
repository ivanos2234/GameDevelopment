using UnityEngine;

/// <summary>A straight-flying shot. Hurts the player; walls, the chain and the saw stop it.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyProjectile2D : MonoBehaviour
{
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField, Min(0.1f)] private float lifetime = 4f;

    private Rigidbody2D body;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    public void Launch(Vector2 velocity)
    {
        body.linearVelocity = velocity;
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerHealth2D player = other.GetComponentInParent<PlayerHealth2D>();
        if (player != null)
        {
            player.TakeDamage(damage, body.position);
            Destroy(gameObject);
            return;
        }

        // Pass through enemies and other shots; anything else solid (walls, chain, saw) blocks it.
        if (other.GetComponentInParent<EnemyHealth2D>() != null ||
            other.GetComponentInParent<EnemyProjectile2D>() != null)
            return;

        if (!other.isTrigger || other.GetComponentInParent<SawImpactDamage2D>() != null)
            Destroy(gameObject);
    }
}
