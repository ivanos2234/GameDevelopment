using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Logs one level attempt for <see cref="GameLogger"/>: player movement, saw handling, distance to enemies and
/// shots, hits, kills and the level's start, end and summary. Added at runtime to the scene's LevelManager.
/// Continuous values are measured every physics step but written as one line every few tenths of a second.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(LevelManager))]
public sealed class LevelLogProbe : MonoBehaviour
{
    // An enemy or a Guardian the player can get hurt by.
    private sealed class Hazard
    {
        public string id;
        public string kind;
        public Transform transform;
        public int killOrder;
        public bool alive = true;
        public bool inDanger;
        public float dangerSince;
        public float dangerMin;
        public float firstHitAt = -1f;
        public int passes;
    }

    // One sweep of the saw through an enemy: from its first damage tick until the ticks stop.
    private sealed class SawPass
    {
        public Hazard hazard;
        public int ticks;
        public float damage;
        public float spinSum;
        public float maxImpact;
        public float lastTick;
    }

    private sealed class ShotTrack
    {
        public Collider2D collider;
        public float minGap = float.PositiveInfinity;
    }

    // Keys compare by reference, so entries for destroyed enemies and shots can still be found.
    private sealed class ByReference<T> : IEqualityComparer<T> where T : class
    {
        public bool Equals(T a, T b) => ReferenceEquals(a, b);
        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private LevelManager level;
    private LogConfig config;
    private GameLogger logger;

    private string sceneName;
    private int levelIndex;
    private int attempt;
    private bool started;
    private bool ended;
    private float lastLevelTime;

    private PlayerHealth2D player;
    private Rigidbody2D playerBody;
    private Collider2D playerCollider;
    private SawImpactDamage2D saw;
    private Rigidbody2D sawBody;
    private float chainLength;
    private bool chainFromTether;
    private float capDamagePerSecond;

    private readonly Dictionary<Object, Hazard> hazardsByObject = new(new ByReference<Object>());
    private readonly List<Hazard> liveHazards = new();
    private readonly Dictionary<string, int> kindCounters = new();
    private readonly Dictionary<EnemyHealth2D, SawPass> openPasses = new(new ByReference<EnemyHealth2D>());
    private readonly Dictionary<EnemyProjectile2D, ShotTrack> shots = new(new ByReference<EnemyProjectile2D>());
    private readonly List<EnemyHealth2D> scratchEnemies = new();
    private readonly HashSet<Vector2Int> visitedCells = new();

    // Time
    private float activeTime;      // scaled seconds while Playing
    private float playReal;        // real seconds while Playing
    private float pausedReal;      // real seconds while Paused
    private float lastRealtime;

    // Movement
    private Vector2 lastPosition;
    private float distance;
    private float moveTimer;
    private int moveSamples;
    private int stillSamples;

    // Proximity and risk
    private float nearestHazard = float.PositiveInfinity;
    private float nearestShot = float.PositiveInfinity;
    private int dangerCount;
    private double riskSum;
    private int riskSteps;
    private float windowRiskSum;
    private int windowRiskSteps;
    private float dangerTime;
    private float lowHpDangerTime;
    private float[] riskHistory;
    private int riskHistoryCount;
    private int riskHistoryNext;
    private int closeCalls;
    private int hits;
    private string lastHit = "-";

    // Saw, current window
    private float sawTimer;
    private int sawSteps;
    private float spinSum;
    private float spinMax;
    private int poweredSteps;
    private int cappedSteps;
    private float orbitDegrees;
    private int windowReversals;
    private float extensionSum;
    // Saw, whole attempt
    private int totalSawSteps;
    private double totalSpinSum;
    private int totalPoweredSteps;
    private int totalReversals;
    private int totalPasses;
    private float totalPassDamage;
    private int kills;
    // Orbit direction tracking
    private bool hasOrbitAngle;
    private float orbitAngle;
    private float orbitUnwrapped;
    private float orbitExtreme;
    private int orbitDirection;
    // Spin-up
    private bool spinUpDone;
    private float spinUpTime = -1f;
    private float poweredRunStart = -1f;

    /// <summary>The level clock shown on the HUD (pauses excluded).</summary>
    public float LevelTime => level != null ? (lastLevelTime = level.ElapsedTime) : lastLevelTime;

    private void Awake()
    {
        level = GetComponent<LevelManager>();
        config = LogConfig.Current;
        logger = GameLogger.Instance;
        sceneName = SceneManager.GetActiveScene().name;
        levelIndex = LevelIndexOf(sceneName);

        int historyLength = Mathf.Max(1, Mathf.CeilToInt(config.lossRiskWindow / config.moveInterval));
        riskHistory = new float[historyLength];
    }

    private IEnumerator Start()
    {
        if (logger == null)
        {
            enabled = false;
            yield break;
        }

        // Wait one frame so LevelManager.Start has registered the enemies already in the scene.
        yield return null;
        BeginLevel();
    }

    private void OnDestroy()
    {
        // Scene changed some other way (or the level object was removed): close the attempt anyway.
        if (started && !ended)
            EndLevel("exit");
        Unsubscribe();
    }

    // ---------- Level start and end ----------

    private void BeginLevel()
    {
        player = level.Player;
        if (player != null)
        {
            playerBody = player.GetComponent<Rigidbody2D>();
            playerCollider = player.GetComponent<Collider2D>();
        }

        saw = FindFirstObjectByType<SawImpactDamage2D>();
        if (saw != null)
        {
            sawBody = saw.GetComponent<Rigidbody2D>();
            DistanceJoint2D tether = saw.GetComponent<DistanceJoint2D>();
            chainFromTether = tether != null && tether.distance > 0f;
            chainLength = chainFromTether ? tether.distance : 0f;
            capDamagePerSecond = saw.EstimateDamagePerSecond(1000000f);
        }

        attempt = logger.NextAttempt(sceneName);
        logger.RegisterProbe(this, levelIndex);
        logger.Write("level_start", sceneName, levelIndex, attempt, Difficulty.Label.ToLowerInvariant(),
            level.HasTimeLimit ? level.TimeLimit : 0f, level.EnemiesTotal, level.KillInOrder,
            player != null ? player.CurrentHearts : (object)null);

        foreach (GuardianOrbit2D guardian in FindObjectsByType<GuardianOrbit2D>(FindObjectsSortMode.None))
            AddHazard(guardian, guardian.transform, "Guardian", 0);
        SyncEnemies();

        level.StateChanged += OnStateChanged;
        level.EnemiesChanged += SyncEnemies;
        LevelManager.SceneLeaving += OnSceneLeaving;
        EnemyProjectile2D.Ended += OnShotEnded;
        if (player != null)
            player.Damaged += OnPlayerDamaged;
        if (saw != null)
            saw.DamagedEnemy += OnSawDamage;

        if (playerBody != null)
            lastPosition = playerBody.position;
        lastRealtime = Time.realtimeSinceStartup;
        started = true;
    }

    /// <summary>Closes the attempt: result is won, lost, restart, menu, quit or exit.</summary>
    public void EndLevel(string result)
    {
        if (!started || ended)
            return;
        CountRealTime();
        RecordPendingKills();
        ended = true;

        scratchEnemies.Clear();
        scratchEnemies.AddRange(openPasses.Keys);
        foreach (EnemyHealth2D enemy in scratchEnemies)
            ClosePass(enemy, false);
        foreach (Hazard hazard in liveHazards)
            if (hazard.inDanger)
                ExitDanger(hazard);
        WriteSaw();

        bool won = result == "won";
        logger.Write("level_end", sceneName, attempt, result, result == "lost" ? LossReason() : "-",
            level.ElapsedTime, won ? level.StarsEarned : 0, player != null ? player.CurrentHearts : (object)null,
            level.EnemiesRemaining, won && level.IsNewBestTime);

        float minutes = Mathf.Max(activeTime, 1f) / 60f;
        logger.Write("level_summary", sceneName, attempt, distance, visitedCells.Count,
            Percent(stillSamples, moveSamples),
            totalSawSteps > 0 ? Mathf.RoundToInt((float)(totalSpinSum / totalSawSteps)) : (object)null,
            Percent(totalPoweredSteps, totalSawSteps),
            totalReversals / minutes,
            spinUpDone ? spinUpTime : (object)null,
            totalPasses, totalPasses > 0 ? totalPassDamage / totalPasses : (object)null,
            kills,
            riskSteps > 0 ? Mathf.RoundToInt((float)(riskSum / riskSteps) * 100f) : (object)null,
            activeTime > 0f ? Mathf.RoundToInt(dangerTime / activeTime * 100f) : (object)null,
            closeCalls, hits, lowHpDangerTime);

        logger.ReportLevelEnd(this, levelIndex, won, playReal, pausedReal);
        logger.Flush();
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        LevelManager.SceneLeaving -= OnSceneLeaving;
        EnemyProjectile2D.Ended -= OnShotEnded;
        if (level != null)
        {
            level.StateChanged -= OnStateChanged;
            level.EnemiesChanged -= SyncEnemies;
        }
        if (player != null)
            player.Damaged -= OnPlayerDamaged;
        if (saw != null)
            saw.DamagedEnemy -= OnSawDamage;
        foreach (KeyValuePair<Object, Hazard> pair in hazardsByObject)
            if (pair.Key is EnemyHealth2D enemy && enemy != null)
                enemy.Died -= OnEnemyDied;
    }

    private void OnStateChanged()
    {
        switch (level.State)
        {
            case LevelState.Won:
                EndLevel("won");
                break;
            case LevelState.Lost:
                WriteLoss();
                EndLevel("lost");
                break;
            case LevelState.Paused:
                logger.Flush();
                break;
        }
    }

    private void OnSceneLeaving(string nextScene)
    {
        // Only reached while the level is still running: a restart (R) or going back to the menu.
        EndLevel(nextScene == sceneName ? "restart" : "menu");
    }

    private string LossReason()
    {
        if (player != null && player.IsDead)
            return "defeated";
        if (level.HasTimeLimit && level.ElapsedTime >= level.TimeLimit)
            return "time";
        if (level.KillInOrder)
            return "wrong_order";
        return "other";
    }

    private void WriteLoss()
    {
        if (!started || ended)
            return;

        Vector2 position = playerBody != null ? playerBody.position : Vector2.zero;
        StringBuilder nearby = new();
        foreach (Hazard hazard in liveHazards)
        {
            if (hazard.transform == null)
                continue;
            if (Vector2.Distance(position, hazard.transform.position) <= config.nearbyRadius)
                nearby.Append(nearby.Length > 0 ? "+" : "").Append(hazard.id);
        }

        float recentRisk = 0f;
        for (int i = 0; i < riskHistoryCount; i++)
            recentRisk += riskHistory[i];
        object risk = riskHistoryCount > 0 ? Mathf.RoundToInt(recentRisk / riskHistoryCount * 100f) : (object)null;

        logger.Write("loss", position.x, position.y, LossReason(), lastHit, nearby.ToString(), risk);
    }

    // ---------- Enemies ----------

    private void SyncEnemies()
    {
        foreach (EnemyHealth2D enemy in level.LivingEnemies)
        {
            if (enemy == null || hazardsByObject.ContainsKey(enemy))
                continue;

            AddHazard(enemy, enemy.transform, enemy.Kind.ToString(), enemy.KillOrder);
            enemy.Died += OnEnemyDied;
        }
    }

    private Hazard AddHazard(Object key, Transform target, string kind, int killOrder)
    {
        kindCounters.TryGetValue(kind, out int count);
        kindCounters[kind] = ++count;

        Hazard hazard = new()
        {
            id = kind + "#" + count,
            kind = kind,
            transform = target,
            killOrder = killOrder,
        };
        hazardsByObject[key] = hazard;
        liveHazards.Add(hazard);

        Vector2 position = target.position;
        logger.Write("enemy", hazard.id, kind, position.x, position.y, killOrder);
        return hazard;
    }

    private Hazard FindHazard(Object key)
    {
        if (ReferenceEquals(key, null))
            return null;
        hazardsByObject.TryGetValue(key, out Hazard hazard);
        return hazard;
    }

    private void OnEnemyDied(EnemyHealth2D enemy)
    {
        enemy.Died -= OnEnemyDied;
        Hazard hazard = FindHazard(enemy);
        if (hazard != null && hazard.alive && started && !ended)
            RecordKill(enemy, hazard);
    }

    // LevelManager also listens to Died and may end the level (last enemy, wrong order) before this probe hears
    // about the death, so EndLevel records enemies that have just died but were not counted yet.
    private void RecordPendingKills()
    {
        scratchEnemies.Clear();
        foreach (KeyValuePair<Object, Hazard> pair in hazardsByObject)
            if (pair.Key is EnemyHealth2D enemy && pair.Value.alive && enemy.IsDead) // health is a plain field, safe after Destroy
                scratchEnemies.Add(enemy);
        foreach (EnemyHealth2D enemy in scratchEnemies)
            RecordKill(enemy, FindHazard(enemy));
    }

    private void RecordKill(EnemyHealth2D enemy, Hazard hazard)
    {
        // Judge the kill order before this enemy leaves the list of living ones.
        object orderOk = "-";
        if (level.KillInOrder && hazard.killOrder > 0)
        {
            int next = int.MaxValue;
            foreach (Hazard other in liveHazards)
                if (other.alive && other.killOrder > 0 && other.killOrder < next)
                    next = other.killOrder;
            orderOk = hazard.killOrder == next;
        }

        if (openPasses.ContainsKey(enemy))
            ClosePass(enemy, true);
        if (hazard.inDanger)
            ExitDanger(hazard);

        hazard.alive = false;
        liveHazards.Remove(hazard);
        kills++;

        object timeToKill = hazard.firstHitAt >= 0f ? activeTime - hazard.firstHitAt : (object)null;
        object range = playerBody != null && enemy != null
            ? Vector2.Distance(playerBody.position, enemy.transform.position)
            : (object)null;
        logger.Write("kill", hazard.id, hazard.kind, timeToKill, hazard.passes, range, orderOk);
    }

    // ---------- Hits on the player ----------

    private void OnPlayerDamaged(int amount, GameObject source)
    {
        if (!started || ended)
            return;

        string sourceType = "-";
        Hazard hazard = null;
        if (source != null)
        {
            EnemyProjectile2D shot = source.GetComponent<EnemyProjectile2D>();
            GuardianOrbit2D guardian = source.GetComponentInParent<GuardianOrbit2D>();
            if (shot != null)
            {
                sourceType = "shot";
                hazard = FindHazard(shot.Shooter);
            }
            else if (guardian != null)
            {
                sourceType = "guardian";
                hazard = FindHazard(guardian);
            }
            else
            {
                sourceType = "contact";
                hazard = FindHazard(source.GetComponentInParent<EnemyHealth2D>());
            }
        }

        hits++;
        lastHit = sourceType + ":" + (hazard != null ? hazard.id : "-");
        Vector2 position = playerBody != null ? playerBody.position : Vector2.zero;
        logger.Write("hit", sourceType, hazard?.kind, hazard?.id, player.CurrentHearts, position.x, position.y);
    }

    // ---------- Saw ----------

    private void OnSawDamage(EnemyHealth2D enemy, float damage, float spin, float impactSpeed)
    {
        if (!started || ended)
            return;

        Hazard hazard = FindHazard(enemy);
        if (hazard == null)
        {
            SyncEnemies();
            hazard = FindHazard(enemy);
            if (hazard == null)
                return;
        }

        if (hazard.firstHitAt < 0f)
            hazard.firstHitAt = activeTime;

        if (openPasses.TryGetValue(enemy, out SawPass pass) && activeTime - pass.lastTick > config.passGap)
        {
            ClosePass(enemy, false);
            pass = null;
        }
        if (pass == null)
        {
            pass = new SawPass { hazard = hazard };
            openPasses[enemy] = pass;
            hazard.passes++;
        }

        pass.ticks++;
        pass.damage += damage;
        pass.spinSum += Mathf.Abs(spin);
        pass.maxImpact = Mathf.Max(pass.maxImpact, impactSpeed);
        pass.lastTick = activeTime;
    }

    private void ClosePass(EnemyHealth2D enemy, bool killed)
    {
        if (!openPasses.Remove(enemy, out SawPass pass))
            return;

        totalPasses++;
        totalPassDamage += pass.damage;
        logger.Write("saw_pass", pass.hazard.id, pass.hazard.kind, pass.ticks, pass.damage,
            Mathf.RoundToInt(pass.spinSum / Mathf.Max(1, pass.ticks)), pass.maxImpact, killed);
    }

    private void CloseStalePasses()
    {
        scratchEnemies.Clear();
        foreach (KeyValuePair<EnemyHealth2D, SawPass> pair in openPasses)
            if (activeTime - pair.Value.lastTick > config.passGap)
                scratchEnemies.Add(pair.Key);
        foreach (EnemyHealth2D enemy in scratchEnemies)
            ClosePass(enemy, false);
    }

    // ---------- Sampling ----------

    private void Update()
    {
        if (started && !ended)
            CountRealTime();
    }

    // Real time keeps running while paused (timeScale 0), unlike the physics steps below.
    private void CountRealTime()
    {
        float now = Time.realtimeSinceStartup;
        float delta = now - lastRealtime;
        lastRealtime = now;

        if (level.State == LevelState.Playing)
            playReal += delta;
        else if (level.State == LevelState.Paused)
            pausedReal += delta;
    }

    private void FixedUpdate()
    {
        if (!started || ended || playerBody == null || level.State != LevelState.Playing)
            return;

        float dt = Time.fixedDeltaTime;
        activeTime += dt;

        Vector2 position = playerBody.position;
        distance += Vector2.Distance(position, lastPosition);
        lastPosition = position;
        float cell = config.coverageCellSize;
        visitedCells.Add(new Vector2Int(Mathf.FloorToInt(position.x / cell), Mathf.FloorToInt(position.y / cell)));

        SampleHazards(position, dt);
        SampleShots(position);
        if (sawBody != null)
            SampleSaw(position, dt);

        moveTimer += dt;
        if (moveTimer >= config.moveInterval)
        {
            moveTimer -= config.moveInterval;
            WriteMove(position);
        }

        sawTimer += dt;
        if (sawTimer >= config.sawWindow)
        {
            sawTimer -= config.sawWindow;
            WriteSaw();
            CloseStalePasses();
        }
    }

    private void SampleHazards(Vector2 position, float dt)
    {
        float radius = config.dangerRadius;
        nearestHazard = float.PositiveInfinity;
        dangerCount = 0;

        for (int i = liveHazards.Count - 1; i >= 0; i--)
        {
            Hazard hazard = liveHazards[i];
            if (hazard.transform == null)
            {
                // A Guardian that shattered with its Master.
                if (hazard.inDanger)
                    ExitDanger(hazard);
                hazard.alive = false;
                liveHazards.RemoveAt(i);
                continue;
            }

            float d = Vector2.Distance(position, hazard.transform.position);
            nearestHazard = Mathf.Min(nearestHazard, d);

            if (!hazard.inDanger)
            {
                if (d <= radius)
                {
                    hazard.inDanger = true;
                    hazard.dangerSince = activeTime;
                    hazard.dangerMin = d;
                    logger.Write("danger", "enter", hazard.id, hazard.kind, d, null);
                }
            }
            else
            {
                hazard.dangerMin = Mathf.Min(hazard.dangerMin, d);
                if (d > radius + config.dangerExitMargin)
                    ExitDanger(hazard);
            }

            if (hazard.inDanger)
                dangerCount++;
        }

        float risk = float.IsPositiveInfinity(nearestHazard) ? 0f : Mathf.Clamp01(1f - nearestHazard / radius);
        riskSum += risk;
        riskSteps++;
        windowRiskSum += risk;
        windowRiskSteps++;

        if (dangerCount > 0)
        {
            dangerTime += dt;
            if (player != null && player.CurrentHearts == 1)
                lowHpDangerTime += dt;
        }
    }

    private void ExitDanger(Hazard hazard)
    {
        hazard.inDanger = false;
        logger.Write("danger", "exit", hazard.id, hazard.kind, hazard.dangerMin, activeTime - hazard.dangerSince);
    }

    private void SampleShots(Vector2 position)
    {
        nearestShot = float.PositiveInfinity;
        IReadOnlyList<EnemyProjectile2D> live = EnemyProjectile2D.Live;
        for (int i = 0; i < live.Count; i++)
        {
            EnemyProjectile2D shot = live[i];
            if (shot == null)
                continue;

            nearestShot = Mathf.Min(nearestShot, Vector2.Distance(position, shot.transform.position));

            if (!shots.TryGetValue(shot, out ShotTrack track))
            {
                track = new ShotTrack { collider = shot.GetComponent<Collider2D>() };
                shots[shot] = track;
            }

            if (track.collider != null && playerCollider != null)
            {
                ColliderDistance2D gap = Physics2D.Distance(track.collider, playerCollider);
                if (gap.isValid)
                    track.minGap = Mathf.Min(track.minGap, gap.distance);
            }
        }
    }

    private void OnShotEnded(EnemyProjectile2D shot)
    {
        if (!shots.Remove(shot, out ShotTrack track) || !started || ended)
            return;

        if (!shot.HitPlayer && track.minGap <= config.closeCallGap)
        {
            closeCalls++;
            Hazard shooter = FindHazard(shot.Shooter);
            logger.Write("close_call", shooter != null ? shooter.kind : null, Mathf.Max(0f, track.minGap));
        }
    }

    private void SampleSaw(Vector2 playerPosition, float dt)
    {
        float spin = Mathf.Abs(sawBody.angularVelocity);
        bool powered = spin >= config.poweredSpin;

        sawSteps++;
        spinSum += spin;
        spinMax = Mathf.Max(spinMax, spin);
        totalSawSteps++;
        totalSpinSum += spin;
        if (powered)
        {
            poweredSteps++;
            totalPoweredSteps++;
        }
        if (capDamagePerSecond > 0f && saw.EstimateDamagePerSecond(spin) >= capDamagePerSecond - 0.01f)
            cappedSteps++;

        Vector2 offset = sawBody.worldCenterOfMass - playerPosition;
        float reach = offset.magnitude;
        if (!chainFromTether)
            chainLength = Mathf.Max(chainLength, reach, 0.01f); // no tether found: use the longest reach seen
        extensionSum += reach / chainLength;

        // Which way the saw is circling the player, and how often that flips.
        if (reach > 0.2f)
        {
            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            if (hasOrbitAngle)
            {
                float delta = Mathf.DeltaAngle(orbitAngle, angle);
                orbitDegrees += delta;
                orbitUnwrapped += delta;
                TrackOrbitDirection();
            }
            else
            {
                orbitExtreme = orbitUnwrapped;
            }
            orbitAngle = angle;
            hasOrbitAngle = true;
        }

        // spin_up: when the spin first stays powered for spinUpHold seconds.
        if (!spinUpDone)
        {
            if (powered)
            {
                if (poweredRunStart < 0f)
                    poweredRunStart = activeTime - dt;
                if (activeTime - poweredRunStart >= config.spinUpHold)
                {
                    spinUpDone = true;
                    spinUpTime = poweredRunStart;
                    logger.Write("spin_up", spinUpTime);
                }
            }
            else
            {
                poweredRunStart = -1f;
            }
        }
    }

    // A reversal counts once the saw has come back more than the dead zone from its furthest point.
    private void TrackOrbitDirection()
    {
        float deadZone = config.reversalDeadZone;
        if (orbitDirection == 0)
        {
            if (orbitUnwrapped - orbitExtreme > deadZone)
                SetOrbitDirection(1, false);
            else if (orbitExtreme - orbitUnwrapped > deadZone)
                SetOrbitDirection(-1, false);
        }
        else if (orbitDirection > 0)
        {
            if (orbitUnwrapped > orbitExtreme)
                orbitExtreme = orbitUnwrapped;
            else if (orbitExtreme - orbitUnwrapped > deadZone)
                SetOrbitDirection(-1, true);
        }
        else
        {
            if (orbitUnwrapped < orbitExtreme)
                orbitExtreme = orbitUnwrapped;
            else if (orbitUnwrapped - orbitExtreme > deadZone)
                SetOrbitDirection(1, true);
        }
    }

    private void SetOrbitDirection(int direction, bool isReversal)
    {
        orbitDirection = direction;
        orbitExtreme = orbitUnwrapped;
        if (isReversal)
        {
            windowReversals++;
            totalReversals++;
        }
    }

    // ---------- Lines ----------

    private void WriteMove(Vector2 position)
    {
        Vector2 velocity = playerBody.linearVelocity;
        float speed = velocity.magnitude;
        object heading = speed > 0.05f
            ? Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg, 360f))
            : (object)null;

        moveSamples++;
        if (speed < config.stillSpeed)
            stillSamples++;

        float windowRisk = windowRiskSteps > 0 ? windowRiskSum / windowRiskSteps : 0f;
        windowRiskSum = 0f;
        windowRiskSteps = 0;
        riskHistory[riskHistoryNext] = windowRisk;
        riskHistoryNext = (riskHistoryNext + 1) % riskHistory.Length;
        riskHistoryCount = Mathf.Min(riskHistoryCount + 1, riskHistory.Length);

        logger.Write("move", position.x, position.y, speed, heading,
            DistanceOrDash(nearestHazard), DistanceOrDash(nearestShot), dangerCount,
            player != null ? player.CurrentHearts : (object)null);
    }

    private void WriteSaw()
    {
        if (sawSteps == 0)
            return;

        logger.Write("saw", Mathf.RoundToInt(spinSum / sawSteps), Mathf.RoundToInt(spinMax),
            Percent(poweredSteps, sawSteps), Percent(cappedSteps, sawSteps), Mathf.RoundToInt(orbitDegrees),
            windowReversals, extensionSum / sawSteps);

        sawSteps = 0;
        spinSum = 0f;
        spinMax = 0f;
        poweredSteps = 0;
        cappedSteps = 0;
        orbitDegrees = 0f;
        windowReversals = 0;
        extensionSum = 0f;
    }

    // ---------- Helpers ----------

    private static object Percent(int part, int whole) =>
        whole > 0 ? Mathf.RoundToInt(part * 100f / whole) : (object)null;

    private static object DistanceOrDash(float value) =>
        float.IsPositiveInfinity(value) ? null : (object)value;

    /// <summary>Tutorial = 0, Level_04 = 4; anything else = -1.</summary>
    private static int LevelIndexOf(string scene)
    {
        if (scene == "Tutorial")
            return 0;
        int underscore = scene.LastIndexOf('_');
        if (underscore >= 0 && int.TryParse(scene.Substring(underscore + 1), out int number))
            return number;
        return -1;
    }
}
