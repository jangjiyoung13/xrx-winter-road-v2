using UnityEngine;

/// <summary>
/// 기존 PanoramaCamera 시스템과 호환되는 구형 뷰 렌더러
/// 외부에서 바라보는 형태의 구형 이미지를 생성합니다.
/// </summary>
public class SphericalViewRenderer : MonoBehaviour
{
    [Header("카메라 참조")]
    [SerializeField] private Camera sourceCamera;
    
    [Header("출력 설정")]
    [SerializeField] private RenderTexture outputTexture;
    [SerializeField] private int textureSize = 512;
    
    [Header("구형 왜곡 설정")]
    [SerializeField] private DistortionType distortionMode = DistortionType.Spherical;
    [Range(0f, 3f)]
    [SerializeField] private float distortionIntensity = 1.5f;
    [Range(0.1f, 1f)]
    [SerializeField] private float effectRadius = 0.8f;
    [Range(0f, 1f)]
    [SerializeField] private float centerOffsetX = 0.5f;
    [Range(0f, 1f)]
    [SerializeField] private float centerOffsetY = 0.5f;
    
    [Header("고급 설정")]
    [SerializeField] private bool enableAntiAliasing = true;
    [SerializeField] private bool enableMipMaps = false;
    
    private RenderTexture intermediateTexture;
    private Material distortionMaterial;
    private bool isInitialized = false;
    
    public enum DistortionType
    {
        None,
        Spherical,
        Fisheye,
        BubbleView
    }
    
    void Start()
    {
        InitializeRenderer();
    }
    
    void InitializeRenderer()
    {
        if (isInitialized) return;
        
        SetupCamera();
        CreateTextures();
        CreateMaterial();
        
        isInitialized = true;
    }
    
    void SetupCamera()
    {
        if (sourceCamera == null)
        {
            sourceCamera = GetComponent<Camera>();
            if (sourceCamera == null)
            {
                Debug.LogError($"{name}: 소스 카메라를 찾을 수 없습니다!");
                return;
            }
        }
    }
    
    void CreateTextures()
    {
        // 중간 렌더 텍스처 생성 (원본 카메라 출력용)
        intermediateTexture = new RenderTexture(
            textureSize, 
            textureSize, 
            24, 
            RenderTextureFormat.ARGB32
        );
        intermediateTexture.name = $"{name}_Intermediate";
        intermediateTexture.antiAliasing = enableAntiAliasing ? 4 : 1;
        intermediateTexture.useMipMap = enableMipMaps;
        intermediateTexture.Create();
        
        // 출력 텍스처 생성 (왜곡 적용 후)
        if (outputTexture == null)
        {
            outputTexture = new RenderTexture(
                textureSize, 
                textureSize, 
                0, 
                RenderTextureFormat.ARGB32
            );
            outputTexture.name = $"{name}_SphericalOutput";
            outputTexture.antiAliasing = enableAntiAliasing ? 4 : 1;
            outputTexture.useMipMap = enableMipMaps;
            outputTexture.Create();
        }
        
        // 카메라 출력을 중간 텍스처로 설정
        sourceCamera.targetTexture = intermediateTexture;
    }
    
    void CreateMaterial()
    {
        Shader shader = null;
        
        switch (distortionMode)
        {
            case DistortionType.Spherical:
                shader = Shader.Find("Custom/SphericalDistortion");
                break;
            case DistortionType.Fisheye:
                shader = Shader.Find("Custom/FisheyeDistortion");
                break;
            case DistortionType.BubbleView:
                shader = Shader.Find("Custom/SphericalDistortion"); // 같은 셰이더 사용
                break;
        }
        
        if (shader != null)
        {
            distortionMaterial = new Material(shader);
        }
        else
        {
            Debug.LogError($"{name}: 왜곡 셰이더를 찾을 수 없습니다! ({distortionMode})");
        }
    }
    
    void LateUpdate()
    {
        if (!isInitialized) return;
        
        ApplySphericalDistortion();
    }
    
    void ApplySphericalDistortion()
    {
        if (distortionMaterial == null || outputTexture == null) return;
        
        // 왜곡 모드에 따른 매개변수 설정
        UpdateDistortionParameters();
        
        // 왜곡 효과 적용
        Graphics.Blit(intermediateTexture, outputTexture, distortionMaterial);
    }
    
    void UpdateDistortionParameters()
    {
        if (distortionMaterial == null) return;
        
        Vector4 center = new Vector4(centerOffsetX, centerOffsetY, 0, 0);
        distortionMaterial.SetVector("_Center", center);
        
        switch (distortionMode)
        {
            case DistortionType.Spherical:
            case DistortionType.BubbleView:
                distortionMaterial.SetFloat("_DistortionStrength", distortionIntensity);
                distortionMaterial.SetFloat("_SphereRadius", effectRadius);
                break;
                
            case DistortionType.Fisheye:
                distortionMaterial.SetFloat("_Strength", distortionIntensity);
                distortionMaterial.SetFloat("_LensRadius", effectRadius);
                break;
        }
    }
    
    // 공개 API
    public RenderTexture GetSphericalTexture()
    {
        return outputTexture;
    }
    
    public void SetDistortionMode(DistortionType mode)
    {
        if (distortionMode != mode)
        {
            distortionMode = mode;
            CreateMaterial(); // 새로운 셰이더로 머티리얼 재생성
        }
    }
    
    public void SetDistortionIntensity(float intensity)
    {
        distortionIntensity = Mathf.Clamp(intensity, 0f, 3f);
    }
    
    public void SetEffectRadius(float radius)
    {
        effectRadius = Mathf.Clamp(radius, 0.1f, 1f);
    }
    
    public void SetCenter(Vector2 center)
    {
        centerOffsetX = Mathf.Clamp01(center.x);
        centerOffsetY = Mathf.Clamp01(center.y);
    }
    
    public void SetTextureSize(int size)
    {
        if (textureSize != size)
        {
            textureSize = size;
            
            // 텍스처 재생성
            if (isInitialized)
            {
                CleanupTextures();
                CreateTextures();
            }
        }
    }
    
    void CleanupTextures()
    {
        if (intermediateTexture != null)
        {
            intermediateTexture.Release();
            DestroyImmediate(intermediateTexture);
        }
        
        if (outputTexture != null)
        {
            outputTexture.Release();
            DestroyImmediate(outputTexture);
        }
    }
    
    void OnDestroy()
    {
        CleanupTextures();
        
        if (distortionMaterial != null)
        {
            DestroyImmediate(distortionMaterial);
        }
    }
    
    void OnValidate()
    {
        // 에디터에서 값 변경 시 실시간 업데이트
        if (Application.isPlaying && isInitialized)
        {
            UpdateDistortionParameters();
        }
    }
    
    // 기존 PanoramaCamera와의 호환성을 위한 메서드
    public void SetTargetTexture(RenderTexture target)
    {
        outputTexture = target;
    }
    
    public void RenderSphericalView()
    {
        if (isInitialized)
        {
            ApplySphericalDistortion();
        }
    }
}



