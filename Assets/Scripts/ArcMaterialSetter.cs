using UnityEngine;

public class ArcMaterialSetter : MonoBehaviour
{
    public Material arcMaterial;   // 例: Mat_RedNeon (URP/Lit + Emission 赤)

    void LateUpdate()
    {
        if (arcMaterial == null) return;

        // 生成された子の Renderer に一括適用（軽量）
        var renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r.sharedMaterial != arcMaterial)
                r.sharedMaterial = arcMaterial;
        }
    }
}
