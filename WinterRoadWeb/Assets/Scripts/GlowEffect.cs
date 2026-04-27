using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택된 원소 버튼에 빛나는 원형 Glow 효과를 표시합니다.
/// UI/GlowRing 셰이더를 사용하여 프로시저럴 Glow 링을 렌더링합니다.
/// </summary>
[RequireComponent(typeof(Image))]
public class GlowEffect : MonoBehaviour
{
    [Header("Pulse Settings")]
    [SerializeField] private float pulseSpeed = 1.5f;
    [SerializeField] private float minAlpha = 0.5f;
    [SerializeField] private float maxAlpha = 1.0f;
    [SerializeField] private float minScale = 0.97f;
    [SerializeField] private float maxScale = 1.03f;
    
    private Image glowImage;
    private Material glowMaterial;
    private RectTransform rectTransform;
    private Color glowColor = new Color(0.5f, 0.9f, 1f, 1f);
    private Coroutine pulseCoroutine;
    private bool isActive = false;
    
    void Awake()
    {
        glowImage = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
        
        // 셰이더로 Material 생성
        Shader glowShader = Shader.Find("UI/GlowRing");
        if (glowShader != null)
        {
            glowMaterial = new Material(glowShader);
            glowImage.material = glowMaterial;
        }
        else
        {
            Debug.LogWarning("⚠️ UI/GlowRing 셰이더를 찾을 수 없습니다!");
        }
        
        // 투명 스프라이트 없이 빈 이미지로 표시
        glowImage.sprite = null;
        glowImage.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        glowImage.raycastTarget = false;
        gameObject.SetActive(false);
    }
    
    /// <summary>
    /// Glow 색상 설정
    /// </summary>
    public void SetColor(Color color)
    {
        glowColor = color;
        if (glowMaterial != null)
        {
            glowMaterial.SetColor("_Color", color);
        }
        if (glowImage != null)
        {
            glowImage.color = new Color(color.r, color.g, color.b, glowImage.color.a);
        }
    }
    
    /// <summary>
    /// Glow 효과 시작
    /// </summary>
    public void Show()
    {
        if (glowImage == null) return;
        
        isActive = true;
        gameObject.SetActive(true);
        
        if (pulseCoroutine != null)
            StopCoroutine(pulseCoroutine);
        
        pulseCoroutine = StartCoroutine(PulseAnimation());
    }
    
    /// <summary>
    /// Glow 효과 숨기기
    /// </summary>
    public void Hide()
    {
        isActive = false;
        
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
        
        if (glowImage != null)
        {
            glowImage.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        }
        
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one;
        }
        
        gameObject.SetActive(false);
    }
    
    /// <summary>
    /// 숨쉬기 효과 (알파 + 스케일 펄스)
    /// </summary>
    private IEnumerator PulseAnimation()
    {
        float time = 0f;
        
        // 페이드인 (0.2초)
        float fadeInDuration = 0.2f;
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeInDuration;
            float alpha = Mathf.Lerp(0f, maxAlpha, t);
            glowImage.color = new Color(glowColor.r, glowColor.g, glowColor.b, alpha);
            yield return null;
        }
        
        // 펄스 루프
        while (isActive)
        {
            time += Time.deltaTime * pulseSpeed;
            float sin = (Mathf.Sin(time * Mathf.PI * 2f) + 1f) * 0.5f;
            
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, sin);
            glowImage.color = new Color(glowColor.r, glowColor.g, glowColor.b, alpha);
            
            float scale = Mathf.Lerp(minScale, maxScale, sin);
            if (rectTransform != null)
                rectTransform.localScale = new Vector3(scale, scale, 1f);
            
            yield return null;
        }
    }
    
    void OnDisable()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
    }
    
    void OnDestroy()
    {
        if (glowMaterial != null)
        {
            Destroy(glowMaterial);
        }
    }
}
