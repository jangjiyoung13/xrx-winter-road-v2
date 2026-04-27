using UnityEngine;

[ExecuteAlways]
public class ParticleGroupAlpha : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float intensity = 0.5f;

    [Tooltip("Additive 머티리얼이면 체크 (RGB로 밝기 조절). 해제하면 Alpha로 조절")]
    [SerializeField] private bool isAdditive = true;

    [Tooltip("비활성 자식 파티클도 포함할지 여부")]
    [SerializeField] private bool includeInactive = true;

    private Color[] originalColors;
    private ParticleSystem[] cachedSystems;

    private void OnEnable()
    {
        CacheOriginalColors();
        ApplyIntensity();
    }

    private void OnValidate()
    {
        if (!isActiveAndEnabled) return;

        if (cachedSystems == null || cachedSystems.Length == 0)
        {
            CacheOriginalColors();
        }
        ApplyIntensity();
    }

    private void CacheOriginalColors()
    {
        cachedSystems = GetComponentsInChildren<ParticleSystem>(includeInactive);
        originalColors = new Color[cachedSystems.Length];

        for (int i = 0; i < cachedSystems.Length; i++)
        {
            originalColors[i] = cachedSystems[i].main.startColor.color;
        }
    }

    [ContextMenu("Apply Intensity")]
    public void ApplyIntensity()
    {
        if (cachedSystems == null || originalColors == null) return;

        for (int i = 0; i < cachedSystems.Length; i++)
        {
            if (cachedSystems[i] == null) continue;

            var main = cachedSystems[i].main;
            Color original = originalColors[i];
            Color adjusted = original;

            if (isAdditive)
            {
                adjusted.r = original.r * intensity;
                adjusted.g = original.g * intensity;
                adjusted.b = original.b * intensity;
                adjusted.a = original.a;
            }
            else
            {
                adjusted.a = original.a * intensity;
            }

            main.startColor = new ParticleSystem.MinMaxGradient(adjusted);
        }
    }

    [ContextMenu("Reset to Original")]
    public void ResetToOriginal()
    {
        if (cachedSystems == null || originalColors == null) return;

        for (int i = 0; i < cachedSystems.Length; i++)
        {
            if (cachedSystems[i] == null) continue;

            var main = cachedSystems[i].main;
            main.startColor = new ParticleSystem.MinMaxGradient(originalColors[i]);
        }
    }

    [ContextMenu("Re-cache Original Colors")]
    public void RecacheOriginalColors()
    {
        CacheOriginalColors();
        ApplyIntensity();
    }

    public void SetIntensity(float value)
    {
        intensity = Mathf.Clamp01(value);
        ApplyIntensity();
    }
}
