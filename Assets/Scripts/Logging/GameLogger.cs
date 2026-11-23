using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Writes the gameplay log: one text line per event, fields separated by semicolons, into one file per session
/// under Application.persistentDataPath/logs. Lines are buffered in memory and written to disk every few seconds
/// (LogConfig.flushInterval) and at every level end, pause and quit, never every frame.
/// Starts by itself before the first scene loads and attaches a <see cref="LevelLogProbe"/> to every level.
/// </summary>
public sealed class GameLogger : MonoBehaviour
{
    public const int FormatVersion = 1;

    private const string PlayerIdKey = "Log.PlayerId";
    private const string TotalPlayKey = "Log.TotalPlaySeconds";
    private const string AttemptsPrefix = "Log.Attempts.";

    public static GameLogger Instance { get; private set; }

    private readonly StringBuilder buffer = new(8192);
    private LogConfig config;
    private string filePath;
    private float sessionStart;
    private float nextFlushAt;
    private bool sessionEnded;
    private LevelLogProbe probe;

    private float playSeconds;
    private float pausedSeconds;
    private int attempts;
    private int maxReached = -1;
    private int maxCleared = -1;

    public string FilePath => filePath;
    public string PlayerId { get; private set; }
    public string SessionId { get; private set; }

    // Resets the static state when domain reload is turned off in the Editor's play mode options.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null || !LogConfig.Current.loggingEnabled)
            return;

        GameObject host = new("GameLogger");
        DontDestroyOnLoad(host);
        host.AddComponent<GameLogger>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        config = LogConfig.Current;
        sessionStart = Time.realtimeSinceStartup;
        nextFlushAt = sessionStart + config.flushInterval;

        PlayerId = PlayerPrefs.GetString(PlayerIdKey, "");
        if (string.IsNullOrEmpty(PlayerId))
        {
            PlayerId = Guid.NewGuid().ToString("N").Substring(0, 8);
            PlayerPrefs.SetString(PlayerIdKey, PlayerId);
            PlayerPrefs.Save();
        }
        SessionId = Guid.NewGuid().ToString("N").Substring(0, 8);

        string folder = Path.Combine(Application.persistentDataPath, config.folderName);
        filePath = Path.Combine(folder, $"session_{DateTime.Now:yyyyMMdd_HHmmss}_{SessionId}.log");
        try
        {
            Directory.CreateDirectory(folder);
            CloseCrashedLogs(folder);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameLogger] Could not prepare the log folder: {e.Message}");
        }

        WriteHeader();
        Write("session_start", PlayerId, Application.version, Application.platform.ToString(),
            $"{Screen.width}x{Screen.height}", Difficulty.Label.ToLowerInvariant(),
            PlayerPrefs.GetInt(TutorialDirector.DoneKey, 0) == 1, CountClearedLevels(),
            PlayerPrefs.GetFloat(TotalPlayKey, 0f) / 60f);
        Flush();
        Debug.Log($"[GameLogger] Writing the gameplay log to {filePath}");

        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.quitting += OnQuitting;
    }

    private void Start()
    {
        // Covers the first scene in case its sceneLoaded call came before this object existed.
        AttachProbe();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Application.quitting -= OnQuitting;
        Instance = null;
    }

    private void Update()
    {
        if (Time.realtimeSinceStartup >= nextFlushAt)
            Flush();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            Flush();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            Flush();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachProbe();
    }

    private static void AttachProbe()
    {
        LevelManager level = FindFirstObjectByType<LevelManager>();
        if (level != null && level.GetComponent<LevelLogProbe>() == null)
            level.gameObject.AddComponent<LevelLogProbe>();
    }

    private void OnQuitting()
    {
        if (probe != null)
            probe.EndLevel("quit");
        EndSession("quit");
    }

    private void EndSession(string reason)
    {
        if (sessionEnded)
            return;

        float duration = Time.realtimeSinceStartup - sessionStart;
        Write("session_end", duration, playSeconds, pausedSeconds,
            Mathf.Max(0f, duration - playSeconds - pausedSeconds), attempts,
            IndexOrDash(maxReached), IndexOrDash(maxCleared), reason);
        sessionEnded = true;
        Flush();
        PlayerPrefs.Save();
    }

    // ---------- Called by the level probe ----------

    /// <summary>Counts a new attempt at <paramref name="scene"/> (kept across sessions) and returns its number.</summary>
    public int NextAttempt(string scene)
    {
        int attempt = PlayerPrefs.GetInt(AttemptsPrefix + scene, 0) + 1;
        PlayerPrefs.SetInt(AttemptsPrefix + scene, attempt);
        return attempt;
    }

    public void RegisterProbe(LevelLogProbe levelProbe, int levelIndex)
    {
        probe = levelProbe;
        attempts++;
        maxReached = Mathf.Max(maxReached, levelIndex);
    }

    public void ReportLevelEnd(LevelLogProbe levelProbe, int levelIndex, bool won, float playedSeconds, float pausedTime)
    {
        playSeconds += playedSeconds;
        pausedSeconds += pausedTime;
        if (won)
            maxCleared = Mathf.Max(maxCleared, levelIndex);
        PlayerPrefs.SetFloat(TotalPlayKey, PlayerPrefs.GetFloat(TotalPlayKey, 0f) + playedSeconds);

        if (ReferenceEquals(probe, levelProbe))
            probe = null;
    }

    // ---------- Writing ----------

    /// <summary>Adds one line: t;lt;event;fields. Floats get up to 2 decimals with a '.', null or "" becomes '-'.</summary>
    public void Write(string eventName, params object[] fields)
    {
        if (sessionEnded)
            return;

        buffer.Append(Num(Time.realtimeSinceStartup - sessionStart)).Append(';');
        buffer.Append(ReferenceEquals(probe, null) ? "-" : Num(probe.LevelTime)).Append(';');
        buffer.Append(eventName);
        if (fields != null)
            foreach (object field in fields)
                buffer.Append(';').Append(Field(field));
        buffer.Append('\n');

        if (buffer.Length >= config.maxBufferedChars)
            Flush();
    }

    /// <summary>Writes the buffered lines to the log file.</summary>
    public void Flush()
    {
        nextFlushAt = Time.realtimeSinceStartup + config.flushInterval;
        if (buffer.Length == 0 || string.IsNullOrEmpty(filePath))
            return;

        try
        {
            File.AppendAllText(filePath, buffer.ToString());
            buffer.Clear();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameLogger] Could not write the log: {e.Message}");
            // Keep the lines for the next try, but do not grow without limit if the disk keeps failing.
            if (buffer.Length > config.maxBufferedChars * 8)
                buffer.Clear();
        }
    }

    public static string Num(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return "-";
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string Field(object value)
    {
        switch (value)
        {
            case null:
                return "-";
            case string text:
                return text.Length == 0 ? "-" : Clean(text);
            case float f:
                return Num(f);
            case double d:
                return Num((float)d);
            case int i:
                return i.ToString(CultureInfo.InvariantCulture);
            case bool b:
                return b ? "1" : "0";
            default:
                return Clean(Convert.ToString(value, CultureInfo.InvariantCulture));
        }
    }

    // Keeps one event on one line and the separator unambiguous.
    private static string Clean(string text)
    {
        return text.Replace(';', ',').Replace('\n', ' ').Replace('\r', ' ');
    }

    private static object IndexOrDash(int index) => index >= 0 ? index : (object)"-";

    private void WriteHeader()
    {
        buffer.Append("# Ninja Spinner gameplay log, format ").Append(FormatVersion).Append('\n');
        buffer.Append("# One line = one event: t;lt;event;fields  (t = real seconds since session start, ")
              .Append("lt = level time without pauses or '-', '-' = no value, distances in Unity units)\n");
        buffer.Append("# session_start;playerId;gameVersion;platform;resolution;difficulty;tutorialDone;levelsCleared;totalPlayMin\n");
        buffer.Append("# level_start;scene;index;attempt;difficulty;timeLimit;enemies;killInOrder;hearts\n");
        buffer.Append("# enemy;enemyId;kind;x;y;killOrder\n");
        buffer.Append("# move;x;y;speed;heading;nearEnemy;nearShot;danger;hearts  (every ")
              .Append(Num(config.moveInterval)).Append(" s)\n");
        buffer.Append("# saw;avgSpin;maxSpin;poweredPct;cappedPct;orbitDeg;reversals;extension  (every ")
              .Append(Num(config.sawWindow)).Append(" s; powered = spin >= ").Append(Num(config.poweredSpin)).Append(" deg/s)\n");
        buffer.Append("# saw_pass;enemyId;kind;ticks;damage;avgSpin;impactSpeed;killed\n");
        buffer.Append("# spin_up;seconds\n");
        buffer.Append("# danger;enter|exit;enemyId;kind;minDist;seconds  (danger radius ").Append(Num(config.dangerRadius)).Append(")\n");
        buffer.Append("# close_call;shooterKind;minGap\n");
        buffer.Append("# hit;source;kind;enemyId;hearts;x;y\n");
        buffer.Append("# kill;enemyId;kind;timeToKill;passes;dist;orderOk\n");
        buffer.Append("# loss;x;y;reason;lastHit;nearby;risk\n");
        buffer.Append("# level_end;scene;attempt;result;reason;elapsed;stars;hearts;enemiesLeft;newBest\n");
        buffer.Append("# level_summary;scene;attempt;distance;cells;stillPct;avgSpin;poweredPct;reversalsPerMin;spinUp;")
              .Append("passes;dmgPerPass;kills;risk;dangerPct;closeCalls;hits;lowHpDangerSec\n");
        buffer.Append("# session_end;duration;playTime;pausedTime;otherTime;attempts;maxReached;maxCleared;reason\n");
    }

    private static int CountClearedLevels()
    {
        int cleared = 0;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scene = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
            if (LevelProgress.IsCompleted(scene))
                cleared++;
        }
        return cleared;
    }

    // A session that never wrote session_end (crash, killed process) gets one, so its length is known.
    private void CloseCrashedLogs(string folder)
    {
        foreach (string path in Directory.GetFiles(folder, "session_*.log"))
        {
            if (path == filePath)
                continue;

            string last = ReadLastLine(path);
            if (string.IsNullOrEmpty(last) || last.StartsWith("#") || last.Contains(";session_end;"))
                continue;

            string t = last.Split(';')[0];
            File.AppendAllText(path, $"{t};-;session_end;{t};-;-;-;-;-;-;crash\n");
        }
    }

    private static string ReadLastLine(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        long length = stream.Length;
        if (length == 0)
            return null;

        int size = (int)Math.Min(length, 4096);
        byte[] bytes = new byte[size];
        stream.Seek(length - size, SeekOrigin.Begin);
        int read = stream.Read(bytes, 0, size);
        string tail = Encoding.UTF8.GetString(bytes, 0, read).TrimEnd('\n', '\r');
        int lineStart = tail.LastIndexOf('\n');
        return lineStart >= 0 ? tail.Substring(lineStart + 1) : tail;
    }
}
