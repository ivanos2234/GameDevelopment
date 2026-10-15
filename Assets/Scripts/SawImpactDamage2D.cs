using System.Collections.Generic;
using UnityEngine;

/// <summary>Spins the saw faster as it swings and deals damage while its hitbox overlaps enemies.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class SawImpactDamage2D : MonoBehaviour
{
    [Header("Swing-driven spin")]
    [SerializeField] private Transform swingAnchor;
    [SerializeField, Min(0f)] private float baseSpinDegreesPerSecond = 360f;
    [SerializeField, Min(0f)] private float extraSpinPerSwingSpeed = 120f;
    [SerializeField, Min(1f)] private float spinAcceleration = 1800f;
    [SerializeField, Range(0.5f, 1f)] private float speedRetentionOnEnemyPass = 0.92f;

    [Header("Continuous damage")]
    [SerializeField, Min(0f)] private float minimumDamageSpin = 180f;
    [SerializeField, Min(0f)] private float damagePer100Spin = 6f;
    [SerializeField, Min(0f)] private float maximumDamagePerSecond = 60f;
    [SerializeField, Min(0.02f)] private float damageTickInterval = 0.2f;

    [Header("Enemy-only hitbox")]
    [SerializeField, Min(0.1f)] private float hitboxRadius = 0.85f;
    [SerializeField] private string hitboxLayerName = "SawHitbox";

    private readonly Dictionary<EnemyHealth2D, float> nextDamageTime = new();
    private Rigidbody2D sawBody;
    private Rigidbody2D anchorBody;
    private bool initializedHitbox;

    public float LastImpactSpeed { get; private set; }
    public float LastDamageDealt { get; private set; }
    public float LastHitTime { get; private set; } = -1f;

    private void Awake()
    {
        sawBody = GetComponent<Rigidbody2D>();
        if (swingAnchor == null)
        {
            GameObject player = GameObject.Find("Player");
            if (player != null) swingAnchor = player.transform;
        }
        if (swingAnchor != null)
            anchorBody = swingAnchor.GetComponent<Rigidbody2D>();

        // The saw must be allowed to spin independently of the final chain link.
        HingeJoint2D sawHinge = GetComponent<HingeJoint2D>();
        if (sawHinge != null) sawHinge.useLimits = false;

        CreateDamageHitbox();
    }

    private void FixedUpdate()
    {
        Vector2 relativeVelocity = sawBody.linearVelocity;
        float swingSpeed = relativeVelocity.magnitude;

        if (anchorBody != null)
        {
            Vector2 radial = sawBody.worldCenterOfMass - anchorBody.worldCenterOfMass;
            if (radial.sqrMagnitude > 0.0001f)
            {
                Vector2 tangent = new Vector2(-radial.y, radial.x).normalized;
                relativeVelocity -= anchorBody.GetPointVelocity(sawBody.worldCenterOfMass);
                float signedSwingSpeed = Vector2.Dot(relativeVelocity, tangent);
                swingSpeed = Mathf.Abs(signedSwingSpeed);
            }
        }

        // Keep spinning in one direction, with extra speed as the chain swings faster.
        float targetSpin = baseSpinDegreesPerSecond + swingSpeed * extraSpinPerSwingSpeed;
        sawBody.angularVelocity = Mathf.MoveTowards(
            sawBody.angularVelocity,
            targetSpin,
            spinAcceleration * Time.fixedDeltaTime);
    }

    public float EstimateDamagePerSecond(float spinDegreesPerSecond)
    {
        float spin = Mathf.Abs(spinDegreesPerSecond);
        if (spin <= minimumDamageSpin)
            return 0f;

        return Mathf.Min((spin - minimumDamageSpin) / 100f * damagePer100Spin, maximumDamagePerSecond);
    }

    // The trigger is a child collider attached to this Rigidbody2D, so Unity sends
    // its trigger callbacks to this component on the Saw as well.
    private void OnTriggerEnter2D(Collider2D other)
    {
        OnEnemyEntered(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamageEnemy(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        ForgetEnemy(other);
    }

    public void TryDamageEnemy(Collider2D other)
    {
        EnemyHealth2D enemy = other.GetComponentInParent<EnemyHealth2D>();
        if (enemy == null)
            return;

        if (nextDamageTime.TryGetValue(enemy, out float nextTime) && Time.time < nextTime)
            return;

        float damagePerSecond = EstimateDamagePerSecond(sawBody.angularVelocity);
        if (damagePerSecond <= 0f)
            return;

        float damage = damagePerSecond * damageTickInterval;
        enemy.TakeDamage(damage);

        Vector2 hitPoint = other.ClosestPoint(sawBody.worldCenterOfMass);
        LastImpactSpeed = sawBody.GetPointVelocity(hitPoint).magnitude;
        LastDamageDealt = damage;
        LastHitTime = Time.time;
        nextDamageTime[enemy] = Time.time + damageTickInterval;
    }

    public void OnEnemyEntered(Collider2D other)
    {
        if (other.GetComponentInParent<EnemyHealth2D>() == null)
            return;

        sawBody.linearVelocity *= speedRetentionOnEnemyPass;
        sawBody.angularVelocity *= speedRetentionOnEnemyPass;
    }

    public void ForgetEnemy(Collider2D other)
    {
        EnemyHealth2D enemy = other.GetComponentInParent<EnemyHealth2D>();
        if (enemy != null)
            nextDamageTime.Remove(enemy);
    }

    private void CreateDamageHitbox()
    {
        if (initializedHitbox)
            return;

        Transform existing = transform.Find("Saw Damage Hitbox");
        GameObject hitboxObject = existing != null ? existing.gameObject : new GameObject("Saw Damage Hitbox");
        hitboxObject.transform.SetParent(transform, false);

        int hitboxLayer = LayerMask.NameToLayer(hitboxLayerName);
        hitboxObject.layer = hitboxLayer >= 0 ? hitboxLayer : gameObject.layer;

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0 && hitboxLayer >= 0)
        {
            // The solid saw body passes through enemies; only the child trigger detects them.
            Physics2D.IgnoreLayerCollision(gameObject.layer, enemyLayer, true);
            Physics2D.IgnoreLayerCollision(hitboxLayer, enemyLayer, false);
        }
        else
        {
            Debug.LogWarning("Create the SawHitbox and Enemy layers so the saw can pass through enemies and still damage them.", this);
        }

        CircleCollider2D trigger = hitboxObject.GetComponent<CircleCollider2D>();
        if (trigger == null) trigger = hitboxObject.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = hitboxRadius;

        initializedHitbox = true;
    }
}
