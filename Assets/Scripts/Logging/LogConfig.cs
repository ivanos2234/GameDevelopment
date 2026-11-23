using UnityEngine;

/// <summary>
/// Settings for the gameplay logger. Optional: create one with Assets > Create > Ninja Spinner > Log Config and
/// put it at Resources/LogConfig to tune the values in the Inspector. Without it the defaults below are used.
/// </summary>
[CreateAssetMenu(menuName = "Ninja Spinner/Log Config", fileName = "LogConfig")]
public sealed class LogConfig : ScriptableObject
{
    [Tooltip("Turns all gameplay logging on or off.")]
    public bool loggingEnabled = true;

    [Header("Writing")]
    [Min(1f), Tooltip("Seconds between writes of the buffered lines to the log file (real time). " +
                      "The file is also written at every level end, pause and quit.")]
    public float flushInterval = 5f;
    [Min(1024), Tooltip("Write early if the buffer grows past this many characters.")]
    public int maxBufferedChars = 32768;
    [Tooltip("Folder under Application.persistentDataPath.")]
    public string folderName = "logs";

    [Header("Sampling")]
    [Min(0.1f), Tooltip("Seconds between 'move' lines.")]
    public float moveInterval = 0.5f;
    [Min(0.25f), Tooltip("Seconds summarised by each 'saw' line.")]
    public float sawWindow = 1f;
    [Min(0f), Tooltip("Below this speed (u/s) the player counts as standing still.")]
    public float stillSpeed = 0.5f;
    [Min(0.25f), Tooltip("Size of the grid cells used to count how much of the arena was visited.")]
    public float coverageCellSize = 1f;

    [Header("Saw")]
    [Min(0f), Tooltip("Spin (deg/s) that counts as a powered swing. 650 is the tutorial's spin goal.")]
    public float poweredSpin = 650f;
    [Min(0f), Tooltip("Seconds the spin must stay powered for 'spin_up'.")]
    public float spinUpHold = 1f;
    [Min(0f), Tooltip("Degrees the saw must move back around the player before it counts as a direction reversal.")]
    public float reversalDeadZone = 15f;
    [Min(0.05f), Tooltip("A pause in saw damage longer than this ends a 'saw_pass'.")]
    public float passGap = 0.45f;

    [Header("Proximity and risk")]
    [Min(0.1f), Tooltip("Enemies closer than this (centre to centre) put the player in danger. 2.8 = chaser backstep distance.")]
    public float dangerRadius = 2.8f;
    [Min(0f), Tooltip("An enemy must get this much further than the danger radius before it counts as having left.")]
    public float dangerExitMargin = 0.5f;
    [Min(0f), Tooltip("A shot that passes this close to the player's collider (gap, in units) without hitting is a close call.")]
    public float closeCallGap = 0.4f;
    [Min(0f), Tooltip("Radius for listing nearby enemies in the 'loss' line.")]
    public float nearbyRadius = 4f;
    [Min(1f), Tooltip("Seconds of risk averaged in the 'loss' line.")]
    public float lossRiskWindow = 10f;

    private static LogConfig current;

    public static LogConfig Current
    {
        get
        {
            if (current == null)
                current = Resources.Load<LogConfig>("LogConfig");
            if (current == null)
                current = CreateInstance<LogConfig>();
            return current;
        }
    }
}
