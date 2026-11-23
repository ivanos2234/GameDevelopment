using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chases the player along a path around walls and standing enemies (see <see cref="NavGrid2D"/>), slides past
/// anything still in the way, and briefly backs away when the player charges at close range.
/// Ranged enemies can instead hold their distance and circle the player (see Keep distance).
/// </summary>
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

    [Header("Keep distance (ranged enemies)")]
    [SerializeField, Min(0f), Tooltip("Back away when the player is closer than this. 0 = never.")]
    private float fleeDistance = 0f;
    [SerializeField, Min(0f), Tooltip("Circle the player at this speed while within stop distance. 0 = stand still.")]
    private float strafeSpeed = 0f;
    [SerializeField, Min(0.1f)] private float strafeSwitchInterval = 2f;

    [Header("Wall avoidance")]
    [SerializeField, Tooltip("Layers the enemy steers around, usually the walls.")]
    private LayerMask obstacleLayers = 1;
    [SerializeField, Min(0f)] private float avoidLookAhead = 1.2f;
    [SerializeField, Min(0.05f)] private float avoidRadius = 0.4f;
    [SerializeField, Min(0f)] private float avoidSideHoldTime = 1f;
    [SerializeField, Tooltip("Also steer around other enemies, not only walls.")]
    private bool avoidEnemies = true;

    [Header("Pathfinding")]
    [SerializeField, Tooltip("Find a way around walls and standing enemies instead of heading straight for the player.")]
    private bool usePathfinding = true;
    [SerializeField, Min(0.1f)] private float repathInterval = 0.4f;
    [SerializeField, Min(0.05f)] private float waypointReach = 0.35f;

    private Rigidbody2D body;
    private Rigidbody2D playerBody;
    private float retreatUntil;
    private float canRetreatAt;
    private int avoidSide;
    private float keepAvoidSideUntil;
    private int strafeSide = 1;
    private float switchStrafeAt;
    private readonly List<Vector2> path = new();
    private int pathIndex;
    private float repathAt;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        float speed = Difficulty.Current.enemySpeed;
        chaseSpeed *= speed;
        retreatSpeed *= speed;
        strafeSpeed *= speed;

        if (playerTarget == null)
        {
            GameObject player = GameObject.Find("Player");
            if (player != null)
                playerTarget = player.transform;
        }

        if (playerTarget != null)
            playerBody = playerTarget.GetComponent<Rigidbody2D>();

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (avoidEnemies && enemyLayer >= 0)
            obstacleLayers |= 1 << enemyLayer;
        repathAt = Random.Range(0f, repathInterval);
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
        else if (distance < fleeDistance)
        {
            desiredVelocity = SteerAroundWalls(-directionToPlayer) * chaseSpeed;
        }
        else if (distance > stopDistance)
        {
            desiredVelocity = SteerAroundWalls(ChaseDirection(directionToPlayer)) * chaseSpeed;
        }
        else if (strafeSpeed > 0f)
        {
            desiredVelocity = Strafe(directionToPlayer);
        }

        body.linearVelocity = Vector2.MoveTowards(
            body.linearVelocity,
            desiredVelocity,
            acceleration * Time.fixedDeltaTime);
    }

    // Circle the player, now and then switching direction, and turn back instead of grinding into a wall.
    private Vector2 Strafe(Vector2 directionToPlayer)
    {
        if (Time.time >= switchStrafeAt)
        {
            strafeSide = Random.value < 0.5f ? -1 : 1;
            switchStrafeAt = Time.time + strafeSwitchInterval * Random.Range(0.75f, 1.25f);
        }

        Vector2 side = new Vector2(-directionToPlayer.y, directionToPlayer.x) * strafeSide;
        if (CastObstacle(side, out _))
        {
            strafeSide = -strafeSide;
            side = -side;
        }

        return side * strafeSpeed;
    }

    // Slide along a wall that blocks the straight path, going round the short way (away from the
    // wall's middle). The side is kept while the way stays blocked, so the enemy circles the
    // obstacle in one direction instead of flipping back and forth in front of it.
    private Vector2 SteerAroundWalls(Vector2 direction)
    {
        if (avoidLookAhead <= 0f)
            return direction;

        if (!CastObstacle(direction, out RaycastHit2D hit))
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

    // Heads for the next waypoint of a path around walls and standing enemies; straight at the player when the way is clear.
    private Vector2 ChaseDirection(Vector2 directionToPlayer)
    {
        if (!usePathfinding)
            return directionToPlayer;

        NavGrid2D grid = NavGrid2D.Instance;
        Vector2 position = body.position;
        Vector2 target = playerTarget.position;
        if (grid.IsClear(position, target))
        {
            path.Clear();
            return directionToPlayer;
        }

        if (Time.time >= repathAt || pathIndex >= path.Count)
        {
            repathAt = Time.time + repathInterval * Random.Range(0.8f, 1.2f);
            grid.FindPath(position, target, path);
            pathIndex = 0;
        }

        // Skip waypoints already reached, and cut corners when the next one is in plain view.
        while (pathIndex < path.Count - 1 &&
               (Vector2.Distance(position, path[pathIndex]) < waypointReach || grid.IsClear(position, path[pathIndex + 1])))
            pathIndex++;

        if (pathIndex < path.Count)
        {
            Vector2 toWaypoint = path[pathIndex] - position;
            if (toWaypoint.sqrMagnitude > 0.0001f)
                return toWaypoint.normalized;
        }
        return directionToPlayer;
    }

    // The nearest solid thing in the way, ignoring this enemy's own collider.
    private bool CastObstacle(Vector2 direction, out RaycastHit2D nearest)
    {
        nearest = default;
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = obstacleLayers, useTriggers = false };
        int count = Physics2D.CircleCast(body.position, avoidRadius, direction, filter, castHits, avoidLookAhead);
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            if (castHits[i].rigidbody == body || castHits[i].distance <= 0f)
                continue;
            if (!found || castHits[i].distance < nearest.distance)
            {
                nearest = castHits[i];
                found = true;
            }
        }
        return found;
    }
}
