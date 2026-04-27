using UnityEngine;

/// <summary>
/// 구체 외부에서 볼 수 있도록 카메라 영상을 구체 표면에 매핑
/// 3D 구체 모델을 사용해 표면 텍스처 생성
/// </summary>
public class SphereSurfaceMapper : MonoBehaviour
{
    [Header("카메라 설정")]
    public Camera frontCamera;
    public Camera backCamera;
    
    [Header("입력 렌더텍스처")]
    public RenderTexture frontRenderTexture;
    public RenderTexture backRenderTexture;
    
    [Header("출력 설정")]
    public RenderTexture sphereSurfaceTexture;  // 구체 표면 UV 텍스처
    [SerializeField] private int textureWidth = 2048;
    [SerializeField] private int textureHeight = 2048;
    
    [Header("3D 구체 설정")]
    [SerializeField] private GameObject sphereModel;  // 구체 3D 모델
    [SerializeField] private Camera renderCamera;     // 구체를 렌더링할 카메라
    
    private Material sphereMaterial;
    private bool isInitialized = false;
    
    void Start()
    {
        InitializeSystem();
    }
    
    void InitializeSystem()
    {
        if (isInitialized) return;
        
        SetupCameras();
        CreateSphereModel();
        CreateRenderCamera();
        CreateOutputTexture();
        
        isInitialized = true;
        Debug.Log("SphereSurfaceMapper 초기화 완료");
    }
    
    void SetupCameras()
    {
        if (frontCamera != null && frontRenderTexture != null)
        {
            frontCamera.targetTexture = frontRenderTexture;
            frontCamera.enabled = true;
        }
        
        if (backCamera != null && backRenderTexture != null)
        {
            backCamera.targetTexture = backRenderTexture;
            backCamera.enabled = true;
        }
    }
    
    void CreateSphereModel()
    {
        if (sphereModel == null)
        {
            // 3D 구체 생성
            sphereModel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereModel.name = "SphereLED_Model";
            sphereModel.transform.parent = transform;
            sphereModel.transform.localPosition = Vector3.zero;
            sphereModel.transform.localScale = Vector3.one * 10f; // 크기 조절
            
            // 레이어 설정 (다른 카메라에 안 보이게)
            sphereModel.layer = LayerMask.NameToLayer("Default");
        }
        
        // 셰이더 머티리얼 생성
        CreateSphereMaterial();
    }
    
    void CreateSphereMaterial()
    {
        // Unlit 셰이더 사용 (조명 영향 없이 텍스처만 표시)
        Shader unlitShader = Shader.Find("Unlit/Texture");
        sphereMaterial = new Material(unlitShader);
        
        // 구체 렌더러에 머티리얼 적용
        Renderer renderer = sphereModel.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = sphereMaterial;
        }
    }
    
    void CreateRenderCamera()
    {
        if (renderCamera == null)
        {
            // 새 카메라 생성
            GameObject camObj = new GameObject("SphereRenderCamera");
            camObj.transform.parent = transform;
            camObj.transform.localPosition = new Vector3(0, 0, -20f);
            camObj.transform.LookAt(sphereModel.transform);
            
            renderCamera = camObj.AddComponent<Camera>();
            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = Color.black;
            renderCamera.cullingMask = 1 << sphereModel.layer;
            renderCamera.enabled = false; // 수동 렌더링
        }
    }
    
    void CreateOutputTexture()
    {
        if (sphereSurfaceTexture == null)
        {
            sphereSurfaceTexture = new RenderTexture(
                textureWidth, 
                textureHeight, 
                24, 
                RenderTextureFormat.ARGB32
            );
            sphereSurfaceTexture.name = "SphereSurface_Output";
        }
        
        // 렌더 카메라의 출력 타겟 설정
        if (renderCamera != null)
        {
            renderCamera.targetTexture = sphereSurfaceTexture;
        }
    }
    
    void LateUpdate()
    {
        if (!isInitialized) return;
        
        UpdateSphereTexture();
        RenderSphereView();
    }
    
    void UpdateSphereTexture()
    {
        // 구체 머티리얼에 카메라 텍스처 적용
        // 여기서는 전면 텍스처만 예시로 사용
        // 실제로는 멀티 텍스처 셰이더 필요
        if (sphereMaterial != null && frontRenderTexture != null)
        {
            sphereMaterial.mainTexture = frontRenderTexture;
        }
    }
    
    void RenderSphereView()
    {
        if (renderCamera != null)
        {
            renderCamera.Render();
        }
    }
    
    // 공개 접근자
    public RenderTexture GetSphereSurfaceTexture() => sphereSurfaceTexture;
    
    void OnDestroy()
    {
        if (sphereModel != null) Destroy(sphereModel);
        if (renderCamera != null) Destroy(renderCamera.gameObject);
        if (sphereMaterial != null) DestroyImmediate(sphereMaterial);
        if (sphereSurfaceTexture != null) sphereSurfaceTexture.Release();
    }
}


