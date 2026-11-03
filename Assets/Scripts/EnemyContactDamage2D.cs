using UnityEngine;

/// <summary>
/// Hurts the player when they come within reach of this enemy's collider.
/// Chasers stop just short of the player, so a small reach lets them still land hits.
/// The player's invulnerability window limits how often damage is dealt.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class EnemyContactDamage2D : MonoBehaviour
{
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField, Min(0f), Tooltip("Gap between the colliders at which the hit still lands.")]
    private float reach = 0.3f;

    private Collider2D ownCollider;
    private PlayerHealth2D player;
    private Collider2D playerCollider;

    private void Awake()
    {
        ownCollider = GetComponent<Collider2D>();

        GameObject playerObject = GameObject.Find("Player");
        if (playerObject != null)
        {
            player = playerObject.GetComponent<PlayerHealth2D>();
            playerCollider = playerObject.GetComponent<Collider2D>();
        }
    }

    private void FixedUpdate()
    {
        if (player == null || playerCollider == null || !ownCollider.enabled || player.IsDead)
            return;

        ColliderDistance2D gap = Physics2D.Distance(ownCollider, playerCollider);
        if (gap.isValid && gap.distance <= reach)
            player.TakeDamage(damage, transform.position);
    }
}
