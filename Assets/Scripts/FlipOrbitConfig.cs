// Assets/Scripts/FlipOrbit/FlipOrbitConfig.cs
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "FlipOrbit/Config", fileName = "FlipOrbitConfig")]
public class FlipOrbitConfig : ScriptableObject
{
    // アーク当たり判定のモード
    public enum ArcHitMode
    {
        StickOnly,
        TravelAndStick,
        TelegraphTravelStick,
        AllPhasesIncludingFade
    }

    [System.Serializable]
    public struct Stage
    {
        [Header("Stage Start (sec)")]
        public float startSec;

        [Header("Spawn Interval (sec)")]
        public float spawnIntervalMin;
        public float spawnIntervalMax;

        [Header("Arc Shape / Timing")]
        public float arcSizeDeg;
        public float telegraphTimeSec;
        public float travelTimeSec;
        public float stickTimeSec;
        public float fadeOutSec;

        [Header("Spawn From")]
        public float startRadiusScale;
        public float telegraphOffsetScale;

        public static Stage Default => new Stage
        {
            startSec = 0f,
            spawnIntervalMin = 0.75f,
            spawnIntervalMax = 1.20f,
            arcSizeDeg = 32f,
            telegraphTimeSec = 0.25f,
            travelTimeSec = 0.38f,
            stickTimeSec = 0.30f,
            fadeOutSec = 0.45f,
            startRadiusScale = 2.0f,
            telegraphOffsetScale = 1.35f
        };
    }

    [Header("Stages (at least 1)")]
    public Stage[] stages = new Stage[] { Stage.Default };

    // ========= Player Speed / Difficulty =========
    [Header("Player Speed Scaling")]
    public float scoreForMaxSpeed = 220f;
    [Tooltip("プレイヤー速度カーブ（>1で序盤の伸びを抑える）")]
    public float playerSpeedGamma = 1.25f;
    public float playerMinSpeedDegPerSec = 110f;
    public float playerMaxSpeedDegPerSec = 300f;

    [Header("Global Difficulty (for spawner)")]
    [FormerlySerializedAs("speedGamma")]
    [Tooltip("出現間隔などの“難易度t”のカーブ")]
    public float difficultyGamma = 0.5f;

    // ========= Collision / Graze =========
    [Header("Collision / Graze")]
    [Tooltip("true: 弧幅ぴったりで判定 / false: 下のhitPadDegを加算")]
    public bool useExactArcHit = true;
    public float hitPadDeg = 3.0f;
    public float grazeThresholdDeg = 6f;

    // ★追加：当たり有効フェーズ
    [Header("Arc Hit Mode")]
    public ArcHitMode arcHitMode = ArcHitMode.StickOnly;

    // ========= Orb / Chain =========
    [Header("Orb / Chain")]
    public int orbScore = 10;
    public float chainStep = 0.2f;
    public float chainMax = 3.0f;
    public float chainResetIfNoPickupSec = 5f;
    public float orbLifetimeSec = 4.0f;
    public float orbJitterDeg = 6f;
    public float orbPickupPadDeg = 4f;

    // ========= Spawner Visual =========
    [Header("Spawner / Arc Visual")]
    public int arcVerts = 96;
    public float arcLineWidth = 0.1f;
    public int arcOrderInLayer = 20;

    [Header("Gaps / Separation")]
    public float minSpawnSepFromPlayerDeg = 35f;

    // ========= Mass Fade（一括フェード） =========
    [Header("Mass Fade")]
    public float forceFadeAllSec = 0.3f;
    [Tooltip("★オーブ取得時に赤アークを一括フェードさせる")]
    public bool fadeArcsWhenOrbPicked = false;           // ★デフォルトOFF（=今回の要望）
    [Tooltip("オーブの寿命切れ時に赤アークを一括フェードさせる")]
    public bool fadeArcsWhenOrbTimeout = true;

    // ========= Difficulty → Arc Travel Speed =========
    [Header("Difficulty → Arc Travel Speed")]
    [Range(0.1f, 1f)] public float travelTimeMinScale = 0.35f;
    [Range(0.1f, 1f)] public float telegraphTimeMinScale = 0.60f;
    [Range(0.2f, 2.0f)] public float travelDifficultyGamma = 0.8f;

    // ========= 同時出現アーク上限 =========
    [Header("Max Concurrent Arcs")]
    public int baseMaxConcurrentArcs = 3;
    public int maxMaxConcurrentArcs = 9;
    [Range(0.2f, 2.0f)] public float arcsMaxGamma = 0.8f;

    // ========= Methods =========
    public float CalcDifficultyT(int score)
    {
        float x = Mathf.Clamp01(score / Mathf.Max(1f, scoreForMaxSpeed));
        return Mathf.Pow(x, difficultyGamma);
    }

    public float CalcAngularSpeedDegPerSec(int score)
    {
        float x = Mathf.Clamp01(score / Mathf.Max(1f, scoreForMaxSpeed));
        float tPlayer = Mathf.Pow(x, playerSpeedGamma);
        return Mathf.Lerp(playerMinSpeedDegPerSec, playerMaxSpeedDegPerSec, tPlayer);
    }

    public int CalcMaxConcurrentArcs(float difficultyT)
    {
        float t = Mathf.Pow(Mathf.Clamp01(difficultyT), arcsMaxGamma);
        float f = Mathf.Lerp(baseMaxConcurrentArcs, maxMaxConcurrentArcs, t);
        return Mathf.Clamp(Mathf.RoundToInt(f), 1, Mathf.Max(baseMaxConcurrentArcs, maxMaxConcurrentArcs));
    }
}
