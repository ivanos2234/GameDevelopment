using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Health and a simple world-space sprite health bar for a prototype enemy.</summary>
public sealed class EnemyHealth2D : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private Transform healthBarFill;
    [SerializeField, Tooltip("Sets the color code: rim, death burst and HUD marker.")]
    private EnemyKind kind = EnemyKind.Dummy;

    [Header("Look")]
    [SerializeField, Tooltip("Colored disc behind the body, tinted with the kind's color.")]
    private SpriteRenderer rim;
    [SerializeField] private SpriteRenderer shadow;

    [Header("Hit flash")]
    [SerializeField] private Color hitFlashColor = new(1f, 0.45f, 0.45f, 1f);
    [SerializeField, Min(0f)] private float hitFlashDuration = 0.1f;

    [Header("Damage numbers")]
    [SerializeField, Min(0f), Tooltip("Saw ticks are added up and shown at most this often.")]
    private float damageNumberInterval = 0.35f;

    private float currentHealth;
    private Vector3 fillScale;
    private float fillCenterX;
    private readonly List<SpriteRenderer> bodySprites = new();
    private readonly List<Color> bodyColors = new();
    private Coroutine flashRoutine;
    private float pendingDamage;
    private float nextDamageNumberAt;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => currentHealth <= 0f;
    public EnemyKind Kind => kind;

    /// <summary>Raised once, just before the enemy is destroyed.</summary>
    public event Action<EnemyHealth2D> Died;

    private void Awake()
    {
        currentHealth = maxHealth;
        if (healthBarFill != null)
        {
            fillScale = healthBarFill.localScale;
            fillCenterX = healthBarFill.localPosition.x;
        }
        RefreshHealthBar();
        CollectBodySprites();

        if (rim != null)
            rim.color = UiTheme.WithAlpha(UiTheme.Current.EnemyColor(kind), 0.55f);
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, transform.position);
    }

    /// <summary>Damages the enemy; <paramref name="hitPoint"/> is where sparks and the damage number appear.</summary>
    public void TakeDamage(float amount, Vector2 hitPoint)
    {
        if (amount <= 0f || currentHealth <= 0f)
            return;

        float dealt = Mathf.Min(amount, currentHealth);
        currentHealth -= dealt;
        RefreshHealthBar();

        if (currentHealth <= 0f)
        {
            Sfx.Play(SfxKind.EnemyDeath);
            DeathBurst2D.Spawn(transform.position, DebrisSprite, UiTheme.Current.EnemyColor(kind), 14, 5f, 0.6f);
            ShowDamageNumber(dealt, hitPoint, true);
            Died?.Invoke(this);
            Destroy(gameObject);
            return;
        }

        Sfx.Play(SfxKind.SawHit);
        DeathBurst2D.Spawn(hitPoint, DebrisSprite, UiTheme.Current.gold, 4, 3f, 0.2f);
        ShowDamageNumber(dealt, hitPoint, false);
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(HitFlash());
    }

    private Sprite DebrisSprite
    {
        get
        {
            SpriteRenderer bar = healthBarFill != null ? healthBarFill.GetComponent<SpriteRenderer>() : null;
            return bar != null ? bar.sprite : null;
        }
    }

    private void ShowDamageNumber(float amount, Vector2 hitPoint, bool flush)
    {
        pendingDamage += amount;
        if (!flush && Time.time < nextDamageNumberAt)
            return;

        int shown = Mathf.Max(1, Mathf.RoundToInt(pendingDamage));
        FloatingText2D.Spawn((Vector3)hitPoint + Vector3.up * 0.4f, "-" + shown, UiTheme.Current.gold);
        pendingDamage = 0f;
        nextDamageNumberAt = Time.time + damageNumberInterval;
    }

    private void RefreshHealthBar()
    {
        if (healthBarFill == null)
            return;

        float fraction = maxHealth > 0f ? currentHealth / maxHealth : 0f;
        healthBarFill.localScale = new Vector3(fillScale.x * fraction, fillScale.y, fillScale.z);

        // Keep the left edge fixed as the bar shrinks.
        healthBarFill.localPosition = new Vector3(
            fillCenterX - fillScale.x * (1f - fraction) * 0.5f,
            healthBarFill.localPosition.y,
            healthBarFill.localPosition.z);
    }

    private void CollectBodySprites()
    {
        // Flash only the enemy itself, not the health bar sprites.
        Transform healthBar = healthBarFill != null ? healthBarFill.parent : null;
        foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>())
        {
            if (healthBar != null && sprite.transform.IsChildOf(healthBar))
                continue;
            if (sprite == rim || sprite == shadow)
                continue;

            bodySprites.Add(sprite);
            bodyColors.Add(sprite.color);
        }
    }

    private IEnumerator HitFlash()
    {
        for (int i = 0; i < bodySprites.Count; i++)
            bodySprites[i].color = bodyColors[i] * hitFlashColor;

        yield return new WaitForSeconds(hitFlashDuration);

        for (int i = 0; i < bodySprites.Count; i++)
            bodySprites[i].color = bodyColors[i];
        flashRoutine = null;
    }
}
