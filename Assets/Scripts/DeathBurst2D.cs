using UnityEngine;

/// <summary>
/// A short burst of square debris where an enemy died. Runs on unscaled time so the last kill
/// of a level still finishes its burst after the game freezes on the level cleared screen.
/// </summary>
public sealed class DeathBurst2D : MonoBehaviour
{
    private Transform[] pieces;
    private SpriteRenderer[] renderers;
    private Vector2[] velocities;
    private Color color;
    private float lifetime;
    private float startedAt;

    public static void Spawn(Vector3 position, Sprite sprite, Color color, int count = 10, float speed = 4f, float lifetime = 0.5f)
    {
        if (sprite == null)
            return;

        var root = new GameObject("Death Burst");
        root.transform.position = position;
        root.AddComponent<DeathBurst2D>().Init(sprite, color, count, speed, lifetime);
    }

    private void Init(Sprite sprite, Color burstColor, int count, float speed, float duration)
    {
        color = burstColor;
        lifetime = duration;
        startedAt = Time.unscaledTime;
        pieces = new Transform[count];
        renderers = new SpriteRenderer[count];
        velocities = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            var piece = new GameObject("Piece");
            piece.transform.SetParent(transform, false);
            piece.transform.localScale = Vector3.one * Random.Range(0.1f, 0.2f);
            piece.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));

            var spriteRenderer = piece.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = 10;

            pieces[i] = piece.transform;
            renderers[i] = spriteRenderer;
            velocities[i] = Random.insideUnitCircle.normalized * speed * Random.Range(0.5f, 1f);
        }
    }

    private void Update()
    {
        float age = Time.unscaledTime - startedAt;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.unscaledDeltaTime;
        float drag = Mathf.Clamp01(1f - 5f * dt);
        Color faded = color;
        faded.a *= 1f - age / lifetime;

        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i].localPosition += (Vector3)(velocities[i] * dt);
            velocities[i] *= drag;
            renderers[i].color = faded;
        }
    }
}
