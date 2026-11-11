using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public enum WeatherKind
{
    None,
    Dust,
    Petals,
    Fireflies,
    Rain,
    Leaves,
    Snow,
    Motes,
    Ash,
    Embers,
}

/// <summary>
/// A level's theme: tints the floor and walls, sets the backdrop and the 2D light (a dark level can light only
/// the area around the player) and adds weather drifting through the view. Everything is set in the Inspector.
/// </summary>
public sealed class LevelAmbience : MonoBehaviour
{
    [SerializeField, Tooltip("Shown under the level title in the intro.")] private string themeName = "";

    [Header("Colors")]
    [SerializeField] private Color floorTint = Color.white;
    [SerializeField] private Color wallTint = Color.white;
    [SerializeField] private Color background = new(0.055f, 0.07f, 0.125f, 1f);

    [Header("Light")]
    [SerializeField] private Color ambientLight = Color.white;
    [SerializeField, Min(0f)] private float ambientIntensity = 1f;
    [SerializeField, Min(0f), Tooltip("Above 0: a light that follows the player. Pair with a low ambient intensity for a dark level.")]
    private float playerLightRadius;
    [SerializeField] private Color playerLightColor = new(1f, 0.85f, 0.6f, 1f);
    [SerializeField, Min(0f)] private float playerLightIntensity = 1.2f;

    [Header("Weather")]
    [SerializeField] private WeatherKind weather = WeatherKind.None;
    [SerializeField] private Color weatherColor = Color.white;
    [SerializeField] private Color weatherColorAlt = Color.white;
    [SerializeField, Range(0, 200)] private int weatherAmount = 60;
    [SerializeField] private Sprite particleSprite;

    private sealed class Particle
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector2 velocity;
        public float phase;
        public float spin;
        public float baseAlpha;
    }

    private readonly List<Particle> particles = new();
    private Camera view;

    public string ThemeName => themeName;

    private void Awake()
    {
        view = Camera.main;
        if (view != null)
            view.backgroundColor = background;

        Tint("Repeating Background", floorTint, false);
        Tint("Arena", wallTint, true);
        Tint("Obstacles", wallTint, true);

        foreach (Light2D light in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
        {
            if (light.lightType != Light2D.LightType.Global)
                continue;
            light.color = ambientLight;
            light.intensity = ambientIntensity;
        }

        if (playerLightRadius > 0f)
            AddPlayerLight();

        SpawnWeather();
    }

    private static void Tint(string rootName, Color color, bool children)
    {
        GameObject root = GameObject.Find(rootName);
        if (root == null)
            return;

        if (!children)
        {
            SpriteRenderer sprite = root.GetComponent<SpriteRenderer>();
            if (sprite != null)
                sprite.color = color;
            return;
        }

        foreach (SpriteRenderer sprite in root.GetComponentsInChildren<SpriteRenderer>())
            sprite.color = color;
    }

    private void AddPlayerLight()
    {
        PlayerHealth2D player = FindFirstObjectByType<PlayerHealth2D>();
        if (player == null)
            return;

        var lightObject = new GameObject("Player Light");
        lightObject.transform.SetParent(player.transform, false);
        var light = lightObject.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.pointLightOuterRadius = playerLightRadius;
        light.pointLightInnerRadius = playerLightRadius * 0.35f;
        light.color = playerLightColor;
        light.intensity = playerLightIntensity;
    }

    // ---------- weather ----------

    private void SpawnWeather()
    {
        if (weather == WeatherKind.None || particleSprite == null || view == null)
            return;

        Rect area = ViewRect(1f);
        for (int i = 0; i < weatherAmount; i++)
        {
            var piece = new GameObject("Weather");
            piece.transform.SetParent(transform, false);
            var renderer = piece.AddComponent<SpriteRenderer>();
            renderer.sprite = particleSprite;
            renderer.sortingOrder = 30;

            var particle = new Particle
            {
                transform = piece.transform,
                renderer = renderer,
                phase = Random.value * 10f,
            };
            Setup(particle, i);
            particle.transform.position = new Vector3(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax), 0f);
            particles.Add(particle);
        }
    }

    // Size, color and motion for one piece of weather.
    private void Setup(Particle p, int index)
    {
        Color color = index % 2 == 0 ? weatherColor : weatherColorAlt;
        float size = 0.08f;
        switch (weather)
        {
            case WeatherKind.Dust:
                size = Random.Range(0.04f, 0.07f); p.velocity = new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(-0.1f, 0.15f)); p.baseAlpha = 0.35f; break;
            case WeatherKind.Petals:
                size = Random.Range(0.14f, 0.2f); p.velocity = new Vector2(Random.Range(0.4f, 0.9f), Random.Range(-0.9f, -0.5f)); p.spin = Random.Range(-160f, 160f); p.baseAlpha = 0.85f; break;
            case WeatherKind.Leaves:
                size = Random.Range(0.11f, 0.16f); p.velocity = new Vector2(Random.Range(-0.9f, -0.4f), Random.Range(-1.1f, -0.6f)); p.spin = Random.Range(-200f, 200f); p.baseAlpha = 0.9f; break;
            case WeatherKind.Fireflies:
                size = Random.Range(0.07f, 0.1f); p.velocity = Random.insideUnitCircle * 0.4f; p.baseAlpha = 0.9f; break;
            case WeatherKind.Motes:
                size = Random.Range(0.05f, 0.09f); p.velocity = new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.15f, 0.35f)); p.baseAlpha = 0.6f; break;
            case WeatherKind.Rain:
                p.velocity = new Vector2(-2.5f, Random.Range(-15f, -11f)); p.baseAlpha = 0.45f; break;
            case WeatherKind.Snow:
                size = Random.Range(0.06f, 0.11f); p.velocity = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(-1.2f, -0.6f)); p.baseAlpha = 0.9f; break;
            case WeatherKind.Ash:
                size = Random.Range(0.05f, 0.09f); p.velocity = new Vector2(Random.Range(-0.3f, 0.1f), Random.Range(-0.5f, -0.2f)); p.spin = Random.Range(-90f, 90f); p.baseAlpha = 0.55f; break;
            case WeatherKind.Embers:
                size = Random.Range(0.05f, 0.09f); p.velocity = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.8f, 1.8f)); p.baseAlpha = 0.95f; break;
        }

        p.transform.localScale = weather == WeatherKind.Rain
            ? new Vector3(0.03f, Random.Range(0.35f, 0.55f), 1f)
            : new Vector3(size, size, 1f);
        if (weather == WeatherKind.Rain)
            p.transform.rotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(p.velocity.x, -p.velocity.y) * Mathf.Rad2Deg);
        color.a = p.baseAlpha;
        p.renderer.color = color;
    }

    private Rect ViewRect(float margin)
    {
        float height = view.orthographicSize * 2f;
        float width = height * view.aspect;
        Vector2 center = view.transform.position;
        return new Rect(center.x - width / 2f - margin, center.y - height / 2f - margin, width + margin * 2f, height + margin * 2f);
    }

    private void Update()
    {
        if (particles.Count == 0 || view == null)
            return;

        float dt = Time.unscaledDeltaTime;
        float time = Time.unscaledTime;
        Rect area = ViewRect(1f);

        foreach (Particle p in particles)
        {
            Vector2 velocity = p.velocity;
            switch (weather)
            {
                case WeatherKind.Petals:
                case WeatherKind.Leaves:
                case WeatherKind.Snow:
                    velocity.x += Mathf.Sin(time * 1.5f + p.phase) * 0.5f;
                    break;
                case WeatherKind.Fireflies:
                case WeatherKind.Dust:
                    velocity += new Vector2(Mathf.Sin(time * 0.9f + p.phase), Mathf.Cos(time * 0.7f + p.phase * 1.3f)) * 0.3f;
                    break;
            }

            Vector3 position = p.transform.position + (Vector3)(velocity * dt);
            if (p.spin != 0f)
                p.transform.Rotate(0f, 0f, p.spin * dt);

            // Wrap around the view so the weather never runs out.
            if (position.x < area.xMin) position.x = area.xMax;
            else if (position.x > area.xMax) position.x = area.xMin;
            if (position.y < area.yMin) position.y = area.yMax;
            else if (position.y > area.yMax) position.y = area.yMin;
            p.transform.position = position;

            if (weather == WeatherKind.Fireflies || weather == WeatherKind.Embers || weather == WeatherKind.Motes)
            {
                Color c = p.renderer.color;
                c.a = p.baseAlpha * (0.45f + 0.55f * Mathf.Abs(Mathf.Sin(time * 2.2f + p.phase)));
                p.renderer.color = c;
            }
        }
    }
}
