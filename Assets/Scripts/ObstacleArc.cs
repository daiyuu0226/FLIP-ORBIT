// Assets/Scripts/FlipOrbit/ObstacleArc.cs
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[DisallowMultipleComponent]
public class ObstacleArc : MonoBehaviour
{
    public enum Phase { Telegraph, Travelling, Stick, Fading, Dead }

    [Header("Refs")]
    public CircleRenderer track;

    [Header("Setup (readonly)")]
    public float centerAngleRad;
    public float halfWidthRad;
    public float telegraphTime;
    public float travelTime;
    public float stickTime;
    public float fadeTime;
    public float startRadiusScale = 2.0f;
    public float telegraphOffsetScale = 1.35f;
    public Color baseColor = new Color(1f, 0.25f, 0.2f, 1f);

    [Header("Runtime")]
    public Phase phase = Phase.Telegraph;
    public float timer = 0f;

    private LineRenderer lr;
    private int verts = 96;
    private float lineWidth = 0.1f;
    private string sortingLayer = "Default";
    private int orderInLayer = 20;

    // 強制フェード（その場の半径で消える）
    private bool forcedFade = false;
    private float fadeStartRadius = 0f;

    public bool IsAlive => phase != Phase.Dead;
    public bool IsStick => phase == Phase.Stick;

    public void Init(
        CircleRenderer track,
        float centerAngleRad,
        float arcSizeDeg,
        float telegraph,
        float travel,
        float stick,
        float fade,
        float startRadiusScale,
        float telegraphOffsetScale,
        int verts,
        float lineWidth,
        string sortingLayer,
        int orderInLayer,
        Material mat,
        Color color
    )
    {
        this.track = track;
        this.centerAngleRad = centerAngleRad;
        this.halfWidthRad = Mathf.Deg2Rad * Mathf.Abs(arcSizeDeg) * 0.5f;
        this.telegraphTime = Mathf.Max(0.05f, telegraph);
        this.travelTime = Mathf.Max(0.01f, travel);
        this.stickTime = Mathf.Max(0.0f, stick);
        this.fadeTime = Mathf.Max(0.01f, fade);
        this.startRadiusScale = Mathf.Max(1.25f, startRadiusScale);
        this.telegraphOffsetScale = Mathf.Max(1.10f, telegraphOffsetScale);
        this.verts = Mathf.Max(16, verts);
        this.lineWidth = Mathf.Max(0.01f, lineWidth);
        this.sortingLayer = sortingLayer;
        this.orderInLayer = orderInLayer;
        this.baseColor = color;

        lr = GetComponent<LineRenderer>() ?? gameObject.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.positionCount = this.verts;
        lr.sortingLayerName = sortingLayer;
        lr.sortingOrder = orderInLayer;
        lr.startWidth = lr.endWidth = this.lineWidth;

        if (mat != null) lr.material = mat;
        if (lr.material == null) lr.material = new Material(Shader.Find("Sprites/Default"));

        phase = Phase.Telegraph;
        timer = 0f;
        forcedFade = false;
        fadeStartRadius = 0f;

        Rebuild(1f);
    }

    public void Tick(float dt)
    {
        if (phase == Phase.Dead) return;
        timer += dt;

        switch (phase)
        {
            case Phase.Telegraph:
                if (timer >= telegraphTime) { phase = Phase.Travelling; timer = 0f; forcedFade = false; }
                break;
            case Phase.Travelling:
                if (timer >= travelTime) { phase = Phase.Stick; timer = 0f; forcedFade = false; }
                break;
            case Phase.Stick:
                if (timer >= stickTime) { phase = Phase.Fading; timer = 0f; forcedFade = false; }
                break;
            case Phase.Fading:
                if (timer >= fadeTime) { phase = Phase.Dead; lr.enabled = false; }
                break;
        }

        Rebuild(Alpha());
    }

    // 既存：Stick中のみの厳密判定
    public bool HitsExact(float playerAngleRad)
    {
        if (phase != Phase.Stick) return false;
        float d = DeltaAngleRad(playerAngleRad, centerAngleRad);
        return Mathf.Abs(d) <= halfWidthRad;
    }

    // 既存：Stick中のみのパッド付き判定
    public bool Hits(float playerAngleRad, float padRad)
    {
        if (phase != Phase.Stick) return false;
        float d = DeltaAngleRad(playerAngleRad, centerAngleRad);
        return Mathf.Abs(d) <= (halfWidthRad + padRad);
    }

    // ★追加：フェーズ指定で当たり判定（厳密）
    public bool HitsExactPhaseAware(float playerAngleRad, FlipOrbitConfig.ArcHitMode mode)
    {
        if (!IsDangerous(mode)) return false;
        float d = DeltaAngleRad(playerAngleRad, centerAngleRad);
        return Mathf.Abs(d) <= halfWidthRad;
    }

    // ★追加：フェーズ指定で当たり判定（パッド付き）
    public bool HitsPhaseAware(float playerAngleRad, float padRad, FlipOrbitConfig.ArcHitMode mode)
    {
        if (!IsDangerous(mode)) return false;
        float d = DeltaAngleRad(playerAngleRad, centerAngleRad);
        return Mathf.Abs(d) <= (halfWidthRad + padRad);
    }

    // ★追加：このArcが現在“危険”かどうか（モードに応じて判定）
    public bool IsDangerous(FlipOrbitConfig.ArcHitMode mode)
    {
        switch (mode)
        {
            case FlipOrbitConfig.ArcHitMode.StickOnly:
                return phase == Phase.Stick;
            case FlipOrbitConfig.ArcHitMode.TravelAndStick:
                return phase == Phase.Travelling || phase == Phase.Stick;
            case FlipOrbitConfig.ArcHitMode.TelegraphTravelStick:
                return phase == Phase.Telegraph || phase == Phase.Travelling || phase == Phase.Stick;
            case FlipOrbitConfig.ArcHitMode.AllPhasesIncludingFade:
                return phase == Phase.Telegraph || phase == Phase.Travelling || phase == Phase.Stick || phase == Phase.Fading;
            default:
                return phase == Phase.Stick;
        }
    }

    public float EdgeDistanceDeg(float playerAngleRad)
    {
        float d = Mathf.Abs(DeltaAngleRad(playerAngleRad, centerAngleRad)) - halfWidthRad;
        return Mathf.Max(0f, d) * Mathf.Rad2Deg;
    }

    // 強制フェード：いまの半径のまま透明化
    public void ForceFade(float sec)
    {
        float rNow = CurrentRadius();
        forcedFade = true;
        fadeStartRadius = rNow;

        phase = Phase.Fading;
        fadeTime = Mathf.Max(0.01f, sec);
        timer = 0f;
    }

    private float Alpha()
    {
        return phase switch
        {
            Phase.Telegraph => Mathf.Abs(Mathf.Sin(timer * Mathf.PI * 5f)) * 0.55f + 0.25f,
            Phase.Travelling => 0.85f,
            Phase.Stick => 1.00f,
            Phase.Fading => 1f - Mathf.Clamp01(timer / fadeTime),
            _ => 0f
        };
    }

    private float CurrentRadius()
    {
        float baseR = track != null ? track.Radius : 3f;

        if (phase == Phase.Telegraph)
            return baseR * telegraphOffsetScale;

        if (phase == Phase.Travelling)
        {
            float t = Mathf.Clamp01(timer / travelTime);
            float scale = Mathf.Lerp(startRadiusScale, 1f, t * t * (3f - 2f * t)); // SmoothStep
            return baseR * scale;
        }

        if (phase == Phase.Fading)
        {
            if (forcedFade) return fadeStartRadius;
            return baseR;
        }

        return baseR; // Stickなど
    }

    private Color PhaseColor(float alpha)
    {
        Color c = baseColor;
        if (phase == Phase.Telegraph) c = Color.Lerp(baseColor, new Color(1f, 0.5f, 0.4f), 0.25f);
        if (phase == Phase.Stick) c = Color.Lerp(baseColor, Color.white, 0.30f);
        c.a = alpha;
        return c;
    }

    private void Rebuild(float alpha)
    {
        if (!lr) return;
        float r = CurrentRadius();
        float from = centerAngleRad - halfWidthRad;
        float to = centerAngleRad + halfWidthRad;

        lr.startColor = lr.endColor = PhaseColor(alpha);

        int count = lr.positionCount;
        for (int i = 0; i < count; i++)
        {
            float t = (count <= 1) ? 0f : (float)i / (count - 1);
            float a = Mathf.Lerp(from, to, t);
            Vector3 pos = (track != null)
                ? track.transform.position + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f)
                : new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            lr.SetPosition(i, pos);
        }
    }

    private static float DeltaAngleRad(float a, float b)
    {
        return Mathf.DeltaAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg) * Mathf.Deg2Rad;
    }
}
