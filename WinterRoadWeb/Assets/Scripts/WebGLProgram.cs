using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HybridWebSocket;
using System.Text;
using UnityEngine.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

// JSON 메시지 구조 클래스들 (Newtonsoft.Json용 - Unity Serializable 제거)
public class BaseMessage
{
    public string type;
}

public class JoinedRoomMessage : BaseMessage
{
    public JoinedRoomData data;
}

public class JoinedRoomData
{
    public string playerId;
    public PlayerInfo player;
    public RoomInfo room;
    public PlayerInfo[] players; // 전체 플레이어 목록
}

public class PlayerInfo
{
    public string id;
    public string name;
    public string team;
    public string element; // 선택한 원소 (Joy, Sadness, Courage, Love, Hope, Friendship)
    public int score;
    public int pressCount;
    public long lastPressTime;
    public bool connected;
    public bool isAdmin;
    public bool isObserver; // 서버에서 추가로 보내주는 필드
}

public class RoomInfo
{
    public string id;
    public string status;
    public int remainingTime;
    public TeamData teams;
    public string qrCodeUrl;
}

public class TeamData
{
    public TeamInfo Red;
    public TeamInfo Blue;
}

public class TeamInfo
{
    public int score;
    public int playerCount;
}

public class PlayerJoinedMessage : BaseMessage
{
    public PlayerJoinedData data;
}

public class PlayerJoinedData
{
    public string playerId;
    public string playerName;
    public string team;
    public TeamData teams;
}

public class PlayerLeftMessage : BaseMessage
{
    public PlayerLeftData data;
}

public class PlayerLeftData
{
    public string playerId;
    public string playerName;
    public string team;
    public TeamData teams;
}

public class GameStateUpdateMessage : BaseMessage
{
    public string status;
    public int remainingTime;
    public TeamData teams;
    public PlayerCountData playerCount;
}

public class PlayerCountData
{
    public int active;
    public int observers;
}

public class ScoreUpdateMessage : BaseMessage
{
    public TeamData teams;
    public ScoreUpdateData data;
}

public class ScoreUpdateData
{
    public PlayerInfo[] players;  // 서버에서 보내는 전체 플레이어 배열
    public LastPressInfo lastPress;
    public PlayerUpdateInfo playerUpdate;  // 레거시 호환용
}

public class LastPressInfo
{
    public string playerId;
    public string playerName;
    public string team;
    public int playerScore;
    public int teamScore;
    public long timestamp;
}

public class PlayerUpdateInfo
{
    public string playerId;
    public string playerName;
    public string team;
    public int newScore;
    public int newPressCount;
}

public class TimeUpdateMessage : BaseMessage
{
    public int remainingTime;
}

public class GameStartMessage : BaseMessage
{
    public int gameDuration;
    public GameStartData data;
}

public class GameStartData
{
    public long startTime;
    public int duration;
    public TeamData teams;
    public PlayerInfo[] players;
}

public class GameEndMessage : BaseMessage
{
    public string winner;
    public TeamData finalScores;
    public GameEndData data;
}

public class GameEndData
{
    public WinnerData winner;
    public FinalScoreData finalScores;
    public TeamData teams;
    public PlayerRankData[] rankings;
    public bool returnToNickname;
}

public class WinnerData
{
    public string nickname;
    public int score;
    public string playerId;
}

public class FinalScoreData
{
    public int Red;
    public int Blue;
}

public class ErrorMessage : BaseMessage
{
    public string message;
    public string code;
}

public class CountdownStartMessage : BaseMessage
{
    public int countdown;
}

public class CountdownUpdateMessage : BaseMessage
{
    public int countdown;
}

public class RoomInfoMessage : BaseMessage
{
    public RoomInfoData data;
}

public class RoomInfoData
{
    public RoomInfo room;
    public PlayerInfo player;
}

public class WebGLProgram : MonoBehaviour
{
    [Header("WebSocket Settings")]
    public string serverUrl = "ws://localhost:3000";
    
    // NOTE: playerId는 서버에서 생성/관리 (클라이언트 캐싱 제거 - 개인전)
    
    // 안전한 JSON 파싱 헬퍼 메서드
    private T SafeJsonDeserialize<T>(string json) where T : class
    {
        try
        {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            try 
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (System.Exception)
            {
                return JsonUtility.FromJson<T>(json);
            }
#else
            return JsonUtility.FromJson<T>(json);
#endif
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ JSON 파싱 실패: {ex.Message}");
            return null;
        }
    }
    
    [Header("Player Settings")]
    public string playerName = "Player"; // 서버에서 받은 playerId로 업데이트됨
    private string cachedPlayerId = ""; // 결과 화면용 플레이어 ID 캐시
    private const string DEFAULT_ROOM_ID = "main_room"; // 고정된 룸 ID

    private IWebSocket webSocket;
    private bool isConnected = false;
    private bool isInGame = false;
    private int remainingTime = 0;
    
    // WebSocket 재연결 설정
    private bool isReconnecting = false;
    private float reconnectDelay = 1f;
    private const float MAX_RECONNECT_DELAY = 30f;
    private const float INITIAL_RECONNECT_DELAY = 1f;
    private Coroutine reconnectCoroutine = null;

    // 세션 복구 (서버 grace period 재연결)
    private bool isAwaitingReconnect = false;
    private const float RECONNECT_RESPONSE_TIMEOUT = 3f;
    
    /// <summary>
    /// 플레이어 닉네임 설정 (NickNamePanel에서 호출)
    /// </summary>
    public void SetPlayerNickname(string nickname)
    {
        if (!string.IsNullOrEmpty(nickname))
        {
            playerName = nickname.Trim();
            Debug.Log($"🏷️ 플레이어 닉네임 설정: {playerName}");
        }
        else
        {
            Debug.LogWarning("⚠️ 빈 닉네임은 설정할 수 없습니다.");
        }
    }
    
    // Thread-safe flags for handling WebSocket events
    private bool shouldHandleOpen = false;
    private Queue<string> pendingMessages = new Queue<string>();
    private readonly object messageLock = new object();
    
    // Unity 메인 스레드 전용 메시지 큐 (WebGL 안전성)
    private Queue<string> mainThreadMessageQueue = new Queue<string>();

    // pressButton은 GamePanel에서 관리 - 아래 프로퍼티로 접근
    private Button pressButton => gamePanel != null ? gamePanel.PressButton : null;
    
    // NOTE: joinButton 제거 - 대기방 입장은 NickNamePanel에서 관리

    [Header("Debug")]
    public string testMessage = "Hello Server!";
    
    [Header("Game Panel")]
    [SerializeField] private GamePanel gamePanel;
    
    [Header("Nickname Panel")]
    [SerializeField] private NickNamePanel nickNamePanel;
    
    [Header("Intro Panel")]
    [SerializeField] private GameObject introPanel;

    [Header("Loading Panel")]
    [SerializeField] private GameObject loadingPanel;

    [Header("Waiting Room Panel")]
    [SerializeField] private WaitingRoomPanel waitingRoomPanel;

    [Header("Result Panel")]
    [SerializeField] private ResultPanel resultPanel;
    
    [Header("Crystal Ball")]
    [SerializeField] private CrystalBall crystalBall;

    [Header("Frost Camera Effect")]
    [SerializeField] private FrostEffect frostCameraEffect;
    // 게임 종료 연출 중 플래그 (5초 딜레이 연출 동안 true)
    private bool isShowingGameEndSequence = false;
    // Frost 효과 감소 코루틴 관리
    private Coroutine frostDecreaseCoroutine = null;

    // Start is called before the first frame update
    void Start()
    {
        // 모든 패널을 먼저 닫기
        HideAllPanels();

        // 로딩 패널 표시 (서버 접속 완료까지)
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            Debug.Log("⏳ LoadingPanel 표시 - 서버 접속 대기 중");
        }

        InitializeUI();
        ResolveServerUrl(); // 하드코딩 URL 대신 브라우저 URL에서 자동 추출
        InitializeWebSocket();

        // 대기방 입장은 NickNamePanel에서 관리

        // Attempt to auto-connect after 3 seconds (for testing)
        Invoke("AutoConnect", 3f);
        
        // Test ping after 5 seconds to verify connection
        Invoke("TestPing", 5f);
    }

    // Player ID Management 제거 (서버에서 playerId 생성/관리)

    #region WebGL LocalStorage & Vibration (WebGL 빌드 전용)

#if UNITY_WEBGL && !UNITY_EDITOR
    
    // WebGL용 LocalStorage JavaScript 인터페이스
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void SaveToLocalStorageInternal(string key, string value);
    
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern string GetFromLocalStorageInternal(string key);
    
    // WebGL용 Vibration API JavaScript 인터페이스
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern int VibrateInternal(int milliseconds);
    
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern int VibrateSecondsInternal(float seconds);
    
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern int VibratePatternInternal(int[] pattern, int patternLength);
    
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern int StopVibrationInternal();
    
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern int IsVibrationSupportedInternal();
    
    // 브라우저 URL에서 WebSocket 서버 주소 추출
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern string GetServerUrlFromBrowser();
    
    // WebGL LocalStorage 헬퍼 메서드들
    private void SaveToLocalStorage(string key, string value)
    {
        try
        {
            SaveToLocalStorageInternal(key, value);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ LocalStorage 저장 실패: {e.Message}");
        }
    }
    
    private string GetFromLocalStorage(string key)
    {
        try
        {
            return GetFromLocalStorageInternal(key);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ LocalStorage 읽기 실패: {e.Message}");
            return "";
        }
    }
    
#endif

    #endregion

    #region Vibration API (WebGL & 크로스 플랫폼)
    
    /// <summary>
    /// 진동을 밀리초 단위로 실행합니다.
    /// </summary>
    /// <param name="milliseconds">진동 지속 시간 (밀리초)</param>
    /// <returns>성공 여부</returns>
    public bool Vibrate(int milliseconds)
    {
        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            int result = VibrateInternal(milliseconds);
            Debug.Log($"📳 WebGL 진동 실행: {milliseconds}ms, 결과: {result == 1}");
            return result == 1;
#elif UNITY_ANDROID && !UNITY_EDITOR
            // 안드로이드 진동 (Handheld.Vibrate 사용)
            Handheld.Vibrate();
            Debug.Log($"📳 Android 진동 실행: {milliseconds}ms");
            return true;
#elif UNITY_IOS && !UNITY_EDITOR
            // iOS 진동 (Handheld.Vibrate 사용)
            Handheld.Vibrate();
            Debug.Log($"📳 iOS 진동 실행: {milliseconds}ms");
            return true;
#else
            Debug.LogWarning($"⚠️ 현재 플랫폼에서는 진동이 지원되지 않습니다: {milliseconds}ms");
            return false;
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 진동 실행 실패: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 진동을 초 단위로 실행합니다.
    /// </summary>
    /// <param name="seconds">진동 지속 시간 (초)</param>
    /// <returns>성공 여부</returns>
    public bool VibrateSeconds(float seconds)
    {
        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            int result = VibrateSecondsInternal(seconds);
            Debug.Log($"📳 WebGL 진동 실행: {seconds}초, 결과: {result == 1}");
            return result == 1;
#else
            // 다른 플랫폼에서는 밀리초로 변환하여 사용
            return Vibrate(Mathf.RoundToInt(seconds * 1000));
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 진동 실행 실패: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 진동 패턴을 실행합니다. (진동-멈춤-진동-멈춤... 순서)
    /// </summary>
    /// <param name="pattern">진동 패턴 배열 (밀리초 단위)</param>
    /// <returns>성공 여부</returns>
    public bool VibratePattern(int[] pattern)
    {
        try
        {
            if (pattern == null || pattern.Length == 0)
            {
                Debug.LogWarning("⚠️ 진동 패턴이 비어있습니다.");
                return false;
            }
            
#if UNITY_WEBGL && !UNITY_EDITOR
            int result = VibratePatternInternal(pattern, pattern.Length);
            Debug.Log($"📳 WebGL 진동 패턴 실행: [{string.Join(", ", pattern)}]ms, 결과: {result == 1}");
            return result == 1;
#else
            Debug.LogWarning("⚠️ 진동 패턴은 WebGL에서만 지원됩니다. 단일 진동으로 대체됩니다.");
            // 첫 번째 진동만 실행
            return Vibrate(pattern[0]);
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 진동 패턴 실행 실패: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 현재 진동을 중단합니다.
    /// </summary>
    /// <returns>성공 여부</returns>
    public bool StopVibration()
    {
        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            int result = StopVibrationInternal();
            Debug.Log($"📳 WebGL 진동 중단, 결과: {result == 1}");
            return result == 1;
#else
            Debug.Log("📳 다른 플랫폼에서는 진동 중단 기능이 제한적입니다.");
            return true;
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 진동 중단 실패: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 진동 API 지원 여부를 확인합니다.
    /// </summary>
    /// <returns>지원 여부</returns>
    public bool IsVibrationSupported()
    {
        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            int result = IsVibrationSupportedInternal();
            bool supported = result == 1;
            Debug.Log($"📳 WebGL 진동 지원: {supported}");
            return supported;
#elif UNITY_ANDROID || UNITY_IOS
            Debug.Log("📳 모바일 플랫폼 진동 지원: true");
            return true;
#else
            Debug.Log("📳 현재 플랫폼 진동 지원: false");
            return false;
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 진동 지원 확인 실패: {e.Message}");
            return false;
        }
    }
    
    #endregion

    /// <summary>
    /// 서버 URL을 브라우저 URL에서 자동 추출 (하드코딩 제거)
    /// WebGL 빌드: jslib의 window.location에서 추출
    /// 에디터/기타: 기본값(ws://localhost:3000) 사용
    /// </summary>
    private void ResolveServerUrl()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string browserUrl = GetServerUrlFromBrowser();
        if (!string.IsNullOrEmpty(browserUrl))
        {
            serverUrl = browserUrl;
            Debug.Log($"🌐 브라우저 URL에서 서버 주소 추출: {serverUrl}");
            return;
        }
        Debug.LogWarning("⚠️ jslib URL 추출 실패, 기본값 사용");
#endif
        Debug.Log($"🌐 기본 서버 URL 사용: {serverUrl}");
    }

    /// <summary>
    /// 모든 패널을 비활성화합니다 (초기화 시 사용)
    /// </summary>
    private void HideAllPanels()
    {
        if (gamePanel != null) gamePanel.gameObject.SetActive(false);
        if (nickNamePanel != null) nickNamePanel.gameObject.SetActive(false);
        if (introPanel != null) introPanel.SetActive(false);
        if (resultPanel != null) resultPanel.gameObject.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (waitingRoomPanel != null) waitingRoomPanel.gameObject.SetActive(false);
        
        Debug.Log("🔒 모든 패널 비활성화 완료");
    }

    void InitializeUI()
    {
        // Game control buttons
        if (pressButton) 
        {
            pressButton.onClick.AddListener(OnPressButton);
            Debug.Log("✅ Press button initialized");
        }
        
        // NOTE: joinButton 제거 - 대기방 입장은 NickNamePanel에서 관리
        
        Debug.Log("🎮 UI initialization complete - using GamePanel for all UI updates");
    }

    void AutoConnect()
    {
        if (!isConnected)
        {
            Debug.Log("🔄 Attempting to auto-connect...");
            ConnectToServer();
        }
    }

    void TestPing()
    {
        if (webSocket != null && isConnected)
        {
            Debug.Log("🏓 Sending ping to server...");
            SendMessage("{\"type\":\"ping\"}");
        }
        else
        {
            Debug.LogWarning("⚠️ Cannot send ping - not connected to server");
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Handle WebSocket events on main thread
        HandleWebSocketEventsOnMainThread();
        
        #region Keyboard Controls (에디터/데스크톱 전용)
        
#if UNITY_EDITOR || UNITY_STANDALONE

        // Game controls
        // NOTE: J키 JoinRoom 단축키 제거 - 대기방 입장은 NickNamePanel에서 관리

        if (Input.GetKeyDown(KeyCode.Space) && isInGame)
        {
            // 스피어 진동 효과
            if (gamePanel != null)
            {
                gamePanel.VibrateSphere();
                gamePanel.RegisterCombo();
            }
            
            // 크리스탈 볼 흔들림 효과
            if (crystalBall != null)
            {
                crystalBall.Shake();
            }
            
            SendPress();
        }
#endif
        
        #endregion
    }
    
    #region WebSocket Event Handling
    
    private void HandleWebSocketEventsOnMainThread()
    {
        // Handle connection open event
        if (shouldHandleOpen)
        {
            shouldHandleOpen = false;
            StartCoroutine(HandleWebSocketOpenSafely());
        }
        
        string messageToProcess = null;
        
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL 빌드에서는 Unity 메인 스레드 전용 큐 사용 (lock 없이)
        if (mainThreadMessageQueue.Count > 0)
        {
            messageToProcess = mainThreadMessageQueue.Dequeue();
            Debug.Log($"🔄 WebGL: Processing message from main thread queue. Remaining: {mainThreadMessageQueue.Count}");
        }
#else
        // 에디터나 다른 플랫폼에서는 기존 방식 사용 (thread-safe)
        lock (messageLock)
        {
            if (pendingMessages.Count > 0)
            {
                messageToProcess = pendingMessages.Dequeue();
            }
        }
#endif
        
        if (!string.IsNullOrEmpty(messageToProcess))
        {
            Debug.Log($"🔄 Processing message: {messageToProcess.Substring(0, Math.Min(100, messageToProcess.Length))}...");
            
            try
            {
                // Unity 메인 스레드에서 안전하게 Unity 오브젝트 참조
                ProcessReceivedMessage(messageToProcess);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Error processing message: {e.Message}");
                Debug.LogError($"❌ Message: {messageToProcess}");
                Debug.LogError($"❌ Stack trace: {e.StackTrace}");
            }
        }
    }
    
    #endregion
    
    private IEnumerator HandleWebSocketOpenSafely()
    {
        yield return null; // 다음 프레임까지 대기

        // 1) 이전 세션이 있으면 재연결 먼저 시도
        if (!string.IsNullOrEmpty(cachedPlayerId))
        {
            Debug.Log($"🔄 이전 세션 발견(playerId={cachedPlayerId}) → reconnect 요청");
            isAwaitingReconnect = true;

            // 로딩 패널은 유지 (응답 올 때까지)
            string reconnectMsg = $"{{\"type\":\"reconnect\",\"playerId\":\"{cachedPlayerId}\"}}";
            SendMessage(reconnectMsg);

            // 응답 대기 (3초 타임아웃)
            float elapsed = 0f;
            while (isAwaitingReconnect && elapsed < RECONNECT_RESPONSE_TIMEOUT)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (isAwaitingReconnect)
            {
                Debug.LogWarning("⌛ reconnect 응답 없음(타임아웃) → 신규 세션으로 진입");
                isAwaitingReconnect = false;
                cachedPlayerId = "";
                SendPlayerHello();
                ShowNickNamePanelFallback();
            }
            // 응답을 받은 경우(성공/실패)는 해당 case 핸들러에서 UI 처리됨
            yield break;
        }

        // 2) 첫 연결: 신규 세션 → 서버에 player 정체 알림 (TD goto_live 트리거)
        SendPlayerHello();
        ShowNickNamePanelFallback();
    }

    /// <summary>
    /// WebGL 신규 세션 진입을 서버에 알리는 메시지.
    /// Desktop(Admin)/Observer/ConnectionTest는 이 메시지를 보내지 않으므로,
    /// 서버는 playerHello 수신 시점만 TD `/goto_live` 트리거 시점으로 사용한다.
    /// reconnect 흐름에서는 호출하지 않음 → 재접속은 트리거 대상에서 자연 제외.
    /// </summary>
    private void SendPlayerHello()
    {
        const string msg = "{\"type\":\"playerHello\",\"roomId\":\"main_room\"}";
        SendMessage(msg);
        Debug.Log("📤 playerHello 송신 (TD live 트리거 요청)");
    }

    private void ShowNickNamePanelFallback()
    {
        try
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
                Debug.Log("✅ LoadingPanel 숨김");
            }

            if (nickNamePanel != null)
            {
                nickNamePanel.gameObject.SetActive(true);
                Debug.Log("✅ NickNamePanel 활성화 - 닉네임 입력 대기");
            }
            else
            {
                Debug.LogWarning("⚠️ NickNamePanel이 할당되지 않았습니다!");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ ShowNickNamePanelFallback 오류: {ex.Message}");
        }
    }

    void InitializeWebSocket()
    {
        try
        {
            Debug.Log($"🔄 WebSocket initialization. URL: {serverUrl}");

            // URL validation
            if (!IsValidWebSocketUrl(serverUrl))
            {
                Debug.LogError($"❌ Invalid URL format: {serverUrl}");
                UpdateStatus("Invalid server URL");
                return;
            }

            Debug.Log($"✅ URL validation passed.");

            // Create WebSocket instance
            webSocket = WebSocketFactory.CreateInstance(serverUrl);
            Debug.Log("✅ WebSocket instance created.");

            // Setup event handlers
            SetupEventHandlers();
            Debug.Log("✅ Event handlers registered.");

            Debug.Log($"✅ WebSocket initialization complete. Server URL: {serverUrl}");
            UpdateStatus("Connecting to server...");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ WebSocket initialization failed: {e.Message}");
            if (e.InnerException != null)
            {
                Debug.LogError($"❌ Inner exception: {e.InnerException.Message}");
            }
            UpdateStatus("Connection failed");
        }
    }

    bool IsValidWebSocketUrl(string url)
    {
        try
        {
            System.Uri uri = new System.Uri(url);
            return uri.Scheme == "ws" || uri.Scheme == "wss";
        }
        catch
        {
            return false;
        }
    }

    void SetupEventHandlers()
    {
        Debug.Log("🔧 Setting up WebSocket event handlers...");
        
        // Called on successful connection
        webSocket.OnOpen += OnWebSocketOpen;
        Debug.Log("✅ OnOpen handler registered");

        // Called when a message is received
        webSocket.OnMessage += OnWebSocketMessage;
        Debug.Log("✅ OnMessage handler registered");

        // Called when an error occurs
        webSocket.OnError += OnWebSocketError;
        Debug.Log("✅ OnError handler registered");

        // Called on connection close
        webSocket.OnClose += OnWebSocketClose;
        Debug.Log("✅ OnClose handler registered");
        
        Debug.Log("🔧 All event handlers registered successfully");
    }

    public void ConnectToServer()
    {
        if (webSocket != null && !isConnected)
        {
            try
            {
                Debug.Log($"🔌 Attempting to connect to: {serverUrl}");
                Debug.Log($"🔌 WebSocket state before connect: {webSocket.GetState()}");
                
                webSocket.Connect();
                Debug.Log("🔌 Connect() called successfully");
                
                // Check state immediately after connect
                StartCoroutine(CheckConnectionState());
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Connection failed: {e.Message}");
                Debug.LogError($"❌ Exception type: {e.GetType().Name}");
                if (e.InnerException != null)
                {
                    Debug.LogError($"❌ Inner exception: {e.InnerException.Message}");
                }
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ Cannot connect: WebSocket={webSocket != null}, Already connected={isConnected}");
        }
    }
    
    private IEnumerator CheckConnectionState()
    {
        Debug.Log("🔍 Checking connection state...");
        
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForSeconds(0.5f);
            
            try
            {
                var state = webSocket.GetState();
                Debug.Log($"🔍 Connection state after {i * 0.5f}s: {state}");
                
                if (state == WebSocketState.Open)
                {
                    Debug.Log("✅ WebSocket is now OPEN!");
                    break;
                }
                else if (state == WebSocketState.Closed)
                {
                    Debug.LogWarning("⚠️ WebSocket is CLOSED - connection may have failed");
                    break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Error checking state: {e.Message}");
                break;
            }
        }
    }

    public void DisconnectFromServer()
    {
        if (webSocket != null && isConnected)
        {
            try
            {
                webSocket.Close();
                Debug.Log("Disconnecting from server...");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Disconnection failed: {e.Message}");
            }
        }
    }

    public void JoinRoom(string roomId)
    {
        try
        {
            if (webSocket == null)
            {
                Debug.LogWarning("⚠️ Cannot join room - WebSocket is null");
                return;
            }
            
            if (!isConnected)
            {
                Debug.LogWarning("⚠️ Cannot join room - Not connected to server");
                return;
            }
            
            if (string.IsNullOrEmpty(roomId))
            {
                Debug.LogWarning("⚠️ Cannot join room - Room ID is empty");
                return;
            }
            
            // 개인전 - team/playerId 불필요 (서버에서 생성)
            string message = $"{{\"type\":\"joinRoom\",\"roomId\":\"{roomId}\",\"playerName\":\"{this.playerName}\"}}";
            SendMessage(message);
            Debug.Log($"✅ Joining room: {roomId} with name: {this.playerName} (개인전)");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error in JoinRoom: {e.Message}");
            Debug.LogError($"❌ Stack trace: {e.StackTrace}");
        }
    }

    public void SendPress()
    {
        Debug.Log($"🔍 SendPress called - WebSocket: {webSocket != null}, Connected: {isConnected}, InGame: {isInGame}");

        if (webSocket != null && isConnected)
        {
            if (isInGame)
            {
                string message = "{\"type\":\"press\"}";
                SendMessage(message);
                Debug.Log("✅ Press event sent successfully - 점수가 증가됩니다!");
                Debug.Log("📊 서버에서 점수 업데이트 메시지를 받아 UI가 업데이트될 예정입니다.");

                // Press 성공 시 짧은 진동 (100ms)
                Vibrate(100);
            }
            else
            {
                Debug.LogWarning("⚠️ 게임이 진행 중이 아닙니다. 게임 시작 후 버튼을 눌러주세요.");

                // 오류 시 길고 약한 진동 (200ms)
                Vibrate(200);
            }
        }
        else
        {
            Debug.LogWarning("❌ Cannot send press - WebSocket not ready or not connected");

            // 연결 오류 시 긴 진동 (300ms)
            Vibrate(300);
        }
    }

    public void SendTestMessage()
    {
        SendMessage(testMessage);
    }

    public void RequestRoomInfo()
    {
        if (webSocket != null && isConnected)
        {
            string message = "{\"type\":\"getRoomInfo\"}";
            SendMessage(message);
            Debug.Log("📋 Requesting room info...");
        }
    }

    public void SendMessage(string message)
    {
        if (webSocket != null && isConnected)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                webSocket.Send(data);
                Debug.Log($"Sending message: {message}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to send message: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("Not connected to the server. Please connect first.");
        }
    }

    // NOTE: OnJoinButton 제거 - 대기방 입장은 NickNamePanel에서 관리

    public void OnPressButton()
    {
        Debug.Log("🎯 Press Button clicked!");
        
        // 버튼이 비활성화되어 있다면 경고 메시지 출력
        if (pressButton != null && !pressButton.interactable)
        {
            Debug.LogWarning("⚠️ Press 버튼이 비활성화되어 있습니다. 게임이 시작되지 않았거나 종료되었습니다.");
            return;
        }
        
        // 스피어 진동 효과 & 콤보 등록 & 터치 파티클
        if (gamePanel != null)
        {
            gamePanel.VibrateSphere();
            gamePanel.RegisterCombo();
            gamePanel.PlayTouchParticle();
        }
        
        // 크리스탈 볼 흔들림 효과
        if (crystalBall != null)
        {
            crystalBall.Shake();
        }
        
        SendPress();
    }

    // 팀 선택 핸들러 제거 (개인전)

    #region WebSocket Event Handlers
    
    // WebSocket event handlers
    private void OnWebSocketOpen()
    {
        Debug.Log("🎉 OnWebSocketOpen called!");
        isConnected = true;
        reconnectDelay = INITIAL_RECONNECT_DELAY;
        isReconnecting = false;
        shouldHandleOpen = true; // 메인 스레드에서 처리하도록 플래그 설정
        Debug.Log("✅ Successfully connected to server!");
    }

    private void OnWebSocketMessage(byte[] data)
    {
        try
        {
            Debug.Log($"📨 OnWebSocketMessage called with {data?.Length ?? 0} bytes");
            
            if (data == null)
            {
                Debug.LogWarning("⚠️ Received null data in OnWebSocketMessage - possibly a WebSocket control frame");
                return;
            }
            
            if (data.Length == 0)
            {
                Debug.LogWarning("⚠️ Received empty data (0 bytes) in OnWebSocketMessage");
                Debug.LogWarning("⚠️ This might be a WebSocket PING/PONG frame or server heartbeat");
                return;
            }
            
            string message = Encoding.UTF8.GetString(data);
            Debug.Log($"📨 Message received from server: {message}");
            Debug.Log($"📨 Message length: {data.Length} bytes");

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL 빌드에서는 Unity 오브젝트 참조 없이 단순히 큐에만 추가
            mainThreadMessageQueue.Enqueue(message);
            Debug.Log($"📨 Message added to WebGL main thread queue. Queue size: {mainThreadMessageQueue.Count}");
#else
            // 에디터나 다른 플랫폼에서는 기존 방식 사용 (thread-safe)
            lock (messageLock)
            {
                pendingMessages.Enqueue(message);
                Debug.Log($"📨 Message queued. Queue size: {pendingMessages.Count}");
            }
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error in OnWebSocketMessage: {e.Message}");
            Debug.LogError($"❌ Data length: {data?.Length ?? 0}");
            Debug.LogError($"❌ Stack trace: {e.StackTrace}");
        }
    }

    private void OnWebSocketError(string error)
    {
        Debug.LogError($"❌ OnWebSocketError called: {error}");
        
        // Update GamePanel status
        if (gamePanel != null)
        {
            Debug.Log("❌ WebSocket 오류 상태");
        }
        
        // 연결이 끊어진 상태에서 오류 발생 시 재연결
        if (!isConnected)
        {
            TriggerReconnect();
        }
    }

    private void OnWebSocketClose(WebSocketCloseCode closeCode)
    {
        Debug.Log($"🔌 OnWebSocketClose called with code: {closeCode}");
        isConnected = false;
        isInGame = false;
        Debug.Log($"🔌 Server connection closed. Close code: {closeCode}");
        
        // Update GamePanel status
        if (gamePanel != null)
        {
            Debug.Log("⚠️ 연결 끊김 상태");
        }
        
        // Disable press button
        if (pressButton) pressButton.interactable = false;
        
        // 정상 종료(Normal)가 아닌 경우 재연결 시도
        if (closeCode != WebSocketCloseCode.Normal)
        {
            TriggerReconnect();
        }
    }
    
    /// <summary>
    /// 재연결 트리거 (중복 방지)
    /// </summary>
    private void TriggerReconnect()
    {
        if (!isReconnecting && this != null && gameObject.activeInHierarchy)
        {
            isReconnecting = true;
            Debug.Log("🔄 WebSocket 재연결 시작...");
            reconnectCoroutine = StartCoroutine(ReconnectWithBackoff());
        }
    }
    
    /// <summary>
    /// 지수 백오프 기반 WebSocket 재연결 코루틴
    /// 1초 → 2초 → 4초 → 8초 → 16초 → 30초(최대) 간격으로 재시도
    /// </summary>
    private IEnumerator ReconnectWithBackoff()
    {
        int attempt = 0;
        
        while (!isConnected && this != null)
        {
            attempt++;
            Debug.Log($"🔄 [{attempt}회] {reconnectDelay:F1}초 후 재연결 시도...");
            
            // UI 상태 업데이트
            if (gamePanel != null)
            {
                Debug.Log($"🔄 재연결 시도 ({attempt})");
            }
            
            yield return new WaitForSeconds(reconnectDelay);
            
            if (isConnected)
            {
                Debug.Log("✅ 대기 중 연결 복구됨 - 재연결 중단");
                break;
            }
            
            Debug.Log($"🔄 [{attempt}회] 재연결 시도 중... ({serverUrl})");
            
            // HybridWebSocket은 인스턴스 재사용이 안 되므로 새로 생성
            InitializeWebSocket();
            ConnectToServer();
            
            // 연결 대기 (최대 5초)
            float waitTime = 0f;
            while (!isConnected && waitTime < 5f)
            {
                yield return new WaitForSeconds(0.5f);
                waitTime += 0.5f;
            }
            
            if (isConnected)
            {
                Debug.Log($"✅ [{attempt}회] 재연결 성공! (백오프 리셋)");
                reconnectDelay = INITIAL_RECONNECT_DELAY;
                isReconnecting = false;
                reconnectCoroutine = null;
                yield break;
            }
            
            // 백오프 증가
            reconnectDelay = Mathf.Min(reconnectDelay * 2f, MAX_RECONNECT_DELAY);
            Debug.Log($"⏳ 다음 재연결 대기: {reconnectDelay:F1}초");
        }
        
        isReconnecting = false;
        reconnectCoroutine = null;
    }
    
    private void OnDestroy()
    {
        // 재연결 코루틴 중지
        if (reconnectCoroutine != null)
        {
            StopCoroutine(reconnectCoroutine);
            reconnectCoroutine = null;
        }
        isReconnecting = false;
        
        // WebSocket 정리
        if (webSocket != null)
        {
            try { webSocket.Close(); } catch { }
        }
    }
    
    #endregion

    private void ProcessReceivedMessage(string message)
    {
        Debug.Log($"🔍 ProcessReceivedMessage called with: {message}");
        Debug.Log($"🔍 Message length: {message.Length}");
        
        if (string.IsNullOrEmpty(message))
        {
            Debug.LogWarning("⚠️ Received empty message!");
            return;
        }
        
        try
        {
            // 먼저 BaseMessage로 타입 확인
            BaseMessage baseMessage = SafeJsonDeserialize<BaseMessage>(message);
            
            if (baseMessage == null)
            {
                Debug.LogError("❌ BaseMessage 파싱 실패!");
                Debug.LogError($"❌ 원본 메시지: {message}");
                return;
            }
            
            if (string.IsNullOrEmpty(baseMessage.type))
            {
                Debug.LogWarning("⚠️ Invalid message format - no type field");
                Debug.LogWarning($"⚠️ 파싱된 baseMessage: {baseMessage}");
                return;
            }

            Debug.Log($"🔍 Message type: '{baseMessage.type}'");
            Debug.Log($"🔍 Message type length: {baseMessage.type.Length}");

            // 타입에 따라 적절한 클래스로 역직렬화하여 처리
            switch (baseMessage.type)
            {
                case "joinedRoom":
                    Debug.Log("🎯 Processing joinedRoom message");
                    Debug.Log($"🎯 Raw joinedRoom message: {message}");
                    var joinedRoomMsg = SafeJsonDeserialize<JoinedRoomMessage>(message);
                    if (joinedRoomMsg == null)
                    {
                        Debug.LogError("❌ Failed to deserialize joinedRoom message, trying alternative parsing");
                        HandleJoinedRoomAlternative(message);
                    }
                    else
                    {
                        HandleJoinedRoom(joinedRoomMsg);
                    }
                    break;

                case "gameStart":
                    Debug.Log("🚀 Processing gameStart message");
                    var gameStartMsg = SafeJsonDeserialize<GameStartMessage>(message);
                    HandleGameStart(gameStartMsg);
                    break;

                case "timeUpdate":
                    Debug.Log("⏰ Processing timeUpdate message");
                    var timeUpdateMsg = SafeJsonDeserialize<TimeUpdateMessage>(message);
                    HandleTimeUpdate(timeUpdateMsg);
                    break;

                case "scoreUpdate":
                    Debug.Log("📊 Processing scoreUpdate message");
                    var scoreUpdateMsg = SafeJsonDeserialize<ScoreUpdateMessage>(message);
                    HandleScoreUpdate(scoreUpdateMsg);
                    break;

                case "gameStateUpdate":
                    Debug.Log("📊 Processing gameStateUpdate message");
                    var gameStateMsg = SafeJsonDeserialize<GameStateUpdateMessage>(message);
                    HandleGameStateUpdate(gameStateMsg);
                    break;

                case "gameEnd":
                    Debug.Log("🏁 Processing gameEnd message");
                    Debug.Log($"📨 Raw gameEnd message: {message}");
                    var gameEndMsg = SafeJsonDeserialize<GameEndMessage>(message);
                    StartCoroutine(HandleGameEndWithDelay(gameEndMsg));
                    break;

                case "error":
                    Debug.Log("❌ Processing error message");
                    var errorMsg = SafeJsonDeserialize<ErrorMessage>(message);
                    HandleError(errorMsg);
                    break;

                case "roomInfo":
                    Debug.Log("📋 Processing roomInfo message");
                    var roomInfoMsg = SafeJsonDeserialize<RoomInfoMessage>(message);
                    HandleRoomInfo(roomInfoMsg);
                    break;

                case "playerJoined":
                    Debug.Log("👤 Processing playerJoined message");
                    Debug.Log($"📨 Raw playerJoined message: {message}");
                    var playerJoinedMsg = SafeJsonDeserialize<PlayerJoinedMessage>(message);
                    Debug.Log($"🔍 Parsed playerJoinedMsg: data={playerJoinedMsg?.data != null}, playerId={playerJoinedMsg?.data?.playerId}, playerName={playerJoinedMsg?.data?.playerName}");
                    HandlePlayerJoined(playerJoinedMsg);
                    break;

                case "playerLeft":
                    Debug.Log("👋 Processing playerLeft message");
                    Debug.Log($"📨 Raw playerLeft message: {message}");
                    var playerLeftMsg = SafeJsonDeserialize<PlayerLeftMessage>(message);
                    Debug.Log($"🔍 Parsed playerLeftMsg: data={playerLeftMsg?.data != null}, playerId={playerLeftMsg?.data?.playerId}, playerName={playerLeftMsg?.data?.playerName}");
                    HandlePlayerLeft(playerLeftMsg);
                    break;

                case "countdownStart":
                    Debug.Log("⏰ Processing countdownStart message");
                    Debug.Log($"📨 Raw countdownStart message: {message}");
                    // Desktop에서 보내는 메시지 구조: {"type":"countdownStart","countdown":5}
                    try {
                        var messageObj = JObject.Parse(message);
                        var countdown = messageObj["countdown"]?.Value<int>() ?? 5;
                        Debug.Log($"⏰ 카운트다운 시작: {countdown}초");
                        HandleCountdownStartFromDesktop(countdown);
                    } catch (System.Exception e) {
                        Debug.LogError($"❌ countdownStart 메시지 파싱 오류: {e.Message}");
                        // 기존 방식으로 폴백
                        var countdownStartMsg = SafeJsonDeserialize<CountdownStartMessage>(message);
                        HandleCountdownStart(countdownStartMsg);
                    }
                    break;

                case "countdownUpdate":
                    Debug.Log("⏰ Processing countdownUpdate message");
                    Debug.Log($"📨 Raw countdownUpdate message: {message}");
                    // Desktop에서 보내는 메시지 구조: {"type":"countdownUpdate","countdown":4}
                    try {
                        var messageObj = JObject.Parse(message);
                        var countdown = messageObj["countdown"]?.Value<int>() ?? 0;
                        Debug.Log($"⏰ 카운트다운 업데이트: {countdown}");
                        HandleCountdownUpdateFromDesktop(countdown);
                    } catch (System.Exception e) {
                        Debug.LogError($"❌ countdownUpdate 메시지 파싱 오류: {e.Message}");
                        // 기존 방식으로 폴백
                        var countdownUpdateMsg = SafeJsonDeserialize<CountdownUpdateMessage>(message);
                        HandleCountdownUpdate(countdownUpdateMsg);
                    }
                    break;

                case "autoReset":
                    Debug.Log("🔄 Processing autoReset message");
                    HandleAutoReset(message);
                    break;

                case "autoStartTimerStarted":
                    Debug.Log("⏰ Processing autoStartTimerStarted message");
                    HandleAutoStartTimerStarted(message);
                    break;

                case "autoStartTimerUpdate":
                    Debug.Log("⏰ Processing autoStartTimerUpdate message");
                    HandleAutoStartTimerUpdate(message);
                    break;

                case "autoStartTimerCancelled":
                    Debug.Log("🛑 Processing autoStartTimerCancelled message");
                    HandleAutoStartTimerCancelled(message);
                    break;

                case "ping":
                    Debug.Log("🏓 Processing ping message");
                    SendMessage("{\"type\":\"pong\"}");
                    break;

                case "nicknameCheckResult":
                    Debug.Log("🔍 Processing nicknameCheckResult message");
                    HandleNicknameCheckResult(message);
                    break;

                case "nicknameSetResult":
                    Debug.Log("✅ Processing nicknameSetResult message");
                    HandleNicknameSetResult(message);
                    break;

                case "canJoinNow":
                    Debug.Log("🚪 Processing canJoinNow message");
                    HandleCanJoinNow(message);
                    break;

                case "returnToNicknameStep":
                    Debug.Log("🔄 Processing returnToNicknameStep message");
                    HandleReturnToNicknameStep(message);
                    break;

                case "lobbyTimerStarted":
                    Debug.Log("🚪 Processing lobbyTimerStarted message");
                    HandleLobbyTimerStarted(message);
                    break;

                case "lobbyTimerUpdate":
                    Debug.Log("🚪 Processing lobbyTimerUpdate message");
                    HandleLobbyTimerUpdate(message);
                    break;

                case "lobbyTimerCancelled":
                    Debug.Log("🚪 Processing lobbyTimerCancelled message");
                    HandleLobbyTimerCancelled(message);
                    break;

                case "movedToGameRoom":
                    Debug.Log("🎮 Processing movedToGameRoom message");
                    HandleMovedToGameRoom(message);
                    break;

                case "reconnected":
                    Debug.Log("✅ Processing reconnected message");
                    HandleReconnected(message);
                    break;

                case "reconnectFailed":
                    Debug.Log("⚠️ Processing reconnectFailed message");
                    HandleReconnectFailed(message);
                    break;

                case "duplicateConnection":
                    Debug.LogWarning("⚠️ Processing duplicateConnection message");
                    HandleDuplicateConnection(message);
                    break;

                case "playerDisconnected":
                    Debug.Log("🔌 Processing playerDisconnected message");
                    // 다른 참가자 일시 연결 끊김 — UI 유지 (grace period 중)
                    break;

                case "playerReconnected":
                    Debug.Log("🔄 Processing playerReconnected message");
                    // 다른 참가자 복귀 — UI 유지
                    break;

                default:
                    Debug.LogWarning($"⚠️ Unknown message type: {baseMessage.type}");
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 메시지 처리 오류: {e.Message}");
            Debug.LogError($"❌ 원본 메시지: {message}");
            Debug.LogError($"❌ Stack trace: {e.StackTrace}");
        }
    }

    private void HandleJoinedRoom(JoinedRoomMessage joinedRoomMsg)
    {
        Debug.Log("🎯 Joined room successfully!");
        
        try
        {
            if (joinedRoomMsg?.data == null)
            {
                Debug.LogError("❌ JoinedRoom message data is null!");
                Debug.LogError("❌ joinedRoomMsg is null: " + (joinedRoomMsg == null));
                if (joinedRoomMsg != null)
                {
                    Debug.LogError("❌ joinedRoomMsg.data is null: " + (joinedRoomMsg.data == null));
                }
                return;
            }

            var data = joinedRoomMsg.data;
            var player = data.player;
            var room = data.room;

            Debug.Log($"🔍 처리할 데이터:");
            Debug.Log($"🔍 PlayerId: '{data.playerId}'");
            Debug.Log($"🔍 Player: {(player != null ? $"ID={player.id}, Name={player.name}, Team={player.team}, Score={player.score}, Connected={player.connected}, Admin={player.isAdmin}, Observer={player.isObserver}" : "null")}");
            Debug.Log($"🔍 Room: {(room != null ? $"ID={room.id}, Status={room.status}" : "null")}");
            Debug.Log($"🔍 Players array: {(data.players != null ? $"Length={data.players.Length}" : "null")}");

            // 플레이어 정보 추출 (개인전 - 팀 없음)
            string playerId = data.playerId ?? player?.id ?? "";
            string team = player?.team ?? ""; // 개인전이므로 팀 없음
            int score = player?.score ?? 0;
            bool connected = player?.connected ?? true;
            
            string displayName = player?.name ?? "Player";
            
            if (!string.IsNullOrEmpty(displayName) && displayName != "Player")
            {
                this.playerName = displayName;
                Debug.Log($"🏷️ 서버에서 받은 플레이어 이름으로 업데이트: {this.playerName}");
            }
            
            // playerId 캐싱 제거 (서버에서 매번 새로 생성)
            Debug.Log($"👤 자신의 플레이어 정보 - ID: '{playerId}', 표시이름: '{displayName}', 점수: {score}, 연결: {connected}");

            // GamePanel에서 현재 플레이어 ID 설정 및 플레이어 UI 생성
            if (gamePanel != null && !string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(displayName))
            {
                // 먼저 현재 플레이어 ID 설정
                gamePanel.SetCurrentPlayerId(playerId);
                cachedPlayerId = playerId;
                Debug.Log($"🆔 현재 플레이어 ID 설정 완료: {playerId} (cachedPlayerId도 저장)");
                
                Debug.Log($"✅ Calling AddOrUpdatePlayer with data:");
                Debug.Log($"   - playerId: '{playerId}'");
                Debug.Log($"   - displayName: '{displayName}'");
                Debug.Log($"   - team: '{team}'");
                Debug.Log($"   - score: {score}");
                Debug.Log($"   - connected: {connected}");
                
                bool isAdmin = player?.isAdmin ?? false;
                bool isObserver = player?.isObserver ?? (team == "Observer" || displayName.Contains("Observer"));
                
                Debug.Log($"   - isAdmin: {isAdmin} (from player: {player?.isAdmin})");
                Debug.Log($"   - isObserver: {isObserver} (from player: {player?.isObserver})");
                
                gamePanel.AddOrUpdatePlayer(playerId, displayName, team, score, connected, isAdmin, isObserver);
                Debug.Log($"✅ 자신의 플레이어 UI 생성 완료: {displayName} ({team}) [Admin: {isAdmin}, Observer: {isObserver}]");
            }
            else
            {
                Debug.LogError($"❌ Cannot create player UI:");
                Debug.LogError($"❌ - gamePanel: {gamePanel != null}");
                Debug.LogError($"❌ - playerId: '{playerId}' (empty: {string.IsNullOrEmpty(playerId)})");
                Debug.LogError($"❌ - displayName: '{displayName}' (empty: {string.IsNullOrEmpty(displayName)})");
            }

            // 기존 플레이어들 정보 처리
            if (data.players != null && data.players.Length > 0 && gamePanel != null)
            {
                Debug.Log($"👥 기존 플레이어 목록 처리 시작 - 총 {data.players.Length}명");
                
                foreach (var existingPlayer in data.players)
                {
                    if (existingPlayer == null) continue;
                    
                    string existingPlayerId = existingPlayer.id ?? "";
                    string existingPlayerName = existingPlayer.name ?? "";
                    string existingTeam = existingPlayer.team ?? "";
                    int existingScore = existingPlayer.score;
                    bool existingConnected = existingPlayer.connected;
                    bool existingIsAdmin = existingPlayer.isAdmin;
                    bool existingIsObserver = existingPlayer.isObserver;
                    
                    // 자신이 아닌 다른 플레이어들만 추가
                    if (existingPlayerId != playerId && !string.IsNullOrEmpty(existingPlayerId) && !string.IsNullOrEmpty(existingPlayerName))
                    {
                        Debug.Log($"👤 기존 플레이어 추가 - ID: {existingPlayerId}, Name: {existingPlayerName}, Team: {existingTeam}, Admin: {existingIsAdmin}, Observer: {existingIsObserver}");
                        gamePanel.AddOrUpdatePlayer(existingPlayerId, existingPlayerName, existingTeam, existingScore, existingConnected, existingIsAdmin, existingIsObserver);
                    }
                }
                
                Debug.Log($"✅ 기존 플레이어 목록 처리 완료");
            }
            else
            {
                Debug.Log($"📝 기존 플레이어 목록이 없음 또는 비어있음 - players: {data.players?.Length ?? 0}명");
            }

            // 룸 상태 업데이트 (개인전 - 팀 점수 대신 playerCount 사용)
            if (room != null && gamePanel != null)
            {
                int playerCount = data.players?.Length ?? 0;
                gamePanel.UpdateTotalPlayersCount(playerCount, 0);

                if (room.remainingTime > 0)
                {
                    gamePanel.UpdateTimer(room.remainingTime);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error processing joinedRoom message: {e.Message}");
            Debug.LogError($"❌ Stack trace: {e.StackTrace}");
        }
        
        Debug.Log("✅ HandleJoinedRoom completed - all UI updates handled by GamePanel");
    }

    private void HandleJoinedRoomAlternative(string message)
    {
        Debug.Log("🔧 Using alternative JSON parsing for joinedRoom");
        
        try
        {
            // JObject를 사용한 안전한 파싱
            var messageObj = JObject.Parse(message);
            var data = messageObj["data"];
            
            if (data == null)
            {
                Debug.LogError("❌ Alternative parsing: data is null");
                return;
            }
            
            // 플레이어 정보 추출
            string playerId = data["playerId"]?.ToString() ?? "";
            var player = data["player"];
            var room = data["room"];
            
            Debug.Log($"🔧 Alternative parsing - PlayerId: '{playerId}'");
            Debug.Log($"🔧 Alternative parsing - Player: {(player != null ? "exists" : "null")}");
            Debug.Log($"🔧 Alternative parsing - Room: {(room != null ? "exists" : "null")}");
            
            if (player != null)
            {
                string playerName = player["name"]?.ToString() ?? "Player";
                string team = player["team"]?.ToString() ?? "";
                int score = player["score"]?.Value<int>() ?? 0;
                bool connected = player["connected"]?.Value<bool>() ?? true;
                bool isAdmin = player["isAdmin"]?.Value<bool>() ?? false;
                bool isObserver = player["isObserver"]?.Value<bool>() ?? false;
                
                Debug.Log($"🔧 Alternative - Player details: Name={playerName}, Team={team}, Score={score}, Connected={connected}, Admin={isAdmin}, Observer={isObserver}");
                
                // GamePanel에서 플레이어 UI 생성
                if (gamePanel != null && !string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(playerName))
                {
                    Debug.Log($"🔧 Alternative - Creating player UI: {playerName} ({team})");
                    gamePanel.AddOrUpdatePlayer(playerId, playerName, team, score, connected, isAdmin, isObserver);
                    Debug.Log($"✅ Alternative - Player UI 생성 완료: {playerName} ({team}) [Admin: {isAdmin}, Observer: {isObserver}]");
                }
                else
                {
                    Debug.LogError($"❌ Alternative - Cannot create player UI: gamePanel={gamePanel != null}, playerId='{playerId}', playerName='{playerName}'");
                }
            }
            
            // 기존 플레이어 목록 처리
            var players = data["players"];
            if (players != null && gamePanel != null)
            {
                var playersArray = players as JArray;
                int playerCount = playersArray?.Count ?? 0;
                Debug.Log($"🔧 Alternative - Processing existing players: {playerCount} players");
                
                foreach (var existingPlayer in players)
                {
                    if (existingPlayer == null) continue;
                    
                    string existingPlayerId = existingPlayer["id"]?.ToString() ?? "";
                    string existingPlayerName = existingPlayer["name"]?.ToString() ?? "";
                    string existingTeam = existingPlayer["team"]?.ToString() ?? "";
                    int existingScore = existingPlayer["score"]?.Value<int>() ?? 0;
                    bool existingConnected = existingPlayer["connected"]?.Value<bool>() ?? true;
                    bool existingIsAdmin = existingPlayer["isAdmin"]?.Value<bool>() ?? false;
                    bool existingIsObserver = existingPlayer["isObserver"]?.Value<bool>() ?? false;
                    
                    // 자신이 아닌 다른 플레이어들만 추가
                    if (existingPlayerId != playerId && !string.IsNullOrEmpty(existingPlayerId) && !string.IsNullOrEmpty(existingPlayerName))
                    {
                        Debug.Log($"🔧 Alternative - Adding existing player: {existingPlayerName} ({existingTeam})");
                        gamePanel.AddOrUpdatePlayer(existingPlayerId, existingPlayerName, existingTeam, existingScore, existingConnected, existingIsAdmin, existingIsObserver);
                    }
                }
            }
            
            // 룸 상태 업데이트 (개인전)
            if (room != null && gamePanel != null)
            {
                string roomStatus = room["status"]?.ToString() ?? "waiting";
                int remainingTime = room["remainingTime"]?.Value<int>() ?? 0;
                int playerCount = (players as JArray)?.Count ?? 0;
                
                gamePanel.UpdateTotalPlayersCount(playerCount, 0);
                
                if (remainingTime > 0)
                {
                    gamePanel.UpdateTimer(remainingTime);
                }
            }
            
            Debug.Log("✅ Alternative joinedRoom parsing completed successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Alternative joinedRoom parsing failed: {e.Message}");
            Debug.LogError($"❌ Stack trace: {e.StackTrace}");
        }
    }

    private void HandleGameStart(GameStartMessage gameStartMsg)
    {
        Debug.Log("🚀 카운트다운 완료 - 게임이 실제로 시작됩니다!");
        Debug.Log($"🚀 Game duration: {gameStartMsg.gameDuration} seconds");
        
        isInGame = true;
        UpdateStatus("게임 진행 중");
        
        // GamePanel 활성화 (HideAllPanels에서 비활성화된 상태이므로 반드시 활성화해야 함)
        if (gamePanel != null && !gamePanel.gameObject.activeSelf)
        {
            gamePanel.gameObject.SetActive(true);
            Debug.Log("✅ GamePanel 활성화 (게임 시작)");
        }
        
        // Press 버튼 활성화 - 이제 플레이어가 눌러서 점수를 획득할 수 있음
        if (pressButton) 
        {
            pressButton.interactable = true;
            Debug.Log("✅ Press 버튼 활성화! 이제 버튼을 눌러 점수를 획득하세요!");
        }
        else
        {
            Debug.LogWarning("⚠️ Press button not found!");
        }

        // 대기 패널 숨기기 (게임 시작되었으므로)
        if (waitingRoomPanel != null)
        {
            waitingRoomPanel.HideWaitingPanel();
            waitingRoomPanel.gameObject.SetActive(false);
        }
        
        // GamePanel 상태 업데이트
        if (gamePanel != null)
        {
            gamePanel.ActivateGameUI();
            Debug.Log("✅ GamePanel 상태를 playing으로 업데이트 - 카운트다운 UI 숨김 처리");
            
            if (gameStartMsg.gameDuration > 0)
            {
                gamePanel.SetTotalGameDuration(gameStartMsg.gameDuration);
                gamePanel.UpdateTimer(gameStartMsg.gameDuration);
                remainingTime = gameStartMsg.gameDuration;
                Debug.Log($"⏰ 게임 타이머 설정: {gameStartMsg.gameDuration}초");
            }
            
            // 플레이어 목록 처리
            if (gameStartMsg.data != null && gameStartMsg.data.players != null && gameStartMsg.data.players.Length > 0)
            {
                Debug.Log($"📋 Processing {gameStartMsg.data.players.Length} players from gameStart");
                gamePanel.ClearAllPlayers();
                
                foreach (var player in gameStartMsg.data.players)
                {
                    Debug.Log($"   👤 Adding player: {player.name} - Score: {player.score}");
                    gamePanel.AddOrUpdatePlayer(
                        player.id,
                        player.name,
                        player.team,
                        player.score,
                        player.connected,
                        player.isAdmin,
                        player.isObserver
                    );
                }
                
                Debug.Log("✅ All players added to GamePanel");
            }
            else
            {
                Debug.LogWarning("⚠️ No player data in gameStart message");
            }
            
            // 플레이어 수 업데이트 (개인전)
            if (gameStartMsg.data?.players != null)
            {
                int totalPlayers = gameStartMsg.data.players.Length;
                Debug.Log($"📊 총 플레이어: {totalPlayers}명 (개인전)");
                gamePanel.UpdateTotalPlayersCount(totalPlayers, 0);
            }
        }
        
        // CrystalBall 리셋 (게임 시작 시 온전한 상태로 초기화)
        if (crystalBall != null)
        {
            crystalBall.ResetCrystalBall();  // 이전 게임의 상태를 초기화
            Debug.Log("💎 CrystalBall 리셋 완료! (게임 종료 시 서버에서 애니메이션 재생 신호를 받음)");
        }
        else
        {
            Debug.LogWarning("⚠️ CrystalBall이 할당되지 않았습니다!");
        }
        
        // ❄️ Frost 효과 60초 동안 감소 시작 (1 → 0) - 풀영상 시스템 게임 시간
        if (frostCameraEffect != null)
        {
            // 이전 코루틴이 실행 중이면 중지
            if (frostDecreaseCoroutine != null)
            {
                StopCoroutine(frostDecreaseCoroutine);
            }
            frostDecreaseCoroutine = StartCoroutine(DecreaseFrostEffect(60f));
            Debug.Log("❄️ Frost 효과 60초 동안 감소 시작 (1.0 → 0.0)");
        }
        else
        {
            Debug.LogWarning("⚠️ FrostCameraEffect가 할당되지 않았습니다!");
        }
        
        // Start local timer
        StartCoroutine(GameTimer());
        Debug.Log("🎮 게임 시작 완료! 플레이어는 이제 Press 버튼을 눌러 점수를 획득할 수 있습니다!");
    }

    private void HandleTimeUpdate(TimeUpdateMessage timeUpdateMsg)
    {
        remainingTime = timeUpdateMsg.remainingTime;
        Debug.Log($"⏰ Time update: {remainingTime} seconds remaining");
        
        // GamePanel 타이머 업데이트
        if (gamePanel != null)
        {
            gamePanel.UpdateTimer(remainingTime);
        }
        
        UpdateTimer();
    }

    private void HandleScoreUpdate(ScoreUpdateMessage scoreUpdateMsg)
    {
        // 개인전 - players 배열로 전체 플레이어 점수 업데이트
        if (scoreUpdateMsg?.data?.players != null && gamePanel != null)
        {
            Debug.Log($"📊 점수 업데이트 수신 - {scoreUpdateMsg.data.players.Length}명의 플레이어");
            
            // currentPlayerId가 설정되지 않았으면 cachedPlayerId로 설정
            if (string.IsNullOrEmpty(gamePanel.GetCurrentPlayerId()) && !string.IsNullOrEmpty(cachedPlayerId))
            {
                gamePanel.SetCurrentPlayerId(cachedPlayerId);
                Debug.Log($"🆔 scoreUpdate에서 currentPlayerId 복구: {cachedPlayerId}");
            }
            
            foreach (var player in scoreUpdateMsg.data.players)
            {
                string playerId = player.id ?? "";
                string playerName = player.name ?? "";
                string team = player.team ?? "";
                int score = player.score;
                bool connected = player.connected;
                bool isAdmin = player.isAdmin;
                bool isObserver = player.isObserver;
                
                gamePanel.AddOrUpdatePlayer(playerId, playerName, team, score, connected, isAdmin, isObserver);
            }
        }
        // 레거시 호환: playerUpdate 필드가 있는 경우
        else if (scoreUpdateMsg?.data?.playerUpdate != null && gamePanel != null)
        {
            var playerUpdate = scoreUpdateMsg.data.playerUpdate;
            string playerId = playerUpdate.playerId ?? "";
            string playerName = playerUpdate.playerName ?? "";
            string team = playerUpdate.team ?? "";
            int newScore = playerUpdate.newScore;
            
            bool isAdmin = playerName.Contains("Admin");
            bool isObserver = playerName.Contains("Observer") || playerName.Contains("Admin");
            gamePanel.AddOrUpdatePlayer(playerId, playerName, team, newScore, true, isAdmin, isObserver);
        }
        else
        {
            Debug.LogWarning("⚠️ 점수 업데이트 실패 - 메시지 데이터가 null이거나 GamePanel이 없습니다.");
        }
        
        // lastPress 정보 처리
        if (scoreUpdateMsg?.data?.lastPress != null)
        {
            var lastPress = scoreUpdateMsg.data.lastPress;
            Debug.Log($"👆 최근 Press: {lastPress.playerName} - 개인 점수: {lastPress.playerScore}");
        }
    }

    private void HandleGameStateUpdate(GameStateUpdateMessage gameStateMsg)
    {
        Debug.Log("📊 Processing gameStateUpdate message");
        
        if (gamePanel != null)
        {
            // 게임 상태 업데이트
            if (!string.IsNullOrEmpty(gameStateMsg.status))
            {
                Debug.Log($"🎮 게임 상태: {gameStateMsg.status}");
            }
            
            // 타이머 업데이트
            if (gameStateMsg.remainingTime > 0)
            {
                gamePanel.UpdateTimer(gameStateMsg.remainingTime);
                remainingTime = gameStateMsg.remainingTime;
            }
            
            // NOTE: 팀 점수 업데이트 제거 (개인전 전환으로 UpdateTeamScores 불필요)
            
            // 플레이어 수 업데이트
            if (gameStateMsg.playerCount != null)
            {
                gamePanel.UpdateTotalPlayersCount(gameStateMsg.playerCount.active, gameStateMsg.playerCount.observers);
            }
        }
    }

    /// <summary>
    /// 게임 종료를 5초 딜레이와 함께 처리하는 코루틴
    /// </summary>
    private IEnumerator HandleGameEndWithDelay(GameEndMessage gameEndMsg)
    {
        Debug.Log("🏁 Game ended! Starting 5 seconds ending sequence...");
        isInGame = false;
        isShowingGameEndSequence = true;  // 게임 종료 연출 시작
        
        // Press 버튼 비활성화 - 게임이 끝났으므로 더 이상 눌러도 점수가 오르지 않음
        if (pressButton) 
        {
            pressButton.interactable = false;
            Debug.Log("🔒 Press 버튼 비활성화 (게임 종료)");
        }
        
        // 개인전 우승자 정보 추출
        string winner = "Unknown";
        
        // 루트 레벨 winner (문자열)
        if (!string.IsNullOrEmpty(gameEndMsg.winner))
        {
            winner = gameEndMsg.winner;
        }
        
        // data.winner (오브젝트 - 서버에서 {nickname, score, playerId} 형태로 전송)
        if (gameEndMsg.data != null && gameEndMsg.data.winner != null)
        {
            winner = gameEndMsg.data.winner.nickname ?? winner;
        }
        
        Debug.Log($"🏆 Game ended - Winner: {winner} (개인전)");
        
        // 게임 종료 시 승리/패배에 따른 진동 패턴
        if (winner == "Draw")
        {
            // 무승부 시 중간 강도 진동 패턴 (100ms x 3번)
            VibratePattern(new int[] { 100, 100, 100, 100, 100 });
        }
        else
        {
            // 승부 결정 시 강한 진동 패턴 (승리/패배 구분 없이 게임 종료를 알림)
            VibratePattern(new int[] { 300, 150, 300, 150, 300 });
        }
        
        // CrystalBall 깨지는 애니메이션 재생 (서버에서 게임 종료 신호를 받았을 때)
        if (crystalBall != null)
        {
            crystalBall.PlayBreakAnimation();
            Debug.Log("💎 CrystalBall 깨지는 애니메이션 재생 시작");
        }
        
        // ❄️ Frost 효과 감소 코루틴 중지 (게임 종료)
        if (frostDecreaseCoroutine != null)
        {
            StopCoroutine(frostDecreaseCoroutine);
            frostDecreaseCoroutine = null;
            Debug.Log("❄️ Frost 효과 감소 코루틴 중지 (게임 종료)");
        }
        
        // GamePanel 업데이트 (게임 화면에 결과 표시 - 5초 동안 유지)
        if (gamePanel != null)
        {
            gamePanel.ShowGameResult(winner);
            
            Debug.Log("🎬 게임 결과 표시 중... (5초 대기, 게임 UI 유지)");
        }

        // 🎬 5초 동안 게임 종료 연출 진행 (게임 UI 유지, CrystalBall은 이미 숨김)
        yield return new WaitForSeconds(5f);
        
        Debug.Log("✅ 5초 경과! 게임 UI 비활성화 및 결과 패널 표시 시작");
        
        // 게임 종료 연출 완료
        isShowingGameEndSequence = false;
        
        // GameUI 비활성화
        if (gamePanel != null)
        {
            gamePanel.DeactivateGameUI();
            Debug.Log("🚫 GameUI 비활성화 완료");
        }
        
        // NickNamePanel 먼저 활성화 (ResultPanel 뒤에 깔리도록)
        if (nickNamePanel != null)
        {
            nickNamePanel.gameObject.SetActive(true);
            Debug.Log("✅ NickNamePanel 활성화 (ResultPanel 뒤에 준비)");
        }
        else
        {
            Debug.LogWarning("⚠️ NickNamePanel이 할당되지 않았습니다!");
        }
        
        // ResultPanel 표시 (위에 표시됨)
        Debug.Log($"🔍 ResultPanel check - resultPanel: {resultPanel != null}, gameEndMsg.data: {gameEndMsg?.data != null}, rankings: {gameEndMsg?.data?.rankings != null}");
        
        if (resultPanel == null)
        {
            Debug.LogError("❌ ResultPanel이 할당되지 않았습니다! WebGLProgram Inspector에서 ResultPanel을 할당하세요.");
        }
        else if (gameEndMsg == null)
        {
            Debug.LogError("❌ gameEndMsg가 null입니다!");
        }
        else if (gameEndMsg.data == null)
        {
            Debug.LogError("❌ gameEndMsg.data가 null입니다!");
        }
        else if (gameEndMsg.data.rankings == null)
        {
            Debug.LogError("❌ gameEndMsg.data.rankings가 null입니다!");
        }
        else
        {
            Debug.Log($"📊 Showing result panel with {gameEndMsg.data.rankings.Length} players");
            
            // PlayerRankData 배열을 List로 변환
            List<PlayerRankData> rankings = new List<PlayerRankData>();
            foreach (var rank in gameEndMsg.data.rankings)
            {
                Debug.Log($"   Rank data: {rank.playerId} - {rank.nickname} - {rank.score}점");
                rankings.Add(rank);
            }
            
            Debug.Log($"🎯 Calling resultPanel.ShowResults (개인전)");
            // ResultPanel 표시 (개인전)
            string currentId = !string.IsNullOrEmpty(cachedPlayerId) ? cachedPlayerId : (gamePanel != null ? gamePanel.GetCurrentPlayerId() : "");
            Debug.Log($"🔑 결과 화면에 사용할 playerId: '{currentId}' (cached: '{cachedPlayerId}')");
            resultPanel.ShowResults(winner, rankings, currentId);
        }
        
        // CrystalBall은 서버 신호에 따라 애니메이션을 재생하므로 별도의 중지가 필요 없음
        // (리셋은 다음 게임 시작 시 자동으로 수행됨)
        if (crystalBall != null)
        {
            Debug.Log("💎 CrystalBall 상태 유지 (리셋은 다음 게임 시작 시)");
        }
        
        UpdateStatus("Game finished");
    }

    private string ExtractWinner(string message)
    {
        int winnerIndex = message.IndexOf("\"winner\":\"") + 10;
        int endIndex = message.IndexOf("\"", winnerIndex);
        
        if (winnerIndex > 9 && endIndex > winnerIndex)
        {
            return message.Substring(winnerIndex, endIndex - winnerIndex);
        }
        return "Unknown";
    }

    private string ExtractFinalScore(string message, string team)
    {
        string searchStr = $"\"{team}\":";
        int index = message.IndexOf(searchStr);
        if (index != -1)
        {
            index = message.IndexOf(":", index) + 1;
            int endIndex = message.IndexOf(",", index);
            if (endIndex == -1) endIndex = message.IndexOf("}", index);
            
            if (endIndex > index)
            {
                return message.Substring(index, endIndex - index).Trim();
            }
        }
        return "0";
    }

    private void HandleError(ErrorMessage errorMsg)
    {
        string errorMessage = errorMsg.message ?? "Unknown error";
        string errorCode = errorMsg.code ?? "";
        
        Debug.LogError($"Server error: {errorMessage} (Code: {errorCode})");
        
        // Update GamePanel to show error state
        if (gamePanel != null)
        {
            Debug.Log("❌ 오류 상태");
        }
    }

    private void HandleRoomInfo(RoomInfoMessage roomInfoMsg)
    {
        Debug.Log($" Room info received");
        
        if (roomInfoMsg?.data?.room == null)
        {
            Debug.LogWarning("⚠️ Room info data is null");
            return;
        }

        var room = roomInfoMsg.data.room;
        var player = roomInfoMsg.data.player;
        string status = room.status ?? "waiting";
        
        Debug.Log($" Room status: {status}, Remaining time: {room.remainingTime}");
        
        // 자신의 플레이어 정보가 있다면 AddOrUpdatePlayer 호출
        if (player != null && gamePanel != null)
        {
            string playerId = player.id ?? "";
            string playerName = player.name ?? this.playerName;
            string team = player.team ?? "";
            int score = player.score;
            bool connected = player.connected;
            bool isAdmin = player.isAdmin;
            
            Debug.Log($" RoomInfo에서 자신의 플레이어 정보 발견 - ID: {playerId}, Name: {playerName}, Score: {score}, Admin: {isAdmin}");
            
            if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(playerName))
            {
                // 먼저 현재 플레이어 ID 설정
                gamePanel.SetCurrentPlayerId(playerId);
                cachedPlayerId = playerId;
                Debug.Log($"🆔 RoomInfo에서 현재 플레이어 ID 설정 완료: {playerId} (cachedPlayerId도 저장)");
                
                Debug.Log($" RoomInfo에서 AddOrUpdatePlayer 호출...");
                // isAdmin과 isObserver 정보도 전달
                bool isObserver = team == "Observer" || playerName.Contains("Observer");
                gamePanel.AddOrUpdatePlayer(playerId, playerName, team, score, connected, isAdmin, isObserver);
                Debug.Log($" RoomInfo에서 플레이어 UI 생성 완료: {playerName} ({team}) [Admin: {isAdmin}]");
            }
        }
        
        // Check if game is already in progress
        if (status == "playing")
        {
            Debug.Log(" Game is already in progress!");
            isInGame = true;
            UpdateStatus("Game in progress");
            
            if (pressButton) 
            {
                pressButton.interactable = true;
                Debug.Log(" Press button activated from room info!");
            }
            
            if (room.remainingTime > 0)
            {
                remainingTime = room.remainingTime;
                Debug.Log($" Remaining time from room info: {remainingTime}");
                
                if (gamePanel != null)
                {
                    gamePanel.UpdateTimer(remainingTime);
                }
                
                UpdateTimer();
                StartCoroutine(GameTimer());
            }
        }
        else
        {
            Debug.Log("Game is waiting to start");
            isInGame = false;
            UpdateStatus("Waiting for game to start");
            
            if (pressButton) pressButton.interactable = false;
        }
        
        // 룸 상태 업데이트 (개인전)
        if (gamePanel != null)
        {
            Debug.Log($"🎮 게임 상태: {status}");
        }
    }

    private void HandlePlayerJoined(PlayerJoinedMessage playerJoinedMsg)
    {
        Debug.Log($"👤 새 플레이어 입장 브로드캐스트 수신!");
        
        try
        {
            if (playerJoinedMsg?.data == null)
            {
                Debug.LogWarning("⚠️ PlayerJoined 메시지 또는 data가 null입니다!");
                Debug.LogWarning($"⚠️ playerJoinedMsg: {playerJoinedMsg != null}");
                if (playerJoinedMsg != null)
                {
                    Debug.LogWarning($"⚠️ playerJoinedMsg.data: {playerJoinedMsg.data != null}");
                }
                return;
            }
            
            var data = playerJoinedMsg.data;
            string playerId = data.playerId ?? "";
            string playerName = data.playerName ?? "";
            string team = data.team ?? "";
            
            Debug.Log($"👤 새 플레이어 정보 - ID: '{playerId}', 이름: '{playerName}', 팀: '{team}'");
            
            // Create player UI in GamePanel
            if (gamePanel != null && !string.IsNullOrEmpty(playerId) && !string.IsNullOrEmpty(playerName))
            {
                // Admin과 Observer 판단 로직 (Desktop과 동일)
                bool isAdmin = playerName.Contains("Admin") || playerName.Contains("AdminDesktop");
                bool isObserver = team == "Observer" || playerName.Contains("Observer") || playerName.Contains("Admin");
                
                Debug.Log($"👤 새 플레이어 UI 생성 중 - Name: {playerName}, Team: {team}, Admin: {isAdmin}, Observer: {isObserver}");
                
                gamePanel.AddOrUpdatePlayer(playerId, playerName, team, 0, true, isAdmin, isObserver);
                Debug.Log($"✅ 새 플레이어 UI 생성 완료: {playerName} ({team}) [Admin: {isAdmin}, Observer: {isObserver}]");
            }
            else
            {
                Debug.LogWarning($"⚠️ 새 플레이어 UI 생성 실패:");
                Debug.LogWarning($"   - gamePanel: {gamePanel != null}");
                Debug.LogWarning($"   - playerId: '{playerId}' (비어있음: {string.IsNullOrEmpty(playerId)})");
                Debug.LogWarning($"   - playerName: '{playerName}' (비어있음: {string.IsNullOrEmpty(playerName)})");
            }
            
            // 개인전 - 팀 정보 업데이트 제거
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error processing playerJoined message: {e.Message}");
        }
    }

    private void HandlePlayerLeft(PlayerLeftMessage playerLeftMsg)
    {
        Debug.Log($"👋 플레이어 퇴장 브로드캐스트 수신!");
        
        try
        {
            if (playerLeftMsg?.data == null)
            {
                Debug.LogWarning("⚠️ PlayerLeft 메시지 또는 data가 null입니다!");
                return;
            }
            
            var data = playerLeftMsg.data;
            string playerId = data.playerId ?? "";
            string playerName = data.playerName ?? "";
            string team = data.team ?? "";
            
            Debug.Log($"👋 플레이어 퇴장 정보 - ID: {playerId}, 이름: {playerName}, 팀: {team}");
            
            // Remove player UI from GamePanel
            if (gamePanel != null && !string.IsNullOrEmpty(playerId))
            {
                gamePanel.RemovePlayer(playerId);
                Debug.Log($"✅ 플레이어 UI 제거 완료: {playerName} ({team})");
            }
            else
            {
                Debug.LogWarning($"⚠️ 플레이어 UI 제거 실패:");
                Debug.LogWarning($"   - gamePanel: {gamePanel != null}");
                Debug.LogWarning($"   - playerId: '{playerId}' (비어있음: {string.IsNullOrEmpty(playerId)})");
            }
            
            // 개인전 - 팀 정보 업데이트 제거
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error processing playerLeft message: {e.Message}");
        }
    }

    private void HandleCountdownStartFromDesktop(int countdown)
    {
        Debug.Log($"⏰ Desktop에서 게임 시작 카운트다운 시작! 시작 카운트: {countdown}");
        
        // 🎬 IntroPanel 비활성화 (풀영상 시스템 - 인트로 구간 삭제)
        if (introPanel != null && introPanel.activeSelf)
        {
            introPanel.gameObject.SetActive(false);
            Debug.Log("🎬 IntroPanel 비활성화 (풀영상 시스템)");
        }
        
        // ❄️ Frost 효과를 최대치로 설정 (게임 시작 전)
        if (frostCameraEffect != null)
        {
            frostCameraEffect.FrostAmount = 1f;
            Debug.Log("❄️ Frost 효과 최대치로 설정 (1.0)");
        }
        else
        {
            Debug.LogWarning("⚠️ FrostCameraEffect가 할당되지 않았습니다!");
        }
        
        // 카운트다운이 0초면 바로 게임 시작 대기
        if (countdown == 0)
        {
            Debug.Log("⚡ 카운트다운 0초 - 바로 게임 시작 준비!");
            
            if (gamePanel != null)
            {
                gamePanel.ActivateGameUI();
                Debug.Log("✅ GamePanel 상태를 playing으로 업데이트 (카운트다운 없음)");
            }
        }
        else if (gamePanel != null)
        {
            // 카운트다운 시작 - 첫 번째 숫자 표시
            string countdownText = countdown > 0 ? countdown.ToString() : "게임시작!";
            Debug.Log($"⏰ 카운트다운: {countdownText}");
            Debug.Log($"✅ GamePanel에 카운트다운 시작 상태 업데이트: {countdownText}");
        }
        
        // Press 버튼 비활성화 (게임 시작 전까지)
        if (pressButton != null)
        {
            pressButton.interactable = false;
            Debug.Log("🔒 Press 버튼 비활성화 (게임 시작 전)");
        }
        
        // 상태 업데이트
        isInGame = false; // 아직 게임이 시작되지 않음
        UpdateStatus(countdown > 0 ? $"게임 시작까지 {countdown}초..." : "게임 시작!");
        
        // 카운트다운 시작 시 짧은 진동
        Vibrate(100);
    }

    private void HandleCountdownStart(CountdownStartMessage countdownStartMsg)
    {
        int countdown = countdownStartMsg?.countdown ?? 0;
        Debug.Log($"⏰ 게임 시작 카운트다운 시작! 시작 카운트: {countdown}");
        
        // GamePanel 활성화 (카운트다운 UI 표시를 위해)
        if (gamePanel != null && !gamePanel.gameObject.activeSelf)
        {
            gamePanel.gameObject.SetActive(true);
            Debug.Log("✅ GamePanel 활성화 (카운트다운 시작)");
        }
        
        // 🎬 IntroPanel 비활성화 (풀영상 시스템 - 인트로 구간 삭제)
        if (introPanel != null && introPanel.activeSelf)
        {
            introPanel.gameObject.SetActive(false);
            Debug.Log("🎬 IntroPanel 비활성화 (풀영상 시스템)");
        }
        
        // ❄️ Frost 효과를 최대치로 설정 (게임 시작 전)
        if (frostCameraEffect != null)
        {
            frostCameraEffect.FrostAmount = 1f;
            Debug.Log("❄️ Frost 효과 최대치로 설정 (1.0)");
        }
        else
        {
            Debug.LogWarning("⚠️ FrostCameraEffect가 할당되지 않았습니다!");
        }
        
        // 카운트다운이 0초면 바로 게임 시작 대기
        if (countdown == 0)
        {
            Debug.Log("⚡ 카운트다운 0초 - 바로 게임 시작 준비!");
            
            if (gamePanel != null)
            {
                gamePanel.ActivateGameUI();
                Debug.Log("✅ GamePanel 상태를 playing으로 업데이트 (카운트다운 없음)");
            }
        }
        else if (gamePanel != null)
        {
            // 카운트다운 시작 - 첫 번째 숫자 표시
            string countdownText = countdown > 0 ? countdown.ToString() : "게임시작!";
            Debug.Log($"⏰ 카운트다운: {countdownText}");
            Debug.Log($"✅ GamePanel에 카운트다운 시작 상태 업데이트: {countdownText}");
        }
        
        // Press 버튼 비활성화 (게임 시작 전까지)
        if (pressButton != null)
        {
            pressButton.interactable = false;
            Debug.Log("🔒 Press 버튼 비활성화 (게임 시작 전)");
        }
        
        // 상태 업데이트
        isInGame = false; // 아직 게임이 시작되지 않음
        UpdateStatus(countdown > 0 ? $"게임 시작까지 {countdown}초..." : "게임 시작!");
        
        // 카운트다운 시작 시 짧은 진동
        Vibrate(100);
    }

    private void HandleCountdownUpdateFromDesktop(int countdown)
    {
        Debug.Log($"⏰ Desktop에서 카운트다운 업데이트: {countdown}");
        
        if (gamePanel != null)
        {
            // GamePanel에 카운트다운 상태를 문자열로 전달
            string countdownText = countdown > 0 ? countdown.ToString() : "게임시작!";
            Debug.Log($"⏰ 카운트다운: {countdownText}");
            Debug.Log($"✅ GamePanel에 카운트다운 상태 업데이트: {countdownText}");
            
            // 추가적으로 상태 텍스트 업데이트 (GamePanel에서 지원하는 경우)
            if (countdown > 0)
            {
                // 큰 숫자로 카운트다운 표시
                Debug.Log($"🔢 카운트다운 큰 숫자 표시: {countdown}");
            }
            else
            {
                // "게임시작!" 텍스트 표시
                Debug.Log($"🚀 게임시작 텍스트 표시");
            }
        }
        
        // 카운트다운 진행 상황에 따른 상태 업데이트
        if (countdown > 0)
        {
            UpdateStatus($"게임 시작까지 {countdown}초...");
            
            // 카운트다운 각 숫자마다 진동 (150ms)
            Vibrate(150);
            
            Debug.Log($"📱 WebGL 카운트다운 UI 업데이트: {countdown}초 남음");
        }
        else
        {
            UpdateStatus("게임 시작!");
            Debug.Log("🎮 카운트다운 완료! 게임 시작 준비");
            
            // 게임 시작 시 강한 진동 패턴 (200ms 진동 - 100ms 멈춤 - 200ms 진동)
            VibratePattern(new int[] { 200, 100, 200 });
            
            Debug.Log("🚀 WebGL에서 '게임시작!' 표시 완료");
            
            // 실제 게임 시작은 gameStart 메시지에서 처리됨
        }
        
        // Press 버튼은 계속 비활성화 상태 유지 (gameStart에서 활성화)
        if (pressButton != null && countdown == 0)
        {
            Debug.Log("🎮 카운트다운 완료 - gameStart 메시지 대기 중...");
        }
    }

    private void HandleCountdownUpdate(CountdownUpdateMessage countdownUpdateMsg)
    {
        int countdown = countdownUpdateMsg.countdown;
        Debug.Log($"⏰ 카운트다운 업데이트: {countdown}");
        
        if (gamePanel != null)
        {
            // GamePanel에 카운트다운 상태를 문자열로 전달
            string countdownText = countdown > 0 ? countdown.ToString() : "게임시작!";
            Debug.Log($"⏰ 카운트다운: {countdownText}");
            Debug.Log($"✅ GamePanel에 카운트다운 상태 업데이트: {countdownText}");
        }
        
        // 카운트다운이 0이 되면 (게임시작!) Press 버튼 활성화 준비
        if (countdown == 0)
        {
            Debug.Log("🎮 게임시작! Press 버튼 활성화 준비");
            // 실제 게임 시작은 gameStart 메시지에서 처리됨
        }
    }

    private void HandleAutoReset(string message)
    {
        Debug.Log("🔄 게임이 자동으로 대기 상태로 리셋되었습니다!");
        
        // 게임 상태 초기화
        isInGame = false;
        
        // Press 버튼 비활성화
        if (pressButton != null)
        {
            pressButton.interactable = false;
            Debug.Log("🔒 Press 버튼 비활성화 (자동 리셋)");
        }
        
        // GamePanel 상태 업데이트
        if (gamePanel != null)
        {
            Debug.Log("🎮 게임 상태: waiting");
            
            // 타이머와 점수 초기화
            try
            {
                // JObject를 사용하여 안전하게 JSON 파싱
                var autoResetData = JObject.Parse(message);
                var data = autoResetData["data"];
                
                if (data?["remainingTime"] != null)
                {
                    int remainingTime = data["remainingTime"].Value<int>();
                    gamePanel.UpdateTimer(remainingTime);
                    this.remainingTime = remainingTime;
                }
                
                if (data?["teams"] != null)
                {
                    // 개인전 - 팀 점수 제거
                    Debug.Log($"🔄 자동 리셋 완료 (개인전)");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ AutoReset 데이터 처리 오류: {e.Message}");
            }
        }
        
        UpdateStatus("Waiting for game to start");
    }

    private void HandlePlayerScoreUpdateFromMessage(string message)
    {
        try
        {
            // JSON에서 playerUpdate 정보 추출
            if (message.Contains("playerUpdate"))
            {
                // JObject를 사용하여 안전하게 JSON 파싱
                var messageData = JObject.Parse(message);
                var playerUpdate = messageData["data"]?["playerUpdate"];
                
                if (playerUpdate != null && gamePanel != null)
                {
                    string playerId = playerUpdate["playerId"]?.ToString() ?? "";
                    string playerName = playerUpdate["playerName"]?.ToString() ?? "";
                    string team = playerUpdate["team"]?.ToString() ?? "";
                    int newScore = playerUpdate["newScore"]?.Value<int>() ?? 0;
                    int newPressCount = playerUpdate["newPressCount"]?.Value<int>() ?? 0;
                    
                    Debug.Log($"🎯 개별 플레이어 점수 업데이트: {playerName} ({team}) - 점수: {newScore}, 누른 횟수: {newPressCount}");
                    
                    // GamePanel에서 해당 플레이어의 정보 업데이트 (기존 메서드 사용)
                    // UpdatePlayerScore 메서드가 없으므로 AddOrUpdatePlayer로 플레이어 정보 업데이트
                    bool isAdmin = playerName.Contains("Admin");
                    bool isObserver = team == "Observer" || playerName.Contains("Observer") || playerName.Contains("Admin");
                    gamePanel.AddOrUpdatePlayer(playerId, playerName, team, newScore, true, isAdmin, isObserver);
                }
                
                // lastPress 정보도 처리
                var lastPress = messageData["data"]?["lastPress"];
                if (lastPress != null)
                {
                    string pressPlayerName = lastPress["playerName"]?.ToString() ?? "";
                    string pressTeam = lastPress["team"]?.ToString() ?? "";
                    int playerScore = lastPress["playerScore"]?.Value<int>() ?? 0;
                    int teamScore = lastPress["teamScore"]?.Value<int>() ?? 0;
                    
                    Debug.Log($"👆 최근 Press: {pressPlayerName} ({pressTeam}) - 개인 점수: {playerScore}, 팀 점수: {teamScore}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 개별 플레이어 점수 업데이트 처리 오류: {e.Message}");
        }
    }

    private string ExtractPlayerInfo(string message, string field)
    {
        string searchStr = $"\"{field}\":\"";
        int index = message.IndexOf(searchStr);
        if (index != -1)
        {
            index += searchStr.Length;
            int endIndex = message.IndexOf("\"", index);
            
            if (endIndex > index)
            {
                return message.Substring(index, endIndex - index);
            }
        }
        return "";
    }

    // Helper methods for parsing joinedRoom message  
    // Based on server structure: {"type":"joinedRoom","data":{"playerId":"xxx","player":{...},"room":{...}}}
    private string ExtractPlayerFromJoinedRoom(string message, string field)
    {
        Debug.Log($"🔍 ExtractPlayerFromJoinedRoom called for field: {field}");
        
        // Look for the player object in the data section
        string searchStr = "\"player\":{";
        int playerStart = message.IndexOf(searchStr);
        Debug.Log($"🔍 Player section found at index: {playerStart}");
        
        if (playerStart == -1)
        {
            Debug.LogWarning($"⚠️ Player section not found in message");
            return "";
        }
        
        // Find the end of the player object to limit search scope
        int playerEnd = FindMatchingBrace(message, playerStart + searchStr.Length - 1);
        Debug.Log($"🔍 Player section ends at index: {playerEnd}");
        
        // Find the specific field within the player object
        string fieldSearchStr = $"\"{field}\":\"";
        int fieldIndex = message.IndexOf(fieldSearchStr, playerStart);
        
        // Make sure we're still within the player object
        if (fieldIndex == -1 || (playerEnd != -1 && fieldIndex > playerEnd))
        {
            Debug.LogWarning($"⚠️ Field '{field}' not found in player section");
            
            // Try without quotes for boolean/numeric fields
            string altFieldSearchStr = $"\"{field}\":";
            int altFieldIndex = message.IndexOf(altFieldSearchStr, playerStart);
            if (altFieldIndex != -1 && (playerEnd == -1 || altFieldIndex < playerEnd))
            {
                Debug.Log($"🔍 Found field '{field}' without quotes at index: {altFieldIndex}");
                altFieldIndex += altFieldSearchStr.Length;
                int altEndIndex = message.IndexOfAny(new char[] { ',', '}' }, altFieldIndex);
                if (altEndIndex > altFieldIndex)
                {
                    string result = message.Substring(altFieldIndex, altEndIndex - altFieldIndex).Trim().Trim('"');
                    Debug.Log($"🔍 Extracted value: '{result}'");
                    return result;
                }
            }
            return "";
        }
        
        fieldIndex += fieldSearchStr.Length;
        int endIndex = message.IndexOf("\"", fieldIndex);
        
        if (endIndex > fieldIndex)
        {
            string result = message.Substring(fieldIndex, endIndex - fieldIndex);
            Debug.Log($"🔍 Extracted value: '{result}'");
            return result;
        }
        
        Debug.LogWarning($"⚠️ Could not extract value for field '{field}'");
        return "";
    }
    
    // Helper method to find matching closing brace
    private int FindMatchingBrace(string message, int startIndex)
    {
        int braceCount = 1;
        for (int i = startIndex + 1; i < message.Length; i++)
        {
            if (message[i] == '{') braceCount++;
            else if (message[i] == '}') braceCount--;
            
            if (braceCount == 0) return i;
        }
        return -1;
    }
    
    private int ExtractPlayerScoreFromJoinedRoom(string message)
    {
        string searchStr = "\"player\":{";
        int playerStart = message.IndexOf(searchStr);
        if (playerStart == -1) return 0;
        
        string scoreSearchStr = "\"score\":";
        int scoreIndex = message.IndexOf(scoreSearchStr, playerStart);
        if (scoreIndex == -1) return 0;
        
        scoreIndex += scoreSearchStr.Length;
        int endIndex = message.IndexOf(",", scoreIndex);
        if (endIndex == -1) endIndex = message.IndexOf("}", scoreIndex);
        
        if (endIndex > scoreIndex)
        {
            string scoreStr = message.Substring(scoreIndex, endIndex - scoreIndex);
            if (int.TryParse(scoreStr, out int score))
            {
                return score;
            }
        }
        return 0;
    }
    
    private bool ExtractPlayerConnectedFromJoinedRoom(string message)
    {
        string searchStr = "\"player\":{";
        int playerStart = message.IndexOf(searchStr);
        if (playerStart == -1) return false;
        
        string connectedSearchStr = "\"connected\":";
        int connectedIndex = message.IndexOf(connectedSearchStr, playerStart);
        if (connectedIndex == -1) return true; // default to true
        
        connectedIndex += connectedSearchStr.Length;
        int endIndex = message.IndexOf(",", connectedIndex);
        if (endIndex == -1) endIndex = message.IndexOf("}", connectedIndex);
        
        if (endIndex > connectedIndex)
        {
            string connectedStr = message.Substring(connectedIndex, endIndex - connectedIndex);
            return connectedStr.Trim() == "true";
        }
        return true;
    }
    
    private int ExtractTeamCountFromJoinedRoom(string message, string team)
    {
        Debug.Log($"🔍 ExtractTeamCountFromJoinedRoom called for team: {team}");
        
        // Look for teams object within room object: "room":{"teams":{"White":{"score":X,"playerCount":Y}}}
        string roomSearchStr = "\"room\":{";
        int roomStart = message.IndexOf(roomSearchStr);
        if (roomStart == -1) 
        {
            Debug.LogWarning($"⚠️ Room section not found");
            return 0;
        }
        
        string teamsSearchStr = "\"teams\":{";
        int teamsStart = message.IndexOf(teamsSearchStr, roomStart);
        if (teamsStart == -1) 
        {
            Debug.LogWarning($"⚠️ Teams section not found");
            return 0;
        }
        
        string teamSearchStr = $"\"{team}\":{{";
        int teamStart = message.IndexOf(teamSearchStr, teamsStart);
        if (teamStart == -1) 
        {
            Debug.LogWarning($"⚠️ Team '{team}' not found");
            return 0;
        }
        
        string countSearchStr = "\"playerCount\":";
        int countIndex = message.IndexOf(countSearchStr, teamStart);
        if (countIndex == -1) 
        {
            Debug.LogWarning($"⚠️ PlayerCount field not found for team '{team}'");
            return 0;
        }
        
        countIndex += countSearchStr.Length;
        int endIndex = message.IndexOfAny(new char[] { ',', '}' }, countIndex);
        
        if (endIndex > countIndex)
        {
            string countStr = message.Substring(countIndex, endIndex - countIndex);
            if (int.TryParse(countStr, out int count))
            {
                Debug.Log($"🔍 Extracted {team} team count: {count}");
                return count;
            }
        }
        
        Debug.LogWarning($"⚠️ Failed to parse team count for '{team}'");
        return 0;
    }
    
    private int ExtractTeamScoreFromJoinedRoom(string message, string team)
    {
        Debug.Log($"🔍 ExtractTeamScoreFromJoinedRoom called for team: {team}");
        
        // Look for teams object within room object: "room":{"teams":{"White":{"score":X,"playerCount":Y}}}
        string roomSearchStr = "\"room\":{";
        int roomStart = message.IndexOf(roomSearchStr);
        if (roomStart == -1) return 0;
        
        string teamsSearchStr = "\"teams\":{";
        int teamsStart = message.IndexOf(teamsSearchStr, roomStart);
        if (teamsStart == -1) return 0;
        
        string teamSearchStr = $"\"{team}\":{{\"score\":";
        int teamStart = message.IndexOf(teamSearchStr, teamsStart);
        if (teamStart == -1) return 0;
        
        teamStart += teamSearchStr.Length;
        int endIndex = message.IndexOfAny(new char[] { ',', '}' }, teamStart);
        
        if (endIndex > teamStart)
        {
            string scoreStr = message.Substring(teamStart, endIndex - teamStart);
            if (int.TryParse(scoreStr, out int score))
            {
                Debug.Log($"🔍 Extracted {team} team score: {score}");
                return score;
            }
        }
        return 0;
    }

    private int ExtractScore(string message, string team)
    {
        string searchStr = $"\"{team}\":{{\"score\":";
        int index = message.IndexOf(searchStr);
        if (index != -1)
        {
            index += searchStr.Length;
            int endIndex = message.IndexOf(",", index);
            if (endIndex == -1) endIndex = message.IndexOf("}", index);
            
            if (endIndex > index)
            {
                string scoreStr = message.Substring(index, endIndex - index);
                if (int.TryParse(scoreStr, out int score))
                {
                    return score;
                }
            }
        }
        return 0;
    }

    private IEnumerator GameTimer()
    {
        while (isInGame && remainingTime > 0)
        {
            UpdateTimer();
            yield return new WaitForSeconds(1f);
            remainingTime--;
        }
        
        // 로컬 타이머가 0에 도달했을 때 처리
        if (isInGame && remainingTime <= 0)
        {
            Debug.Log("⏰ 로컬 타이머 종료! 서버에서 게임 종료 메시지를 기다리는 중...");
            
            // Press 버튼 미리 비활성화 (서버 메시지를 기다리지 않고)
            if (pressButton)
            {
                pressButton.interactable = false;
                Debug.Log("🔒 시간 종료로 Press 버튼 비활성화");
            }
            
            // GamePanel에 시간 종료 상태 표시
            if (gamePanel != null)
            {
                gamePanel.UpdateTimer(0);
                Debug.Log("⏰ 게임 시간 종료!");
            }
        }
    }
    
    /// <summary>
    /// Frost 효과를 지정된 시간 동안 1에서 0으로 점진적으로 감소시키는 코루틴
    /// </summary>
    /// <param name="duration">감소 시간 (초)</param>
    private IEnumerator DecreaseFrostEffect(float duration)
    {
        if (frostCameraEffect == null)
        {
            Debug.LogWarning("⚠️ FrostCameraEffect가 null입니다. 코루틴 종료.");
            yield break;
        }
        
        float startAmount = frostCameraEffect.FrostAmount;
        float elapsedTime = 0f;
        
        Debug.Log($"❄️ Frost 감소 시작 - 시작값: {startAmount}, 목표값: 0, 지속시간: {duration}초");
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            
            // 선형 보간으로 1 → 0으로 감소
            frostCameraEffect.FrostAmount = Mathf.Lerp(startAmount, 0f, t);
            
            yield return null; // 다음 프레임까지 대기
        }
        
        // 정확히 0으로 설정
        frostCameraEffect.FrostAmount = 0f;
        frostDecreaseCoroutine = null;
        
        Debug.Log("❄️ Frost 효과 감소 완료 - 최종값: 0.0");
    }

        // These methods are no longer needed as GamePanel handles all UI updates
    // Kept for legacy compatibility but GamePanel should be used instead
    
    private void UpdateStatus()
    {
        // All status updates handled by GamePanel
        Debug.Log("📝 Status update request - using GamePanel instead");
    }

    private void UpdateStatus(string status)
    {
        // All status updates handled by GamePanel
        Debug.Log($"📝 Status update request: {status} - using GamePanel instead");
    }

    private void UpdatePlayerInfo()
    {
        // Player info updates handled by GamePanel
        Debug.Log("👤 Player info update request - using GamePanel instead");
    }

    private void UpdateTimer()
    {
        // Timer updates handled by GamePanel
        Debug.Log("⏰ Timer update request - using GamePanel instead");
    }

    // Methods for sending game-related messages
    //[ContextMenu("Send Press")]
    //void SendPress()
    //{
    //    SendMessage("{\"type\":\"press\"}");
    //}

    [ContextMenu("Join Main Room")]
    void JoinMainRoom()
    {
        JoinRoom(DEFAULT_ROOM_ID);
    }

    // 팀 선택 ContextMenu 제거 (개인전)

    [ContextMenu("Test Vibration - Short")]
    void TestVibrateShort()
    {
        Vibrate(100);
        Debug.Log("📳 테스트 진동 - 짧음 (100ms)");
    }

    [ContextMenu("Test Vibration - Medium")]
    void TestVibrateMedium()
    {
        Vibrate(300);
        Debug.Log("📳 테스트 진동 - 중간 (300ms)");
    }

    [ContextMenu("Test Vibration - Long")]
    void TestVibrateLong()
    {
        VibrateSeconds(1.0f);
        Debug.Log("📳 테스트 진동 - 김 (1초)");
    }

    [ContextMenu("Test Vibration Pattern")]
    void TestVibratePattern()
    {
        VibratePattern(new int[] { 200, 100, 200, 100, 200 });
        Debug.Log("📳 테스트 진동 패턴 - [200, 100, 200, 100, 200]ms");
    }

    [ContextMenu("Stop Vibration")]
    void TestStopVibration()
    {
        StopVibration();
        Debug.Log("📳 진동 중단");
    }

    [ContextMenu("Check Vibration Support")]
    void TestVibrationSupport()
    {
        bool supported = IsVibrationSupported();
        Debug.Log($"📳 진동 지원 여부: {supported}");
    }


    // Methods for testing in the Inspector
    [ContextMenu("Connect")]
    void ContextConnect()
    {
        ConnectToServer();
    }

    [ContextMenu("Disconnect")]
    void ContextDisconnect()
    {
        DisconnectFromServer();
    }

    [ContextMenu("Send Test Message")]
    void ContextSendTest()
    {
        SendTestMessage();
    }

    #region Nickname Handlers
    
    /// <summary>
    /// 닉네임 중복 확인 결과 처리
    /// </summary>
    private void HandleNicknameCheckResult(string message)
    {
        try
        {
            Debug.Log($"🔍 닉네임 중복 확인 결과 처리: {message}");
            
            var messageObj = JObject.Parse(message);
            var data = messageObj["data"];
            
            if (data == null)
            {
                Debug.LogError("❌ nicknameCheckResult data is null");
                return;
            }
            
            bool available = data["available"]?.Value<bool>() ?? false;
            string resultMessage = data["message"]?.ToString() ?? "";
            
            Debug.Log($"🔍 닉네임 확인 결과 - Available: {available}, Message: {resultMessage}");
            
            // NickNamePanel에 결과 전달
            if (nickNamePanel != null)
            {
                nickNamePanel.OnNicknameCheckResult(available, resultMessage);
            }
            else
            {
                Debug.LogWarning("⚠️ NickNamePanel이 할당되지 않았습니다!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 닉네임 확인 결과 처리 오류: {e.Message}");
            Debug.LogError($"❌ Stack trace: {e.StackTrace}");
        }
    }
    
    /// <summary>
    /// 닉네임 설정 결과 처리 (대기방 입장 또는 게임 진행 중 대기)
    /// </summary>
    private void HandleNicknameSetResult(string message)
    {
        try
        {
            Debug.Log($"🎯 닉네임 설정 결과 처리: {message}");
            
            var messageObj = JObject.Parse(message);
            var data = messageObj["data"];
            
            if (data == null)
            {
                Debug.LogError("❌ nicknameSetResult data is null");
                return;
            }
            
            bool success = data["success"]?.Value<bool>() ?? false;
            bool inLobby = data["inLobby"]?.Value<bool>() ?? false;
            bool isWaiting = data["isWaiting"]?.Value<bool>() ?? false;
            int countdown = 0;
            string gameStatus = data["gameStatus"]?.ToString() ?? "lobby";
            string resultMessage = data["message"]?.ToString() ?? "";
            string playerId = data["playerId"]?.ToString() ?? "";
            string nickname = data["nickname"]?.ToString() ?? "";
            string team = data["team"]?.ToString() ?? "";
            
            // countdown는 상태에 따라 다른 필드에서 가져옴
            if (inLobby)
            {
                countdown = data["lobbyCountdown"]?.Value<int>() ?? 0;
            }
            else if (isWaiting)
            {
                countdown = data["remainingTime"]?.Value<int>() ?? 0;
            }
            
            Debug.Log($"✅ 닉네임 설정 결과 - Success: {success}, InLobby: {inLobby}, IsWaiting: {isWaiting}, GameStatus: {gameStatus}");
            Debug.Log($"✅ PlayerId: {playerId}, Nickname: {nickname}, Team: {team}, Countdown: {countdown}초");
            Debug.Log($"✅ Message: {resultMessage}");
            
            // PlayerId 캐싱 (결과 화면에서 사용)
            if (success && !string.IsNullOrEmpty(playerId))
            {
                cachedPlayerId = playerId;
                Debug.Log($"🔑 cachedPlayerId 저장 (nicknameSetResult): {playerId}");
            }
            
            // NickNamePanel에 결과 전달
            if (nickNamePanel != null)
            {
                nickNamePanel.OnNicknameSetResult(success, inLobby, isWaiting, countdown, gameStatus, resultMessage, playerId);
            }
            else
            {
                Debug.LogWarning("⚠️ NickNamePanel이 할당되지 않았습니다!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 닉네임 설정 결과 처리 오류: {e.Message}");
            Debug.LogError($"❌ Stack trace: {e.StackTrace}");
        }
    }
    
    /// <summary>
    /// 입장 가능 알림 처리 (더 이상 사용하지 않음 - 대기방 시스템으로 대체)
    /// </summary>
    private void HandleCanJoinNow(string message)
    {
        try
        {
            Debug.Log($"🚪 입장 가능 알림 처리 (deprecated): {message}");
            Debug.LogWarning("⚠️ canJoinNow 메시지는 더 이상 사용되지 않습니다. 대기방 시스템을 사용하세요.");
            
            // 대기방 시스템에서는 사용하지 않지만, 하위 호환성을 위해 메서드는 유지
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 입장 가능 알림 처리 오류: {e.Message}");
            Debug.LogError($"❌ Stack trace: {e.StackTrace}");
        }
    }
    
    /// <summary>
    /// 닉네임 스텝으로 복귀 처리
    /// </summary>
    private void HandleReturnToNicknameStep(string message)
    {
        try
        {
            Debug.Log($"🔄 닉네임 스텝으로 복귀 처리: {message}");
            
            // 게임 종료 연출 중이면 무시 (5초 딜레이 연출 진행 중)
            if (isShowingGameEndSequence)
            {
                Debug.Log("⏸️ 게임 종료 연출 중이므로 닉네임 스텝 복귀를 나중에 처리합니다.");
                return;
            }
            
            // ResultPanel이 활성화되어 있으면 무시 (사용자가 결과를 보고 있음)
            if (resultPanel != null && resultPanel.gameObject.activeSelf)
            {
                Debug.Log("⏸️ ResultPanel이 활성화되어 있어 닉네임 스텝 복귀를 무시합니다.");
                return;
            }
            
            var messageObj = JObject.Parse(message);
            var data = messageObj["data"];
            
            if (data == null)
            {
                Debug.LogError("❌ returnToNicknameStep data is null");
                return;
            }
            
            string resultMessage = data["message"]?.ToString() ?? "Game finished. Please set your nickname to play again.";
            string previousNickname = data["previousNickname"]?.ToString() ?? "";
            string previousTeam = data["previousTeam"]?.ToString() ?? "";
            
            Debug.Log($"🔄 닉네임 스텝 복귀 - Message: {resultMessage}, PreviousNickname: {previousNickname}, PreviousTeam: {previousTeam}");
            
            // 게임 상태 초기화
            isInGame = false;
            
            // Press 버튼 비활성화
            if (pressButton != null)
            {
                pressButton.interactable = false;
            }
            
            // GamePanel 숨기기 및 대기방 패널 숨기기
            if (gamePanel != null)
            {
                waitingRoomPanel.HideWaitingPanel();
            }
            
            // NickNamePanel에 알림 전달
            if (nickNamePanel != null)
            {
                nickNamePanel.OnReturnToNicknameStep(resultMessage, previousNickname);
            }
            else
            {
                Debug.LogWarning("⚠️ NickNamePanel이 할당되지 않았습니다!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 닉네임 스텝 복귀 처리 오류: {e.Message}");
            Debug.LogError($"❌ Stack trace: {e.StackTrace}");
        }
    }
    
    #endregion
    
    #region Lobby Handlers
    
    /// <summary>
    /// 대기방 타이머 시작 처리
    /// </summary>
    private void HandleLobbyTimerStarted(string message)
    {
        try
        {
            Debug.Log($"🚪 대기방 타이머 시작 처리: {message}");
            
            var messageObj = JObject.Parse(message);
            var data = messageObj["data"];
            int countdown = data?["countdown"]?.Value<int>() ?? 120;
            int lobbyPlayersCount = data?["playerCount"]?.Value<int>() ?? 0;
            
            Debug.Log($"🚪 대기방 타이머 시작 - {countdown}초 후 게임 시작, 대기 인원: {lobbyPlayersCount}명");
            
            // WaitingRoomPanel에 대기방 상태 표시
            if (waitingRoomPanel != null)
            {
                waitingRoomPanel.ShowWaitingPanel(countdown, lobbyPlayersCount);
            }

            if (gamePanel != null)
            {
                Debug.Log("🎮 로비 상태");
                Debug.Log($"✅ 대기방 패널 표시 - 남은 시간: {countdown}초, 대기 인원: {lobbyPlayersCount}명");
            }
            else
            {
                Debug.LogWarning("⚠️ GamePanel이 할당되지 않았습니다!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 대기방 타이머 시작 처리 오류: {e.Message}");
            Debug.LogError($"❌ Stack trace: {e.StackTrace}");
        }
    }
    
    /// <summary>
    /// 대기방 타이머 업데이트 처리
    /// </summary>
    private void HandleLobbyTimerUpdate(string message)
    {
        try
        {
            var messageObj = JObject.Parse(message);
            var data = messageObj["data"];
            int countdown = data?["countdown"]?.Value<int>() ?? 0;
            int lobbyPlayersCount = data?["playerCount"]?.Value<int>() ?? 0;
            
            Debug.Log($"🚪 대기방 타이머 업데이트 - 남은 시간: {countdown}초, 대기 인원: {lobbyPlayersCount}명");
            
            // WaitingRoomPanel에 대기방 타이머 + 인원 업데이트
            if (waitingRoomPanel != null)
            {
                // 패널이 비활성화 상태면 자동 활성화 (lobbyTimerStarted를 놓친 경우 대비)
                if (!waitingRoomPanel.gameObject.activeSelf)
                {
                    Debug.Log("🚪 WaitingRoomPanel이 비활성화 상태 → 자동 활성화 (lobbyTimerStarted 누락 대비)");
                    waitingRoomPanel.ShowWaitingPanel(countdown, lobbyPlayersCount);
                }
                else
                {
                    waitingRoomPanel.UpdateWaitingInfo(countdown, lobbyPlayersCount);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 대기방 타이머 업데이트 처리 오류: {e.Message}");
            Debug.LogError($"❌ Stack trace: {e.StackTrace}");
        }
    }
    
    /// <summary>
    /// 대기방 타이머 취소 처리
    /// </summary>
    private void HandleLobbyTimerCancelled(string message)
    {
        try
        {
            Debug.Log($"🛑 대기방 타이머 취소 처리: {message}");
            
            // GamePanel에 대기방 패널 숨기기
            if (gamePanel != null)
            {
                waitingRoomPanel.HideWaitingPanel();
                Debug.Log("✅ 대기방 패널 숨김 완료");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 대기방 타이머 취소 처리 오류: {e.Message}");
            Debug.LogError($"❌ Stack trace: {e.StackTrace}");
        }
    }
    
    /// <summary>
    /// 게임방으로 이동 처리
    /// </summary>
    private void HandleMovedToGameRoom(string message)
    {
        try
        {
            Debug.Log($"🎮 게임방 이동 처리 (풀영상 시스템): {message}");
            
            var messageObj = JObject.Parse(message);
            var data = messageObj["data"];
            string moveMessage = data?["message"]?.ToString() ?? "Game will start soon!";
            
            Debug.Log($"🎮 게임방 이동 완료: {moveMessage}");
            
            // 🎬 IntroPanel 비활성화 확인 (풀영상 시스템 - 인트로 구간 없음)
            if (introPanel != null && introPanel.activeSelf)
            {
                introPanel.gameObject.SetActive(false);
                Debug.Log("🎬 IntroPanel 비활성화 (풀영상 시스템 - 인트로 구간 삭제)");
            }
            
            // Press 버튼 비활성화 (게임 시작 전까지 비활성화 상태 유지)
            if (pressButton != null)
            {
                pressButton.interactable = false;
                Debug.Log("🔒 Press 버튼 비활성화 (게임방 입장, 게임 시작 대기 중)");
            }
            
            // GamePanel 상태 업데이트 (대기방 -> 게임방)
            if (gamePanel != null)
            {
                // 대기방 패널 숨기기
                waitingRoomPanel.HideWaitingPanel();
                
                // 게임 대기 상태로 전환
                
                // 게임 UI 활성화 (매치방 입장 시)
                gamePanel.ActivateGameUI();
                Debug.Log("✅ 게임방 이동 완료 - 게임 UI 활성화, 게임 시작 대기 중 (풀영상 시스템)");
            }
            
            // 진동 알림 (게임 시작 임박)
            VibratePattern(new int[] { 100, 50, 100 });
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 게임방 이동 처리 오류: {e.Message}");
            Debug.LogError($"❌ Stack trace: {e.StackTrace}");
        }
    }
    
    #endregion
    
    #region Auto Start Timer Handlers
    
    /// <summary>
    /// 자동 시작 타이머 시작 처리
    /// </summary>
    private void HandleAutoStartTimerStarted(string message)
    {
        try
        {
            Debug.Log($"⏰ 자동 시작 타이머 시작 처리: {message}");
            
            var messageObj = JObject.Parse(message);
            var data = messageObj["data"];
            int countdown = data?["countdown"]?.Value<int>() ?? 30;
            int playerCount = data?["playerCount"]?.Value<int>() ?? 0;
            
            Debug.Log($"⏰ 자동 시작 타이머 시작 - {countdown}초 후 게임 시작, 대기 인원: {playerCount}명");
            
            // WaitingRoomPanel 대기 패널 표시
            if (waitingRoomPanel != null)
            {
                waitingRoomPanel.ShowWaitingPanel(countdown, playerCount);
            }
            else
            {
                Debug.LogWarning("⚠️ WaitingRoomPanel이 할당되지 않았습니다!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 자동 시작 타이머 시작 처리 오류: {e.Message}");
            Debug.LogError($"❌ Stack trace: {e.StackTrace}");
        }
    }
    
    /// <summary>
    /// 자동 시작 타이머 업데이트 처리
    /// </summary>
    private void HandleAutoStartTimerUpdate(string message)
    {
        try
        {
            Debug.Log($"⏰ 자동 시작 타이머 업데이트 처리: {message}");
            
            var messageObj = JObject.Parse(message);
            var data = messageObj["data"];
            int countdown = data?["countdown"]?.Value<int>() ?? 0;
            int playerCount = data?["playerCount"]?.Value<int>() ?? 0;
            
            Debug.Log($"⏰ 자동 시작 타이머 업데이트 - 남은 시간: {countdown}초, 대기 인원: {playerCount}명");
            
            // WaitingRoomPanel 대기 텍스트 + 인원 업데이트
            if (waitingRoomPanel != null)
            {
                waitingRoomPanel.UpdateWaitingInfo(countdown, playerCount);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 자동 시작 타이머 업데이트 처리 오류: {e.Message}");
            Debug.LogError($"❌ Stack trace: {e.StackTrace}");
        }
    }
    
    /// <summary>
    /// 자동 시작 타이머 취소 처리
    /// </summary>
    private void HandleAutoStartTimerCancelled(string message)
    {
        try
        {
            Debug.Log($"🛑 자동 시작 타이머 취소 처리: {message}");
            
            // GamePanel의 대기 패널 숨기기
            if (gamePanel != null)
            {
                waitingRoomPanel.HideWaitingPanel();
                Debug.Log("✅ 대기 패널 숨김 완료");
            }
            else
            {
                Debug.LogWarning("⚠️ GamePanel이 할당되지 않았습니다!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 자동 시작 타이머 취소 처리 오류: {e.Message}");
            Debug.LogError($"❌ Stack trace: {e.StackTrace}");
        }
    }
    
    /// <summary>
    /// ResultPanel이 닫힐 때 호출 (사용자가 "Return to Lobby" 버튼을 누름)
    /// </summary>
    public void OnResultPanelClosed()
    {
        Debug.Log("📋 ResultPanel 닫힘 - 게임 상태 초기화");

        // 게임 상태 초기화
        isInGame = false;

        // Press 버튼 비활성화
        if (pressButton != null)
        {
            pressButton.interactable = false;
        }

        // 대기 패널 숨기기
        if (waitingRoomPanel != null)
        {
            waitingRoomPanel.HideWaitingPanel();
        }
    }

    /// <summary>
    /// 서버가 재연결 성공을 확인 — 기존 세션 복원
    /// </summary>
    private void HandleReconnected(string message)
    {
        try
        {
            var obj = JObject.Parse(message);
            var data = obj["data"];
            if (data == null) return;

            string newPlayerId = data["playerId"]?.ToString() ?? cachedPlayerId;
            cachedPlayerId = newPlayerId;
            isAwaitingReconnect = false;

            string location = data["location"]?.ToString() ?? "";
            string nickname = data["nickname"]?.ToString() ?? "";
            int score = data["score"]?.Value<int>() ?? 0;

            Debug.Log($"✅ 재연결 성공 — location={location}, nickname={nickname}, score={score}");

            if (loadingPanel != null) loadingPanel.SetActive(false);

            // Unity 인스턴스가 네트워크 단절 동안 살아있었으므로 대부분의 UI 상태는 유지됨.
            // 최소 동기화: playerId와 방 상태를 서버 기준으로 재요청.
            if (gamePanel != null && !string.IsNullOrEmpty(newPlayerId))
            {
                gamePanel.SetCurrentPlayerId(newPlayerId);
            }

            // 서버에 현재 상태 스냅샷 요청 (점수/남은시간 등 갱신)
            RequestRoomInfo();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ HandleReconnected 오류: {e.Message}");
            isAwaitingReconnect = false;
        }
    }

    /// <summary>
    /// 서버가 재연결 거부 (grace 만료 또는 알 수 없는 playerId)
    /// </summary>
    private void HandleReconnectFailed(string message)
    {
        try
        {
            var obj = JObject.Parse(message);
            string reason = obj["data"]?["reason"]?.ToString() ?? "unknown";
            Debug.LogWarning($"⚠️ 재연결 실패 — 이유: {reason}. 신규 세션으로 진입합니다.");

            cachedPlayerId = "";
            isAwaitingReconnect = false;
            if (gamePanel != null) gamePanel.SetCurrentPlayerId("");

            ShowNickNamePanelFallback();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ HandleReconnectFailed 오류: {e.Message}");
        }
    }

    /// <summary>
    /// 같은 playerId로 다른 탭/기기에서 접속 — 이 세션은 종료
    /// </summary>
    private void HandleDuplicateConnection(string message)
    {
        Debug.LogWarning("⚠️ 다른 세션이 열렸습니다. 이 연결은 종료됩니다.");
        cachedPlayerId = "";
        isAwaitingReconnect = false;
        if (gamePanel != null) gamePanel.SetCurrentPlayerId("");

        // 자동 재연결 중단
        if (reconnectCoroutine != null)
        {
            StopCoroutine(reconnectCoroutine);
            reconnectCoroutine = null;
        }
        isReconnecting = false;

        try
        {
            if (webSocket != null) webSocket.Close(WebSocketCloseCode.Normal, "Duplicate session");
        }
        catch { }
    }

    #endregion
}