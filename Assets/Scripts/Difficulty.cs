using System;
using UnityEngine;

public enum DifficultyLevel
{
    Easy,
    Normal,
}

/// <summary>
/// Difficulty presets as multipliers on the values set in the prefabs and levels. Normal is 1 everywhere,
/// i.e. the game as designed. Lives at Resources/Difficulty so every value can be tuned in the Inspector.
/// </summary>
[CreateAssetMenu(menuName = "Ninja Spinner/Difficulty", fileName = "Difficulty")]
public sealed class Difficulty : ScriptableObject
{
    [Serializable]
    public sealed class Preset
    {
        [Min(0.05f), Tooltip("Enemy health.")] public float enemyHealth = 1f;
        [Min(0.05f), Tooltip("How fast enemies move (chasers, gunners, guardians).")] public float enemySpeed = 1f;
        [Min(0.05f), Tooltip("How fast enemy shots fly.")] public float projectileSpeed = 1f;
        [Min(0.05f), Tooltip("Time between enemy shots. Above 1 = they shoot less often.")] public float fireInterval = 1f;
        [Min(0.05f), Tooltip("Level time limits.")] public float timeLimit = 1f;
    }

    private const string PrefsKey = "Difficulty";

    public Preset easy = new()
    {
        enemyHealth = 0.6f,
        enemySpeed = 0.7f,
        projectileSpeed = 0.65f,
        fireInterval = 1.3f,
        timeLimit = 1.2f,
    };

    public Preset normal = new();

    private static Difficulty settings;

    private static Difficulty Settings
    {
        get
        {
            if (settings == null)
                settings = Resources.Load<Difficulty>("Difficulty");
            if (settings == null)
                settings = CreateInstance<Difficulty>();
            return settings;
        }
    }

    public static DifficultyLevel Level
    {
        get => (DifficultyLevel)Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey, (int)DifficultyLevel.Normal), 0, 1);
        set
        {
            PlayerPrefs.SetInt(PrefsKey, (int)value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>The multipliers for the chosen difficulty.</summary>
    public static Preset Current => Level == DifficultyLevel.Easy ? Settings.easy : Settings.normal;

    public static string Label => Level == DifficultyLevel.Easy ? "EASY" : "NORMAL";
}
