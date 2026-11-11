using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Health and a simple world-space sprite health bar for a prototype enemy.</summary>
public sealed class EnemyHealth2D : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private Transform healthBarFill;
    [SerializeField, Tooltip("Sets the color code: rim, death burst and HUD marker.")]
    private EnemyKind kind = EnemyKind.Dummy;
    [SerializeField, Min(0), Tooltip("Position in the kill order on levels that use one (1 = first). 0 = any time.")]
    private int killOrder;

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
    private Transform orderBadge;
    private SpriteRenderer orderPlate;
    private TextMeshPro orderText;
    private bool isNextInOrder;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => currentHealth <= 0f;
    public EnemyKind Kind => kind;
    public int KillOrder => killOrder;

    /// <summary>Raised once, just before the enemy is destroyed.</summary>
    public event Action<EnemyHealth2D> Died;

    private void Awake()
    {
        maxHealth *= Difficulty.Current.enemyHealth;
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
        if (killOrder > 0)
            CreateOrderBadge();
    }

    private void Update()
    {
        if (orderBadge != null)
            orderBadge.localScale = Vector3.one * (isNextInOrder ? 1f + 0.12f * Mathf.Abs(Mathf.Sin(Time.time * 5f)) : 1f);
    }

    /// <summary>Highlights the badge of the enemy that has to fall next on a kill-order level.</summary>
    public void SetNextInOrder(bool isNext)
    {
        isNextInOrder = isNext;
        if (orderBadge == null)
            return;

        UiTheme theme = UiTheme.Current;
        orderPlate.color = isNext ? theme.gold : UiTheme.WithAlpha(theme.bar, 0.9f);
        orderText.color = isNext ? theme.bar : theme.paper;
    }

    // A numbered plate above the enemy: its place in the kill order.
    private void CreateOrderBadge()
    {
        orderBadge = new GameObject("Order Badge").transform;
        orderBadge.SetParent(transform, false);
        orderBadge.localPosition = new Vector3(0f, 1.05f, 0f);

        orderPlate = new GameObject("Plate").AddComponent<SpriteRenderer>();
        orderPlate.transform.SetParent(orderBadge, false);
        orderPlate.transform.localScale = new Vector3(0.5f, 0.42f, 1f);
        orderPlate.sprite = DebrisSprite;
        orderPlate.sortingOrder = 14;

        orderText = new GameObject("Number").AddComponent<TextMeshPro>();
        orderText.transform.SetParent(orderBadge, false);
        if (UiTheme.Current.titleFont != null)
            orderText.font = UiTheme.Current.titleFont;
        orderText.text = killOrder.ToString();
        orderText.fontSize = 2.6f;
        orderText.alignment = TextAlignmentOptions.Center;
        orderText.rectTransform.sizeDelta = new Vector2(1f, 0.5f);
        orderText.sortingOrder = 15;

        SetNextInOrder(false);
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
