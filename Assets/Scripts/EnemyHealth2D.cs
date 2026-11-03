using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Health and a simple world-space sprite health bar for a prototype enemy.</summary>
public sealed class EnemyHealth2D : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private Transform healthBarFill;

    [Header("Hit flash")]
    [SerializeField] private Color hitFlashColor = new(1f, 0.45f, 0.45f, 1f);
    [SerializeField, Min(0f)] private float hitFlashDuration = 0.1f;

    [Header("Death")]
    [SerializeField] private Color deathBurstColor = new(0.9f, 0.3f, 0.3f, 1f);

    private float currentHealth;
    private Vector3 fillScale;
    private float fillCenterX;
    private readonly List<SpriteRenderer> bodySprites = new();
    private readonly List<Color> bodyColors = new();
    private Coroutine flashRoutine;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => currentHealth <= 0f;

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
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || currentHealth <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        RefreshHealthBar();

        if (currentHealth <= 0f)
        {
            Sfx.Play(SfxKind.EnemyDeath);
            SpriteRenderer bar = healthBarFill != null ? healthBarFill.GetComponent<SpriteRenderer>() : null;
            DeathBurst2D.Spawn(transform.position, bar != null ? bar.sprite : null, deathBurstColor);
            Died?.Invoke(this);
            Destroy(gameObject);
            return;
        }

        Sfx.Play(SfxKind.SawHit);
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(HitFlash());
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
