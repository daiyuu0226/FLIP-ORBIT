// Assets/Scripts/FlipOrbit/PulsateText.cs
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class PulsateText : MonoBehaviour
{
    public float speed = 2.0f;          // “_–Å‘¬“x
    [Range(0f, 1f)] public float minA = 0.25f;
    [Range(0f, 1f)] public float maxA = 1.0f;

    private TextMeshProUGUI tmp;
    private Color baseColor;

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
        if (tmp) baseColor = tmp.color;
    }

    void Update()
    {
        if (!tmp) return;
        float t = (Mathf.Sin(Time.unscaledTime * speed * Mathf.PI) * 0.5f + 0.5f);
        float a = Mathf.Lerp(minA, maxA, t);
        var c = baseColor; c.a = a;
        tmp.color = c;
    }
}
