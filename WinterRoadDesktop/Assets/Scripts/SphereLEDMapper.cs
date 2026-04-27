using UnityEngine;

/// <summary>
/// 전면/후면 카메라를 구체 LED용 UV 매핑 텍스처로 실시간 변환
/// Cubemap → Equirectangular 투영 방식 사용
/// </summary>
public class SphereLEDMapper : MonoBehaviour
{
    [Header("카메라 설정")]
    public Camera frontCamera;
    public Camera backCamera;
    
    [Header("출력 설정")]
    public RenderTexture sphereUVTexture;  // 구체 LED용 최종 UV 매핑 텍스처
    [SerializeField] private int outputWidth = 3840;   // 구체 LED 해상도
    [SerializeField] private int outputHeight = 1920;  // 2:1 비율 (Equirectangular)
    
    [Header("품질 설정")]
    [SerializeField] private int cubemapResolution = 2048;
    
    private Cubemap frontCubemap;
    private Cubemap backCubemap;
    private RenderTexture frontPanorama;
    private RenderTexture backPanorama;
    private Material equirectMaterial;
    private Material combineMaterial;
    
    void Start()
    {
        InitializeSystem();
    }
    
    void InitializeSystem()
    {
        // Cubemap 생성
        frontCubemap = new Cubemap(cubemapResolution, TextureFormat.RGB24, false);
        backCubemap = new Cubemap(cubemapResolution, TextureFormat.RGB24, false);
        
        // 중간 파노라마 텍스처 생성 (각각 절반 크기)
        frontPanorama = new RenderTexture(outputWidth / 2, outputHeight, 0, RenderTextureFormat.ARGB32);
        backPanorama = new RenderTexture(outputWidth / 2, outputHeight, 0, RenderTextureFormat.ARGB32);
        
        // 최종 출력 텍스처 생성
        if (sphereUVTexture == null)
        {
            sphereUVTexture = new RenderTexture(outputWidth, outputHeight, 0, RenderTextureFormat.ARGB32);
            sphereUVTexture.name = "SphereLED_UVMap";
        }
        
        // Equirectangular 변환 머티리얼
        Shader equirectShader = Shader.Find("Hidden/CubemapToEquirectangular");
        if (equirectShader != null)
        {
            equirectMaterial = new Material(equirectShader);
            Debug.Log("Equirectangular 변환 셰이더 로드 완료");
        }
        else
        {
            Debug.LogError("Hidden/CubemapToEquirectangular 셰이더를 찾을 수 없습니다!");
        }
        
        // 카메라 비활성화 (수동 렌더링)
        if (frontCamera != null) frontCamera.enabled = false;
        if (backCamera != null) backCamera.enabled = false;
        
        Debug.Log($"SphereLEDMapper 초기화 완료: {outputWidth}x{outputHeight}");
    }
    
    void LateUpdate()
    {
        RenderSphereLEDTexture();
    }
    
    void RenderSphereLEDTexture()
    {
        if (equirectMaterial == null) return;
        
        // 1. 전면 카메라 → Cubemap → Equirectangular
        if (frontCamera != null && frontPanorama != null)
        {
            frontCamera.RenderToCubemap(frontCubemap);
            Graphics.Blit(frontCubemap, frontPanorama, equirectMaterial);
        }
        
        // 2. 후면 카메라 → Cubemap → Equirectangular
        if (backCamera != null && backPanorama != null)
        {
            backCamera.RenderToCubemap(backCubemap);
            Graphics.Blit(backCubemap, backPanorama, equirectMaterial);
        }
        
        // 3. 좌우로 합성 (전면 좌측, 후면 우측)
        CombinePanoramas();
    }
    
    void CombinePanoramas()
    {
        if (sphereUVTexture == null) return;
        
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = sphereUVTexture;
        
        GL.Clear(true, true, Color.black);
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, outputWidth, outputHeight, 0);
        
        // 좌측: 전면 파노라마
        if (frontPanorama != null)
        {
            Graphics.DrawTexture(new Rect(0, 0, outputWidth / 2, outputHeight), frontPanorama);
        }
        
        // 우측: 후면 파노라마
        if (backPanorama != null)
        {
            Graphics.DrawTexture(new Rect(outputWidth / 2, 0, outputWidth / 2, outputHeight), backPanorama);
        }
        
        GL.PopMatrix();
        RenderTexture.active = previous;
    }
    
    // 공개 접근자
    public RenderTexture GetSphereLEDTexture() => sphereUVTexture;
    
    // 설정 변경
    public void SetOutputResolution(int width, int height)
    {
        if (outputWidth != width || outputHeight != height)
        {
            outputWidth = width;
            outputHeight = height;
            
            // 텍스처 재생성
            CleanupTextures();
            InitializeSystem();
        }
    }
    
    void CleanupTextures()
    {
        if (frontCubemap != null) Destroy(frontCubemap);
        if (backCubemap != null) Destroy(backCubemap);
        if (frontPanorama != null) frontPanorama.Release();
        if (backPanorama != null) backPanorama.Release();
        if (sphereUVTexture != null) sphereUVTexture.Release();
    }
    
    void OnDestroy()
    {
        CleanupTextures();
        if (equirectMaterial != null) DestroyImmediate(equirectMaterial);
        if (combineMaterial != null) DestroyImmediate(combineMaterial);
    }
    
    [ContextMenu("시스템 상태 출력")]
    void PrintStatus()
    {
        Debug.Log("=== SphereLEDMapper 상태 ===");
        Debug.Log($"출력 해상도: {outputWidth}x{outputHeight}");
        Debug.Log($"Cubemap 해상도: {cubemapResolution}");
        Debug.Log($"전면 카메라: {(frontCamera != null ? "연결됨" : "없음")}");
        Debug.Log($"후면 카메라: {(backCamera != null ? "연결됨" : "없음")}");
        Debug.Log($"최종 UV 텍스처: {(sphereUVTexture != null ? "생성됨" : "없음")}");
    }
}


