using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum LevelState
{
    Playing,
    Paused,
    Won,
    Lost,
}

/// <summary>
/// Runs one level: the goal is to destroy every enemy before the timer runs out.
/// Put one in each level scene; enemies already in the scene are found automatically.
/// </summary>
public sealed class LevelManager : MonoBehaviour
{
    [Header("Level")]
    [SerializeField] private string levelTitle = "Level 1";
    [SerializeField] private string objective = "Destroy all enemies";
    [SerializeField, TextArea, Tooltip("Optional hint shown in a box at the start of the level.")]
    private string tip = "";
    [SerializeField, Min(0f), Tooltip("Seconds to clear the level. 0 = no time limit.")]
    private float timeLimit = 90f;
    [SerializeField, Tooltip("Enemies with a kill order number must die in that order; killing one too early loses the level.")]
    private bool killInOrder;

    [Header("Stars (clearing the level gives the first one)")]
    [SerializeField, Min(0f), Tooltip("Clear within this many seconds for 2 stars. 0 = two thirds of the time limit.")]
    private float twoStarTime = 0f;
    [SerializeField, Min(0f), Tooltip("Clear within this many seconds for 3 stars. 0 = 40% of the time limit.")]
    private float threeStarTime = 0f;

    [Header("Scenes")]
    [SerializeField, Tooltip("Scene loaded after this level is cleared. Leave empty on the last level.")]
    private string nextLevelScene = "";
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("References (found automatically when empty)")]
    [SerializeField] private PlayerHealth2D player;

    private readonly HashSet<EnemyHealth2D> livingEnemies = new();

    public LevelState State { get; private set; } = LevelState.Playing;
    public string LevelTitle => levelTitle;
    public string Objective => objective;
    public string Tip => tip;
    public IReadOnlyCollection<EnemyHealth2D> LivingEnemies => livingEnemies;
    public bool HasTimeLimit => timeLimit > 0f;
    public float TimeLimit => timeLimit;
    public float ElapsedTime { get; private set; }
    public float TimeRemaining => HasTimeLimit ? Mathf.Max(0f, timeLimit - ElapsedTime) : 0f;
    public int EnemiesTotal { get; private set; }
    public int EnemiesRemaining => livingEnemies.Count;
    public PlayerHealth2D Player => player;
    public bool KillInOrder => killInOrder;
    /// <summary>Kill order number of the enemy that has to fall next, or 0 when none is left.</summary>
    public int NextKillOrder { get; private set; }
    /// <summary>Lets a script (the tutorial) hold the clock until it is time for it.</summary>
    public bool TimerRunning { get; set; } = true;
    /// <summary>While true, killing the last enemy does not clear the level (the tutorial adds enemies step by step).</summary>
    public bool WinBlocked { get; set; }
    public bool HasNextLevel => !string.IsNullOrEmpty(nextLevelScene);
    public string LoseReason { get; private set; } = "";
    public float BestTime { get; private set; } = -1f;
    public bool IsNewBestTime { get; private set; }
    public int StarsEarned { get; private set; }
    public float TwoStarTime => twoStarTime > 0f ? twoStarTime : Mathf.Round(timeLimit * 2f / 3f);
    public float ThreeStarTime => threeStarTime > 0f ? threeStarTime : Mathf.Round(timeLimit * 0.4f);

    public event Action StateChanged;
    public event Action EnemiesChanged;

    private static string SceneName => SceneManager.GetActiveScene().name;

    private void Awake()
    {
        Time.timeScale = 1f;
        BestTime = LevelProgress.GetBestTime(SceneName);

        Difficulty.Preset difficulty = Difficulty.Current;
        timeLimit *= difficulty.timeLimit;
        twoStarTime *= difficulty.timeLimit;
        threeStarTime *= difficulty.timeLimit;

        if (player == null)
            player = FindFirstObjectByType<PlayerHealth2D>();
        if (player != null)
            player.Died += OnPlayerDied;
    }

    private void Start()
    {
        foreach (EnemyHealth2D enemy in FindObjectsByType<EnemyHealth2D>(FindObjectsSortMode.None))
            RegisterEnemy(enemy);

        if (EnemiesTotal == 0 && !WinBlocked)
            Debug.LogWarning("This level has no enemies, so it can never be cleared.", this);
        RefreshKillOrder();
    }

    private void OnDestroy()
    {
        if (player != null)
            player.Died -= OnPlayerDied;
        foreach (EnemyHealth2D enemy in livingEnemies)
            if (enemy != null)
                enemy.Died -= OnEnemyDied;

        Time.timeScale = 1f;
    }

    /// <summary>Adds an enemy to the goal. Call this for enemies spawned after the level has started.</summary>
    public void RegisterEnemy(EnemyHealth2D enemy)
    {
        if (enemy == null || enemy.IsDead || !livingEnemies.Add(enemy))
            return;

        enemy.Died += OnEnemyDied;
        EnemiesTotal++;
        RefreshKillOrder();
        EnemiesChanged?.Invoke();
    }

    /// <summary>Clears the level if every enemy is already gone, e.g. after <see cref="WinBlocked"/> is lifted.</summary>
    public void TryFinish()
    {
        if (livingEnemies.Count == 0 && State == LevelState.Playing && !WinBlocked)
            Win();
    }

    private void RefreshKillOrder()
    {
        if (!killInOrder)
            return;

        NextKillOrder = 0;
        foreach (EnemyHealth2D enemy in livingEnemies)
            if (enemy.KillOrder > 0 && (NextKillOrder == 0 || enemy.KillOrder < NextKillOrder))
                NextKillOrder = enemy.KillOrder;

        foreach (EnemyHealth2D enemy in livingEnemies)
            enemy.SetNextInOrder(enemy.KillOrder > 0 && enemy.KillOrder == NextKillOrder);
    }

    private void Update()
    {
        HandleKeyboard();

        if (State != LevelState.Playing)
            return;

        if (!TimerRunning)
            return;

        ElapsedTime += Time.deltaTime;
        if (HasTimeLimit && ElapsedTime >= timeLimit)
        {
            ElapsedTime = timeLimit;
            Lose("Time's up!");
        }
    }

    private void HandleKeyboard()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame)
            TogglePause();
        else if (keyboard.rKey.wasPressedThisFrame)
            Restart();
        else if (keyboard.mKey.wasPressedThisFrame)
            Sfx.Muted = !Sfx.Muted;
        else if (State == LevelState.Won && HasNextLevel &&
                 (keyboard.enterKey.wasPressedThisFrame || keyboard.nKey.wasPressedThisFrame))
            LoadNextLevel();
    }

    public void TogglePause()
    {
        if (State == LevelState.Playing)
            SetState(LevelState.Paused, 0f);
        else if (State == LevelState.Paused)
            SetState(LevelState.Playing, 1f);
    }

    public void Restart()
    {
        LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadNextLevel()
    {
        LoadScene(HasNextLevel ? nextLevelScene : mainMenuScene);
    }

    public void LoadMainMenu()
    {
        LoadScene(mainMenuScene);
    }

    private void OnEnemyDied(EnemyHealth2D enemy)
    {
        enemy.Died -= OnEnemyDied;
        if (!livingEnemies.Remove(enemy))
            return;

        // Any enemy can be hit, but the numbered ones must fall in order.
        if (killInOrder && enemy.KillOrder > 0 && enemy.KillOrder != NextKillOrder && State == LevelState.Playing)
        {
            EnemiesChanged?.Invoke();
            Lose($"Wrong order! #{NextKillOrder} had to fall before #{enemy.KillOrder}.");
            return;
        }

        RefreshKillOrder();
        EnemiesChanged?.Invoke();
        TryFinish();
    }

    private void OnPlayerDied()
    {
        Lose("You were defeated!");
    }

    private void Win()
    {
        StarsEarned = 1;
        if (ElapsedTime <= TwoStarTime)
            StarsEarned++;
        if (ElapsedTime <= ThreeStarTime)
            StarsEarned++;

        IsNewBestTime = LevelProgress.RecordWin(SceneName, ElapsedTime, StarsEarned);
        BestTime = LevelProgress.GetBestTime(SceneName);

        Sfx.Play(SfxKind.LevelCleared);
        SetState(LevelState.Won, 0f);
    }

    private void Lose(string reason)
    {
        if (State == LevelState.Won || State == LevelState.Lost)
            return;

        LoseReason = reason;
        Sfx.Play(SfxKind.GameOver);
        SetState(LevelState.Lost, 0f);
    }

    private void SetState(LevelState state, float timeScale)
    {
        State = state;
        Time.timeScale = timeScale;
        StateChanged?.Invoke();
    }

    private static void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public static string FormatTime(float seconds)
    {
        // Work in whole tenths so 59.97 shows as 00:59.9 instead of rounding up to 00:60.0.
        int tenths = Mathf.FloorToInt(Mathf.Max(0f, seconds) * 10f);
        return $"{tenths / 600:00}:{tenths % 600 / 10:00}.{tenths % 10}";
    }
}
