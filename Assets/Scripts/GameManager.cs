// Assets/Scripts/FlipOrbit/GameManager.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public enum State { Title, Playing, Paused, GameOver }

    [Header("Refs")]
    public FlipOrbitConfig config;
    public CircleRenderer track;
    public PlayerController player;
    public ObstacleSpawner spawner;
    public OrbManager orb;
    public Transform obstaclesContainer;

    [Header("UI")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI multText;
    public GameObject titlePanel;
    public GameObject gameOverPanel;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI bestScoreText;

    [Header("Optional")]
    public AudioManager audioMgr;
    public CameraShake cameraShake;

    [Header("Runtime")]
    public State state = State.Title;
    public int score = 0;
    public float multiplier = 1f;
    public int bestScore = 0;

    // 入力取りこぼし対策（バッファ）
    [Header("Input Buffer")]
    [Range(0.04f, 0.25f)] public float flipBufferWindow = 0.10f;
    private float flipBufferTimer = 0f;
    private int pendingFlipCount = 0;

    // スイープ（連続）当たり判定
    [Header("Swept Hit Test")]
    [Range(1, 8)] public int collisionSubsteps = 3;
    [Range(0f, 5f)] public float highSpeedExtraPadDeg = 1.0f;

    [Header("Debug")]
    public bool showHitDebug = false;
    private string debugLine = "";

    private float grazeAccum = 0f;
    private const float GRAZE_POINT_SEC = 0.1f;
    private float chainNoPickupTimer = 0f;

    private float padRad;
    private float grazeThresholdDeg;
    private float orbPickupPadRad;

    void Start()
    {
        if (!track) track = FindObjectOfType<CircleRenderer>();
        if (!player) player = FindObjectOfType<PlayerController>();
        if (!spawner) spawner = FindObjectOfType<ObstacleSpawner>();
        if (!orb) orb = FindObjectOfType<OrbManager>();

        if (!config) Debug.LogError("FlipOrbitConfig is not assigned.");

        if (player && track) player.SetTrack(track);
        if (orb)
        {
            orb.SetTrack(track);
            if (config) orb.lifetimeSec = config.orbLifetimeSec;
        }

        padRad = Mathf.Deg2Rad * (config ? config.hitPadDeg : 3f);
        grazeThresholdDeg = config ? config.grazeThresholdDeg : 6f;
        orbPickupPadRad = Mathf.Deg2Rad * (config ? config.orbPickupPadDeg : 4f);

        ApplyUI();
        SwitchState(State.Title, true);
    }

    void Update()
    {
        HandleGlobalInputs();

        switch (state)
        {
            case State.Title:
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                    StartGame();
                break;

            case State.Playing:
                TickPlaying(Time.deltaTime);
                break;

            case State.Paused:
                break;

            case State.GameOver:
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.R))
                {
                    if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.R))
                        Restart(); // タイトルへ
                    else
                        StartGame(); // 即リトライ
                }
                break;
        }
    }

    private void HandleGlobalInputs()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            QueueFlip();

        if (state == State.Playing)
        {
            if (Input.GetKeyDown(KeyCode.P)) { PauseToggle(); return; }
        }
        else if (state == State.Paused)
        {
            if (Input.GetKeyDown(KeyCode.P)) PauseToggle();
        }
    }

    private void QueueFlip()
    {
        pendingFlipCount = Mathf.Min(pendingFlipCount + 1, 2);
        flipBufferTimer = flipBufferWindow;
    }

    private void ConsumeFlipBuffered()
    {
        if (pendingFlipCount > 0)
        {
            player.Flip();
            audioMgr?.PlayFlip();
            pendingFlipCount--;
        }
    }

    private void StartGame()
    {
        score = 0;
        multiplier = 1f;
        chainNoPickupTimer = 0f;
        grazeAccum = 0f;
        debugLine = "";

        pendingFlipCount = 0;
        flipBufferTimer = 0f;

        player?.ResetPlayer(0f);

        if (spawner)
        {
            spawner.ResetAll();
            spawner.container = obstaclesContainer;
            spawner.track = track;
            spawner.cfg = config;
        }

        orb?.ResetAll();

        ApplySpeedFromScore();
        ApplyUI();

        SwitchState(State.Playing, true);
        TrySpawnOrb();
    }

    private void TickPlaying(float dt)
    {
        if (flipBufferTimer > 0f)
        {
            flipBufferTimer -= dt;
            if (flipBufferTimer < 0f) { flipBufferTimer = 0f; pendingFlipCount = 0; }
        }

        ConsumeFlipBuffered();

        float prevAngle = player.AngleRad;
        player?.Tick(dt);
        float currAngle = player.AngleRad;

        if (spawner)
        {
            spawner.difficultyT = config ? config.CalcDifficultyT(score) : 0f;
            spawner.Tick(dt, currAngle);
        }

        orb?.Tick(dt);

        // ★タイムアウト時の一括フェードはフラグで制御
        if (orb != null && orb.ConsumeJustTimedOut())
        {
            if (config == null || config.fadeArcsWhenOrbTimeout)
                spawner?.ForceFadeAll(config ? config.forceFadeAllSec : 0.3f);

            TrySpawnOrb();
        }

        // フェーズ対応のスイープ判定
        if (CheckHitSwept(prevAngle, currAngle, dt))
        {
            OnMiss();
            return;
        }

        DoGraze(dt);
        TryPickupOrb();

        if (orb != null && !orb.isAlive)
            TrySpawnOrb();

        ApplySpeedFromScore();
        ApplyUI();
    }

    private bool CheckHitSwept(float prevAngle, float currAngle, float dt)
    {
        if (!spawner || !player || !track) return false;

        float delta = DeltaAngleRad(currAngle, prevAngle);
        int steps = Mathf.Max(1, collisionSubsteps);
        float deg = Mathf.Abs(delta) * Mathf.Rad2Deg;
        if (deg > 90f) steps = Mathf.Clamp(steps + 2, 1, 8);
        else if (deg > 45f) steps = Mathf.Clamp(steps + 1, 1, 8);

        bool exact = config ? config.useExactArcHit : true;

        float speedDegPerSec = (dt > 1e-6f) ? (Mathf.Abs(delta) * Mathf.Rad2Deg / dt) : 0f;
        float spd01 = Mathf.Clamp01((speedDegPerSec - 180f) / 180f);
        float extraPadDeg = (!exact) ? (highSpeedExtraPadDeg * spd01) : 0f;
        float padDeg = (config ? config.hitPadDeg : 3f) + extraPadDeg;
        float padRadLocal = Mathf.Deg2Rad * padDeg;

        var arcs = spawner.ActiveArcs;
        debugLine = "";

        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            float a = WrapRad(prevAngle + delta * t);

            for (int j = 0; j < arcs.Count; j++)
            {
                var arc = arcs[j];
                if (arc == null) continue;

                bool hit = exact
                    ? arc.HitsExactPhaseAware(a, config.arcHitMode)
                    : arc.HitsPhaseAware(a, padRadLocal, config.arcHitMode);

                if (showHitDebug && i == steps)
                {
                    float dDeg = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, arc.centerAngleRad * Mathf.Rad2Deg));
                    float halfDeg = arc.halfWidthRad * Mathf.Rad2Deg;
                    debugLine = $"d={dDeg:0.0}°, half={halfDeg:0.0}° (+pad {padDeg:0.0}°) steps={steps} phase={arc.phase}";
                }

                if (hit) return true;
            }
        }
        return false;
    }

    private void OnGUI()
    {
        if (!showHitDebug || state != State.Playing) return;
        var style = new GUIStyle(GUI.skin.box) { fontSize = 14 };
        GUI.Box(new Rect(12, 12, 560, 32), debugLine, style);
    }

    private void OnMiss()
    {
        SwitchState(State.GameOver, true);
        audioMgr?.PlayMiss();
        cameraShake?.Shake(0.25f, 0.15f);

        if (score > bestScore) bestScore = score;
        if (finalScoreText) finalScoreText.text = $"SCORE  {score}";
        if (bestScoreText) bestScoreText.text = $"BEST   {bestScore}";
    }

    private void DoGraze(float dt)
    {
        if (!spawner || !player) return;

        float minEdge = float.MaxValue;
        var arcs = spawner.ActiveArcs;
        for (int i = 0; i < arcs.Count; i++)
        {
            var a = arcs[i];
            if (a && a.IsStick)
            {
                float d = a.EdgeDistanceDeg(player.AngleRad);
                if (d < minEdge) minEdge = d;
            }
        }

        float th = config ? config.grazeThresholdDeg : 6f;
        if (minEdge < th && minEdge > 0f)
        {
            grazeAccum += dt;
            while (grazeAccum >= GRAZE_POINT_SEC)
            {
                grazeAccum -= GRAZE_POINT_SEC;
                score += 1;
            }
        }
        else
        {
            grazeAccum = Mathf.Max(0f, grazeAccum - dt * 0.5f);
        }
    }

    private void TryPickupOrb()
    {
        if (!orb || !player || !orb.isAlive) return;

        float diff = Mathf.Abs(DeltaAngleRad(player.AngleRad, orb.angleRad));
        if (diff <= orbPickupPadRad)
        {
            int add = (config ? config.orbScore : 10);
            score += Mathf.RoundToInt(add * multiplier);

            if (config) multiplier = Mathf.Min(config.chainMax, multiplier + config.chainStep);
            else multiplier = Mathf.Min(3f, 0.2f + multiplier);

            float pct = config ? Mathf.Clamp01((multiplier - 1f) / config.chainMax) : Mathf.Clamp01((multiplier - 1f) / 3f);
            audioMgr?.PlayPickup(1f + pct * 0.4f);

            // ★オーブ取得時の一括フェードはフラグで制御（今回false推奨）
            if (config == null || config.fadeArcsWhenOrbPicked)
                spawner?.ForceFadeAll(config ? config.forceFadeAllSec : 0.3f);

            orb.Kill();
            TrySpawnOrb();

            chainNoPickupTimer = 0f;
        }
        else
        {
            chainNoPickupTimer += Time.deltaTime;
            if (config && chainNoPickupTimer >= config.chainResetIfNoPickupSec)
            {
                multiplier = 1f;
                chainNoPickupTimer = 0f;
            }
        }
    }

    private void TrySpawnOrb()
    {
        if (!orb || orb.isAlive) return;

        float candidate = FindWidestGapMidAngle();
        float jitter = (config ? config.orbJitterDeg : 6f) * Mathf.Deg2Rad;
        candidate += Random.Range(-jitter, jitter);
        candidate = WrapRad(candidate);

        orb.SpawnAtAngle(candidate);
    }

    private float FindWidestGapMidAngle()
    {
        if (!spawner || !player) return 0f;

        var arcs = spawner.ActiveArcs;
        var blocks = new List<(float from, float to)>();
        float inflate = Mathf.Deg2Rad * 5f;

        for (int i = 0; i < arcs.Count; i++)
        {
            var a = arcs[i];
            if (!a) continue;

            float from = a.centerAngleRad - a.halfWidthRad - inflate;
            float to = a.centerAngleRad + a.halfWidthRad + inflate;

            NormalizeInterval(from, to, out float f1, out float t1, out bool wrapped);
            if (!wrapped) { blocks.Add((f1, t1)); }
            else { blocks.Add((0f, t1)); blocks.Add((f1, Mathf.PI * 2f)); }
        }

        if (blocks.Count == 0) return WrapRad(player.AngleRad + Mathf.PI);

        blocks.Sort((x, y) => x.from.CompareTo(y.from));
        var merged = new List<(float from, float to)>();
        var cur = blocks[0];

        for (int i = 1; i < blocks.Count; i++)
        {
            var b = blocks[i];
            if (b.from <= cur.to) cur.to = Mathf.Max(cur.to, b.to);
            else { merged.Add(cur); cur = b; }
        }
        merged.Add(cur);

        float maxGap = -1f;
        float bestMid = 0f;

        if (merged[0].from > 0f)
        {
            float gap = merged[0].from - 0f;
            float mid = gap * 0.5f;
            if (gap > maxGap) { maxGap = gap; bestMid = mid; }
        }
        for (int i = 0; i < merged.Count - 1; i++)
        {
            float gap = merged[i + 1].from - merged[i].to;
            if (gap > maxGap) { maxGap = gap; bestMid = merged[i].to + gap * 0.5f; }
        }
        float lastGap = (Mathf.PI * 2f) - merged[^1].to;
        if (lastGap > maxGap) { maxGap = lastGap; bestMid = merged[^1].to + lastGap * 0.5f; }

        float sep = Mathf.Abs(DeltaAngleRad(bestMid, player.AngleRad));
        float minSep = (config ? config.minSpawnSepFromPlayerDeg : 35f) * Mathf.Deg2Rad;
        if (sep < minSep) bestMid = WrapRad(player.AngleRad + Mathf.PI);

        return WrapRad(bestMid);
    }

    private void ApplySpeedFromScore()
    {
        if (!config || !player) return;
        float w = config.CalcAngularSpeedDegPerSec(score);
        player.SetAngularSpeed(w);
    }

    private void ApplyUI()
    {
        if (scoreText) scoreText.text = score.ToString();
        if (multText) multText.text = $"x{multiplier:0.0}";
        if (titlePanel) titlePanel.SetActive(state == State.Title);
        if (gameOverPanel) gameOverPanel.SetActive(state == State.GameOver);
    }

    private void SwitchState(State s, bool immediate = false)
    {
        state = s;
        Time.timeScale = (state == State.Paused) ? 0f : 1f;
        ApplyUI();
    }

    private void PauseToggle()
    {
        if (state == State.Playing) SwitchState(State.Paused, true);
        else if (state == State.Paused) SwitchState(State.Playing, true);
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        Scene cur = SceneManager.GetActiveScene();
        SceneManager.LoadScene(cur.buildIndex);
    }

    private static float WrapRad(float a)
    {
        float twoPi = Mathf.PI * 2f;
        a = a % twoPi;
        if (a < 0f) a += twoPi;
        return a;
    }

    private static float DeltaAngleRad(float a, float b)
    {
        return Mathf.DeltaAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg) * Mathf.Deg2Rad;
    }

    private static void NormalizeInterval(float from, float to, out float f, out float t, out bool wrapped)
    {
        float twoPi = Mathf.PI * 2f;
        f = from % twoPi; if (f < 0f) f += twoPi;
        t = to % twoPi; if (t < 0f) t += twoPi;
        wrapped = t < f;
    }
}
