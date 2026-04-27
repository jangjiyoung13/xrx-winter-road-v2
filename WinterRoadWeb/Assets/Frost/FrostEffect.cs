using UnityEngine;

/// <summary>
/// 카메라에 서리/얼음 오버레이 효과를 적용하는 이미지 이펙트.
/// FrostAmount를 1(완전 서리)에서 0(서리 없음)으로 조절하여 효과를 제어합니다.
/// </summary>
[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class FrostEffect : MonoBehaviour
{
    [Header("Frost Settings")]
    [SerializeField, Range(0f, 1f)]
    private float frostAmount = 1f;

    [SerializeField]
    private Material frostMaterial;

    [SerializeField]
    private Texture2D frostTexture;

    [SerializeField]
    private Texture2D frostNormalMap;

    [Header("Frost Parameters")]
    [SerializeField, Range(0f, 1f)]
    private float edgeSharpness = 0.5f;

    [SerializeField, Range(0f, 1f)]
    private float distortionStrength = 0.1f;

    [SerializeField]
    private Color tintColor = new Color(0.8f, 0.9f, 1.0f, 1.0f);

    /// <summary>
    /// 서리 효과의 강도 (0 = 없음, 1 = 최대)
    /// WebGLProgram에서 게임 시작 시 1→0으로 감소시킵니다.
    /// </summary>
    public float FrostAmount
    {
        get => frostAmount;
        set => frostAmount = Mathf.Clamp01(value);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (frostMaterial == null || frostAmount <= 0.001f)
        {
            Graphics.Blit(source, destination);
            return;
        }

        frostMaterial.SetFloat("_FrostAmount", frostAmount);
        frostMaterial.SetFloat("_EdgeSharpness", edgeSharpness);
        frostMaterial.SetFloat("_DistortionStrength", distortionStrength);
        frostMaterial.SetColor("_TintColor", tintColor);

        if (frostTexture != null)
            frostMaterial.SetTexture("_FrostTex", frostTexture);

        if (frostNormalMap != null)
            frostMaterial.SetTexture("_FrostNormal", frostNormalMap);

        Graphics.Blit(source, destination, frostMaterial);
    }
}
