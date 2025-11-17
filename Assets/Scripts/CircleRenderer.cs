// Assets/Scripts/FlipOrbit/CircleRenderer.cs
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[DisallowMultipleComponent]
public class CircleRenderer : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float radius = 3f;
    [SerializeField, Min(0.001f)] private float lineWidth = 0.06f;
    [SerializeField, Min(16)] private int segments = 128;
    [SerializeField] private int orderInLayer = 0;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private Material lineMaterial;

    private LineRenderer lr;

    public float Radius => radius;
    public float LineWidth => lineWidth;

    void Awake()
    {
        lr = GetComponent<LineRenderer>() ?? gameObject.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.textureMode = LineTextureMode.Stretch;

        if (lineMaterial != null) lr.material = lineMaterial;
        if (lr.material == null)
        {
            var sh = Shader.Find("Sprites/Default");
            lr.material = new Material(sh);
        }

        lr.sortingLayerName = sortingLayerName;
        lr.sortingOrder = orderInLayer;
        lr.startWidth = lr.endWidth = lineWidth;

        Rebuild();
    }

    void OnValidate()
    {
        if (!lr) lr = GetComponent<LineRenderer>();
        if (lr) lr.startWidth = lr.endWidth = lineWidth;
        Rebuild();
    }

    public void SetSorting(string layer, int order)
    {
        if (!lr) lr = GetComponent<LineRenderer>();
        lr.sortingLayerName = layer;
        lr.sortingOrder = order;
    }

    public void SetStyle(Material mat, float width)
    {
        if (!lr) lr = GetComponent<LineRenderer>();
        if (mat) lr.material = mat;
        else if (lr.material == null)
            lr.material = new Material(Shader.Find("Sprites/Default"));
        lineWidth = width;
        lr.startWidth = lr.endWidth = lineWidth;
    }

    public void SetRadius(float r)
    {
        radius = Mathf.Max(0.01f, r);
        Rebuild();
    }

    public Vector3 GetWorldPosFromAngleRad(float angleRad)
    {
        float x = Mathf.Cos(angleRad) * radius;
        float y = Mathf.Sin(angleRad) * radius;
        return transform.position + new Vector3(x, y, 0f);
    }

    public void Rebuild()
    {
        if (!lr) return;
        int count = Mathf.Max(3, segments);
        lr.positionCount = count;
        float step = Mathf.PI * 2f / count;
        for (int i = 0; i < count; i++)
        {
            float a = step * i;
            lr.SetPosition(i, GetWorldPosFromAngleRad(a));
        }
    }
}
