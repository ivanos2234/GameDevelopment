using UnityEngine;

/// <summary>Chases the player and briefly backs away when the player charges at close range.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyChase2D : MonoBehaviour
{
    [Header("Chasing")]
    [SerializeField] private Transform playerTarget;
    [SerializeField, Min(0f)] private float chaseSpeed = 2.2f;
    [SerializeField, Min(0f)] private float acceleration = 12f;
    [SerializeField, Min(0f)] private float stopDistance = 1.1f;

    [Header("Backstep")]
    [SerializeField, Min(0f)] private float retreatTriggerDistance = 2.8f;
    [SerializeField, Min(0f)] private float playerChargeSpeed = 2f;
    [SerializeField, Min(0f)] private float retreatSpeed = 3.5f;
    [SerializeField, Min(0f)] private float retreatDuration = 0.45f;
    [SerializeField, Min(0f)] private float retreatCooldown = 1.6f;

    private Rigidbody2D body;
    private Rigidbody2D playerBody;
    private float retreatUntil;
    private float canRetreatAt;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();

        if (playerTarget == null)
        {
            GameObject player = GameObject.Find("Player");
            if (player != null)
                playerTarget = player.transform;
        }

        if (playerTarget != null)
            playerBody = playerTarget.GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        if (playerTarget == null)
        {
            body.linearVelocity = Vector2.MoveTowards(
                body.linearVelocity, Vector2.zero, acceleration * Time.fixedDeltaTime);
            return;
        }

        Vector2 toPlayer = (Vector2)playerTarget.position - body.position;
        float distance = toPlayer.magnitude;
        if (distance < 0.001f)
            return;

        Vector2 directionToPlayer = toPlayer / distance;
        Vector2 desiredVelocity = Vector2.zero;

        // Measure only the player's movement toward the enemy. Including the
        // enemy's velocity can falsely look like a charge when it catches a
        // player who is actually running away.
        float playerTowardEnemySpeed = 0f;
        if (playerBody != null)
            playerTowardEnemySpeed = -Vector2.Dot(playerBody.linearVelocity, directionToPlayer);

        bool playerIsCharging = distance <= retreatTriggerDistance &&
                                playerTowardEnemySpeed >= playerChargeSpeed;

        if (Time.time >= retreatUntil && Time.time >= canRetreatAt &&
            playerIsCharging)
        {
            retreatUntil = Time.time + retreatDuration;
            canRetreatAt = retreatUntil + retreatCooldown;
        }

        if (Time.time < retreatUntil)
        {
            desiredVelocity = -directionToPlayer * retreatSpeed;
        }
        else if (distance > stopDistance)
        {
            desiredVelocity = directionToPlayer * chaseSpeed;
        }

        body.linearVelocity = Vector2.MoveTowards(
            body.linearVelocity,
            desiredVelocity,
            acceleration * Time.fixedDeltaTime);
    }
}
