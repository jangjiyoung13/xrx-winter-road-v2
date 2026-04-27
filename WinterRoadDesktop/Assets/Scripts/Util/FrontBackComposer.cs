using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 전면과 후면 카메라에 각각 구형 왜곡을 적용한 후 
/// 가로로 합쳐서 1920x928 크기의 최종 이미지를 생성합니다.
/// </summary>
public class FrontBackComposer : MonoBehaviour
{
    [Header("카메라 연결")]
    public Camera frontCamera;
    public Camera backCamera;
    
    [Header("입력 렌더 텍스처")]
    public RenderTexture frontRenderTexture;  // 전면 카메라 입력
    public RenderTexture backRenderTexture;   // 후면 카메라 입력
    
    [Header("중간 처리 텍스처 (구형 왜곡된)")]
    public RenderTexture frontSphericalTexture;  // 전면 구형 왜곡 결과
    public RenderTexture backSphericalTexture;   // 후면 구형 왜곡 결과
    
    [Header("최종 출력")]
    public RenderTexture finalComposedTexture;   // 1920x928 최종 합성 이미지
    [SerializeField] private int outputWidth = 3840;   // 4K 해상도로 증가
    [SerializeField] private int outputHeight = 1856;  // 비율 유지
    [SerializeField] private int sphericalSize = 1920; // 개별 구형 이미지 크기 (1920x1856)
    
    [Header("구형 왜곡 설정")]
    [SerializeField] public bool enableSphericalDistortion = true;
    [Range(0f, 3f)]
    [SerializeField] private float distortionStrength = 1.5f;
    [Range(0.1f, 1f)]
    [SerializeField] private float sphereRadius = 0.8f;
    [SerializeField] private Vector2 frontCenter = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 backCenter = new Vector2(0.5f, 0.5f);
    
    [Header("어안 렌즈 설정")]
    [SerializeField] public bool useFisheyeInstead = false;
    [Range(0f, 5f)]
    [SerializeField] private float fisheyeStrength = 2.0f;
    [Range(0.1f, 1f)]
    [SerializeField] private float fisheyeRadius = 0.8f;
    
    private Material sphericalMaterial;   // 구형 왜곡용 머티리얼
    private Material fisheyeMaterial;     // 어안 렌즈용 머티리얼
    private Material horizontalComposeMaterial; // 가로 합성용 머티리얼
    private bool isInitialized = false;
    
    #if UNITY_EDITOR
    [Header("에디터 자동 생성")]
    [SerializeField] private bool generateAssetsOnPlay = true; // Play 시 자동 생성/복사
    #endif
    
    void Start()
    {
        InitializeSystem();
        #if UNITY_EDITOR
        if (generateAssetsOnPlay)
        {
            StartCoroutine(CoEnsureAssetsCreatedAndCopied());
        }
        #endif
    }
    
    void InitializeSystem()
    {
        if (isInitialized) return;
        
        SetupCameras();
        CreateMaterials();
        CreateOutputTextures();
        
        isInitialized = true;
        
        Debug.Log("FrontBackComposer: 구형 왜곡 + 가로 합성 시스템이 초기화되었습니다.");
    }

    #if UNITY_EDITOR
    System.Collections.IEnumerator CoEnsureAssetsCreatedAndCopied()
    {
        // 렌더타겟이 실제로 한두 프레임 그려지도록 대기 후 생성/복사
        yield return null;
        yield return null;
        CreateRenderTextureAssets();
        CopyRuntimeTexturesToAssets();
    }
    #endif
    
    void SetupCameras()
    {
        // 카메라들이 기존 렌더텍스처에 연결되어 있는지 확인
        if (frontCamera != null && frontRenderTexture != null)
        {
            frontCamera.enabled = true;
            frontCamera.targetTexture = frontRenderTexture;
            Debug.Log($"전면 카메라 → {frontRenderTexture.name} 연결됨 (활성화됨)");
        }
        else
        {
            Debug.LogWarning("전면 카메라 또는 렌더텍스처가 연결되지 않았습니다!");
        }
        
        if (backCamera != null && backRenderTexture != null)
        {
            backCamera.enabled = true;
            backCamera.targetTexture = backRenderTexture;
            Debug.Log($"후면 카메라 → {backRenderTexture.name} 연결됨 (활성화됨)");
        }
        else
        {
            Debug.LogWarning("후면 카메라 또는 렌더텍스처가 연결되지 않았습니다!");
        }
    }
    
    void CreateMaterials()
    {
        // 구형 왜곡 머티리얼
        Shader sphericalShader = Shader.Find("Custom/SphericalDistortion");
        if (sphericalShader != null)
        {
            sphericalMaterial = new Material(sphericalShader);
            Debug.Log("구형 왜곡 머티리얼 생성 완료");
        }
        else
        {
            Debug.LogError("SphericalDistortion 셰이더를 찾을 수 없습니다!");
        }
        
        // 어안 렌즈 머티리얼
        Shader fisheyeShader = Shader.Find("Custom/FisheyeDistortion");
        if (fisheyeShader != null)
        {
            fisheyeMaterial = new Material(fisheyeShader);
            Debug.Log("어안 렌즈 머티리얼 생성 완료");
        }
        else
        {
            Debug.LogError("FisheyeDistortion 셰이더를 찾을 수 없습니다!");
        }
        
        // 가로 합성용 머티리얼 (기본 Blit 사용)
        horizontalComposeMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
    }
    
    void CreateOutputTextures()
    {
        // 구형 왜곡 중간 텍스처들 생성 (각각 960x928)
        if (frontSphericalTexture == null)
        {
            frontSphericalTexture = new RenderTexture(sphericalSize, outputHeight, 0, RenderTextureFormat.ARGB32);
            frontSphericalTexture.name = "FrontSpherical_Runtime";
            frontSphericalTexture.Create();
        }
        
        if (backSphericalTexture == null)
        {
            backSphericalTexture = new RenderTexture(sphericalSize, outputHeight, 0, RenderTextureFormat.ARGB32);
            backSphericalTexture.name = "BackSpherical_Runtime";
            backSphericalTexture.Create();
        }
        
        // 최종 합성 텍스처 생성 (1920x928)
        if (finalComposedTexture == null)
        {
            finalComposedTexture = new RenderTexture(outputWidth, outputHeight, 0, RenderTextureFormat.ARGB32);
            finalComposedTexture.name = "FinalComposed_Runtime";
            finalComposedTexture.Create();
        }
        
        Debug.Log($"출력 텍스처 생성 완료 - 개별: {sphericalSize}x{outputHeight}, 최종: {outputWidth}x{outputHeight}");
        
        // Asset 폴더에서 볼 수 있도록 Asset 생성 (에디터에서만)
        #if UNITY_EDITOR
        CreateRenderTextureAssets();
        #endif
    }
    
    void LateUpdate()
    {
        if (!isInitialized) return;
        
        // 1단계: 각 카메라에 구형 왜곡 적용
        if (enableSphericalDistortion)
        {
            ApplySphericalDistortionToEach();
        }
        else
        {
            // 왜곡 없이 그대로 복사
            CopyWithoutDistortion();
        }
        
        // 디버그: 중간 텍스처 상태 로그 (5초마다)
        if (Time.frameCount % 300 == 0)
        {
            Debug.Log($"[FrontBackComposer] Front: {(frontSphericalTexture != null ? "OK" : "NULL")}, " +
                     $"Back: {(backSphericalTexture != null ? "OK" : "NULL")}, " +
                     $"Final: {(finalComposedTexture != null ? "OK" : "NULL")}");
        }
        
        // 2단계: 왜곡된 2개 이미지를 가로로 합성
        CombineHorizontally();
    }
    
    void ApplySphericalDistortionToEach()
    {
        Material distortionMaterial = useFisheyeInstead ? fisheyeMaterial : sphericalMaterial;
        if (distortionMaterial == null) return;
        
        // 왜곡 머티리얼 속성 업데이트
        UpdateDistortionMaterialProperties(distortionMaterial);
        
        // 전면 카메라에 구형 왜곡 적용
        if (frontRenderTexture != null && frontSphericalTexture != null)
        {
            distortionMaterial.SetVector("_Center", new Vector4(frontCenter.x, frontCenter.y, 0, 0));
            Graphics.Blit(frontRenderTexture, frontSphericalTexture, distortionMaterial);
        }
        
        // 후면 카메라에 구형 왜곡 적용
        if (backRenderTexture != null && backSphericalTexture != null)
        {
            distortionMaterial.SetVector("_Center", new Vector4(backCenter.x, backCenter.y, 0, 0));
            Graphics.Blit(backRenderTexture, backSphericalTexture, distortionMaterial);
        }
    }
    
    void CopyWithoutDistortion()
    {
        // 왜곡 없이 원본을 중간 텍스처로 복사
        if (frontRenderTexture != null && frontSphericalTexture != null)
        {
            Graphics.Blit(frontRenderTexture, frontSphericalTexture);
        }
        
        if (backRenderTexture != null && backSphericalTexture != null)
        {
            Graphics.Blit(backRenderTexture, backSphericalTexture);
        }
    }
    
    void CombineHorizontally()
    {
        if (finalComposedTexture == null)
        {
            Debug.LogWarning("finalComposedTexture가 null입니다!");
            return;
        }
        
        // 디버그: 입력 텍스처 상태 확인
        if (frontSphericalTexture == null)
        {
            Debug.LogWarning("frontSphericalTexture가 null입니다!");
        }
        
        if (backSphericalTexture == null)
        {
            Debug.LogWarning("backSphericalTexture가 null입니다!");
        }
        
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = finalComposedTexture;
        
        // 클리어
        GL.Clear(true, true, Color.black);
        
        // Material을 사용한 Blit 방식으로 변경
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, outputWidth, outputHeight, 0);
        
        // 좌측에 전면 카메라 (구형 왜곡된) 그리기
        if (frontSphericalTexture != null)
        {
            Graphics.DrawTexture(new Rect(0, 0, sphericalSize, outputHeight), frontSphericalTexture);
        }
        else
        {
            // 테스트용: 좌측을 파란색으로 채움
            GL.Begin(GL.QUADS);
            GL.Color(Color.blue);
            GL.Vertex3(0, 0, 0);
            GL.Vertex3(sphericalSize, 0, 0);
            GL.Vertex3(sphericalSize, outputHeight, 0);
            GL.Vertex3(0, outputHeight, 0);
            GL.End();
        }
        
        // 우측에 후면 카메라 (구형 왜곡된) 그리기
        if (backSphericalTexture != null)
        {
            Graphics.DrawTexture(new Rect(sphericalSize, 0, sphericalSize, outputHeight), backSphericalTexture);
        }
        else
        {
            // 테스트용: 우측을 빨간색으로 채움
            GL.Begin(GL.QUADS);
            GL.Color(Color.red);
            GL.Vertex3(sphericalSize, 0, 0);
            GL.Vertex3(outputWidth, 0, 0);
            GL.Vertex3(outputWidth, outputHeight, 0);
            GL.Vertex3(sphericalSize, outputHeight, 0);
            GL.End();
        }
        
        GL.PopMatrix();
        RenderTexture.active = previous;
    }
    
    void UpdateDistortionMaterialProperties(Material material)
    {
        if (material == null) return;
        
        if (material.shader.name == "Custom/SphericalDistortion")
        {
            material.SetFloat("_DistortionStrength", distortionStrength);
            material.SetFloat("_SphereRadius", sphereRadius);
        }
        else if (material.shader.name == "Custom/FisheyeDistortion")
        {
            material.SetFloat("_Strength", fisheyeStrength);
            material.SetFloat("_LensRadius", fisheyeRadius);
        }
    }
    
    // 공개 접근자 메서드들
    public RenderTexture GetFinalComposedTexture() => finalComposedTexture;      // 1920x928 최종 이미지
    public RenderTexture GetFrontSphericalTexture() => frontSphericalTexture;    // 전면 구형 왜곡
    public RenderTexture GetBackSphericalTexture() => backSphericalTexture;      // 후면 구형 왜곡
    public RenderTexture GetFrontOriginalTexture() => frontRenderTexture;        // 전면 원본
    public RenderTexture GetBackOriginalTexture() => backRenderTexture;          // 후면 원본
    
    // 설정 변경 메서드들
    public void SetSphericalDistortion(bool enabled)
    {
        enableSphericalDistortion = enabled;
    }
    
    public void SetDistortionStrength(float strength)
    {
        distortionStrength = Mathf.Clamp(strength, 0f, 3f);
    }
    
    public void SetSphereRadius(float radius)
    {
        sphereRadius = Mathf.Clamp(radius, 0.1f, 1f);
    }
    
    public void SetUseFisheye(bool useFisheye)
    {
        useFisheyeInstead = useFisheye;
    }
    
    public void SetFisheyeStrength(float strength)
    {
        fisheyeStrength = Mathf.Clamp(strength, 0f, 5f);
    }
    
    public void SetFrontCenter(Vector2 center)
    {
        frontCenter = center;
    }
    
    public void SetBackCenter(Vector2 center)
    {
        backCenter = center;
    }
    
    public void SetOutputSize(int width, int height)
    {
        if (outputWidth != width || outputHeight != height)
        {
            outputWidth = width;
            outputHeight = height;
            sphericalSize = width / 2; // 가로 절반씩
            
            // 텍스처 재생성
            if (isInitialized)
            {
                DestroyTextures();
                CreateOutputTextures();
            }
        }
    }
    
    void DestroyTextures()
    {
        if (frontSphericalTexture != null)
        {
            frontSphericalTexture.Release();
            DestroyImmediate(frontSphericalTexture);
            frontSphericalTexture = null;
        }
        
        if (backSphericalTexture != null)
        {
            backSphericalTexture.Release();
            DestroyImmediate(backSphericalTexture);
            backSphericalTexture = null;
        }
        
        if (finalComposedTexture != null)
        {
            finalComposedTexture.Release();
            DestroyImmediate(finalComposedTexture);
            finalComposedTexture = null;
        }
    }
    
    void OnDestroy()
    {
        // 메모리 정리
        if (sphericalMaterial != null)
            DestroyImmediate(sphericalMaterial);
        
        if (fisheyeMaterial != null)
            DestroyImmediate(fisheyeMaterial);
        
        if (horizontalComposeMaterial != null)
            DestroyImmediate(horizontalComposeMaterial);
        
        DestroyTextures();
    }
    
    // 에디터에서 실시간 변경사항 적용
    void OnValidate()
    {
        if (Application.isPlaying && isInitialized)
        {
            UpdateDistortionMaterialProperties(useFisheyeInstead ? fisheyeMaterial : sphericalMaterial);
        }
    }
    
    // 디버깅용 정보 출력
    [ContextMenu("시스템 상태 출력")]
    void PrintSystemStatus()
    {
        Debug.Log("=== FrontBackComposer 상태 ===");
        Debug.Log($"초기화: {isInitialized}");
        Debug.Log($"구형 왜곡 활성화: {enableSphericalDistortion}");
        Debug.Log($"어안렌즈 모드: {useFisheyeInstead}");
        Debug.Log($"출력 크기: {outputWidth}x{outputHeight}");
        Debug.Log($"개별 구형 크기: {sphericalSize}x{outputHeight}");
        Debug.Log($"최종 텍스처: {(finalComposedTexture != null ? "생성됨" : "없음")}");
        Debug.Log($"전면 입력: {(frontRenderTexture != null ? frontRenderTexture.name : "없음")}");
        Debug.Log($"후면 입력: {(backRenderTexture != null ? backRenderTexture.name : "없음")}");
    }
    
    #if UNITY_EDITOR
    public void CreateRenderTextureAssets()
    {
        // Asset 폴더에 RenderTexture 생성 (Preview용)
        string folderPath = "Assets/RenderTexture/Generated";
        
        // 폴더가 없으면 생성
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            string parentFolder = "Assets/RenderTexture";
            if (!AssetDatabase.IsValidFolder(parentFolder))
            {
                AssetDatabase.CreateFolder("Assets", "RenderTexture");
            }
            AssetDatabase.CreateFolder(parentFolder, "Generated");
        }
        
        // 전면 구형 텍스처 Asset 생성
        CreateRenderTextureAsset($"{folderPath}/FrontSphericalPreview.renderTexture", 
                                sphericalSize, outputHeight, "FrontSphericalPreview");
        
        // 후면 구형 텍스처 Asset 생성  
        CreateRenderTextureAsset($"{folderPath}/BackSphericalPreview.renderTexture", 
                                sphericalSize, outputHeight, "BackSphericalPreview");
        
        // 최종 합성 텍스처 Asset 생성
        CreateRenderTextureAsset($"{folderPath}/FinalComposedPreview.renderTexture", 
                                outputWidth, outputHeight, "FinalComposedPreview");
        
        AssetDatabase.Refresh();
        Debug.Log("RenderTexture Asset들이 Assets/RenderTexture/Generated/ 폴더에 생성되었습니다.");
    }
    
    void CreateRenderTextureAsset(string path, int width, int height, string assetName)
    {
        // 기존 파일이 있으면 삭제
        if (AssetDatabase.LoadAssetAtPath<RenderTexture>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }
        
        // 새 RenderTexture Asset 생성
        RenderTexture rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        rt.name = assetName;
        
        AssetDatabase.CreateAsset(rt, path);
    }
    
    [ContextMenu("Asset 폴더에 미리보기 텍스처 생성")]
    public void CreatePreviewTextures()
    {
        CreateRenderTextureAssets();
    }
    
    [ContextMenu("실행중인 텍스처를 Asset에 복사")]
    public void CopyRuntimeTexturesToAssets()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("시스템이 초기화되지 않았습니다.");
            return;
        }
        
        string folderPath = "Assets/RenderTexture/Generated";
        
        // 런타임 텍스처들을 Asset으로 복사
        if (frontSphericalTexture != null)
        {
            CopyTextureToAsset(frontSphericalTexture, $"{folderPath}/FrontSphericalPreview.renderTexture");
        }
        
        if (backSphericalTexture != null)
        {
            CopyTextureToAsset(backSphericalTexture, $"{folderPath}/BackSphericalPreview.renderTexture");
        }
        
        if (finalComposedTexture != null)
        {
            CopyTextureToAsset(finalComposedTexture, $"{folderPath}/FinalComposedPreview.renderTexture");
        }
        
        AssetDatabase.Refresh();
        Debug.Log("런타임 텍스처들이 Asset에 복사되었습니다!");
    }
    
    void CopyTextureToAsset(RenderTexture source, string assetPath)
    {
        RenderTexture target = AssetDatabase.LoadAssetAtPath<RenderTexture>(assetPath);
        if (target != null && source != null)
        {
            Graphics.Blit(source, target);
        }
    }
    #endif
}