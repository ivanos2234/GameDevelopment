using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A straight-flying shot. Hurts the player; walls, the chain and the saw stop it.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyProjectile2D : MonoBehaviour
{
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField, Min(0.1f)] private float lifetime = 4f;

    private static readonly List<EnemyProjectile2D> live = new();

    private Rigidbody2D body;

    /// <summary>Shots currently in flight (used by the gameplay log).</summary>
    public static IReadOnlyList<EnemyProjectile2D> Live => live;
    /// <summary>Raised when a shot is destroyed, for whatever reason.</summary>
    public static event Action<EnemyProjectile2D> Ended;

    /// <summary>The enemy that fired this shot, if known.</summary>
    public EnemyHealth2D Shooter { get; private set; }
    /// <summary>True once the shot has reached the player (even during their invulnerability).</summary>
    public bool HitPlayer { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        live.Add(this);
    }

    private void OnDisable()
    {
        live.Remove(this);
    }

    private void OnDestroy()
    {
        Ended?.Invoke(this);
    }

    public void Launch(Vector2 velocity, EnemyHealth2D shooter = null)
    {
        Shooter = shooter;
        body.linearVelocity = velocity;
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerHealth2D player = other.GetComponentInParent<PlayerHealth2D>();
        if (player != null)
        {
            HitPlayer = true;
            player.TakeDamage(damage, body.position, gameObject);
            Destroy(gameObject);
            return;
        }

        // Pass through enemies and other shots; anything else solid (walls, chain, saw) blocks it.
        if (other.GetComponentInParent<EnemyHealth2D>() != null ||
            other.GetComponentInParent<EnemyProjectile2D>() != null)
            return;

        if (!other.isTrigger || other.GetComponentInParent<SawImpactDamage2D>() != null)
            Destroy(gameObject);
    }
}
