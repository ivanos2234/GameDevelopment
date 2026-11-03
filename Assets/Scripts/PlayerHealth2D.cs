using System;
using System.Collections;
using UnityEngine;

/// <summary>Player hit points shown as hearts, with knockback and a short invulnerability window after each hit.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerHealth2D : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHearts = 5;
    [SerializeField, Min(0f)] private float invulnerabilityDuration = 1f;
    [SerializeField, Min(0f)] private float knockbackImpulse = 6f;
    [SerializeField, Min(0.02f)] private float flashInterval = 0.08f;
    [SerializeField] private Color deadTint = new(0.35f, 0.35f, 0.35f, 1f);

    private Rigidbody2D body;
    private SpriteRenderer[] sprites;
    private Color[] spriteColors;
    private float invulnerableUntil;

    public int MaxHearts => maxHearts;
    public int CurrentHearts { get; private set; }
    public bool IsDead => CurrentHearts <= 0;

    /// <summary>Raised with (current, max) whenever the number of hearts changes.</summary>
    public event Action<int, int> HeartsChanged;
    public event Action Died;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        sprites = GetComponentsInChildren<SpriteRenderer>();
        spriteColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
            spriteColors[i] = sprites[i].color;

        CurrentHearts = maxHearts;
    }

    /// <summary>Removes hearts unless the player is still invulnerable from the previous hit.</summary>
    public bool TakeDamage(int amount, Vector2 sourcePosition)
    {
        if (amount <= 0 || IsDead || Time.time < invulnerableUntil)
            return false;

        CurrentHearts = Mathf.Max(0, CurrentHearts - amount);
        invulnerableUntil = Time.time + invulnerabilityDuration;

        Vector2 away = body.position - sourcePosition;
        if (away.sqrMagnitude > 0.0001f)
            body.AddForce(away.normalized * knockbackImpulse, ForceMode2D.Impulse);

        HeartsChanged?.Invoke(CurrentHearts, maxHearts);

        if (IsDead)
            Die();
        else
            StartCoroutine(FlashWhileInvulnerable());

        return true;
    }

    public bool Heal(int amount)
    {
        if (amount <= 0 || IsDead || CurrentHearts >= maxHearts)
            return false;

        CurrentHearts = Mathf.Min(maxHearts, CurrentHearts + amount);
        HeartsChanged?.Invoke(CurrentHearts, maxHearts);
        return true;
    }

    private void Die()
    {
        StopAllCoroutines();

        PlayerMovement2D movement = GetComponent<PlayerMovement2D>();
        if (movement != null)
            movement.enabled = false;
        body.linearVelocity = Vector2.zero;

        for (int i = 0; i < sprites.Length; i++)
            sprites[i].color = spriteColors[i] * deadTint;

        Died?.Invoke();
    }

    private IEnumerator FlashWhileInvulnerable()
    {
        bool dimmed = false;
        while (Time.time < invulnerableUntil)
        {
            dimmed = !dimmed;
            SetAlpha(dimmed ? 0.3f : 1f);
            yield return new WaitForSeconds(flashInterval);
        }
        SetAlpha(1f);
    }

    private void SetAlpha(float alphaScale)
    {
        for (int i = 0; i < sprites.Length; i++)
        {
            Color c = spriteColors[i];
            c.a *= alphaScale;
            sprites[i].color = c;
        }
    }
}
