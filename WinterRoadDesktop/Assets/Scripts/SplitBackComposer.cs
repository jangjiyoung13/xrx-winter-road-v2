using UnityEngine;

/// <summary>
/// 후면 렌더러를 세로로 반 잘라서 전면 양옆에 배치
/// 최종 레이아웃: [후면 우측] [전면] [후면 좌측]
/// </summary>
public class SplitBackComposer : MonoBehaviour
{
    [Header("카메라 연결")]
    public Camera frontCamera;
    public Camera backCamera;
    
    [Header("입력 렌더텍스처")]
    public RenderTexture frontRenderTexture;
    public RenderTexture backRenderTexture;
    
    [Header("최종 출력")]
    public RenderTexture composedTexture;
    [SerializeField] private int outputWidth = 1920;
    [SerializeField] private int outputHeight = 928;
    
    [Header("테두리 설정")]
    public Texture2D borderSprite;
    [Range(0f, 1f)]
    [SerializeField] private float borderOpacity = 1f;
    [Range(0.5f, 2f)]
    [SerializeField] private float frontBorderScale = 1f;
    [Range(0.5f, 2f)]
    [SerializeField] private float backBorderScale = 1f;
    
    private Material composeMaterial;
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
        Debug.Log("SplitBackComposer 초기화 완료");
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
        // 테두리가 있으면 테두리 버전 셰이더 사용
        Shader shader = borderSprite != null ? 
            Shader.Find("Custom/SplitBackComposerWithBorder") : 
            Shader.Find("Custom/SplitBackComposer");
            
        if (shader != null)
        {
            composeMaterial = new Material(shader);
            Debug.Log($"셰이더 로드 완료: {shader.name}");
        }
        else
        {
            Debug.LogError("SplitBackComposer 셰이더를 찾을 수 없습니다!");
        }
    }
    
    void CreateOutputTexture()
    {
        if (composedTexture == null)
        {
            composedTexture = new RenderTexture(
                outputWidth,
                outputHeight,
                0,
                RenderTextureFormat.ARGB32
            );
            composedTexture.name = "SplitBack_Composed";
            composedTexture.Create();
        }
        
        Debug.Log($"출력 텍스처 생성: {outputWidth}x{outputHeight}");
    }
    
    void LateUpdate()
    {
        if (!isInitialized) return;
        
        ComposeTextures();
    }
    
    void ComposeTextures()
    {
        if (composeMaterial == null || composedTexture == null)
        {
            Debug.LogWarning("머티리얼 또는 출력 텍스처가 없습니다!");
            return;
        }
        
        // 카메라 텍스처를 셰이더에 전달
        if (frontRenderTexture != null)
        {
            composeMaterial.SetTexture("_FrontTex", frontRenderTexture);
        }
        else
        {
            Debug.LogWarning("frontRenderTexture가 null입니다!");
        }
        
        if (backRenderTexture != null)
        {
            composeMaterial.SetTexture("_BackTex", backRenderTexture);
        }
        else
        {
            Debug.LogWarning("backRenderTexture가 null입니다!");
        }
        
        // 테두리 설정 (테두리 셰이더인 경우에만)
        if (borderSprite != null && composeMaterial.shader.name.Contains("WithBorder"))
        {
            composeMaterial.SetTexture("_BorderTex", borderSprite);
            composeMaterial.SetFloat("_BorderOpacity", borderOpacity);
            composeMaterial.SetFloat("_FrontBorderScale", frontBorderScale);
            composeMaterial.SetFloat("_BackBorderScale", backBorderScale);
        }
        
        // 합성 실행
        Graphics.Blit(null, composedTexture, composeMaterial);
    }
    
    // 공개 접근자
    public RenderTexture GetComposedTexture() => composedTexture;
    
    public void SetOutputResolution(int width, int height)
    {
        if (outputWidth != width || outputHeight != height)
        {
            outputWidth = width;
            outputHeight = height;
            
            if (isInitialized)
            {
                if (composedTexture != null)
                {
                    composedTexture.Release();
                    DestroyImmediate(composedTexture);
                }
                CreateOutputTexture();
            }
        }
    }
    
    void OnDestroy()
    {
        if (composeMaterial != null)
        {
            DestroyImmediate(composeMaterial);
        }
        
        if (composedTexture != null)
        {
            composedTexture.Release();
            DestroyImmediate(composedTexture);
        }
    }
    
    [ContextMenu("시스템 상태 출력")]
    void PrintStatus()
    {
        Debug.Log("=== SplitBackComposer 상태 ===");
        Debug.Log($"출력 해상도: {outputWidth}x{outputHeight}");
        Debug.Log($"레이아웃: [후면 우측 1/4] [전면 2/4] [후면 좌측 1/4]");
        Debug.Log($"전면 카메라: {(frontCamera != null ? "연결됨" : "없음")}");
        Debug.Log($"후면 카메라: {(backCamera != null ? "연결됨" : "없음")}");
        Debug.Log($"합성 텍스처: {(composedTexture != null ? "생성됨" : "없음")}");
    }
}

