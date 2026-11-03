using UnityEngine;

/// <summary>Chases the player, slides around walls in the way, and briefly backs away when the player charges at close range.</summary>
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

    [Header("Wall avoidance")]
    [SerializeField, Tooltip("Layers the enemy steers around, usually the walls.")]
    private LayerMask obstacleLayers = 1;
    [SerializeField, Min(0f)] private float avoidLookAhead = 1.2f;
    [SerializeField, Min(0.05f)] private float avoidRadius = 0.4f;
    [SerializeField, Min(0f)] private float avoidSideHoldTime = 1f;

    private Rigidbody2D body;
    private Rigidbody2D playerBody;
    private float retreatUntil;
    private float canRetreatAt;
    private int avoidSide;
    private float keepAvoidSideUntil;

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
            desiredVelocity = SteerAroundWalls(directionToPlayer) * chaseSpeed;
        }

        body.linearVelocity = Vector2.MoveTowards(
            body.linearVelocity,
            desiredVelocity,
            acceleration * Time.fixedDeltaTime);
    }

    // Slide along a wall that blocks the straight path, going round the short way (away from the
    // wall's middle). The side is kept while the way stays blocked, so the enemy circles the
    // obstacle in one direction instead of flipping back and forth in front of it.
    private Vector2 SteerAroundWalls(Vector2 direction)
    {
        if (avoidLookAhead <= 0f)
            return direction;

        RaycastHit2D hit = Physics2D.CircleCast(body.position, avoidRadius, direction, avoidLookAhead, obstacleLayers);
        if (hit.collider == null)
            return direction;

        Vector2 tangent = new Vector2(-hit.normal.y, hit.normal.x);
        if (Time.time >= keepAvoidSideUntil)
        {
            Vector2 toWallMiddle = (Vector2)hit.collider.bounds.center - hit.point;
            avoidSide = Vector2.Dot(tangent, toWallMiddle) > 0f ? -1 : 1;
        }
        keepAvoidSideUntil = Time.time + avoidSideHoldTime;

        return tangent * avoidSide;
    }
}
