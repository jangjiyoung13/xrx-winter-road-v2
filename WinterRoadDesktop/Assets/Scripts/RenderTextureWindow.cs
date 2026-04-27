using UnityEngine;

/// <summary>
/// 빌드 후 RenderTexture를 별도 창에 실시간으로 표시
/// OnGUI를 사용한 오버레이 방식
/// </summary>
public class RenderTextureWindow : MonoBehaviour
{
    [Header("표시할 RenderTexture")]
    public RenderTexture targetTexture;
    
    [Header("창 설정")]
    public bool showWindow = true;
    [SerializeField] private bool fullscreen = false;
    [SerializeField] private int windowX = 0;
    [SerializeField] private int windowY = 0;
    [SerializeField] private int windowWidth = 1920;
    [SerializeField] private int windowHeight = 928;
    
    [Header("자동 소스 연결")]
    public SphereUVMapper sphereMapper;
    
    [Header("핫키 설정")]
    public KeyCode toggleKey = KeyCode.F1;
    public KeyCode fullscreenKey = KeyCode.F11;
    
    void Update()
    {
        // 핫키 처리
        if (Input.GetKeyDown(toggleKey))
        {
            showWindow = !showWindow;
            Debug.Log($"RenderTexture 창: {(showWindow ? "표시" : "숨김")}");
        }
        
        if (Input.GetKeyDown(fullscreenKey))
        {
            fullscreen = !fullscreen;
            Debug.Log($"전체화면: {(fullscreen ? "ON" : "OFF")}");
        }
        
        // 자동으로 텍스처 가져오기
        if (targetTexture == null && sphereMapper != null)
        {
            targetTexture = sphereMapper.GetSphereUVTexture();
        }
    }
    
    void OnGUI()
    {
        if (!showWindow || targetTexture == null) return;
        
        Rect windowRect;
        
        if (fullscreen)
        {
            // 전체화면 모드
            windowRect = new Rect(0, 0, Screen.width, Screen.height);
        }
        else
        {
            // 창 모드
            windowRect = new Rect(windowX, windowY, windowWidth, windowHeight);
        }
        
        // RenderTexture 그리기
        GUI.DrawTexture(windowRect, targetTexture, ScaleMode.StretchToFill);
        
        // 정보 표시
        DrawInfo();
    }
    
    void DrawInfo()
    {
        // 작은 정보 패널
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 12;
        style.normal.textColor = Color.white;
        
        string info = $"{toggleKey}: 창 On/Off | {fullscreenKey}: 전체화면 | ";
        if (targetTexture != null)
        {
            info += $"{targetTexture.width}x{targetTexture.height}";
        }
        
        GUI.Box(new Rect(10, 10, 400, 30), info, style);
    }
    
    // 공개 메서드
    public void SetTexture(RenderTexture texture)
    {
        targetTexture = texture;
    }
    
    public void ToggleWindow()
    {
        showWindow = !showWindow;
    }
    
    public void ToggleFullscreen()
    {
        fullscreen = !fullscreen;
    }
}


