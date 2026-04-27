using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 게임 관리자 UI를 동적으로 생성하고 관리하는 컨트롤러
/// </summary>
public class AdminUIController : MonoBehaviour
{
    [Header("UI 설정")]
    [SerializeField] private Canvas mainCanvas;
    [SerializeField] private Font defaultFont;
    
    // UI 컴포넌트들
    private TMP_Text connectionStatusText;
    private TMP_Text roomStatusText;
    private TMP_Text redTeamCountText;
    private TMP_Text blueTeamCountText;
    private TMP_Text redTeamScoreText;
    private TMP_Text blueTeamScoreText;
    private TMP_Text gameTimerText;
    private TMP_Text winnerText;
    private TMP_Text logText;
    private Button startGameButton;
    private Button resetGameButton;
    private Button reconnectButton;
    private ScrollRect logScrollRect;
    
    // 패널들
    private GameObject waitingPanel;
    private GameObject playingPanel;
    private GameObject finishedPanel;
    
    private GameAdminManager adminManager;

    void Start()
    {
        CreateAdminUI();
        
        // GameAdminManager 찾기 또는 생성
        adminManager = FindObjectOfType<GameAdminManager>();
        if (adminManager == null)
        {
            GameObject adminObject = new GameObject("GameAdminManager");
            adminManager = adminObject.AddComponent<GameAdminManager>();
        }
        
        // GameAdminManager에 UI 컴포넌트들 할당
        SetupAdminManager();
    }

    private void CreateAdminUI()
    {
        // 메인 캔버스가 없으면 생성
        if (mainCanvas == null)
        {
            GameObject canvasObject = new GameObject("AdminCanvas");
            mainCanvas = canvasObject.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        // 메인 패널 생성
        GameObject mainPanel = CreatePanel(mainCanvas.transform, "MainPanel", new Vector2(0, 0), new Vector2(1, 1));
        
        // 배경 이미지 설정
        Image mainBg = mainPanel.GetComponent<Image>();
        mainBg.color = new Color(0.1f, 0.1f, 0.2f, 0.95f);

        // 제목 생성
        CreateTitle(mainPanel.transform);
        
        // 연결 상태 패널
        CreateConnectionPanel(mainPanel.transform);
        
        // 게임 정보 패널
        CreateGameInfoPanel(mainPanel.transform);
        
        // 컨트롤 패널
        CreateControlPanel(mainPanel.transform);
        
        // 로그 패널
        CreateLogPanel(mainPanel.transform);
    }

    private void CreateTitle(Transform parent)
    {
        GameObject titleObject = new GameObject("Title");
        titleObject.transform.SetParent(parent, false);
        
        TMP_Text titleText = titleObject.AddComponent<TextMeshProUGUI>();
        titleText.text = "🎮 Winter Road 게임 관리자";
        titleText.fontSize = 28;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontStyle = FontStyles.Bold;
        
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.9f);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.offsetMin = new Vector2(20, 0);
        titleRect.offsetMax = new Vector2(-20, -10);
    }

    private void CreateConnectionPanel(Transform parent)
    {
        GameObject connectionPanel = CreatePanel(parent, "ConnectionPanel", new Vector2(0, 0.8f), new Vector2(1, 0.9f));
        connectionPanel.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 0.8f);
        
        // 연결 상태 텍스트
        GameObject statusObject = new GameObject("ConnectionStatus");
        statusObject.transform.SetParent(connectionPanel.transform, false);
        
        connectionStatusText = statusObject.AddComponent<TextMeshProUGUI>();
        connectionStatusText.text = "서버 연결 상태: 연결 중...";
        connectionStatusText.fontSize = 16;
        connectionStatusText.color = Color.yellow;
        connectionStatusText.alignment = TextAlignmentOptions.Center;
        
        RectTransform statusRect = statusObject.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 0);
        statusRect.anchorMax = new Vector2(0.8f, 1);
        statusRect.offsetMin = new Vector2(10, 5);
        statusRect.offsetMax = new Vector2(-10, -5);
        
        // 재연결 버튼
        reconnectButton = CreateButton(connectionPanel.transform, "ReconnectButton", "재연결", 
            new Vector2(0.8f, 0.2f), new Vector2(0.95f, 0.8f));
        reconnectButton.onClick.AddListener(() => {
            if (adminManager != null) adminManager.Reconnect();
        });
    }

    private void CreateGameInfoPanel(Transform parent)
    {
        GameObject gameInfoPanel = CreatePanel(parent, "GameInfoPanel", new Vector2(0, 0.3f), new Vector2(1, 0.8f));
        gameInfoPanel.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.25f, 0.9f);
        
        // 방 상태
        CreateInfoText(gameInfoPanel.transform, "RoomStatus", "방 상태: 대기 중", 
            new Vector2(0, 0.85f), new Vector2(1, 1), out roomStatusText);
        
        // 팀 정보 패널
        CreateTeamInfoPanel(gameInfoPanel.transform);
        
        // 타이머 패널
        CreateTimerPanel(gameInfoPanel.transform);
        
        // 승자 표시 패널
        CreateWinnerPanel(gameInfoPanel.transform);
    }

    private void CreateTeamInfoPanel(Transform parent)
    {
        // 레드팀 패널
        GameObject redTeamPanel = CreatePanel(parent, "RedTeamPanel", new Vector2(0, 0.5f), new Vector2(0.48f, 0.85f));
        redTeamPanel.GetComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f, 0.3f);
        
        CreateInfoText(redTeamPanel.transform, "RedTeamTitle", "🔴 레드팀", 
            new Vector2(0, 0.75f), new Vector2(1, 1), out var redTitle);
        redTitle.alignment = TextAlignmentOptions.Center;
        redTitle.fontStyle = FontStyles.Bold;
        
        CreateInfoText(redTeamPanel.transform, "RedTeamCount", "플레이어: 0명", 
            new Vector2(0, 0.5f), new Vector2(1, 0.75f), out redTeamCountText);
        
        CreateInfoText(redTeamPanel.transform, "RedTeamScore", "점수: 0", 
            new Vector2(0, 0.25f), new Vector2(1, 0.5f), out redTeamScoreText);
        
        // 블루팀 패널
        GameObject blueTeamPanel = CreatePanel(parent, "BlueTeamPanel", new Vector2(0.52f, 0.5f), new Vector2(1, 0.85f));
        blueTeamPanel.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.8f, 0.3f);
        
        CreateInfoText(blueTeamPanel.transform, "BlueTeamTitle", "🔵 블루팀", 
            new Vector2(0, 0.75f), new Vector2(1, 1), out var blueTitle);
        blueTitle.alignment = TextAlignmentOptions.Center;
        blueTitle.fontStyle = FontStyles.Bold;
        
        CreateInfoText(blueTeamPanel.transform, "BlueTeamCount", "플레이어: 0명", 
            new Vector2(0, 0.5f), new Vector2(1, 0.75f), out blueTeamCountText);
        
        CreateInfoText(blueTeamPanel.transform, "BlueTeamScore", "점수: 0", 
            new Vector2(0, 0.25f), new Vector2(1, 0.5f), out blueTeamScoreText);
    }

    private void CreateTimerPanel(Transform parent)
    {
        playingPanel = CreatePanel(parent, "TimerPanel", new Vector2(0, 0.25f), new Vector2(1, 0.5f));
        playingPanel.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.1f, 0.5f);
        playingPanel.SetActive(false);
        
        CreateInfoText(playingPanel.transform, "TimerTitle", "⏰ 남은 시간", 
            new Vector2(0, 0.6f), new Vector2(1, 1), out var timerTitle);
        timerTitle.alignment = TextAlignmentOptions.Center;
        timerTitle.fontStyle = FontStyles.Bold;
        
        CreateInfoText(playingPanel.transform, "GameTimer", "05:00", 
            new Vector2(0, 0), new Vector2(1, 0.6f), out gameTimerText);
        gameTimerText.alignment = TextAlignmentOptions.Center;
        gameTimerText.fontSize = 36;
        gameTimerText.color = Color.yellow;
        gameTimerText.fontStyle = FontStyles.Bold;
    }

    private void CreateWinnerPanel(Transform parent)
    {
        finishedPanel = CreatePanel(parent, "WinnerPanel", new Vector2(0, 0.25f), new Vector2(1, 0.5f));
        finishedPanel.GetComponent<Image>().color = new Color(0.1f, 0.3f, 0.1f, 0.7f);
        finishedPanel.SetActive(false);
        
        CreateInfoText(finishedPanel.transform, "WinnerTitle", "🏆 게임 결과", 
            new Vector2(0, 0.6f), new Vector2(1, 1), out var winnerTitle);
        winnerTitle.alignment = TextAlignmentOptions.Center;
        winnerTitle.fontStyle = FontStyles.Bold;
        
        CreateInfoText(finishedPanel.transform, "WinnerText", "결과 대기 중...", 
            new Vector2(0, 0), new Vector2(1, 0.6f), out winnerText);
        winnerText.alignment = TextAlignmentOptions.Center;
        winnerText.fontSize = 20;
        winnerText.color = new Color(1f, 0.84f, 0f); // Gold color
        winnerText.fontStyle = FontStyles.Bold;
    }

    private void CreateControlPanel(Transform parent)
    {
        GameObject controlPanel = CreatePanel(parent, "ControlPanel", new Vector2(0, 0.2f), new Vector2(1, 0.3f));
        controlPanel.GetComponent<Image>().color = new Color(0.2f, 0.3f, 0.2f, 0.8f);
        
        // 게임 시작 버튼
        startGameButton = CreateButton(controlPanel.transform, "StartButton", "게임 시작", 
            new Vector2(0.1f, 0.2f), new Vector2(0.45f, 0.8f));
        startGameButton.GetComponent<Image>().color = new Color(0.2f, 0.8f, 0.2f, 1f);
        
        // 게임 리셋 버튼
        resetGameButton = CreateButton(controlPanel.transform, "ResetButton", "게임 리셋", 
            new Vector2(0.55f, 0.2f), new Vector2(0.9f, 0.8f));
        resetGameButton.GetComponent<Image>().color = new Color(0.8f, 0.4f, 0.2f, 1f);
        
        // 대기 상태 패널 (빈 공간으로 활용)
        waitingPanel = CreatePanel(parent, "WaitingPanel", new Vector2(0, 0.25f), new Vector2(1, 0.5f));
        waitingPanel.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.3f, 0.3f);
        
        CreateInfoText(waitingPanel.transform, "WaitingText", "게임 시작을 기다리는 중...", 
            new Vector2(0, 0), new Vector2(1, 1), out var waitingText);
        waitingText.alignment = TextAlignmentOptions.Center;
        waitingText.fontSize = 18;
        waitingText.color = new Color(0.8f, 0.8f, 0.8f, 0.7f);
        waitingText.fontStyle = FontStyles.Italic;
    }

    private void CreateLogPanel(Transform parent)
    {
        GameObject logPanel = CreatePanel(parent, "LogPanel", new Vector2(0, 0), new Vector2(1, 0.2f));
        logPanel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.9f);
        
        // 로그 제목
        CreateInfoText(logPanel.transform, "LogTitle", "📝 시스템 로그", 
            new Vector2(0, 0.8f), new Vector2(1, 1), out var logTitle);
        logTitle.alignment = TextAlignmentOptions.Center;
        logTitle.fontSize = 14;
        logTitle.fontStyle = FontStyles.Bold;
        
        // 스크롤 가능한 로그 영역
        GameObject scrollObject = new GameObject("LogScrollView");
        scrollObject.transform.SetParent(logPanel.transform, false);
        
        logScrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);
        
        RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 0.8f);
        scrollRect.offsetMin = new Vector2(10, 5);
        scrollRect.offsetMax = new Vector2(-10, -5);
        
        // 로그 내용
        GameObject logContent = new GameObject("LogContent");
        logContent.transform.SetParent(scrollObject.transform, false);
        
        logText = logContent.AddComponent<TextMeshProUGUI>();
        logText.text = "[시스템] 게임 관리자 UI 초기화 완료";
        logText.fontSize = 12;
        logText.color = Color.white;
        logText.alignment = TextAlignmentOptions.TopLeft;
        
        RectTransform logContentRect = logContent.GetComponent<RectTransform>();
        logContentRect.anchorMin = new Vector2(0, 0);
        logContentRect.anchorMax = new Vector2(1, 1);
        logContentRect.offsetMin = Vector2.zero;
        logContentRect.offsetMax = Vector2.zero;
        
        logScrollRect.content = logContentRect;
        logScrollRect.vertical = true;
        logScrollRect.horizontal = false;
    }

    private GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(5, 5);
        rect.offsetMax = new Vector2(-5, -5);
        
        return panel;
    }

    private Button CreateButton(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent, false);
        
        Button button = buttonObject.AddComponent<Button>();
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.3f, 0.8f, 1f);
        
        // 버튼 텍스트
        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(buttonObject.transform, false);
        
        TMP_Text buttonText = textObject.AddComponent<TextMeshProUGUI>();
        buttonText.text = text;
        buttonText.fontSize = 14;
        buttonText.color = Color.white;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.fontStyle = FontStyles.Bold;
        
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = anchorMin;
        buttonRect.anchorMax = anchorMax;
        buttonRect.offsetMin = new Vector2(5, 5);
        buttonRect.offsetMax = new Vector2(-5, -5);
        
        return button;
    }

    private void CreateInfoText(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, out TMP_Text textComponent)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        
        textComponent = textObject.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = 14;
        textComponent.color = Color.white;
        // textComponent.alignment = TextAlignmentOptions.MiddleCenter;
        
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = anchorMin;
        textRect.anchorMax = anchorMax;
        textRect.offsetMin = new Vector2(10, 5);
        textRect.offsetMax = new Vector2(-10, -5);
    }

    private void SetupAdminManager()
    {
        if (adminManager == null) return;

        // UI 컴포넌트들을 GameAdminManager에 직접 할당
        adminManager.roomStatusText = roomStatusText;
        adminManager.redTeamCountText = redTeamCountText;
        adminManager.blueTeamCountText = blueTeamCountText;
        adminManager.redTeamScoreText = redTeamScoreText;
        adminManager.blueTeamScoreText = blueTeamScoreText;
        adminManager.gameTimerText = gameTimerText;
        adminManager.winnerText = winnerText;
        adminManager.startGameButton = startGameButton;
        adminManager.resetGameButton = resetGameButton;
        adminManager.connectionStatusText = connectionStatusText;
        adminManager.logText = logText;
        adminManager.waitingPanel = waitingPanel;
        adminManager.playingPanel = playingPanel;
        adminManager.finishedPanel = finishedPanel;
        
        Debug.Log("GameAdminManager에 UI 컴포넌트가 할당되었습니다.");
    }
}
