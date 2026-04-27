using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RenderTexture를 UI Canvas에 실시간 표시
/// 빌드 후에도 작동, 더 안정적
/// </summary>
public class RenderTextureDisplay : MonoBehaviour
{
    [Header("UI 연결")]
    public RawImage displayImage;
    public Canvas displayCanvas;
    
    [Header("소스 설정")]
    public RenderTexture targetTexture;
    public SphereUVMapper sphereMapper;
    public SplitBackComposer splitComposer;
    
    [Header("표시 설정")]
    public bool showOnStart = true;
    public bool alwaysOnTop = true;
    
    [Header("핫키")]
    public KeyCode toggleKey = KeyCode.F1;
    
    private bool isVisible = true;
    
    void Start()
    {
        SetupCanvas();
        SetupDisplay();
        
        isVisible = showOnStart;
        UpdateVisibility();
    }
    
    void SetupCanvas()
    {
        // Canvas가 없으면 자동 생성
        if (displayCanvas == null)
        {
            GameObject canvasObj = new GameObject("RenderTexture_Canvas");
            displayCanvas = canvasObj.AddComponent<Canvas>();
            displayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            displayCanvas.sortingOrder = alwaysOnTop ? 9999 : 0;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasObj.AddComponent<GraphicRaycaster>();
            
            DontDestroyOnLoad(canvasObj);
        }
    }
    
    void SetupDisplay()
    {
        // RawImage가 없으면 자동 생성
        if (displayImage == null && displayCanvas != null)
        {
            GameObject imageObj = new GameObject("RenderTexture_Display");
            imageObj.transform.SetParent(displayCanvas.transform, false);
            
            displayImage = imageObj.AddComponent<RawImage>();
            
            // 전체화면 크기로 설정
            RectTransform rect = displayImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
    
    void Update()
    {
        // 핫키 처리
        if (Input.GetKeyDown(toggleKey))
        {
            isVisible = !isVisible;
            UpdateVisibility();
            Debug.Log($"RenderTexture 표시: {(isVisible ? "ON" : "OFF")}");
        }
        
        // 텍스처 자동 연결 (우선순위: SplitBackComposer > SphereUVMapper)
        if (targetTexture == null)
        {
            if (splitComposer != null)
            {
                targetTexture = splitComposer.GetComposedTexture();
            }
            else if (sphereMapper != null)
            {
                targetTexture = sphereMapper.GetSphereUVTexture();
            }
        }
        
        // RawImage에 텍스처 연결
        if (displayImage != null && targetTexture != null)
        {
            if (displayImage.texture != targetTexture)
            {
                displayImage.texture = targetTexture;
                Debug.Log($"RenderTexture 연결됨: {targetTexture.width}x{targetTexture.height}");
            }
        }
    }
    
    void UpdateVisibility()
    {
        if (displayCanvas != null)
        {
            displayCanvas.enabled = isVisible;
        }
    }
    
    // 공개 메서드
    public void Show()
    {
        isVisible = true;
        UpdateVisibility();
    }
    
    public void Hide()
    {
        isVisible = false;
        UpdateVisibility();
    }
    
    public void Toggle()
    {
        isVisible = !isVisible;
        UpdateVisibility();
    }
    
    public void SetTexture(RenderTexture texture)
    {
        targetTexture = texture;
        if (displayImage != null)
        {
            displayImage.texture = texture;
        }
    }
}

