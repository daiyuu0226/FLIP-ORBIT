// Assets/Scripts/FlipOrbit/GlowBallAttachment.cs
using UnityEngine;

/// <summary>
/// 発光球を "絶対に" 表示するための堅牢版。
/// 1) SpriteRenderer で不透明ディスク（ソフト縁）
/// 2) フォールバックの LineRenderer リング（Spriteが見えなくてもこれが見える）
/// どちらも自動生成。URP 2D でも Built-in でも確実に可視。
/// </summary>
[DisallowMultipleComponent]
public class GlowBallAttachment : MonoBehaviour
{
    [Header("Look")]
    public Color color = new Color(0.07f, 0.95f, 1f, 1f); // シアン
    [Min(0.01f)] public float radius = 0.12f;            // ワールド半径
    public string sortingLayerName = "Default";
    public int orderInLayer = 10;                         // Orb=10 / Player=12 を推奨

    [Header("Outline Ring (failsafe)")]
    [Min(0.001f)] public float ringWidth = 0.035f;
    public float ringAlpha = 0.9f;
    public int ringSegments = 48;

    private SpriteRenderer sr;
    private LineRenderer ring;
    private static Sprite cachedDisc; // 使い回し

    void Awake()
    {
        // --- SpriteRenderer セットアップ ---
        sr = GetComponent<SpriteRenderer>();
        if (!sr) sr = gameObject.AddComponent<SpriteRenderer>();

        // マテリアル：まずは Sprites/Default（最も相性が良く確実）
        var mat = new Material(Shader.Find("Sprites/Default"));
        sr.material = mat;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = orderInLayer;

        if (cachedDisc == null) cachedDisc = CreateSolidDiscSprite(128); // 不透明ディスク
        sr.sprite = cachedDisc;
        sr.color = color;     // 不透明(Alpha=1)で確実に見える

        // 半径をワールド単位で反映（スプライトサイズ1x1を想定）
        ApplyScale();

        // --- フォールバックのリング（表示されない事故に備え） ---
        var go = new GameObject("GlowRing_Fallback");
        go.transform.SetParent(transform, false);
        ring = go.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = Mathf.Max(12, ringSegments);
        ring.startWidth = ring.endWidth = ringWidth;
        ring.sortingLayerName = sortingLayerName;
        ring.sortingOrder = orderInLayer + 1; // 本体の上
        ring.material = new Material(Shader.Find("Sprites/Default"));
        SetRingColor(color);
        RebuildRing(); // 初回
    }

    void LateUpdate()
    {
        // 親の位置が動くので、毎フレームリング頂点を更新
        RebuildRing();
    }

    void OnValidate()
    {
        if (sr)
        {
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = orderInLayer;
            sr.color = color;
            ApplyScale();
        }
        if (ring)
        {
            ring.sortingLayerName = sortingLayerName;
            ring.sortingOrder = orderInLayer + 1;
            SetRingColor(color);
            RebuildRing();
        }
    }

    public void Configure(Color c, float r, string layer, int order)
    {
        color = c;
        radius = Mathf.Max(0.01f, r);
        sortingLayerName = layer;
        orderInLayer = order;
        OnValidate();
    }

    private void ApplyScale()
    {
        float d = radius * 2f;
        transform.localScale = new Vector3(d, d, 1f);
    }

    private void SetRingColor(Color c)
    {
        var rc = c; rc.a = ringAlpha;
        ring.startColor = ring.endColor = rc;
    }

    private void RebuildRing()
    {
        float step = Mathf.PI * 2f / ring.positionCount;
        Vector3 center = transform.position;
        for (int i = 0; i < ring.positionCount; i++)
        {
            float a = step * i;
            Vector3 p = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            ring.SetPosition(i, p);
        }
    }

    /// <summary>
    /// 内部は完全不透明、縁だけ少しだけソフトに落ちる円スプライト。
    /// 透明に見えないよう、中心～9割は Alpha=1。
    /// </summary>
    private static Sprite CreateSolidDiscSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float r = cx;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - cx) / r;
                float dy = (y - cy) / r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy); // 0..√2
                float a = 1f;
                // 外縁10%だけ滑らかに落とす（見栄え用）
                if (dist > 0.9f) a = Mathf.Clamp01(1f - Mathf.InverseLerp(0.9f, 1.0f, dist));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
