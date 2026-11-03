using System;
using UnityEngine;

public enum SfxKind
{
    SawHit,
    EnemyDeath,
    PlayerHurt,
    Shoot,
    Pickup,
    LevelCleared,
    GameOver,
    Click,
}

/// <summary>
/// Sound effects synthesised in code the first time one plays, so the game has audio without any sound files.
/// To use recorded sounds later, replace the clip built for that kind in <see cref="BuildClip"/>.
/// </summary>
public static class Sfx
{
    private const int SampleRate = 44100;
    private const string MutedKey = "Sfx.Muted";

    private static readonly int KindCount = Enum.GetValues(typeof(SfxKind)).Length;

    private static AudioSource source;
    private static AudioClip[] clips;
    private static float[] lastPlayedAt;
    private static bool? muted;

    public static bool Muted
    {
        get => muted ??= PlayerPrefs.GetInt(MutedKey, 0) == 1;
        set
        {
            muted = value;
            PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static void Play(SfxKind kind)
    {
        if (Muted || !EnsureReady())
            return;

        // Rapid-fire sounds (saw ticks, many shooters) are thinned out so they do not pile up.
        int index = (int)kind;
        if (Time.unscaledTime - lastPlayedAt[index] < MinimumGap(kind))
            return;

        lastPlayedAt[index] = Time.unscaledTime;
        source.PlayOneShot(clips[index], Volume(kind));
    }

    private static bool EnsureReady()
    {
        if (source != null)
            return true;

        var audioObject = new GameObject("Sfx");
        UnityEngine.Object.DontDestroyOnLoad(audioObject);
        source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        // Clips made at runtime can be unloaded between play sessions, so rebuild them if they are gone.
        if (clips == null || clips[0] == null)
        {
            clips = new AudioClip[KindCount];
            for (int i = 0; i < KindCount; i++)
                clips[i] = BuildClip((SfxKind)i);
            lastPlayedAt = new float[KindCount];
            for (int i = 0; i < KindCount; i++)
                lastPlayedAt[i] = float.NegativeInfinity;
        }

        return true;
    }

    private static float Volume(SfxKind kind) => kind switch
    {
        SfxKind.SawHit => 0.2f,
        SfxKind.Shoot => 0.15f,
        SfxKind.Click => 0.3f,
        SfxKind.PlayerHurt => 0.6f,
        _ => 0.45f,
    };

    private static float MinimumGap(SfxKind kind) => kind switch
    {
        SfxKind.SawHit => 0.07f,
        SfxKind.Shoot => 0.05f,
        _ => 0f,
    };

    private static AudioClip BuildClip(SfxKind kind)
    {
        switch (kind)
        {
            case SfxKind.SawHit: // short metallic tick
                return Synth("SawHit", 0.05f, (t, noise) =>
                    (0.5f * Sine(t, 2300f) + 0.3f * Sine(t, 3700f) + 0.4f * noise) * Decay(t, 70f));

            case SfxKind.EnemyDeath: // falling pop with a crunchy start
                return Synth("EnemyDeath", 0.22f, (t, noise) =>
                    (0.6f * SoftSquare(SweepPhase(t, 520f, 90f, 0.22f)) + 0.4f * noise * Decay(t, 30f)) * Decay(t, 12f));

            case SfxKind.PlayerHurt: // low thud
                return Synth("PlayerHurt", 0.25f, (t, noise) =>
                    (0.9f * Mathf.Sin(SweepPhase(t, 180f, 55f, 0.25f)) + 0.25f * noise * Decay(t, 40f)) * Decay(t, 10f));

            case SfxKind.Shoot: // quick "pew"
                return Synth("Shoot", 0.12f, (t, _) =>
                    0.5f * SoftSquare(SweepPhase(t, 1100f, 350f, 0.12f)) * Decay(t, 18f));

            case SfxKind.Pickup: // two rising chime notes
                return Synth("Pickup", 0.3f, (t, _) => Notes(t, new[] { 880f, 1320f }, 0.1f, 12f, 0.7f, 0f));

            case SfxKind.LevelCleared: // rising arpeggio
                return Synth("LevelCleared", 0.65f, (t, _) =>
                    Notes(t, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.12f, 6f, 0.3f, 0.4f));

            case SfxKind.GameOver: // falling notes
                return Synth("GameOver", 0.75f, (t, _) =>
                    Notes(t, new[] { 392f, 311.13f, 261.63f, 196f }, 0.17f, 5f, 0.3f, 0.4f));

            default: // Click: tiny blip
                return Synth("Click", 0.03f, (t, _) => 0.6f * Sine(t, 1500f) * Decay(t, 120f));
        }
    }

    private static AudioClip Synth(string name, float seconds, Func<float, float, float> sample)
    {
        int count = Mathf.CeilToInt(seconds * SampleRate);
        int fadeSamples = Mathf.CeilToInt(0.005f * SampleRate);
        var data = new float[count];
        var random = new System.Random(12345);

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            float fadeOut = Mathf.Clamp01((count - i) / (float)fadeSamples); // avoids a click at the end
            data[i] = Mathf.Clamp(sample(t, noise), -1f, 1f) * fadeOut;
        }

        AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // A sequence of short notes; the last one rings until the clip ends.
    private static float Notes(float t, float[] frequencies, float noteLength, float decay, float sineMix, float squareMix)
    {
        int note = Mathf.Min((int)(t / noteLength), frequencies.Length - 1);
        float local = t - note * noteLength;
        float phase = 2f * Mathf.PI * frequencies[note] * t;
        return (sineMix * Mathf.Sin(phase) + squareMix * SoftSquare(phase)) * Decay(local, decay);
    }

    private static float Sine(float t, float frequency) => Mathf.Sin(2f * Mathf.PI * frequency * t);

    private static float SoftSquare(float phase) => Mathf.Clamp(Mathf.Sin(phase) * 3f, -1f, 1f) * 0.7f;

    private static float Decay(float t, float rate) => Mathf.Exp(-t * rate);

    // Phase of a tone gliding linearly from f0 to f1 over the given duration.
    private static float SweepPhase(float t, float f0, float f1, float duration)
    {
        return 2f * Mathf.PI * (f0 * t + (f1 - f0) * t * t / (2f * duration));
    }
}
