// Assets/Scripts/FlipOrbit/OrbManager.cs
using UnityEngine;

[DisallowMultipleComponent]
public class OrbManager : MonoBehaviour
{
    [Header("Refs")]
    public Transform orbVisual; // 子に GlowBallAttachment を必ず付ける
    public CircleRenderer track;

    [Header("Visual Size")]
    [Min(0.01f)] public float visualRadius = 0.16f; // ★サイズをここで調整（既定よりやや大きく）

    [Header("Lifetime")]
    public float lifetimeSec = 4f;

    [Header("Runtime (readonly)")]
    public bool isAlive = false;
    public float angleRad = 0f;
    public float timer = 0f;

    private bool timedOutConsumed = true;

    void Awake()
    {
        EnsureVisualAndGlowBall();
    }

    void OnValidate()
    {
        ApplyRadiusToGlowBall();
    }

    private void EnsureVisualAndGlowBall()
    {
        if (!orbVisual)
        {
            var go = new GameObject("OrbVisual");
            go.transform.SetParent(transform, false);
            orbVisual = go.transform;
        }
        var gb = orbVisual.gameObject.GetComponent<GlowBallAttachment>();
        if (!gb) gb = orbVisual.gameObject.AddComponent<GlowBallAttachment>();
        gb.Configure(
            c: new Color(1f, 0.85f, 0.35f, 1f), // ゴールド
            r: visualRadius,
            layer: "Default",
            order: 10
        );
    }

    private void ApplyRadiusToGlowBall()
    {
        if (!orbVisual) return;
        var gb = orbVisual.GetComponent<GlowBallAttachment>();
        if (gb) gb.Configure(gb.color, visualRadius, gb.sortingLayerName, gb.orderInLayer);
    }

    public void SetTrack(CircleRenderer cr) => track = cr;

    public void SpawnAtAngle(float angleRad)
    {
        this.angleRad = angleRad;
        timer = 0f;
        isAlive = true;
        timedOutConsumed = true;
        UpdateVisual();
    }

    public void Kill()
    {
        isAlive = false;
        timedOutConsumed = true;
    }

    public void ResetAll()
    {
        isAlive = false;
        timedOutConsumed = true;
        timer = 0f;
    }

    public void Tick(float dt)
    {
        if (!isAlive) return;
        timer += dt;
        if (timer >= lifetimeSec)
        {
            isAlive = false;
            timedOutConsumed = false; // 直後に1回だけ true を返す
        }
        UpdateVisual();
    }

    public bool ConsumeJustTimedOut()
    {
        if (!timedOutConsumed)
        {
            timedOutConsumed = true;
            return true;
        }
        return false;
    }

    public Vector3 CurrentWorldPos()
    {
        return track != null ? track.GetWorldPosFromAngleRad(angleRad) : Vector3.zero;
    }

    private void UpdateVisual()
    {
        if (!track || !orbVisual) return;
        orbVisual.position = CurrentWorldPos();
    }
}
