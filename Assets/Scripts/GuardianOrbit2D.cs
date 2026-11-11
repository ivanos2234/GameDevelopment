using UnityEngine;

/// <summary>
/// The Guardian of a Master: an indestructible shield that circles its Master, blocks the saw and enemy shots
/// with its solid body and hurts the player on touch. A light beam ties it to the Master; when the Master
/// dies the beam breaks and the Guardian shatters with it. It takes no damage and does not count as an enemy.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class GuardianOrbit2D : MonoBehaviour
{
    [SerializeField] private EnemyHealth2D master;
    [SerializeField, Min(0.5f)] private float orbitRadius = 1.7f;
    [SerializeField, Tooltip("Degrees per second; negative turns clockwise.")]
    private float orbitSpeed = 70f;
    [SerializeField] private float startAngle = 0f;

    [Header("Look")]
    [SerializeField] private LineRenderer beam;
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private Sprite debrisSprite;

    private Rigidbody2D rb;
    private float angle;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        orbitSpeed *= Difficulty.Current.enemySpeed;
        angle = startAngle;

        // Keep moving in world space even though the Guardian is stored under its Master in the prefab.
        transform.SetParent(null, true);

        if (master != null)
            master.Died += OnMasterDied;
        else
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (master != null)
            master.Died -= OnMasterDied;
    }

    private void FixedUpdate()
    {
        if (master == null)
            return;

        angle += orbitSpeed * Time.fixedDeltaTime;
        float radians = angle * Mathf.Deg2Rad;
        Vector2 center = master.transform.position;
        Vector2 offset = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * orbitRadius;
        rb.MovePosition(center + offset);
        // Face outward, so the shield always points away from the Master.
        rb.MoveRotation(angle - 90f);
    }

    private void LateUpdate()
    {
        if (beam == null || master == null)
            return;

        beam.SetPosition(0, master.transform.position);
        beam.SetPosition(1, transform.position);
        float pulse = 0.55f + 0.25f * Mathf.Sin(Time.time * 6f);
        Color color = UiTheme.Current.guardian;
        beam.startColor = UiTheme.WithAlpha(color, pulse);
        beam.endColor = UiTheme.WithAlpha(color, pulse * 0.6f);
    }

    private void OnMasterDied(EnemyHealth2D _)
    {
        DeathBurst2D.Spawn(transform.position, debrisSprite, UiTheme.Current.guardian, 16, 5f, 0.6f);
        Destroy(gameObject);
    }
}
