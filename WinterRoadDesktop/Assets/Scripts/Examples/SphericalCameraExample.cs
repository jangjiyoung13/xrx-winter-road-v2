using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 구형 카메라 시스템 사용 예시
/// 전면과 후면 카메라에서 구형 이미지를 생성하는 완전한 예제입니다.
/// </summary>
public class SphericalCameraExample : MonoBehaviour
{
    [Header("카메라 설정")]
    [SerializeField] private Camera frontCamera;
    [SerializeField] private Camera backCamera;
    
    [Header("UI 표시")]
    [SerializeField] private RawImage frontDisplayUI;
    [SerializeField] private RawImage backDisplayUI;
    
    [Header("렌더러 컴포넌트")]
    [SerializeField] private SphericalViewRenderer frontRenderer;
    [SerializeField] private SphericalViewRenderer backRenderer;
    
    [Header("실시간 조절")]
    [Range(0f, 3f)]
    [SerializeField] private float globalDistortionIntensity = 1.5f;
    [Range(0.1f, 1f)]
    [SerializeField] private float globalEffectRadius = 0.8f;
    
    [Header("키보드 컨트롤")]
    [SerializeField] private bool enableKeyboardControl = true;
    
    private SphericalViewRenderer.DistortionType currentDistortionType = 
        SphericalViewRenderer.DistortionType.Spherical;
    
    void Start()
    {
        SetupSphericalRenderers();
        SetupUI();
    }
    
    void SetupSphericalRenderers()
    {
        // 전면 카메라 렌더러 설정
        if (frontCamera != null && frontRenderer == null)
        {
            frontRenderer = frontCamera.gameObject.AddComponent<SphericalViewRenderer>();
        }
        
        // 후면 카메라 렌더러 설정
        if (backCamera != null && backRenderer == null)
        {
            backRenderer = backCamera.gameObject.AddComponent<SphericalViewRenderer>();
        }
        
        // 초기 설정 적용
        UpdateRendererSettings();
    }
    
    void SetupUI()
    {
        // UI에 구형 렌더 텍스처 연결
        if (frontDisplayUI != null && frontRenderer != null)
        {
            frontDisplayUI.texture = frontRenderer.GetSphericalTexture();
        }
        
        if (backDisplayUI != null && backRenderer != null)
        {
            backDisplayUI.texture = backRenderer.GetSphericalTexture();
        }
    }
    
    void Update()
    {
        HandleKeyboardInput();
        UpdateRendererSettings();
    }
    
    void HandleKeyboardInput()
    {
        if (!enableKeyboardControl) return;
        
        // 왜곡 모드 변경
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetDistortionType(SphericalViewRenderer.DistortionType.None);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SetDistortionType(SphericalViewRenderer.DistortionType.Spherical);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SetDistortionType(SphericalViewRenderer.DistortionType.Fisheye);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            SetDistortionType(SphericalViewRenderer.DistortionType.BubbleView);
        }
        
        // 강도 조절
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            globalDistortionIntensity = Mathf.Max(0f, globalDistortionIntensity - Time.deltaTime);
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            globalDistortionIntensity = Mathf.Min(3f, globalDistortionIntensity + Time.deltaTime);
        }
        
        // 반지름 조절
        if (Input.GetKey(KeyCode.DownArrow))
        {
            globalEffectRadius = Mathf.Max(0.1f, globalEffectRadius - Time.deltaTime * 0.5f);
        }
        else if (Input.GetKey(KeyCode.UpArrow))
        {
            globalEffectRadius = Mathf.Min(1f, globalEffectRadius + Time.deltaTime * 0.5f);
        }
    }
    
    void UpdateRendererSettings()
    {
        // 전면 렌더러 업데이트
        if (frontRenderer != null)
        {
            frontRenderer.SetDistortionIntensity(globalDistortionIntensity);
            frontRenderer.SetEffectRadius(globalEffectRadius);
        }
        
        // 후면 렌더러 업데이트
        if (backRenderer != null)
        {
            backRenderer.SetDistortionIntensity(globalDistortionIntensity);
            backRenderer.SetEffectRadius(globalEffectRadius);
        }
    }
    
    // 공개 메서드들
    public void SetDistortionType(SphericalViewRenderer.DistortionType type)
    {
        currentDistortionType = type;
        
        if (frontRenderer != null)
            frontRenderer.SetDistortionMode(type);
        
        if (backRenderer != null)
            backRenderer.SetDistortionMode(type);
        
        Debug.Log($"왜곡 모드 변경: {type}");
    }
    
    public void SetDistortionIntensity(float intensity)
    {
        globalDistortionIntensity = Mathf.Clamp(intensity, 0f, 3f);
    }
    
    public void SetEffectRadius(float radius)
    {
        globalEffectRadius = Mathf.Clamp(radius, 0.1f, 1f);
    }
    
    // UI 슬라이더 연결용 메서드들
    public void OnDistortionSliderChanged(float value)
    {
        SetDistortionIntensity(value);
    }
    
    public void OnRadiusSliderChanged(float value)
    {
        SetEffectRadius(value);
    }
    
    // 개별 카메라 중심점 조절
    public void SetFrontCameraCenter(Vector2 center)
    {
        if (frontRenderer != null)
            frontRenderer.SetCenter(center);
    }
    
    public void SetBackCameraCenter(Vector2 center)
    {
        if (backRenderer != null)
            backRenderer.SetCenter(center);
    }
    
    // 텍스처 품질 조절
    public void SetTextureQuality(int quality)
    {
        int size = quality switch
        {
            0 => 256,  // 낮음
            1 => 512,  // 보통
            2 => 1024, // 높음
            3 => 2048, // 최고
            _ => 512
        };
        
        if (frontRenderer != null)
            frontRenderer.SetTextureSize(size);
        
        if (backRenderer != null)
            backRenderer.SetTextureSize(size);
    }
    
    void OnGUI()
    {
        if (!enableKeyboardControl) return;
        
        // 간단한 도움말 표시
        GUI.Box(new Rect(10, 10, 300, 120), "구형 카메라 컨트롤");
        GUI.Label(new Rect(15, 35, 290, 20), "1-4: 왜곡 모드 (없음/구형/어안/버블)");
        GUI.Label(new Rect(15, 55, 290, 20), "←→: 왜곡 강도 조절");
        GUI.Label(new Rect(15, 75, 290, 20), "↑↓: 효과 반지름 조절");
        GUI.Label(new Rect(15, 95, 290, 20), $"현재 모드: {currentDistortionType}");
        GUI.Label(new Rect(15, 110, 290, 20), $"강도: {globalDistortionIntensity:F2}, 반지름: {globalEffectRadius:F2}");
    }
}



