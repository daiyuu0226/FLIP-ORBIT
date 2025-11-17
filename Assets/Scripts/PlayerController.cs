// Assets/Scripts/FlipOrbit/PlayerController.cs
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private Transform visual; // 子に GlowBallAttachment を必ず付ける
    [SerializeField] private CircleRenderer track;

    [Header("Visual Size")]
    [Min(0.01f)] public float visualRadius = 0.18f; // ★サイズをここで調整（既定よりやや大きく）

    [Header("Runtime")]
    [SerializeField] private float angleRad = 0f;
    [SerializeField] private int dir = 1; // +1 / -1
    [SerializeField] private float angularSpeedDegPerSec = 90f;

    public float AngleRad => angleRad;

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
        if (!visual)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(transform, false);
            visual = go.transform;
        }
        var gb = visual.gameObject.GetComponent<GlowBallAttachment>();
        if (!gb) gb = visual.gameObject.AddComponent<GlowBallAttachment>();
        gb.Configure(
            c: new Color(0.07f, 0.95f, 1f, 1f), // シアン
            r: visualRadius,
            layer: "Default",
            order: 12
        );
    }

    private void ApplyRadiusToGlowBall()
    {
        if (!visual) return;
        var gb = visual.GetComponent<GlowBallAttachment>();
        if (gb) gb.Configure(gb.color, visualRadius, gb.sortingLayerName, gb.orderInLayer);
    }

    public void SetTrack(CircleRenderer cr) => track = cr;
    public void SetAngularSpeed(float degPerSec) => angularSpeedDegPerSec = Mathf.Max(0f, degPerSec);

    public void ResetPlayer(float startAngleRad = 0f)
    {
        angleRad = startAngleRad;
        dir = 1;
        UpdateVisualPosition();
    }

    public void Flip() => dir *= -1;

    public void Tick(float dt)
    {
        if (!track) return;
        float deltaDeg = dir * angularSpeedDegPerSec * dt;
        angleRad = WrapRad(angleRad + deltaDeg * Mathf.Deg2Rad);
        UpdateVisualPosition();
    }

    private void UpdateVisualPosition()
    {
        if (!track || !visual) return;
        visual.position = track.GetWorldPosFromAngleRad(angleRad);
    }

    public float GetWorldAngleRad()
    {
        Vector3 c = track ? track.transform.position : Vector3.zero;
        Vector3 p = visual ? visual.position : transform.position;
        return Mathf.Atan2(p.y - c.y, p.x - c.x);
    }

    private static float WrapRad(float a)
    {
        float twoPi = Mathf.PI * 2f;
        a = a % twoPi;
        if (a < 0f) a += twoPi;
        return a;
    }
}
