using UnityEngine;

/// <summary>Saved per-level results (best time and stars), shared by the levels and the level select.</summary>
public static class LevelProgress
{
    private const string BestTimePrefix = "BestTime.";
    private const string StarsPrefix = "Stars.";

    public static bool IsCompleted(string scene) => PlayerPrefs.HasKey(BestTimePrefix + scene);
    public static float GetBestTime(string scene) => PlayerPrefs.GetFloat(BestTimePrefix + scene, -1f);
    public static int GetStars(string scene) => PlayerPrefs.GetInt(StarsPrefix + scene, 0);

    /// <summary>Stores a cleared run, keeping the best time and the most stars. Returns true for a new best time.</summary>
    public static bool RecordWin(string scene, float time, int stars)
    {
        float best = GetBestTime(scene);
        bool newBest = best < 0f || time < best;
        if (newBest)
            PlayerPrefs.SetFloat(BestTimePrefix + scene, time);
        if (stars > GetStars(scene))
            PlayerPrefs.SetInt(StarsPrefix + scene, stars);

        PlayerPrefs.Save();
        return newBest;
    }
}
