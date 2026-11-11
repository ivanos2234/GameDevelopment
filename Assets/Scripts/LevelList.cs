using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>The levels shown in the level select, in play order. Each level unlocks once the one before it is cleared.</summary>
[CreateAssetMenu(menuName = "Ninja Spinner/Level List", fileName = "LevelList")]
public sealed class LevelList : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public string scene;
        public string title;
        [Tooltip("How many enemies of each EnemyKind the level has, for the level select preview.")]
        public int[] enemyCounts = new int[0];
        [Tooltip("Shown as a tag on the level select card.")]
        public bool killInOrder;
        [Tooltip("Floor color of the level's theme, used for the level select preview.")]
        public Color themeColor = new(0.118f, 0.141f, 0.212f, 1f);
    }

    [SerializeField] private List<Entry> levels = new();

    public IReadOnlyList<Entry> Levels => levels;

    public bool IsUnlocked(int index)
    {
        return index <= 0 || (index < levels.Count && LevelProgress.IsCompleted(levels[index - 1].scene));
    }
}
