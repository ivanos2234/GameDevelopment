using UnityEngine;

public enum ShotPattern
{
    /// <summary>One shot straight at the player.</summary>
    Aimed,
    /// <summary>A fan of shots centred on the player.</summary>
    Spread,
    /// <summary>Shots in every direction; the ring turns a little after each volley.</summary>
    Ring,
}

/// <summary>Fires projectiles while the player is in range and, unless disabled, not hidden behind a wall.</summary>
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
    [SerializeField, Tooltip("Off for turrets that keep firing even when the player hides behind a wall.")]
    private bool requireLineOfSight = true;
    [SerializeField, Tooltip("Layers that block line of sight, usually the walls.")]
    private LayerMask lineOfSightBlockers = 1;

    [Header("Pattern")]
    [SerializeField] private ShotPattern pattern = ShotPattern.Aimed;
    [SerializeField, Min(1), Tooltip("Shots per volley for Spread and Ring.")]
    private int projectileCount = 3;
    [SerializeField, Range(0f, 180f), Tooltip("Total angle of the Spread fan.")]
    private float spreadAngle = 30f;
    [SerializeField, Tooltip("Degrees the Ring turns after each volley, so the gaps move.")]
    private float ringTurnPerVolley = 22.5f;

    [Header("Wind-up telegraph")]
    [SerializeField] private Transform telegraphVisual;
    [SerializeField, Min(0f)] private float windUpDuration = 0.4f;
    [SerializeField, Min(1f)] private float windUpScale = 1.25f;

    private PlayerHealth2D playerHealth;
    private Vector3 visualBaseScale;
    private float nextShotAt;
    private float ringAngle;

    private void Awake()
    {
        projectileSpeed *= Difficulty.Current.projectileSpeed;
        fireInterval *= Difficulty.Current.fireInterval;

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

        bool canShoot = (playerHealth == null || !playerHealth.IsDead) && CanSeePlayer();
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

    private bool CanSeePlayer()
    {
        Vector2 from = transform.position;
        Vector2 to = playerTarget.position;
        if ((to - from).sqrMagnitude > range * range)
            return false;

        return !requireLineOfSight || Physics2D.Linecast(from, to, lineOfSightBlockers).collider == null;
    }

    private void Fire()
    {
        Vector2 aim = ((Vector2)(playerTarget.position - transform.position)).normalized;
        int count = Mathf.Max(1, projectileCount);
        Sfx.Play(SfxKind.Shoot);

        switch (pattern)
        {
            case ShotPattern.Aimed:
                FireOne(aim);
                break;

            case ShotPattern.Spread:
                for (int i = 0; i < count; i++)
                {
                    float t = count == 1 ? 0.5f : i / (float)(count - 1);
                    FireOne(Rotate(aim, Mathf.Lerp(-spreadAngle, spreadAngle, t) * 0.5f));
                }
                break;

            case ShotPattern.Ring:
                for (int i = 0; i < count; i++)
                    FireOne(Rotate(Vector2.right, ringAngle + i * 360f / count));
                ringAngle = Mathf.Repeat(ringAngle + ringTurnPerVolley, 360f);
                break;
        }
    }

    private void FireOne(Vector2 direction)
    {
        Vector2 spawn = (Vector2)transform.position + direction * muzzleOffset;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        EnemyProjectile2D shot = Instantiate(projectilePrefab, spawn, Quaternion.Euler(0f, 0f, angle));
        shot.Launch(direction * projectileSpeed);
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        return Quaternion.Euler(0f, 0f, degrees) * direction;
    }

    private void SetWindUp(float amount)
    {
        if (telegraphVisual == null)
            return;

        telegraphVisual.localScale = visualBaseScale * Mathf.Lerp(1f, windUpScale, amount);
    }
}
