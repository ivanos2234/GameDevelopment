using UnityEngine;

/// <summary>Fires projectiles at the player while they are in range and not hidden behind a wall.</summary>
public sealed class EnemyShooter2D : MonoBehaviour
{
    [SerializeField] private EnemyProjectile2D projectilePrefab;
    [SerializeField] private Transform playerTarget;

    [Header("Firing")]
    [SerializeField, Min(0f)] private float range = 9f;
    [SerializeField, Min(0.1f)] private float fireInterval = 2f;
    [SerializeField, Min(0f)] private float firstShotDelay = 1.5f;
    [SerializeField, Min(0f)] private float projectileSpeed = 5f;
    [SerializeField, Min(0f)] private float muzzleOffset = 0.6f;
    [SerializeField, Tooltip("Layers that block line of sight, usually the walls.")]
    private LayerMask lineOfSightBlockers = 1;

    [Header("Wind-up telegraph")]
    [SerializeField] private Transform telegraphVisual;
    [SerializeField, Min(0f)] private float windUpDuration = 0.4f;
    [SerializeField, Min(1f)] private float windUpScale = 1.25f;

    private PlayerHealth2D playerHealth;
    private Vector3 visualBaseScale;
    private float nextShotAt;

    private void Awake()
    {
        if (playerTarget == null)
        {
            GameObject player = GameObject.Find("Player");
            if (player != null)
                playerTarget = player.transform;
        }

        if (playerTarget != null)
            playerHealth = playerTarget.GetComponent<PlayerHealth2D>();

        if (telegraphVisual != null)
            visualBaseScale = telegraphVisual.localScale;
    }

    private void OnEnable()
    {
        // A little jitter keeps several shooters from firing in perfect sync.
        nextShotAt = Time.time + firstShotDelay + Random.Range(0f, fireInterval * 0.5f);
    }

    private void Update()
    {
        if (projectilePrefab == null || playerTarget == null)
            return;

        bool canShoot = (playerHealth == null || !playerHealth.IsDead) && HasLineOfSight();
        if (!canShoot)
        {
            // Hold the shot until the player steps back into view.
            nextShotAt = Mathf.Max(nextShotAt, Time.time + windUpDuration);
            SetWindUp(0f);
            return;
        }

        float timeLeft = nextShotAt - Time.time;
        SetWindUp(windUpDuration > 0f ? 1f - Mathf.Clamp01(timeLeft / windUpDuration) : 0f);

        if (timeLeft <= 0f)
        {
            Fire();
            nextShotAt = Time.time + fireInterval;
            SetWindUp(0f);
        }
    }

    private bool HasLineOfSight()
    {
        Vector2 from = transform.position;
        Vector2 to = playerTarget.position;
        if ((to - from).sqrMagnitude > range * range)
            return false;

        return Physics2D.Linecast(from, to, lineOfSightBlockers).collider == null;
    }

    private void Fire()
    {
        Vector2 direction = ((Vector2)(playerTarget.position - transform.position)).normalized;
        Vector2 spawn = (Vector2)transform.position + direction * muzzleOffset;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        EnemyProjectile2D shot = Instantiate(projectilePrefab, spawn, Quaternion.Euler(0f, 0f, angle));
        shot.Launch(direction * projectileSpeed);
    }

    private void SetWindUp(float amount)
    {
        if (telegraphVisual == null)
            return;

        telegraphVisual.localScale = visualBaseScale * Mathf.Lerp(1f, windUpScale, amount);
    }
}
