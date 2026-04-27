using UnityEngine;

/// <summary>
/// Unity 카메라 영상을 3D 구체에 매핑한 후 UV로 펼쳐서 
/// 구체 LED용 최종 텍스처 생성
/// </summary>
public class SphereUVMapper : MonoBehaviour
{
    [Header("카메라 연결")]
    public Camera frontCamera;
    public Camera backCamera;
    
    [Header("입력 렌더텍스처")]
    public RenderTexture frontRenderTexture;
    public RenderTexture backRenderTexture;
    
    [Header("최종 출력 (구체 LED용 UV 텍스처)")]
    public RenderTexture sphereUVTexture;
    [SerializeField] private int uvTextureWidth = 1920;
    [SerializeField] private int uvTextureHeight = 928;
    
    [Header("블렌딩 설정")]
    [Range(0f, 1f)]
    [SerializeField] private float blendRegion = 0.2f;
    
    private Material unwrapMaterial;
    private bool isInitialized = false;
    
    void Start()
    {
        InitializeSystem();
    }
    
    void InitializeSystem()
    {
        if (isInitialized) return;
        
        SetupCameras();
        CreateMaterial();
        CreateOutputTexture();
        
        isInitialized = true;
        Debug.Log("SphereUVMapper 초기화 완료");
    }
    
    void SetupCameras()
    {
        if (frontCamera != null && frontRenderTexture != null)
        {
            frontCamera.enabled = true;
            frontCamera.targetTexture = frontRenderTexture;
            Debug.Log("전면 카메라 연결됨");
        }
        
        if (backCamera != null && backRenderTexture != null)
        {
            backCamera.enabled = true;
            backCamera.targetTexture = backRenderTexture;
            Debug.Log("후면 카메라 연결됨");
        }
    }
    
    void CreateMaterial()
    {
        // 먼저 간단한 버전 시도
        Shader unwrapShader = Shader.Find("Custom/SphereUVUnwrapSimple");
        
        // 없으면 원래 버전
        if (unwrapShader == null)
        {
            unwrapShader = Shader.Find("Custom/SphereUVUnwrap");
        }
        
        // 둘 다 없으면 기본 Unlit 셰이더 사용
        if (unwrapShader == null)
        {
            Debug.LogWarning("커스텀 셰이더를 찾을 수 없습니다. Unlit/Texture 사용");
            unwrapShader = Shader.Find("Unlit/Texture");
        }
        
        if (unwrapShader != null)
        {
            unwrapMaterial = new Material(unwrapShader);
            Debug.Log($"셰이더 로드 완료: {unwrapShader.name}");
        }
        else
        {
            Debug.LogError("사용 가능한 셰이더가 없습니다!");
        }
    }
    
    void CreateOutputTexture()
    {
        if (sphereUVTexture == null)
        {
            sphereUVTexture = new RenderTexture(
                uvTextureWidth,
                uvTextureHeight,
                0,
                RenderTextureFormat.ARGB32
            );
            sphereUVTexture.name = "SphereUV_Output";
            sphereUVTexture.Create();
        }
        
        Debug.Log($"UV 출력 텍스처 생성: {uvTextureWidth}x{uvTextureHeight}");
    }
    
    void LateUpdate()
    {
        if (!isInitialized) return;
        
        GenerateSphereUVTexture();
    }
    
    void GenerateSphereUVTexture()
    {
        if (unwrapMaterial == null || sphereUVTexture == null)
        {
            Debug.LogWarning("머티리얼 또는 출력 텍스처가 없습니다!");
            return;
        }
        
        // 셰이더 이름 확인
        string shaderName = unwrapMaterial.shader.name;
        
        // 카메라 텍스처를 셰이더에 전달
        if (frontRenderTexture != null)
        {
            unwrapMaterial.SetTexture("_FrontTex", frontRenderTexture);
        }
        else
        {
            Debug.LogWarning("frontRenderTexture가 null입니다!");
        }
        
        if (backRenderTexture != null)
        {
            unwrapMaterial.SetTexture("_BackTex", backRenderTexture);
        }
        else
        {
            Debug.LogWarning("backRenderTexture가 null입니다!");
        }
        
        // 커스텀 셰이더인 경우에만 블렌드 설정
        if (shaderName.Contains("SphereUVUnwrap"))
        {
            unwrapMaterial.SetFloat("_BlendRegion", blendRegion);
        }
        
        // Unlit 셰이더인 경우 전면 텍스처만 사용
        if (shaderName == "Unlit/Texture")
        {
            if (frontRenderTexture != null)
            {
                unwrapMaterial.SetTexture("_MainTex", frontRenderTexture);
                Graphics.Blit(frontRenderTexture, sphereUVTexture);
            }
            return;
        }
        
        // 셰이더로 UV 언랩 실행
        Graphics.Blit(null, sphereUVTexture, unwrapMaterial);
    }
    
    // 공개 접근자
    public RenderTexture GetSphereUVTexture() => sphereUVTexture;
    
    public void SetBlendRegion(float region)
    {
        blendRegion = Mathf.Clamp01(region);
    }
    
    public void SetUVResolution(int width, int height)
    {
        if (uvTextureWidth != width || uvTextureHeight != height)
        {
            uvTextureWidth = width;
            uvTextureHeight = height;
            
            if (isInitialized)
            {
                if (sphereUVTexture != null)
                {
                    sphereUVTexture.Release();
                    DestroyImmediate(sphereUVTexture);
                }
                CreateOutputTexture();
            }
        }
    }
    
    void OnDestroy()
    {
        if (unwrapMaterial != null)
        {
            DestroyImmediate(unwrapMaterial);
        }
        
        if (sphereUVTexture != null)
        {
            sphereUVTexture.Release();
            DestroyImmediate(sphereUVTexture);
        }
    }
    
    [ContextMenu("시스템 상태 출력")]
    void PrintStatus()
    {
        Debug.Log("=== SphereUVMapper 상태 ===");
        Debug.Log($"UV 텍스처 크기: {uvTextureWidth}x{uvTextureHeight}");
        Debug.Log($"전면 카메라: {(frontCamera != null ? "연결됨" : "없음")}");
        Debug.Log($"후면 카메라: {(backCamera != null ? "연결됨" : "없음")}");
        Debug.Log($"전면 텍스처: {(frontRenderTexture != null ? frontRenderTexture.name : "없음")}");
        Debug.Log($"후면 텍스처: {(backRenderTexture != null ? backRenderTexture.name : "없음")}");
        Debug.Log($"최종 UV 텍스처: {(sphereUVTexture != null ? "생성됨" : "없음")}");
    }
}
