// Assets/Scripts/FlipOrbit/ObstacleSpawner.cs
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ObstacleSpawner : MonoBehaviour
{
    [Header("References")]
    public Transform container;  // Obstacles（0,0,0 / rot 0 / scale 1）
    public CircleRenderer track;
    public FlipOrbitConfig cfg;

    [Header("Arc Visual")]
    public Material arcMaterial;
    public string sortingLayerName = "Default";
    public int orderInLayer = 20;     // Trackより前面
    public float lineWidth = 0.1f;
    public int arcVerts = 96;
    public Color arcColor = new Color(1f, 0.25f, 0.2f, 1f);

    [Header("Runtime (readonly)")]
    [Range(0f, 1f)] public float difficultyT = 0f; // GameManagerから更新される
    public float elapsed = 0f;

    private readonly List<ObstacleArc> arcs = new();
    public IReadOnlyList<ObstacleArc> ActiveArcs => arcs;

    private float spawnTimer = 0f;
    private int currentStageIndex = 0;

    public void ResetAll()
    {
        for (int i = 0; i < arcs.Count; i++) if (arcs[i]) Destroy(arcs[i].gameObject);
        arcs.Clear();
        elapsed = 0f;
        spawnTimer = 0f;
        currentStageIndex = 0;
    }

    public void ClearAllNow() => ResetAll();

    public void ForceFadeAll(float sec)
    {
        for (int i = 0; i < arcs.Count; i++)
        {
            var a = arcs[i];
            if (a && a.IsAlive) a.ForceFade(Mathf.Max(0.01f, sec));
        }
    }

    public void Tick(float dt, float playerAngleRad)
    {
        if (!cfg || !track || cfg.stages == null || cfg.stages.Length == 0) return;

        elapsed += dt;
        UpdateStageIndex();

        var stage = cfg.stages[currentStageIndex];

        // 難易度で出現間隔短縮（従来）
        spawnTimer -= dt;
        if (spawnTimer <= 0f)
        {
            // === 同時出現上限に達していない時だけスポーン ===
            int alive = arcs.Count; // DeadはTickで消すので常に生存数
            int allowed = cfg.CalcMaxConcurrentArcs(difficultyT);

            if (alive < allowed)
            {
                float scale = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(difficultyT));
                float interval = Random.Range(stage.spawnIntervalMin, stage.spawnIntervalMax) * scale;
                spawnTimer = Mathf.Max(0.05f, interval);

                float center = DecideSpawnAngle(playerAngleRad, cfg.minSpawnSepFromPlayerDeg * Mathf.Deg2Rad);
                SpawnOne(center, stage);
            }
            else
            {
                // 上限のときは短い間隔で再トライ
                spawnTimer = 0.05f;
            }
        }

        // 更新＆破棄
        for (int i = arcs.Count - 1; i >= 0; i--)
        {
            var a = arcs[i];
            if (!a) { arcs.RemoveAt(i); continue; }
            a.Tick(dt);
            if (!a.IsAlive)
            {
                Destroy(a.gameObject);
                arcs.RemoveAt(i);
            }
        }
    }

    private void UpdateStageIndex()
    {
        int idx = currentStageIndex;
        for (int i = cfg.stages.Length - 1; i >= 0; i--)
        {
            if (elapsed >= cfg.stages[i].startSec) { idx = i; break; }
        }
        currentStageIndex = idx;
    }

    private void SpawnOne(float centerAngleRad, FlipOrbitConfig.Stage stage)
    {
        // 難易度に応じて Travel/Telegraph を短縮（前回実装分）
        float t = Mathf.Pow(Mathf.Clamp01(difficultyT), cfg.travelDifficultyGamma);
        float travelScale = Mathf.Lerp(1f, Mathf.Clamp(cfg.travelTimeMinScale, 0.1f, 1f), t);
        float telegraphScale = Mathf.Lerp(1f, Mathf.Clamp(cfg.telegraphTimeMinScale, 0.1f, 1f), t);

        float travelTime = Mathf.Max(0.03f, stage.travelTimeSec * travelScale);
        float telegraphTime = Mathf.Max(0.05f, stage.telegraphTimeSec * telegraphScale);

        var go = new GameObject($"Arc_{Time.frameCount}");
        if (container) go.transform.SetParent(container, false);

        var arc = go.AddComponent<ObstacleArc>();
        arc.Init(
            track: track,
            centerAngleRad: centerAngleRad,
            arcSizeDeg: stage.arcSizeDeg,
            telegraph: telegraphTime,
            travel: travelTime,
            stick: stage.stickTimeSec,
            fade: stage.fadeOutSec,
            startRadiusScale: stage.startRadiusScale,
            telegraphOffsetScale: stage.telegraphOffsetScale,
            verts: (arcVerts > 0 ? arcVerts : Mathf.Max(16, cfg.arcVerts)),
            lineWidth: (lineWidth > 0 ? lineWidth : Mathf.Max(0.01f, cfg.arcLineWidth)),
            sortingLayer: sortingLayerName,
            orderInLayer: (orderInLayer != 0 ? orderInLayer : cfg.arcOrderInLayer),
            mat: arcMaterial,
            color: arcColor
        );

        arcs.Add(arc);
    }

    private float DecideSpawnAngle(float playerAngleRad, float minSepRad)
    {
        for (int i = 0; i < 16; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            if (Mathf.Abs(DeltaAngleRad(a, playerAngleRad)) >= minSepRad) return a;
        }
        return Random.value * Mathf.PI * 2f;
    }

    private static float DeltaAngleRad(float a, float b)
    {
        return Mathf.DeltaAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg) * Mathf.Deg2Rad;
    }
}
