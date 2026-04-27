using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FrontBackComposer 테스트용 컨트롤러
/// 실시간 합성 및 구형 왜곡 효과를 확인할 수 있습니다.
/// </summary>
public class ComposerTestController : MonoBehaviour
{
    [Header("UI 연결")]
    public RawImage finalComposedImage;   // 1920x928 최종 합성 이미지
    public RawImage frontSphericalImage;  // 전면 구형 왜곡 이미지
    public RawImage backSphericalImage;   // 후면 구형 왜곡 이미지
    public RawImage frontOriginalImage;   // 전면 원본 이미지
    public RawImage backOriginalImage;    // 후면 원본 이미지
    
    [Header("컨트롤 UI")]
    public Slider distortionSlider;
    public Slider radiusSlider;
    public Toggle fisheyeToggle;
    public Toggle sphericalToggle;
    
    [Header("컴포저 연결")]
    public FrontBackComposer composer;
    
    private bool isSetup = false;
    
    void Start()
    {
        SetupComposer();
        SetupUI();
        SetupControls();
        
        // 1초 후에 텍스처 연결 (초기화 대기)
        Invoke(nameof(ConnectTextures), 1f);
        
        isSetup = true;
    }
    
    void SetupComposer()
    {
        if (composer == null)
        {
            composer = FindObjectOfType<FrontBackComposer>();
            if (composer == null)
            {
                Debug.LogError("FrontBackComposer를 찾을 수 없습니다!");
                return;
            }
        }
        
        // 기본 설정
        composer.SetSphericalDistortion(true);
        composer.SetOutputSize(1920, 928);
        
        Debug.Log("FrontBackComposer 설정 완료");
    }
    
    void SetupUI()
    {
        // 슬라이더 초기값 설정
        if (distortionSlider != null)
        {
            distortionSlider.minValue = 0f;
            distortionSlider.maxValue = 3f;
            distortionSlider.value = 1.5f;
            distortionSlider.onValueChanged.AddListener(OnDistortionChanged);
        }
        
        if (radiusSlider != null)
        {
            radiusSlider.minValue = 0.1f;
            radiusSlider.maxValue = 1f;
            radiusSlider.value = 0.8f;
            radiusSlider.onValueChanged.AddListener(OnRadiusChanged);
        }
        
        
        // 토글 설정
        if (fisheyeToggle != null)
        {
            fisheyeToggle.isOn = false;
            fisheyeToggle.onValueChanged.AddListener(OnFisheyeToggled);
        }
        
        if (sphericalToggle != null)
        {
            sphericalToggle.isOn = true;
            sphericalToggle.onValueChanged.AddListener(OnSphericalToggled);
        }
    }
    
    void SetupControls()
    {
        Debug.Log("키보드 컨트롤:");
        Debug.Log("[1] 구형 왜곡 On/Off");
        Debug.Log("[2] 어안렌즈 모드 전환");
        Debug.Log("[3] 출력 크기 변경");
        Debug.Log("[R] 리셋");
    }
    
    void ConnectTextures()
    {
        if (composer == null) return;
        
        // 1920x928 최종 합성 텍스처 연결
        if (finalComposedImage != null)
        {
            var texture = composer.GetFinalComposedTexture();
            if (texture != null)
            {
                finalComposedImage.texture = texture;
                Debug.Log("1920x928 최종 합성 텍스처 연결됨");
            }
        }
        
        // 전면 구형 왜곡 텍스처 연결
        if (frontSphericalImage != null)
        {
            var texture = composer.GetFrontSphericalTexture();
            if (texture != null)
            {
                frontSphericalImage.texture = texture;
                Debug.Log("전면 구형 텍스처 연결됨");
            }
        }
        
        // 후면 구형 왜곡 텍스처 연결
        if (backSphericalImage != null)
        {
            var texture = composer.GetBackSphericalTexture();
            if (texture != null)
            {
                backSphericalImage.texture = texture;
                Debug.Log("후면 구형 텍스처 연결됨");
            }
        }
        
        // 원본 텍스처들 연결
        if (frontOriginalImage != null)
        {
            var texture = composer.GetFrontOriginalTexture();
            if (texture != null)
            {
                frontOriginalImage.texture = texture;
                Debug.Log("전면 원본 텍스처 연결됨");
            }
        }
        
        if (backOriginalImage != null)
        {
            var texture = composer.GetBackOriginalTexture();
            if (texture != null)
            {
                backOriginalImage.texture = texture;
                Debug.Log("후면 원본 텍스처 연결됨");
            }
        }
    }
    
    void Update()
    {
        if (!isSetup) return;
        
        HandleKeyboardInput();
    }
    
    void HandleKeyboardInput()
    {
        if (composer == null) return;
        
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            bool current = sphericalToggle != null ? sphericalToggle.isOn : true;
            composer.SetSphericalDistortion(!current);
            if (sphericalToggle != null) sphericalToggle.isOn = !current;
            Debug.Log($"구형 왜곡: {!current}");
        }
        
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            bool current = fisheyeToggle != null ? fisheyeToggle.isOn : false;
            composer.SetUseFisheye(!current);
            if (fisheyeToggle != null) fisheyeToggle.isOn = !current;
            Debug.Log($"어안렌즈: {!current}");
        }
        
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            CycleOutputSize();
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetToDefaults();
        }
    }
    
    int sizeMode = 0;
    void CycleOutputSize()
    {
        sizeMode = (sizeMode + 1) % 3;
        
        switch (sizeMode)
        {
            case 0: // 1920x928
                composer.SetOutputSize(1920, 928);
                Debug.Log("출력 크기: 1920x928");
                break;
                
            case 1: // 1280x640
                composer.SetOutputSize(1280, 640);
                Debug.Log("출력 크기: 1280x640");
                break;
                
            case 2: // 960x480
                composer.SetOutputSize(960, 480);
                Debug.Log("출력 크기: 960x480");
                break;
        }
        
        // 텍스처 재연결
        Invoke(nameof(ConnectTextures), 0.5f);
    }
    
    void ResetToDefaults()
    {
        if (distortionSlider != null) distortionSlider.value = 1.5f;
        if (radiusSlider != null) radiusSlider.value = 0.8f;
        if (fisheyeToggle != null) fisheyeToggle.isOn = false;
        if (sphericalToggle != null) sphericalToggle.isOn = true;
        
        sizeMode = 0;
        composer.SetOutputSize(1920, 928);
        composer.SetSphericalDistortion(true);
        composer.SetUseFisheye(false);
        
        Invoke(nameof(ConnectTextures), 0.5f);
        Debug.Log("설정 리셋 완료");
    }
    
    // UI 이벤트 핸들러들
    void OnDistortionChanged(float value)
    {
        if (composer != null)
        {
            composer.SetDistortionStrength(value);
            composer.SetFisheyeStrength(value);
        }
    }
    
    void OnRadiusChanged(float value)
    {
        if (composer != null)
        {
            composer.SetSphereRadius(value);
        }
    }
    
    
    void OnFisheyeToggled(bool isOn)
    {
        if (composer != null)
        {
            composer.SetUseFisheye(isOn);
        }
    }
    
    
    void OnSphericalToggled(bool isOn)
    {
        if (composer != null)
        {
            composer.SetSphericalDistortion(isOn);
        }
    }
    
    void OnGUI()
    {
        if (!isSetup || composer == null) return;
        
        // 상태 표시
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Box("실시간 합성 & 구형 왜곡 시스템");
        
        GUILayout.Label($"구형 왜곡: {(composer.enableSphericalDistortion ? "ON" : "OFF")}");
        GUILayout.Label($"어안렌즈: {(composer.useFisheyeInstead ? "ON" : "OFF")}");
        GUILayout.Label($"출력 크기: {GetOutputSizeString()}");
        
        GUILayout.Space(10);
        GUILayout.Label("키보드 컨트롤: 1,2,3,R");
        
        GUILayout.EndArea();
    }
    
    string GetOutputSizeString()
    {
        return sizeMode switch
        {
            0 => "1920x928",
            1 => "1280x640", 
            2 => "960x480",
            _ => "알 수 없음"
        };
    }
    
    // 디버깅용 메서드
    [ContextMenu("텍스처 재연결")]
    void RetryConnection()
    {
        ConnectTextures();
    }
}
