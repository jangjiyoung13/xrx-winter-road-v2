using UnityEngine;
using WebSocketSharp;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Video;

public class ProgramDesktop : MonoBehaviour
{
    [Header("서버 설정")]
    [SerializeField] private string serverUrl = "ws://59.16.72.37:3000";
    [SerializeField] private string roomId = "main_room";
    
    [Header("연결 상태")]
    private WebSocket ws;
    private bool isConnected = false;
    
    // WebSocket 재연결 설정
    private bool isReconnecting = false;
    private float reconnectDelay = 1f;
    private const float MAX_RECONNECT_DELAY = 30f;
    private const float INITIAL_RECONNECT_DELAY = 1f;
    private Coroutine reconnectCoroutine = null;

    // 세션 복구 (서버 grace period)
    private string cachedPlayerId = "";
    private bool isAwaitingReconnect = false;
    private const float RECONNECT_RESPONSE_TIMEOUT = 2f;
    
    // 메인 스레드 처리를 위한 메시지 큐
    private Queue<string> pendingMessages = new Queue<string>();
    private readonly object messageLock = new object();
    
    [Header("게임 관리")]
    [SerializeField] private bool isAdminMode = true;
    private string gameStatus = "waiting";
    private bool canStartGame = false;
    
    [SerializeField] private Button _startButton;


    [Header("Idle 비디오 (항상 루프 재생)")]
    [SerializeField] private VideoPlayer idleVideoPlayer;
    [SerializeField] private RawImage idleImage;

    [Header("종료 비디오 (우승자 element별 재생)")]
    [SerializeField] private VideoPlayer endVideoPlayer;
    [SerializeField] private RawImage endImage;

    [Header("Element별 종료 VideoClip (6종: Joy, Sadness, Courage, Love, Hope, Friendship 순서)")]
    [SerializeField] private VideoClip[] elementEndClips = new VideoClip[6];
    
    [Header("게임 중 비디오 (듀얼 플레이어 크로스 디졸브)")]
    [SerializeField] private VideoPlayer gameVideoPlayerA;
    [SerializeField] private RawImage gameImageA;
    [SerializeField] private VideoPlayer gameVideoPlayerB;
    [SerializeField] private RawImage gameImageB;

    [Header("게임 중 VideoClip (단일 통합 영상, loop 재생)")]
    [SerializeField] private VideoClip gameVideoClip;

    // === Element 비디오 순환 재생 파라미터 ===
    [Header("크로스 디졸브")]
    [Tooltip("Element 영상 간 cross-fade 지속 시간(초). 현재 클립 종료 전 이 시간만큼 다음 클립과 동시 재생되며 alpha lerp로 전환.")]
    [Range(0.1f, 5.0f)]
    [SerializeField] private float crossfadeDuration = 2.0f;

    // 다음 클립 Prepare 최대 대기 시간(초). 타임아웃 시에도 전환을 강행.
    private const float PREPARE_TIMEOUT = 3.0f;
    // Play() 후 첫 프레임이 실제 렌더링될 때까지 대기 최대 프레임 수 (검은 프레임이 페이드인되는 것을 방지).
    private const int FIRST_FRAME_WAIT_MAX_FRAMES = 10;
    // 무효 클립 skip 시 대기(초, 무한루프 방지).
    private const float INVALID_CLIP_SKIP_DELAY = 0.5f;
    // Fade 시작 전 next를 미리 Play해서 첫 프레임 렌더 대기를 소화하는 버퍼(초). 이 시간만큼 next가 frame 0보다 앞에서 시작됨.
    private const float PREPLAY_BUFFER_SECONDS = 0.2f;

    [Header("비디오 전환 하드컷")]
    [Tooltip("current 클립 종료 N 프레임 전에 next로 스왑. 0 = 끝까지 재생 후 스왑, 1~2 = 프레임 지연 보정으로 자연스러움. 소수 가능.")]
    [Range(0f, 10f)]
    [SerializeField] private float transitionPrerollFrames = 1f;

    [Header("비디오 전환 FX 그룹")]
    [SerializeField] private GameObject fxGroupStart;        // 게임 시작 시 트랜지션 FX (씬에 비활성 상태로 배치)
    [SerializeField] private GameObject fxGroupFinish;       // 게임 종료 시 트랜지션 FX (씬에 비활성 상태로 배치)
    [SerializeField] private float fxAutoHideDuration = 3f;  // FX 자동 비활성화까지 걸리는 시간

    private Coroutine fxStartHideCoroutine = null;
    private Coroutine fxFinishHideCoroutine = null;

    [Header("Result → Idle 전환 페이드")]
    [SerializeField] private float resetFadeDuration = 1f;   // gameReset 시 End→Idle 크로스페이드 시간(초)
    [SerializeField] private float endFadeDuration = 1f;     // gameEnd 시 Idle→End 크로스페이드 시간(초)
    [SerializeField] private float endPrepareTimeout = 3f;   // End 비디오 Prepare 타임아웃(초)
    private Coroutine resetFadeCoroutine = null;
    private Coroutine endFadeCoroutine = null;

    // 게임 중 영상 재생 상태
    private Coroutine gameVideoCoroutine = null;
    private bool isGameVideoPlaying = false;

    // VideoPlayer / Idle CanvasGroup (idle → 게임 영상 크로스 디졸브용)
    private CanvasGroup gameCanvasGroupA;
    private CanvasGroup gameCanvasGroupB;
    private CanvasGroup idleCanvasGroup;
    
    // Element 이름 → 인덱스 매핑
    private readonly string[] elementNames = { "Joy", "Sadness", "Courage", "Love", "Hope", "Friendship" };



    [Header("Element별 능력 파티클 (풀링 시스템 - 6종 × 3변형)")]
    [SerializeField] private ParticleSystem[] elementParticlePrefabsA = new ParticleSystem[6]; // 변형 A (Joy, Sadness, Courage, Love, Hope, Friendship)
    [SerializeField] private ParticleSystem[] elementParticlePrefabsB = new ParticleSystem[6]; // 변형 B
    [SerializeField] private ParticleSystem[] elementParticlePrefabsC = new ParticleSystem[6]; // 변형 C
    [SerializeField] private Transform particlePoolContainer;
    [SerializeField] private int poolSize = 21; // Element당 풀 크기 (3변형 × 7개씩)
    
    // Element별 파티클 풀
    private Dictionary<string, Queue<ParticleSystem>> elementParticlePools = new Dictionary<string, Queue<ParticleSystem>>();
    private Dictionary<string, List<ParticleSystem>> activeElementParticles = new Dictionary<string, List<ParticleSystem>>();
    

    /* === ResultPanel 주석 처리 (개인전 전환으로 불필요 - 우승자 비디오만 재생) ===
    [Header("결과창 오브젝트")]
    [SerializeField] private ResultPanel[] resultGroupObjects;
    
    // 결과창 CanvasGroup 캐싱
    private CanvasGroup[] resultPanelCanvasGroups;
    === ResultPanel 주석 처리 끝 === */
    
    // 기존 파티클 시스템 변수 (주석처리 - 사용 안 함)
    // private int redTeamHitCount = 0;
    // private int blueTeamHitCount = 0;
    // private Coroutine particleBurstCoroutine = null;
    // private int lastRedTeamScore = 0;
    // private int lastBlueTeamScore = 0;
    
    // 한 프레임에 처리할 최대 메시지 수.
    // ※ 롤백 방법: 이 값을 1로 바꾸면 변경 전 동작(한 프레임 1개 처리)과 완전히 동일.
    // 8: press burst 시 메시지 backlog 누적으로 인한 frame drop 지속 방지. 1단계(fast path)와 시너지.
    private const int MAX_MESSAGES_PER_FRAME = 8;

    void Update()
    {
        // 메인 스레드에서 펜딩 메시지 처리 (한 프레임 최대 MAX_MESSAGES_PER_FRAME개)
        for (int i = 0; i < MAX_MESSAGES_PER_FRAME; i++)
        {
            string messageToProcess = null;
            lock (messageLock)
            {
                if (pendingMessages.Count == 0) break;
                messageToProcess = pendingMessages.Dequeue();
            }

            if (string.IsNullOrEmpty(messageToProcess)) continue;

            try
            {
                ProcessObserverMessage(messageToProcess);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ 메인 스레드에서 메시지 처리 오류: {e.Message}");
                Debug.LogError($"❌ 메시지: {messageToProcess}");
            }
        }
    }


void Start()
    {
        // 프레임레이트 60fps 고정 (LED 출력 동기화 + 비디오/파티클 부하 안정화)
        // ※ vSyncCount=0이어야 targetFrameRate가 적용됨. vSync가 켜져 있으면 모니터 주사율을 따름.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        // 커맨드라인에서 서버 URL 읽기 (런처에서 --server-url 인자로 전달)
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--server-url" && i + 1 < args.Length)
            {
                serverUrl = args[i + 1];
                // Debug.Log($"🔗 커맨드라인에서 서버 URL 설정: {serverUrl}");
                break;
            }
        }

        // Debug.Log($"🎮 Winter Road Desktop - WebSocket 연결 테스트 시작");
        // Debug.Log($"🔗 대상 서버: {serverUrl}");

        // 2초 후 자동 연결 시도
        Invoke("StartConnectionTest", 2f);

        // 결과창 CanvasGroup 캐싱 (개인전 전환으로 주석 처리)
        // CacheResultPanelCanvasGroups();
        
        // Element별 파티클 풀 초기화
        InitializeElementParticlePool();
        
        // 듀얼 VideoPlayer CanvasGroup 초기화
        InitializeGameVideoCanvasGroups();
        
        // 프로그램 시작 즉시 idle 루프 재생
        PlayIdleVideo();

        // 커맨드라인에서 모니터 인덱스 읽기 (런처에서 --monitor 인자로 전달)
        int displayIndex = 0;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--monitor" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int monitorIdx))
                {
                    displayIndex = monitorIdx;
                    // Debug.Log($"🖥 커맨드라인에서 모니터 인덱스 설정: {displayIndex}");
                }
                break;
            }
        }

        // 사용 가능한 디스플레이 목록 가져오기
        var displayList = Screen.mainWindowDisplayInfo;
        var allDisplays = new List<DisplayInfo>();
        Screen.GetDisplayLayout(allDisplays);

        // Debug.Log($"🖥 감지된 모니터 수: {allDisplays.Count}");
        for (int i = 0; i < allDisplays.Count; i++)
        {
            // Debug.Log($"  모니터 {i}: {allDisplays[i].width}x{allDisplays[i].height} name={allDisplays[i].name}");
        }

        // 유효 범위 확인
        if (displayIndex < 0 || displayIndex >= allDisplays.Count)
        {
            Debug.LogWarning($"⚠️ 모니터 인덱스 {displayIndex}이 범위를 벗어남 (총 {allDisplays.Count}개). 0번 모니터 사용.");
            displayIndex = 0;
        }

        // 대상 모니터로 창 이동 후 전체화면
        var targetDisplay = allDisplays[displayIndex];
        // Debug.Log($"🖥 대상 모니터: {displayIndex} ({targetDisplay.width}x{targetDisplay.height})");

        // 먼저 창을 대상 모니터로 이동
        var moveResult = Screen.MoveMainWindowTo(targetDisplay, new Vector2Int(0, 0));
        // Debug.Log($"🖥 창 이동 결과: {moveResult}");

        // 해당 모니터에서 전체화면으로 전환
        Screen.SetResolution((int)targetDisplay.width, (int)targetDisplay.height, FullScreenMode.FullScreenWindow);
        //_startButton.onClick.AddListener(StartGame);
    }
    
    /* === CacheResultPanelCanvasGroups 주석 처리 (개인전 전환으로 불필요) ===
    /// <summary>
    /// 결과창의 CanvasGroup을 미리 캐싱
    /// </summary>
    void CacheResultPanelCanvasGroups()
    {
        if (resultGroupObjects == null || resultGroupObjects.Length == 0)
        {
            Debug.LogWarning("⚠️ resultGroupObjects가 비어있습니다!");
            return;
        }
        
        resultPanelCanvasGroups = new CanvasGroup[resultGroupObjects.Length];
        
        for (int i = 0; i < resultGroupObjects.Length; i++)
        {
            if (resultGroupObjects[i] != null)
            {
                CanvasGroup canvasGroup = resultGroupObjects[i].GetComponent<CanvasGroup>();
                
                if (canvasGroup == null)
                {
                    Debug.LogWarning($"⚠️ [{resultGroupObjects[i].gameObject.name}] CanvasGroup이 없습니다! 자동 추가합니다.");
                    canvasGroup = resultGroupObjects[i].gameObject.AddComponent<CanvasGroup>();
                }
                
                resultPanelCanvasGroups[i] = canvasGroup;
                // Debug.Log($"✅ CanvasGroup 캐싱: {resultGroupObjects[i].gameObject.name}");
            }
        }
        
        // Debug.Log($"✅ 총 {resultPanelCanvasGroups.Length}개의 CanvasGroup 캐싱 완료");
    }
    === CacheResultPanelCanvasGroups 주석 처리 끝 === */

    void StartConnectionTest()
    {
        // Debug.Log("🔄 외부 서버 연결 테스트 시작...");
        ConnectToServer();
    }
    
    public void ConnectToServer()
    {
        if (isConnected)
        {
            // Debug.Log("⚠️ 이미 서버에 연결되어 있습니다.");
            return;
        }
        
        try
        {
            // Debug.Log($"🔄 서버 연결 시도: {serverUrl}");
            
            ws = new WebSocket(serverUrl);
            
            ws.OnOpen += (sender, e) => {
                isConnected = true;
                reconnectDelay = INITIAL_RECONNECT_DELAY;
                isReconnecting = false;
                // Debug.Log("✅ WebSocket 연결 성공!");
                // Debug.Log("🎯 외부 서버와의 통신이 정상적으로 작동합니다.");
                
                // 간단한 테스트 메시지 전송
                TestBasicConnection();
            };
            
            ws.OnMessage += (sender, e) => {
                // Debug.Log($"📨 서버 응답 수신: {e.Data}");
                // Debug.Log($"📊 메시지 길이: {e.Data.Length}자");
                
                // 메시지를 큐에 추가하여 메인 스레드에서 처리
                lock (messageLock)
                {
                    pendingMessages.Enqueue(e.Data);
                    // Debug.Log($"📨 메시지 큐에 추가됨. 큐 크기: {pendingMessages.Count}");
                }
            };
            
            ws.OnClose += (sender, e) => {
                isConnected = false;
                // Debug.Log($"🔌 연결 종료됨. 코드: {e.Code}, 이유: {e.Reason}");
                // 정상 종료(1000)가 아닌 경우 재연결 시도
                if (e.Code != (ushort)CloseStatusCode.Normal)
                {
                    TriggerReconnect();
                }
            };
            
            ws.OnError += (sender, e) => {
                Debug.LogError($"❌ WebSocket 연결 오류: {e.Message}");
                if (!isConnected)
                {
                    TriggerReconnect();
                }
            };
            
            ws.ConnectAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ 연결 실패: {ex.Message}");
            Debug.LogError("❌ 외부 서버에 접근할 수 없습니다.");
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
            // Debug.Log("🔄 WebSocket 재연결 시작...");
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
            // Debug.Log($"🔄 [{attempt}회] {reconnectDelay:F1}초 후 재연결 시도...");
            
            yield return new WaitForSeconds(reconnectDelay);
            
            if (isConnected)
            {
                // Debug.Log("✅ 대기 중 연결 복구됨 - 재연결 중단");
                break;
            }
            
            // 이전 WebSocket 정리
            if (ws != null)
            {
                try { ws.Close(); } catch { }
                ws = null;
            }
            
            // Debug.Log($"🔄 [{attempt}회] 재연결 시도 중... ({serverUrl})");
            
            // ConnectToServer 내부에서 isConnected 체크를 하므로 직접 연결
            try
            {
                ws = new WebSocket(serverUrl);
                
                ws.OnOpen += (sender, e) => {
                    isConnected = true;
                    reconnectDelay = INITIAL_RECONNECT_DELAY;
                    isReconnecting = false;
                    // Debug.Log($"✅ [{attempt}회] 재연결 성공!");
                    
                    // 재연결 후 서버에 다시 등록
                    TestBasicConnection();
                };
                
                ws.OnMessage += (sender, e) => {
                    lock (messageLock)
                    {
                        pendingMessages.Enqueue(e.Data);
                    }
                };
                
                ws.OnClose += (sender, e) => {
                    isConnected = false;
                    // Debug.Log($"🔌 재연결 후 종료됨. 코드: {e.Code}, 이유: {e.Reason}");
                    if (e.Code != (ushort)CloseStatusCode.Normal)
                    {
                        TriggerReconnect();
                    }
                };
                
                ws.OnError += (sender, e) => {
                    Debug.LogError($"❌ 재연결 오류: {e.Message}");
                };
                
                ws.ConnectAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [{attempt}회] 재연결 실패: {ex.Message}");
            }
            
            // 연결 대기 (최대 5초)
            float waitTime = 0f;
            while (!isConnected && waitTime < 5f)
            {
                yield return new WaitForSeconds(0.5f);
                waitTime += 0.5f;
            }
            
            if (isConnected)
            {
                // Debug.Log($"✅ [{attempt}회] 재연결 완료! (백오프 리셋)");
                reconnectDelay = INITIAL_RECONNECT_DELAY;
                isReconnecting = false;
                reconnectCoroutine = null;
                yield break;
            }
            
            // 백오프 증가
            reconnectDelay = Mathf.Min(reconnectDelay * 2f, MAX_RECONNECT_DELAY);
            // Debug.Log($"⏳ 다음 재연결 대기: {reconnectDelay:F1}초");
        }
        
        isReconnecting = false;
        reconnectCoroutine = null;
    }
    
    void TestBasicConnection()
    {
        // Debug.Log($"🔍 연결 상태 확인 - ws: {ws != null}, isConnected: {isConnected}");

        try
        {
            if (ws != null && isConnected)
            {
                // Debug.Log($"📤 기본 연결 테스트 시작... (연결 상태: {ws.ReadyState})");

                // 간단한 핑 테스트 (JSON 형식으로)
                var pingMessage = new {
                    type = "ping",
                    timestamp = System.DateTime.Now.Ticks
                };
                string pingJson = JsonConvert.SerializeObject(pingMessage);
                ws.Send(pingJson);
                // Debug.Log("📤 핑 메시지 전송 완료!");

                // 이전 세션이 있으면 reconnect 먼저 시도, 실패 시 joinRoom fallback
                if (!string.IsNullOrEmpty(cachedPlayerId))
                {
                    var reconnectMsg = new {
                        type = "reconnect",
                        playerId = cachedPlayerId
                    };
                    string reconnectJson = JsonConvert.SerializeObject(reconnectMsg);
                    ws.Send(reconnectJson);
                    isAwaitingReconnect = true;
                    // Debug.Log($"📤 reconnect 요청 전송: {cachedPlayerId}");
                    StartCoroutine(ReconnectTimeoutFallback());
                }
                else
                {
                    SendJoinRoom();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ 기본 연결 테스트 오류: {ex.Message}");
        }
    }

    private void SendJoinRoom()
    {
        if (ws == null || !isConnected) return;
        var joinMessage = new {
            type = "joinRoom",
            roomId = roomId,
            playerName = "DesktopAdmin"
        };
        string joinJson = JsonConvert.SerializeObject(joinMessage);
        // Debug.Log($"📤 방 참가 요청 전송: {joinJson}");
        ws.Send(joinJson);
    }

    private IEnumerator ReconnectTimeoutFallback()
    {
        yield return new WaitForSeconds(RECONNECT_RESPONSE_TIMEOUT);
        if (isAwaitingReconnect)
        {
            Debug.LogWarning("⌛ reconnect 응답 없음(타임아웃) → joinRoom fallback");
            isAwaitingReconnect = false;
            cachedPlayerId = "";
            SendJoinRoom();
        }
    }
    
    void ProcessObserverMessage(string messageJson)
    {
        try
        {
            // === Fast path: 가장 빈번한 playerPressImmediate 메시지는 JObject 파싱을 우회.
            // 핫 경로의 GC 압박이 누적되면 frame hitch가 길어지는 것을 방지.
            // 메시지에 "type":"playerPressImmediate"가 포함되면 element 필드만 빠르게 추출.
            // 형식이 다르면 일반 경로(JObject.Parse)로 자연스럽게 폴백. ===
            if (messageJson.IndexOf("\"type\":\"playerPressImmediate\"", StringComparison.Ordinal) >= 0)
            {
                if (TryFastParsePressElement(messageJson, out string fastElement))
                {
                    PlayElementParticle(fastElement ?? "None");
                    return;
                }
                // 추출 실패 시 아래 일반 경로로 폴백
            }

            var message = JObject.Parse(messageJson);
            string messageType = message["type"]?.ToString() ?? "";
            
            switch (messageType)
            {
                case "joinedRoom":
                    // Debug.Log($"✅ {(isAdminMode ? "관리자" : "Observer")}로 방 입장 성공!");
                    
                    var data = message["data"];
                    var playerInfo = data?["player"];
                    var roomInfo = data?["room"];
                    string playerId = data?["playerId"]?.ToString() ?? playerInfo?["id"]?.ToString() ?? "";

                    // 다음 재연결 시 세션 복구에 사용
                    if (!string.IsNullOrEmpty(playerId))
                    {
                        cachedPlayerId = playerId;
                        // Debug.Log($"🔑 cachedPlayerId 저장: {cachedPlayerId}");
                    }

                    if (playerInfo != null)
                    {
                        string playerName = playerInfo["name"]?.ToString() ?? (isAdminMode ? "AdminDesktop" : "Observer");
                        bool hasAdminPermission = playerInfo["isAdmin"]?.Value<bool>() ?? false;
                        // Debug.Log($"👤 자신의 플레이어 정보 - ID: {playerId}, Name: {playerName}, Admin: {hasAdminPermission}");
                    }
                    
                    // 관리자 권한 확인 및 게임 상태 업데이트
                    if (isAdminMode && playerInfo != null)
                    {
                        bool hasAdminPermission = playerInfo["isAdmin"]?.Value<bool>() ?? false;
                        
                        if (hasAdminPermission)
                        {
                            // Debug.Log("👑 관리자 권한 확인됨 - 게임 제어 가능");
                            canStartGame = true;
                        }
                        
                        gameStatus = roomInfo?["status"]?.ToString() ?? "waiting";
                        // Debug.Log($"🎮 현재 게임 상태: {gameStatus}");
                    }
                    break;
                case "gameStateUpdate":
                    // Debug.Log("📊 상세 게임 상태 업데이트 수신");
                    ParseGameStateUpdate(message);
                    break;
                case "playerPressImmediate":
                    // 🎆 실시간 Press 알림 수신 (ShockWave 파티클용)
                    // Debug.Log("🎆 [IMMEDIATE PRESS] 실시간 Press 알림 수신!");
                    HandleImmediatePress(message);
                    break;
                case "scoreUpdate":
                    // Debug.Log("🏆 점수 업데이트 수신");
                    // Desktop(LED)에서는 점수 UI 불필요 - 로그만 출력
                    break;
                case "timeUpdate":
                    ParseTimeUpdate(message);
                    break;
                case "gameStart":
                    // Debug.Log("🚀 게임 시작!");
                    PlayFxStartTransition();
                    gameStatus = "playing";
                    canStartGame = false;
                    // 통합 게임 영상 재생 시작 (단일 클립 loop)
                    StartGameVideo();
                    break;
                case "gameEnd":
                    // Debug.Log("🏁🏁🏁 [gameEnd] 게임 종료!");
                    // PlayFxFinishTransition();
                    gameStatus = "finished";
                    canStartGame = false;
                    
                    // 게임 중 순환 영상 / idle 영상은 크로스페이드 완료 후 정지
                    // (PlayElementEndVideo 내부에서 Prepare 완료 후 페이드 → 페이드 끝난 뒤 정지)

                    // 파티클 풀 초기화
                    // Debug.Log("🎥 [gameEnd] 파티클 풀 초기화...");
                    ResetElementParticlePool();
                    
                    // 우승자 element에 맞는 종료 영상 재생
                    string winnerElement = GetWinnerElement(message);
                    if (!string.IsNullOrEmpty(winnerElement) && winnerElement != "None")
                    {
                        // Debug.Log($"🎥 [gameEnd] 우승자 element: {winnerElement} → 종료 영상 재생!");
                        PlayElementEndVideo(winnerElement);
                    }
                    else
                    {
                        // 우승자가 없는 경우 첫 번째 종료 비디오를 기본 재생
                        // Debug.Log("🎥 [gameEnd] 우승자 element 없음 - 첫 번째 종료 비디오 기본 재생");
                        PlayElementEndVideo(elementNames[0]);
                    }
                    // Debug.Log("🏁🏁🏁 [gameEnd] 게임 종료 처리 완료");
                    break;
                case "gameReset":
                    // Debug.Log("🔄 게임 리셋됨");
                    StopAllFxTransition();
                    gameStatus = "waiting";
                    canStartGame = isAdminMode;

                    // 파티클 풀 초기화
                    ResetElementParticlePool();
                    
                    // 결과창 닫기 (개인전 전환으로 주석 처리)
                    // HideResultPanels();
                    
                    // 게임 중 순환 영상 정지
                    StopGameVideo();
                    
                    // 종료 영상 → idle 크로스페이드 전환
                    StartResetFadeTransition();
                    // Debug.Log("🎥 [gameReset] End → Idle 크로스페이드 시작");

                    break;
                case "playerJoined":
                    // Debug.Log("👤 새 플레이어 입장");
                    HandlePlayerJoined(message);
                    break;
                case "playerLeft":
                    // Debug.Log("👋 플레이어 퇴장");
                    HandlePlayerLeft(message);
                    break;
                case "switchToGameVideo":
                    // Debug.Log("🎬 [switchToGameVideo] 메시지 수신! (풀영상 시스템 - 비디오 전환 없음)");
                    // 풀영상이 계속 재생 중이므로 별도 처리 불필요
                    break;
                case "countdownStart":
                    // Debug.Log("⏰ 게임 시작 카운트다운 시작! (풀영상 시스템 - 카운트다운 없음)");
                    // 카운트다운 없이 바로 게임 시작
                    break;
                case "countdownUpdate":
                    // 카운트다운이 없으므로 무시
                    // Debug.Log("⏰ 카운트다운 업데이트 (풀영상 시스템 - 무시)");
                    break;
                case "lobbyTimerUpdate":
                    // 로비 카운트다운 업데이트 (매초 수신)
                    var lobbyCountdown = message["data"]?["countdown"]?.Value<int>() ?? 0;
                    var lobbyPlayerCount = message["data"]?["playerCount"]?.Value<int>() ?? 0;
                    // Debug.Log($"⏰ 로비 타이머: {lobbyCountdown}초 남음 (플레이어: {lobbyPlayerCount}명)");
                    break;
                case "autoReset":
                    // Debug.Log("🔄 게임이 자동으로 대기 상태로 리셋되었습니다!");
                    HandleAutoReset(message);
                    
                    // 결과창 닫기 (개인전 전환으로 주석 처리)
                    // HideResultPanels();
                    
                    // 게임 중 순환 영상 정지
                    StopGameVideo();
                    
                    // 종료 영상 정지 → idle 복귀
                    StopEndVideo();
                    PlayIdleVideo();
                    // Debug.Log("🎥 [autoReset] 게임/종료 영상 정지, idle 재생 복귀");
                    break;
                case "lobbyTimerStarted":
                    // Debug.Log("🏠 대기방 타이머 시작! 첫 유저 입장");
                    // idle 비디오는 이미 재생 중
                    // Debug.Log("🎥 [lobbyTimerStarted] idle 비디오 계속 재생 중");
                    break;
                case "movedToGameRoom":
                    // Debug.Log("🎬 [movedToGameRoom] 게임방 이동! (풀영상 시스템 - 비디오 전환 없음)");
                    // 결과창 닫기 (개인전 전환으로 주석 처리)
                    // HideResultPanels();
                    // 풀영상이 계속 재생 중이므로 별도 처리 불필요
                    break;
                case "pauseIntroVideo":
                    // Debug.Log("⏸️ 인트로 비디오 일시정지 명령 수신 (풀영상 시스템 - 무시)");
                    // 풀영상 시스템에서는 일시정지 없음
                    break;
                case "videoReset":
                    // Debug.Log("🔄🎬 [videoReset] 종료 영상 정지 명령 수신! (게임 종료 후 10초)");
                    // 게임 중 순환 영상 정지
                    StopGameVideo();
                    // 종료 영상 정지
                    StopEndVideo();
                    // 결과창 닫기 (개인전 전환으로 주석 처리)
                    // HideResultPanels();
                    // 파티클 풀 초기화
                    ResetElementParticlePool();
                    // 다음 게임을 위해 대기 상태로 전환
                    gameStatus = "waiting";
                    canStartGame = isAdminMode;
                    // idle 복귀
                    PlayIdleVideo();
                    // Debug.Log("🏠 [videoReset] 대기 상태로 전환 완료 - idle 재생 복귀");
                    break;
                case "pong":
                    // Debug.Log("🏓 Pong 응답 수신 - 연결 정상!");
                    var pongTime = message["timestamp"]?.ToString();
                    // Debug.Log($"🏓 Pong 타임스탬프: {pongTime}");
                    break;
                case "reconnected":
                {
                    isAwaitingReconnect = false;
                    var rcData = message["data"];
                    string pid = rcData?["playerId"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(pid)) cachedPlayerId = pid;

                    bool isAdmin = rcData?["isAdmin"]?.Value<bool>() ?? false;
                    canStartGame = isAdminMode && isAdmin;

                    // Debug.Log($"✅ 재연결 성공 — playerId={cachedPlayerId}, isAdmin={isAdmin}");
                    break;
                }
                case "reconnectFailed":
                {
                    string reason = message["data"]?["reason"]?.ToString() ?? "unknown";
                    Debug.LogWarning($"⚠️ 재연결 실패({reason}) → joinRoom으로 fallback");
                    isAwaitingReconnect = false;
                    cachedPlayerId = "";
                    SendJoinRoom();
                    break;
                }
                case "duplicateConnection":
                    Debug.LogWarning("⚠️ 다른 세션이 접속됨 — 이 Desktop 연결 종료");
                    cachedPlayerId = "";
                    isAwaitingReconnect = false;
                    try { if (ws != null) ws.Close(); } catch { }
                    break;
                case "playerDisconnected":
                case "playerReconnected":
                    // Debug.Log($"ℹ️ {messageType}: {message["data"]?["playerName"]}");
                    break;
                default:
                    // Debug.Log($"❓ 알 수 없는 메시지 타입: {messageType}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Observer 메시지 처리 오류: {ex.Message}");
            Debug.LogError($"❌ 원본 JSON: {messageJson}");
        }
    }
    
    void ParseGameStateUpdate(JObject message)
    {
        try
        {
            // 방 상태 파싱 (서버 구조: data.room.status)
            var gameData = message["data"];
            var roomInfo = gameData?["room"];
            var status = roomInfo?["status"]?.ToString();
            
            switch (status)
            {
                case "waiting":
                    // Debug.Log("🏠 방 상태: 대기 중");
                    break;
                case "lobby":
                    // Debug.Log("🏠 방 상태: 로비");
                    break;
                case "countdown":
                    // Debug.Log("⏰ 방 상태: 카운트다운");
                    break;
                case "playing":
                    // Debug.Log("🎮 방 상태: 게임 진행 중");
                    break;
                case "finished":
                    // Debug.Log("🏆 방 상태: 게임 종료");
                    break;
                default:
                    // Debug.Log($"❓ 알 수 없는 방 상태: {status}");
                    break;
            }
            
            
            // 플레이어 수 파싱
            ParsePlayerCounts(gameData);
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ 게임 상태 파싱 오류: {ex.Message}");
        }
    }

    
    void ParsePlayerCounts(JToken gameData)
    {
        try
        {
            // 서버 구조: data.playerCount는 정수값
            var playerCount = gameData?["playerCount"];
            if (playerCount != null)
            {
                int count = playerCount.Value<int>();
                // Debug.Log($"👥 플레이어 현황 - 활성: {count}명");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ 플레이어 수 파싱 오류: {ex.Message}");
        }
    }
    
    void ParseTimeUpdate(JObject message)
    {
        try
        {
            var remainingTime = message["data"]?["remainingTime"]?.Value<int>() ?? 0;
            
            int minutes = remainingTime / 60;
            int seconds = remainingTime % 60;
            // Debug.Log($"⏰ 남은 시간: {minutes:00}:{seconds:00}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ 시간 파싱 오류: {ex.Message}");
        }
    }
    
    void HandlePlayerJoined(JObject message)
    {
        try
        {
            var data = message["data"];
            if (data != null)
            {
                // 플레이어 정보 파싱
                string playerId = data["playerId"]?.ToString() ?? "";
                string playerName = data["playerName"]?.ToString() ?? "Unknown";
                
                // Debug.Log($"👤 플레이어 입장 상세 - ID: {playerId}, 이름: {playerName} (개인전)");;
                
                // 플레이어 UI 생성
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ 플레이어 입장 처리 오류: {ex.Message}");
        }
    }
    
    void HandlePlayerLeft(JObject message)
    {
        try
        {
            var data = message["data"];
            if (data != null)
            {
                // 플레이어 정보 파싱
                string playerId = data["playerId"]?.ToString() ?? "";
                string playerName = data["playerName"]?.ToString() ?? "Unknown";
                
                // Debug.Log($"👋 플레이어 퇴장 상세 - ID: {playerId}, 이름: {playerName} (개인전)");
                
                // 플레이어 UI 제거
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ 플레이어 퇴장 처리 오류: {ex.Message}");
        }
    }
    
    // 게임 제어 메서드들
    public void StartGame()
    {
        if (!isAdminMode || !canStartGame)
        {
            Debug.LogWarning("⚠️ 게임 시작 권한이 없거나 시작할 수 없는 상태입니다.");
            return;
        }
        
        if (ws != null && isConnected && (gameStatus == "waiting" || gameStatus == "lobby"))
        {
            // Debug.Log($"🚀 서버로 게임 시작 명령 전송! (현재 상태: {gameStatus})");
            
            // 바로 서버에 게임 시작 메시지 전송 (클라이언트 카운트다운 제거)
            var startMessage = new {
                type = "startGame"
            };
            string json = JsonConvert.SerializeObject(startMessage);
            ws.Send(json);
            // Debug.Log("🚀 게임 시작 명령 전송 완료!");
            
            // 게임 상태를 대기중으로 설정 (서버에서 카운트다운 및 시작 처리)
            canStartGame = false;
        }
        else
        {
            Debug.LogWarning("⚠️ 연결되지 않았거나 게임이 이미 시작되었습니다.");
        }
    }
    
    public void ResetGame()
    {
        if (!isAdminMode || !canStartGame)
        {
            Debug.LogWarning("⚠️ 게임 리셋 권한이 없습니다.");
            return;
        }
        
        if (ws != null && isConnected)
        {
            var resetMessage = new {
                type = "resetGame"
            };
            
            string json = JsonConvert.SerializeObject(resetMessage);
            ws.Send(json);
            // Debug.Log("🔄 게임 리셋 명령 전송!");
        }
        else
        {
            Debug.LogWarning("⚠️ 서버에 연결되지 않았습니다.");
        }
    }
    
    void AutoDisconnect()
    {
        if (ws != null && isConnected)
        {
            // Debug.Log("🔄 Observer 테스트 완료 - 연결 종료");
            ws.Close();
        }
    }
    
    void HandleAutoReset(JObject message)
    {
        try
        {
            // Debug.Log("🔄 게임이 자동으로 대기 상태로 리셋되었습니다!");
            
            // 게임 상태 초기화 (서버는 lobby 상태로 전환)
            gameStatus = "lobby";
            canStartGame = isAdminMode;
            
            // 파티클 풀 초기화
            ResetElementParticlePool();
            
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ AutoReset 처리 오류: {ex.Message}");
        }
    }

    /* ===================================
     * 기존 파티클 시스템 (주석처리 - 사용 안 함)
     * ===================================
    /// <summary>
    /// 팀별 히트 카운트 누적 (1초 동안)
    /// </summary>
    /// <param name="team">팀 이름 (Red 또는 Blue)</param>
    void AccumulateTeamHit(string team)
    {
        if (string.IsNullOrEmpty(team))
        {
            Debug.LogWarning("⚠️ [AccumulateTeamHit] 팀 정보가 비어있습니다!");
            return;
        }
        
        string normalizedTeam = team.Trim();
        
        if (normalizedTeam.Equals("Red", StringComparison.OrdinalIgnoreCase))
        {
            redTeamHitCount++;
            // Debug.Log($"🔴 [RED TEAM] 히트 누적: {redTeamHitCount}회");
        }
        else if (normalizedTeam.Equals("Blue", StringComparison.OrdinalIgnoreCase))
        {
            blueTeamHitCount++;
            // Debug.Log($"🔵 [BLUE TEAM] 히트 누적: {blueTeamHitCount}회");
        }
        else
        {
            Debug.LogWarning($"⚠️ [AccumulateTeamHit] 알 수 없는 팀: '{normalizedTeam}'");
            return;
        }
        
        // 첫 히트일 때 파티클 분산 재생 코루틴 시작
        if (particleBurstCoroutine == null)
        {
            particleBurstCoroutine = StartCoroutine(ParticleBurstCycle());
            // Debug.Log("🎆 [PARTICLE CYCLE] 파티클 분산 재생 사이클 시작!");
        }
    }
    
    /// <summary>
    /// 1초 주기로 누적된 파티클을 재생하는 코루틴
    /// </summary>
    IEnumerator ParticleBurstCycle()
    {
        while (true)
        {
            // 1초 대기 (히트 누적)
            yield return new WaitForSeconds(1f);
            
            // 누적된 히트 카운트 가져오기
            int redHits = redTeamHitCount;
            int blueHits = blueTeamHitCount;
            
            // 카운트 초기화
            redTeamHitCount = 0;
            blueTeamHitCount = 0;
            
            // Debug.Log($"🎆 [PARTICLE BURST] 1초 주기 도달 - Red: {redHits}회, Blue: {blueHits}회");
            
            // 히트가 없으면 코루틴 종료
            if (redHits == 0 && blueHits == 0)
            {
                // Debug.Log("🎆 [PARTICLE BURST] 히트가 없어 사이클 종료");
                particleBurstCoroutine = null;
                yield break;
            }
            
            // 파티클 즉시 재생 (Emit으로 한 번에 처리)
            if (redHits > 0)
            {
                PlayParticlesBatch(redteamHitParticle, redHits, "🔴 RED TEAM");
                
                // 추가 파티클 배열 (2개로 나눠서 재생)
                if (redteamHitParticle2 != null && redteamHitParticle2.Length > 0)
                {
                    PlayParticlesArrayBatch(redteamHitParticle2, redHits, "🔴 RED TEAM 2");
                }
            }
            
            if (blueHits > 0)
            {
                PlayParticlesBatch(blueteamHitParticle, blueHits, "🔵 BLUE TEAM");
                
                // 추가 파티클 배열 (2개로 나눠서 재생)
                if (blueteamHitParticle2 != null && blueteamHitParticle2.Length > 0)
                {
                    PlayParticlesArrayBatch(blueteamHitParticle2, blueHits, "🔵 BLUE TEAM 2");
                }
            }
        }
    }
    
    /// <summary>
    /// 파티클을 한 번에 Emit으로 재생
    /// </summary>
    /// <param name="particle">파티클 시스템</param>
    /// <param name="count">재생 횟수</param>
    /// <param name="teamLabel">팀 레이블 (디버그용)</param>
    void PlayParticlesBatch(ParticleSystem particle, int count, string teamLabel)
    {
        if (particle == null)
        {
            Debug.LogError($"❌ [{teamLabel}] 파티클이 NULL입니다!");
            return;
        }
        
        if (count <= 0)
        {
            return;
        }
        
        try
        {
            // 파티클 시스템 설정 확인 (디버깅용)
            var main = particle.main;
            var emission = particle.emission;
            // Debug.Log($"🔍 [{teamLabel}] 파티클 설정 - MaxParticles: {main.maxParticles}, PlayOnAwake: {main.playOnAwake}, Loop: {main.loop}");
            // Debug.Log($"🔍 [{teamLabel}] Emission - RateOverTime: {emission.rateOverTime.constant}, Enabled: {emission.enabled}");
            
            // 파티클 시스템이 자동 재생 중이면 중지
            if (particle.isPlaying)
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                // Debug.Log($"⏹️ [{teamLabel}] 자동 재생 중지 후 Emit 사용");
            }
            
            // 파티클 발사 전 현재 파티클 수
            int beforeCount = particle.particleCount;
            
            particle.Emit(count);
            
            // 파티클 발사 후 현재 파티클 수
            int afterCount = particle.particleCount;
            
            // Debug.Log($"💥 [{teamLabel}] 파티클 Emit({count}) 호출 완료! 파티클 수: {beforeCount} → {afterCount} (실제 발사: {afterCount - beforeCount}개)");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ [{teamLabel}] 파티클 재생 오류: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 파티클 배열을 나눠서 Emit으로 재생
    /// </summary>
    /// <param name="particleArray">파티클 시스템 배열 (2개)</param>
    /// <param name="totalCount">총 재생 횟수</param>
    /// <param name="teamLabel">팀 레이블 (디버그용)</param>
    void PlayParticlesArrayBatch(ParticleSystem[] particleArray, int totalCount, string teamLabel)
    {
        if (particleArray == null || particleArray.Length == 0)
        {
            Debug.LogError($"❌ [{teamLabel}] 파티클 배열이 비어있습니다!");
            return;
        }
        
        if (totalCount <= 0)
        {
            return;
        }
        
        // null이 아닌 파티클만 필터링
        List<ParticleSystem> validParticles = new List<ParticleSystem>();
        for (int i = 0; i < particleArray.Length; i++)
        {
            if (particleArray[i] != null)
            {
                validParticles.Add(particleArray[i]);
            }
        }
        
        if (validParticles.Count == 0)
        {
            Debug.LogError($"❌ [{teamLabel}] 유효한 파티클이 없습니다!");
            return;
        }
        
        // Debug.Log($"🎆 [{teamLabel}] 총 {totalCount}개를 {validParticles.Count}개 파티클에 나눠서 재생!");
        
        // 총 횟수를 파티클 개수로 나눔
        int countPerParticle = totalCount / validParticles.Count;
        int remainder = totalCount % validParticles.Count;
        
        // 각 파티클에 Emit
        for (int i = 0; i < validParticles.Count; i++)
        {
            int emitCount = countPerParticle + (i < remainder ? 1 : 0);
            
            try
            {
                var particle = validParticles[i];
                
                // 파티클 시스템이 자동 재생 중이면 중지
                if (particle.isPlaying)
                {
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    // Debug.Log($"⏹️ [{teamLabel}][{i}] 자동 재생 중지");
                }
                
                int beforeCount = particle.particleCount;
                particle.Emit(emitCount);
                int afterCount = particle.particleCount;
                
                // Debug.Log($"💥 [{teamLabel}][{i}] Emit({emitCount}) - 파티클 수: {beforeCount} → {afterCount} (실제: {afterCount - beforeCount}개)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ [{teamLabel}][{i}] 파티클 재생 오류: {ex.Message}");
            }
        }
        
        // Debug.Log($"✅ [{teamLabel}] 배열 파티클 재생 완료!");
    }
    
    /// <summary>
    /// 지정된 횟수만큼 파티클을 1초 동안 분산하여 재생 (레거시 - 사용 안 함)
    /// </summary>
    /// <param name="particle">파티클 시스템</param>
    /// <param name="count">재생 횟수</param>
    /// <param name="teamLabel">팀 레이블 (디버그용)</param>
    IEnumerator PlayDistributedParticles(ParticleSystem particle, int count, string teamLabel)
    {
        if (particle == null)
        {
            Debug.LogError($"❌ [{teamLabel}] 파티클이 NULL입니다!");
            yield break;
        }
        
        if (count <= 0)
        {
            yield break;
        }
        
        // Debug.Log($"🎆 [{teamLabel}] {count}회 파티클 분산 재생 시작!");
        
        // 1초를 count 개수로 나누어 시간 간격 계산
        float interval = 1f / count;
        
        for (int i = 0; i < count; i++)
        {
            // 파티클 재생
            PlaySingleParticle(particle, teamLabel, i + 1, count);
            
            // 마지막 재생이 아니면 대기
            if (i < count - 1)
            {
                yield return new WaitForSeconds(interval);
            }
        }
        
        // Debug.Log($"✅ [{teamLabel}] {count}회 파티클 분산 재생 완료!");
    }
    
    /// <summary>
    /// 파티클 배열을 나눠서 지정된 횟수만큼 1초 동안 분산하여 재생
    /// </summary>
    /// <param name="particleArray">파티클 시스템 배열 (2개)</param>
    /// <param name="totalCount">총 재생 횟수</param>
    /// <param name="teamLabel">팀 레이블 (디버그용)</param>
    IEnumerator PlayDistributedParticlesArray(ParticleSystem[] particleArray, int totalCount, string teamLabel)
    {
        if (particleArray == null || particleArray.Length == 0)
        {
            Debug.LogError($"❌ [{teamLabel}] 파티클 배열이 비어있습니다!");
            yield break;
        }
        
        if (totalCount <= 0)
        {
            yield break;
        }
        
        // null이 아닌 파티클만 필터링
        List<ParticleSystem> validParticles = new List<ParticleSystem>();
        for (int i = 0; i < particleArray.Length; i++)
        {
            if (particleArray[i] != null)
            {
                validParticles.Add(particleArray[i]);
            }
            else
            {
                Debug.LogWarning($"⚠️ [{teamLabel}] 배열 인덱스 {i}의 파티클이 NULL입니다!");
            }
        }
        
        if (validParticles.Count == 0)
        {
            Debug.LogError($"❌ [{teamLabel}] 유효한 파티클이 없습니다!");
            yield break;
        }
        
        // Debug.Log($"🎆 [{teamLabel}] 총 {totalCount}회를 {validParticles.Count}개 파티클에 나눠서 재생!");
        
        // 총 횟수를 파티클 개수로 나눔
        int countPerParticle = totalCount / validParticles.Count;
        int remainder = totalCount % validParticles.Count;
        
        // 각 파티클에 할당할 횟수 계산
        List<int> countsPerParticle = new List<int>();
        for (int i = 0; i < validParticles.Count; i++)
        {
            int assignedCount = countPerParticle + (i < remainder ? 1 : 0);
            countsPerParticle.Add(assignedCount);
            // Debug.Log($"   [{teamLabel}] 파티클[{i}]: {assignedCount}회 할당");
        }
        
        // 1초를 totalCount로 나눈 간격 계산
        float interval = 1f / totalCount;
        
        // 각 파티클에서 재생할 인덱스 추적
        List<int> playedCounts = new List<int>(new int[validParticles.Count]);
        
        // totalCount만큼 반복하며 파티클을 순서대로 재생
        for (int i = 0; i < totalCount; i++)
        {
            // 현재 어느 파티클을 재생할지 결정 (순환 방식)
            int particleIndex = -1;
            for (int j = 0; j < validParticles.Count; j++)
            {
                if (playedCounts[j] < countsPerParticle[j])
                {
                    particleIndex = j;
                    break;
                }
            }
            
            if (particleIndex == -1)
            {
                Debug.LogWarning($"⚠️ [{teamLabel}] 파티클 할당 오류!");
                yield break;
            }
            
            // 선택된 파티클 재생
            PlaySingleParticle(validParticles[particleIndex], $"{teamLabel}[{particleIndex}]", playedCounts[particleIndex] + 1, countsPerParticle[particleIndex]);
            playedCounts[particleIndex]++;
            
            // 마지막 재생이 아니면 대기
            if (i < totalCount - 1)
            {
                yield return new WaitForSeconds(interval);
            }
        }
        
        // Debug.Log($"✅ [{teamLabel}] 배열 파티클 분산 재생 완료!");
    }
    
    /// <summary>
    /// 단일 파티클 재생
    /// </summary>
    /// <param name="particle">파티클 시스템</param>
    /// <param name="teamLabel">팀 레이블 (디버그용)</param>
    /// <param name="currentCount">현재 재생 순서</param>
    /// <param name="totalCount">전체 재생 횟수</param>
    void PlaySingleParticle(ParticleSystem particle, string teamLabel, int currentCount, int totalCount)
    {
        if (particle == null)
        {
            return;
        }
        
        try
        {
            // 파티클 재생 (Emit 사용으로 더 정확한 제어)
            particle.Emit(1);
            // Debug.Log($"💥 [{teamLabel}] 파티클 재생 ({currentCount}/{totalCount})");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ [{teamLabel}] 파티클 재생 오류: {ex.Message}");
        }
    }
    */ // 기존 파티클 시스템 주석 끝
    
    /* === HandleGameEnd / BlinkResultPanelsFor12Seconds / HideResultPanels 주석 처리 ===
       (개인전 전환으로 ResultPanel 불필요 - 우승자 비디오만 재생)
    
    /// <summary>
    /// 게임 종료 처리 및 결과창 표시 (개인전)
    /// </summary>
    /// <param name="message">gameEnd 메시지</param>
    void HandleGameEnd(JObject message)
    {
        try
        {
            var data = message["data"];
            if (data == null)
            {
                Debug.LogWarning("⚠️ gameEnd 메시지에 data가 없습니다!");
                return;
            }
            
            // Desktop(LED)에서는 winner 정보 불필요 — 결과창만 표시
            // Debug.Log($"🏆 게임 종료! 결과창 표시 (Desktop/LED)");
            
            // 모든 결과창 오브젝트 활성화 및 점멸 효과 적용
            if (resultGroupObjects != null && resultGroupObjects.Length > 0)
            {
                // Debug.Log($"🎊 결과창 표시 시작 - 총 {resultGroupObjects.Length}개");
                
                for (int i = 0; i < resultGroupObjects.Length; i++)
                {
                    var resultPanel = resultGroupObjects[i];
                    if (resultPanel != null)
                    {
                        // 오브젝트 활성화
                        resultPanel.gameObject.SetActive(true);
                        
                        // 개인전: 기본 WinRed 호출 (단일 색상 통일)
                        resultPanel.WinRed();
                        // Debug.Log($"🏆 [결과창] 표시: {resultPanel.gameObject.name} (개인전)");
                    }
                    else
                    {
                        Debug.LogWarning("⚠️ ResultPanel 요소가 null입니다!");
                    }
                }
                
                // Debug.Log("✅ 결과창 표시 완료!");
                
                // 모든 결과창 동시에 12초 동안 점멸 효과 시작
                StartCoroutine(BlinkResultPanelsFor12Seconds());
            }
            else
            {
                Debug.LogWarning("⚠️ resultGroupObjects가 비어있거나 할당되지 않았습니다!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ 게임 종료 처리 오류: {ex.Message}");
            Debug.LogError($"❌ 스택 트레이스: {ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// 결과 패널 12초 동안 점멸 효과 (모든 패널 동시에)
    /// </summary>
    IEnumerator BlinkResultPanelsFor12Seconds()
    {
        if (resultPanelCanvasGroups == null || resultPanelCanvasGroups.Length == 0)
        {
            Debug.LogWarning("⚠️ resultPanelCanvasGroups가 없습니다!");
            yield break;
        }
        
        // Debug.Log("✨ 결과창 12초 점멸 효과 시작 (모든 패널 동시에)");
        
        float totalDuration = 12f; // 총 12초
        float blinkCycleDuration = 1f; // 1초마다 한 사이클 (0.5초 페이드 인 + 0.5초 페이드 아웃)
        float elapsed = 0f;
        
        while (elapsed < totalDuration)
        {
            float cycleTime = elapsed % blinkCycleDuration;
            float normalizedCycleTime = cycleTime / blinkCycleDuration;
            
            float alpha;
            if (normalizedCycleTime < 0.5f)
            {
                alpha = normalizedCycleTime * 2f;
            }
            else
            {
                alpha = (1f - normalizedCycleTime) * 2f;
            }
            
            foreach (var canvasGroup in resultPanelCanvasGroups)
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = alpha;
                }
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Debug.Log("✅ 12초 점멸 완료 - 최종 페이드 아웃 시작");
        
        float fadeOutDuration = 0.5f;
        float fadeOutElapsed = 0f;
        
        while (fadeOutElapsed < fadeOutDuration)
        {
            fadeOutElapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (fadeOutElapsed / fadeOutDuration));
            
            foreach (var canvasGroup in resultPanelCanvasGroups)
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = alpha;
                }
            }
            
            yield return null;
        }
        
        foreach (var canvasGroup in resultPanelCanvasGroups)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }
        
        foreach (var resultPanel in resultGroupObjects)
        {
            if (resultPanel != null)
            {
                resultPanel.gameObject.SetActive(false);
            }
        }
        
        // Debug.Log("🚪 결과창 12초 점멸 후 비활성화 완료");
    }
    
    /// <summary>
    /// 모든 결과창 숨기기 (대기방 복귀 시)
    /// </summary>
    void HideResultPanels()
    {
        if (resultGroupObjects != null && resultGroupObjects.Length > 0)
        {
            // Debug.Log($"🚪 결과창 닫기 시작 - 총 {resultGroupObjects.Length}개");
            
            if (resultPanelCanvasGroups != null)
            {
                foreach (var canvasGroup in resultPanelCanvasGroups)
                {
                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = 1f;
                    }
                }
            }
            
            foreach (var resultPanel in resultGroupObjects)
            {
                if (resultPanel != null)
                {
                    resultPanel.gameObject.SetActive(false);
                    // Debug.Log($"✅ [결과창 닫기] {resultPanel.gameObject.name} (알파값 초기화)");
                }
            }
            
            // Debug.Log("✅ 모든 결과창 닫기 완료!");
        }
        else
        {
            Debug.LogWarning("⚠️ resultGroupObjects가 비어있거나 할당되지 않았습니다!");
        }
    }
    === HandleGameEnd / BlinkResultPanelsFor12Seconds / HideResultPanels 주석 처리 끝 === */
    
    /* 기존 파티클 카운트 초기화 함수 (사용 안 함)
    void ResetParticleCount()
    {
        // Debug.Log($"🔄 [PARTICLE COUNT RESET] Red: {redTeamHitCount}회, Blue: {blueTeamHitCount}회 → 0으로 초기화");
        
        redTeamHitCount = 0;
        blueTeamHitCount = 0;
        
        // 파티클 사이클 코루틴 중지
        if (particleBurstCoroutine != null)
        {
            StopCoroutine(particleBurstCoroutine);
            particleBurstCoroutine = null;
            // Debug.Log("🛑 [PARTICLE CYCLE] 파티클 분산 재생 사이클 중지");
        }
        
        // 팀 점수 추적 초기화
        lastRedTeamScore = 0;
        lastBlueTeamScore = 0;
        // Debug.Log("🔄 [TEAM SCORE] 이전 팀 점수 추적 데이터 초기화");
    }
    */ // 기존 파티클 시스템 주석 끝
    
    // ===================================
    // 🆕 Element별 능력 파티클 풀링 시스템 (6종)
    // ===================================

    /// <summary>
    /// Element별 파티클 풀 초기화
    /// </summary>
    void InitializeElementParticlePool()
    {
        // Debug.Log($"🎆 [Element Pool] 풀 초기화 시작 - Element당 풀 크기: {poolSize} (3변형)");

        ParticleSystem[][] variantArrays = { elementParticlePrefabsA, elementParticlePrefabsB, elementParticlePrefabsC };

        Transform poolParent = particlePoolContainer != null ? particlePoolContainer : transform;

        if (particlePoolContainer != null)
        {
            // Debug.Log($"✅ [Element Pool] 파티클 컨테이너 사용: {particlePoolContainer.name}");
        }
        else
        {
            Debug.LogWarning("⚠️ [Element Pool] particlePoolContainer가 할당되지 않아 현재 오브젝트를 사용합니다.");
        }

        elementParticlePools.Clear();
        activeElementParticles.Clear();

        for (int i = 0; i < elementNames.Length; i++)
        {
            string elementName = elementNames[i];

            List<ParticleSystem> validPrefabs = new List<ParticleSystem>();
            for (int v = 0; v < variantArrays.Length; v++)
            {
                if (variantArrays[v] != null && i < variantArrays[v].Length && variantArrays[v][i] != null)
                {
                    validPrefabs.Add(variantArrays[v][i]);
                }
            }

            if (validPrefabs.Count == 0)
            {
                Debug.LogWarning($"⚠️ [Element Pool] {elementName} 파티클 프리팹이 하나도 할당되지 않았습니다! 건너뜁니다.");
                continue;
            }

            Queue<ParticleSystem> pool = new Queue<ParticleSystem>();
            List<ParticleSystem> activeList = new List<ParticleSystem>();

            int perVariant = poolSize / validPrefabs.Count;
            int remainder = poolSize % validPrefabs.Count;
            int totalCreated = 0;

            for (int v = 0; v < validPrefabs.Count; v++)
            {
                int count = perVariant + (v < remainder ? 1 : 0);
                char variantLabel = (char)('A' + v);

                for (int j = 0; j < count; j++)
                {
                    ParticleSystem ps = Instantiate(validPrefabs[v], poolParent);
                    ps.gameObject.name = $"{elementName}Particle_{variantLabel}_{j}";
                    ps.gameObject.SetActive(false);
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    pool.Enqueue(ps);
                    totalCreated++;
                }
            }

            // 풀 내부를 셔플하여 변형이 랜덤 순서로 나오도록 함
            List<ParticleSystem> shuffleList = new List<ParticleSystem>(pool);
            for (int s = shuffleList.Count - 1; s > 0; s--)
            {
                int r = UnityEngine.Random.Range(0, s + 1);
                var temp = shuffleList[s];
                shuffleList[s] = shuffleList[r];
                shuffleList[r] = temp;
            }
            pool = new Queue<ParticleSystem>(shuffleList);

            elementParticlePools[elementName] = pool;
            activeElementParticles[elementName] = activeList;

            // Debug.Log($"✅ [Element Pool] {elementName}: {totalCreated}개 생성 완료 ({validPrefabs.Count}변형)");
        }

        // Debug.Log($"✅ [Element Pool] 총 {elementParticlePools.Count}개 Element 풀 초기화 완료 (각 {poolSize}개, 3변형 셔플)");
    }

    /// <summary>
    /// 실시간 Press 알림 처리 (element 기반 파티클 재생)
    /// </summary>
    void HandleImmediatePress(JObject message)
    {
        try
        {
            var data = message["data"];
            if (data == null)
            {
                Debug.LogWarning("⚠️ [HandleImmediatePress] data가 없습니다!");
                return;
            }

            // 서버에서 전송한 element 필드 사용
            string element = data["element"]?.ToString() ?? "None";
            string playerName = data["playerName"]?.ToString() ?? "";

            // Debug.Log($"🎆 [IMMEDIATE PRESS] {playerName} (element: {element}) → 능력 파티클 재생!");

            // Element에 해당하는 파티클 즉시 재생
            PlayElementParticle(element);
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ [HandleImmediatePress] 오류: {ex.Message}");
        }
    }

    /// <summary>
    /// playerPressImmediate 메시지의 element 필드를 JObject 파싱 없이 빠르게 추출.
    /// press는 게임 중 가장 빈번한 메시지(초당 수십 회)이므로 이 핫 경로에서
    /// JObject.Parse + JToken chain의 GC 할당을 회피하기 위한 fast path.
    /// 메시지 포맷이 변형되어 추출 실패하면 false 반환 → 호출자가 일반 JObject 파싱으로 폴백.
    /// </summary>
    private static bool TryFastParsePressElement(string json, out string element)
    {
        element = null;
        const string KEY = "\"element\":\"";
        int s = json.IndexOf(KEY, StringComparison.Ordinal);
        if (s < 0) return false;
        s += KEY.Length;
        int e = json.IndexOf('"', s);
        if (e < 0) return false;
        element = json.Substring(s, e - s);
        return true;
    }

    /// <summary>
    /// Element별 능력 파티클 재생
    /// </summary>
    void PlayElementParticle(string element)
    {
        if (string.IsNullOrEmpty(element) || element == "None")
        {
            Debug.LogWarning("⚠️ [PlayElementParticle] element가 비어있거나 None입니다!");
            return;
        }

        // element 이름 정규화 (대소문자 일치 확인)
        string normalizedElement = null;
        for (int i = 0; i < elementNames.Length; i++)
        {
            if (elementNames[i].Equals(element, StringComparison.OrdinalIgnoreCase))
            {
                normalizedElement = elementNames[i];
                break;
            }
        }

        if (normalizedElement == null)
        {
            Debug.LogWarning($"⚠️ [PlayElementParticle] 알 수 없는 element: '{element}'");
            return;
        }

        // 해당 element의 파티클 풀 확인
        if (!elementParticlePools.ContainsKey(normalizedElement))
        {
            Debug.LogWarning($"⚠️ [PlayElementParticle] '{normalizedElement}' 풀이 초기화되지 않았습니다!");
            return;
        }

        Queue<ParticleSystem> pool = elementParticlePools[normalizedElement];
        List<ParticleSystem> activeList = activeElementParticles[normalizedElement];

        if (pool.Count > 0)
        {
            ParticleSystem ps = pool.Dequeue();
            activeList.Add(ps);

            // 랜덤 위치 설정 (x: -1~1, y: -1~1)
            float randomX = UnityEngine.Random.Range(-1f, 1f);
            float randomY = UnityEngine.Random.Range(-1f, 1f);
            ps.transform.localPosition = new Vector3(randomX, randomY, 0f);

            ps.gameObject.SetActive(true);
            ps.Play();

            // Debug.Log($"✨ [{normalizedElement}] 파티클 재생! 위치: ({randomX:F2}, {randomY:F2}) (사용 가능: {pool.Count}, 활성: {activeList.Count})");

            // 1초 후 풀로 반환
            StartCoroutine(ReturnElementParticleToPool(ps, normalizedElement, 1f));
        }
        else
        {
            Debug.LogWarning($"⚠️ [{normalizedElement}] 파티클 풀이 비어있습니다! (활성: {activeList.Count})");
        }
    }

    /// <summary>
    /// Element 파티클을 풀로 반환.
    /// FIX (2026-04-24): Remove() 반환값으로 activeList에 실제 존재했는지 확인 후에만 Enqueue.
    /// ResetElementParticlePool이 코루틴 대기 중 호출되어 이미 풀 복귀 처리한 ps를 중복 Enqueue하던 문제 방지.
    /// </summary>
    IEnumerator ReturnElementParticleToPool(ParticleSystem ps, string element, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (ps == null) yield break;

        // 파티클 정지 및 비활성화
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.gameObject.SetActive(false);

        // 풀로 반환 (중복 enqueue 방지)
        if (elementParticlePools.ContainsKey(element) && activeElementParticles.ContainsKey(element))
        {
            bool wasStillActive = activeElementParticles[element].Remove(ps);
            if (wasStillActive)
            {
                elementParticlePools[element].Enqueue(ps);
                // Debug.Log($"✨ [{element}] 풀 반환 (사용 가능: {elementParticlePools[element].Count}, 활성: {activeElementParticles[element].Count})");
            }
            // else: ResetElementParticlePool이 먼저 처리함. 중복 Enqueue 방지 위해 스킵.
        }
    }

    /// <summary>
    /// Element 파티클 풀 리셋 (게임 종료/리셋 시)
    /// </summary>
    void ResetElementParticlePool()
    {
        // Debug.Log($"🔄 [Element Pool] 풀 리셋 시작...");

        foreach (var kvp in activeElementParticles)
        {
            string element = kvp.Key;
            List<ParticleSystem> activeList = kvp.Value;

            foreach (var ps in activeList)
            {
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.gameObject.SetActive(false);
                    if (elementParticlePools.ContainsKey(element))
                    {
                        elementParticlePools[element].Enqueue(ps);
                    }
                }
            }
            activeList.Clear();
        }

        // 각 풀의 상태 로그
        foreach (var kvp in elementParticlePools)
        {
            // Debug.Log($"✅ [Element Pool] {kvp.Key}: {kvp.Value.Count}개 사용 가능");
        }

        // Debug.Log($"✅ [Element Pool] 리셋 완료");
    }
    
    /* 기존 PlayTeamHitParticle 함수 (사용 안 함)
    void PlayTeamHitParticle(string team)
    {
        // Debug.Log($"🎆 [PlayTeamHitParticle] 시작 - 입력된 팀: '{team}'");
        
        if (string.IsNullOrEmpty(team))
        {
            Debug.LogWarning("⚠️ [PlayTeamHitParticle] 팀 정보가 비어있습니다!");
            return;
        }
        
        // 팀 이름 정규화 (대소문자 무시, 공백 제거)
        string normalizedTeam = team.Trim();
        // Debug.Log($"🎆 [PlayTeamHitParticle] 정규화된 팀: '{normalizedTeam}'");
        
        if (normalizedTeam.Equals("Red", StringComparison.OrdinalIgnoreCase))
        {
            // Debug.Log($"🔴 [RED TEAM] 파티클 재생 시도 - 파티클 객체: {(redteamHitParticle != null ? "할당됨" : "NULL")}");
            
            if (redteamHitParticle != null)
            {
                // 파티클 상세 정보 출력
                // Debug.Log($"🔴 [RED TEAM] 파티클 상태 - isPlaying: {redteamHitParticle.isPlaying}, isStopped: {redteamHitParticle.isStopped}, isPaused: {redteamHitParticle.isPaused}");
                // Debug.Log($"🔴 [RED TEAM] 파티클 GameObject - 이름: {redteamHitParticle.gameObject.name}, 활성화: {redteamHitParticle.gameObject.activeSelf}, 계층 활성화: {redteamHitParticle.gameObject.activeInHierarchy}");
                
                // 🎆 방법 1: Clear() + Play() (가장 확실한 방법)
                try
                {
                    redteamHitParticle.Clear();
                    redteamHitParticle.Play();
                    // Debug.Log($"🔴 [RED TEAM] 파티클 Clear() + Play() 호출 완료!");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"❌ [RED TEAM] 파티클 재생 오류: {ex.Message}");
                }
                
                // 재생 후 상태 확인
                // Debug.Log($"🔴 [RED TEAM] 재생 후 상태 - isPlaying: {redteamHitParticle.isPlaying}");
                
                // 🎆 방법 2: 코루틴으로 강제 재생 (Play()가 작동하지 않는 경우 대비)
                StartCoroutine(ForcePlayParticle(redteamHitParticle, "🔴 RED TEAM"));
            }
            else
            {
                Debug.LogError("❌ [RED TEAM] 파티클이 Inspector에 할당되지 않았습니다! (redteamHitParticle이 NULL입니다)");
            }
        }
        else if (normalizedTeam.Equals("Blue", StringComparison.OrdinalIgnoreCase))
        {
            // Debug.Log($"🔵 [BLUE TEAM] 파티클 재생 시도 - 파티클 객체: {(blueteamHitParticle != null ? "할당됨" : "NULL")}");
            
            if (blueteamHitParticle != null)
            {
                // 파티클 상세 정보 출력
                // Debug.Log($"🔵 [BLUE TEAM] 파티클 상태 - isPlaying: {blueteamHitParticle.isPlaying}, isStopped: {blueteamHitParticle.isStopped}, isPaused: {blueteamHitParticle.isPaused}");
                // Debug.Log($"🔵 [BLUE TEAM] 파티클 GameObject - 이름: {blueteamHitParticle.gameObject.name}, 활성화: {blueteamHitParticle.gameObject.activeSelf}, 계층 활성화: {blueteamHitParticle.gameObject.activeInHierarchy}");
                
                // 🎆 방법 1: Clear() + Play() (가장 확실한 방법)
                try
                {
                    blueteamHitParticle.Clear();
                    blueteamHitParticle.Play();
                    // Debug.Log($"🔵 [BLUE TEAM] 파티클 Clear() + Play() 호출 완료!");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"❌ [BLUE TEAM] 파티클 재생 오류: {ex.Message}");
                }
                
                // 재생 후 상태 확인
                // Debug.Log($"🔵 [BLUE TEAM] 재생 후 상태 - isPlaying: {blueteamHitParticle.isPlaying}");
                
                // 🎆 방법 2: 코루틴으로 강제 재생 (Play()가 작동하지 않는 경우 대비)
                StartCoroutine(ForcePlayParticle(blueteamHitParticle, "🔵 BLUE TEAM"));
            }
            else
            {
                Debug.LogError("❌ [BLUE TEAM] 파티클이 Inspector에 할당되지 않았습니다! (blueteamHitParticle이 NULL입니다)");
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ [PlayTeamHitParticle] 알 수 없는 팀 이름: '{normalizedTeam}' (원본: '{team}')");
            Debug.LogWarning($"⚠️ 예상되는 값: 'Red' 또는 'Blue'");
        }
    }
    
    /// <summary>
    /// 파티클을 강제로 재생하는 코루틴 (Play()가 작동하지 않을 때 대비)
    /// </summary>
    /// <param name="particle">재생할 파티클 시스템</param>
    /// <param name="teamLabel">팀 레이블 (디버그용)</param>
    IEnumerator ForcePlayParticle(ParticleSystem particle, string teamLabel)
    {
        if (particle == null)
        {
            Debug.LogError($"❌ [{teamLabel}] ForcePlayParticle - 파티클이 NULL입니다!");
            yield break;
        }
        
        // Debug.Log($"🔄 [{teamLabel}] ForcePlayParticle 시작 - 0.1초 대기 후 재생");
        
        // 다음 프레임까지 대기
        yield return null;
        
        // 파티클이 재생 중이 아니면 다시 시도
        if (!particle.isPlaying)
        {
            // Debug.Log($"⚠️ [{teamLabel}] 파티클이 재생되지 않음 - 다시 시도");
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            yield return new WaitForSeconds(0.05f);
            particle.Play();
            // Debug.Log($"🔄 [{teamLabel}] Stop() + Play() 재시도 완료");
        }
        else
        {
            // Debug.Log($"✅ [{teamLabel}] 파티클이 정상적으로 재생 중입니다!");
        }
        
        // 최종 상태 확인
        yield return new WaitForSeconds(0.1f);
        // Debug.Log($"🎆 [{teamLabel}] 최종 상태 - isPlaying: {particle.isPlaying}, particleCount: {particle.particleCount}");
    }
    */ // 기존 파티클 시스템 주석 끝
    
    // ===================================
    // 비디오 시스템 (Idle + 게임 중 Element 순환 + Element 종료 영상)
    // ===================================
    
    /// <summary>
    /// VideoPlayer / Idle CanvasGroup 초기화 (idle → 게임 영상 크로스 디졸브용).
    /// </summary>
    void InitializeGameVideoCanvasGroups()
    {
        if (gameImageA != null)
        {
            gameCanvasGroupA = gameImageA.GetComponent<CanvasGroup>();
            if (gameCanvasGroupA == null)
            {
                gameCanvasGroupA = gameImageA.gameObject.AddComponent<CanvasGroup>();
            }
            gameCanvasGroupA.alpha = 1f;
        }

        if (gameImageB != null)
        {
            gameCanvasGroupB = gameImageB.GetComponent<CanvasGroup>();
            if (gameCanvasGroupB == null)
            {
                gameCanvasGroupB = gameImageB.gameObject.AddComponent<CanvasGroup>();
            }
            gameCanvasGroupB.alpha = 0f;
        }

        if (idleImage != null)
        {
            idleCanvasGroup = idleImage.GetComponent<CanvasGroup>();
            if (idleCanvasGroup == null)
            {
                idleCanvasGroup = idleImage.gameObject.AddComponent<CanvasGroup>();
            }
            idleCanvasGroup.alpha = 1f;
        }

        // Debug.Log("✅ [InitGameVideoCanvasGroups] 게임/Idle CanvasGroup 초기화 완료");
    }
    
    /// <summary>
    /// 통합 게임 영상(단일 클립 loop) 재생을 시작합니다.
    /// idle → 게임 영상 크로스 디졸브를 수행하고, 이후에는 VideoPlayer 자체 loop으로 재생됩니다.
    /// </summary>
    void StartGameVideo()
    {
        if (gameVideoPlayerA == null)
        {
            Debug.LogError("❌ [StartGameVideo] gameVideoPlayerA가 할당되지 않았습니다!");
            return;
        }

        if (gameImageA == null)
        {
            Debug.LogError("❌ [StartGameVideo] gameImageA가 할당되지 않았습니다!");
            return;
        }

        if (gameVideoClip == null)
        {
            Debug.LogWarning("⚠️ [StartGameVideo] gameVideoClip이 할당되지 않았습니다 - idle 영상 유지");
            return;
        }

        // idle은 디졸브가 끝난 뒤에 정지 (idle → 게임 영상 크로스 디졸브용)
        gameImageA.gameObject.SetActive(true);
        if (gameImageB != null) gameImageB.gameObject.SetActive(false);
        if (gameCanvasGroupA != null) gameCanvasGroupA.alpha = 0f;
        if (gameCanvasGroupB != null) gameCanvasGroupB.alpha = 0f;
        if (idleCanvasGroup != null) idleCanvasGroup.alpha = 1f;
        isGameVideoPlaying = true;

        if (gameVideoCoroutine != null)
        {
            StopCoroutine(gameVideoCoroutine);
        }
        gameVideoCoroutine = StartCoroutine(StartGameVideoCoroutine());

        // Debug.Log($"▶️ [StartGameVideo] 통합 게임 영상 loop 재생 시작 (clip: {gameVideoClip.name})");
    }

    /// <summary>
    /// 통합 게임 영상을 PlayerA에 loop로 올리고 idle → 게임 영상 크로스 디졸브를 수행합니다.
    /// 디졸브 완료 후에는 VideoPlayer 자체 loop이 영상을 계속 재생하므로 코루틴은 종료됩니다.
    /// </summary>
    IEnumerator StartGameVideoCoroutine()
    {
        // Debug.Log($"🎬 [GameVideo] 재생 시작: {gameVideoClip.name}");

        gameVideoPlayerA.clip = gameVideoClip;
        gameVideoPlayerA.isLooping = true;
        gameVideoPlayerA.time = 0;
        gameVideoPlayerA.Prepare();

        float prepareStart = Time.time;
        while (!gameVideoPlayerA.isPrepared && (Time.time - prepareStart) < PREPARE_TIMEOUT && isGameVideoPlaying)
        {
            yield return null;
        }
        if (!isGameVideoPlaying) yield break;

        gameVideoPlayerA.Play();
        // 첫 프레임 도착 대기 (검은 프레임이 페이드인되는 것을 방지)
        int firstFrameWait = FIRST_FRAME_WAIT_MAX_FRAMES;
        while (gameVideoPlayerA.frame < 0 && firstFrameWait-- > 0 && isGameVideoPlaying)
        {
            yield return null;
        }
        if (!isGameVideoPlaying) yield break;

        // idle → 게임 영상 크로스 디졸브 (idle.alpha 1→0, gameA.alpha 0→1 동시 보간)
        if (idleCanvasGroup != null && gameCanvasGroupA != null && crossfadeDuration > 0f)
        {
            float fadeStart = Time.time;
            while (isGameVideoPlaying)
            {
                float t = (Time.time - fadeStart) / crossfadeDuration;
                if (t >= 1f) break;
                idleCanvasGroup.alpha = 1f - t;
                gameCanvasGroupA.alpha = t;
                yield return null;
            }
        }
        if (!isGameVideoPlaying) yield break;

        if (idleCanvasGroup != null) idleCanvasGroup.alpha = 0f;
        if (gameCanvasGroupA != null) gameCanvasGroupA.alpha = 1f;
        if (gameCanvasGroupB != null) gameCanvasGroupB.alpha = 0f;

        // 디졸브 끝 → idle 비디오 정지
        StopIdleVideo();

        // Debug.Log("✅ [GameVideo] idle → 게임 영상 디졸브 완료, loop 재생 진행 중");
    }
    
    /// <summary>
    /// 게임 중 영상을 정지합니다
    /// </summary>
    void StopGameVideo()
    {
        isGameVideoPlaying = false;

        if (gameVideoCoroutine != null)
        {
            StopCoroutine(gameVideoCoroutine);
            gameVideoCoroutine = null;
            // Debug.Log("⏹️ 게임 중 영상 코루틴 중지");
        }

        if (gameVideoPlayerA != null)
        {
            if (gameVideoPlayerA.isPlaying) gameVideoPlayerA.Stop();
            gameVideoPlayerA.time = 0;
            gameVideoPlayerA.clip = null;
        }

        // PlayerB는 사용하지 않지만, 인스펙터 할당이 남아있을 수 있어 안전하게 정리
        if (gameVideoPlayerB != null)
        {
            if (gameVideoPlayerB.isPlaying) gameVideoPlayerB.Stop();
            gameVideoPlayerB.time = 0;
            gameVideoPlayerB.clip = null;
        }

        if (gameImageA != null) gameImageA.gameObject.SetActive(false);
        if (gameImageB != null) gameImageB.gameObject.SetActive(false);

        if (gameCanvasGroupA != null) gameCanvasGroupA.alpha = 1f;
        if (gameCanvasGroupB != null) gameCanvasGroupB.alpha = 0f;
        // 다음 idle 복귀 시 idleImage가 정상 표시되도록 알파 복구
        if (idleCanvasGroup != null) idleCanvasGroup.alpha = 1f;

        // Debug.Log("⏹️ 게임 영상 정지 및 초기화 완료");
    }
    
    /// <summary>
    /// Idle 비디오를 루프로 재생합니다 (프로그램 시작 시 즉시 호출)
    /// </summary>
    void PlayIdleVideo()
    {
        // Debug.Log("🎥 [PlayIdleVideo] Idle 비디오 재생 시작!");

        if (idleVideoPlayer == null)
        {
            Debug.LogError("❌ idleVideoPlayer가 할당되지 않았습니다!");
            return;
        }

        if (idleImage == null)
        {
            Debug.LogError("❌ idleImage가 할당되지 않았습니다!");
            return;
        }

        // Idle RawImage 활성화
        idleImage.gameObject.SetActive(true);

        // 루프 재생
        if (idleVideoPlayer.clip != null)
        {
            idleVideoPlayer.isLooping = true;
            idleVideoPlayer.time = 0;
            idleVideoPlayer.playbackSpeed = 1.0f;
            idleVideoPlayer.Play();
            // Debug.Log($"▶️ [PlayIdleVideo] Idle 비디오 재생 - Clip: {idleVideoPlayer.clip.name} (루프 모드)");
        }
        else
        {
            Debug.LogError("❌ idleVideoPlayer에 클립이 할당되지 않았습니다!");
        }
    }

    /// <summary>
    /// Idle 비디오를 정지합니다
    /// </summary>
    void StopIdleVideo()
    {
        if (idleVideoPlayer != null)
        {
            if (idleVideoPlayer.isPlaying)
            {
                idleVideoPlayer.Stop();
                // Debug.Log("⏹️ Idle 비디오 정지");
            }
            idleVideoPlayer.time = 0;
        }

        if (idleImage != null)
        {
            idleImage.gameObject.SetActive(false);
            // Debug.Log("🖼️ Idle 비디오 화면 비활성화");
        }

        // 다음 PlayIdleVideo() 시 알파 0인 채로 보이지 않는 문제 방지
        if (idleCanvasGroup != null) idleCanvasGroup.alpha = 1f;
    }

    /// <summary>
    /// gameEnd 메시지에서 우승자의 element를 추출합니다
    /// </summary>
    string GetWinnerElement(JObject message)
    {
        try
        {
            var data = message["data"];
            if (data == null) return "None";
            
            // rankings 배열에서 1등의 element 가져오기
            var rankings = data["rankings"] as JArray;
            if (rankings != null && rankings.Count > 0)
            {
                string element = rankings[0]["element"]?.ToString() ?? "None";
                string nickname = rankings[0]["nickname"]?.ToString() ?? "Unknown";
                // Debug.Log($"🏆 [GetWinnerElement] 우승자: {nickname}, Element: {element}");
                return element;
            }
            
            // rankings가 없으면 winner에서 시도 (fallback)
            var winner = data["winner"];
            if (winner != null)
            {
                string winnerId = winner["playerId"]?.ToString() ?? "";
                // Debug.Log($"🏆 [GetWinnerElement] winner.playerId: {winnerId} (element 정보 없음, rankings에서 검색 필요)");
            }
            
            return "None";
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ [GetWinnerElement] 오류: {ex.Message}");
            return "None";
        }
    }
    
    /// <summary>
    /// Element 이름에 해당하는 종료 VideoClip을 찾아 재생합니다
    /// </summary>
    void PlayElementEndVideo(string elementName)
    {
        // Debug.Log($"🎥 [PlayElementEndVideo] element: {elementName}");

        if (endVideoPlayer == null)
        {
            Debug.LogError("❌ endVideoPlayer가 할당되지 않았습니다!");
            return;
        }

        if (endImage == null)
        {
            Debug.LogError("❌ endImage가 할당되지 않았습니다!");
            return;
        }

        // Element 이름으로 인덱스 찾기
        int clipIndex = -1;
        for (int i = 0; i < elementNames.Length; i++)
        {
            if (elementNames[i].Equals(elementName, StringComparison.OrdinalIgnoreCase))
            {
                clipIndex = i;
                break;
            }
        }

        if (clipIndex < 0)
        {
            Debug.LogWarning($"⚠️ [PlayElementEndVideo] 알 수 없는 element: {elementName}");
            return;
        }

        if (clipIndex >= elementEndClips.Length || elementEndClips[clipIndex] == null)
        {
            Debug.LogWarning($"⚠️ [PlayElementEndVideo] element '{elementName}' (index: {clipIndex})에 해당하는 VideoClip이 할당되지 않았습니다!");
            return;
        }

        // 기존 리셋 페이드 중이면 중단
        if (resetFadeCoroutine != null)
        {
            StopCoroutine(resetFadeCoroutine);
            resetFadeCoroutine = null;
        }
        if (endFadeCoroutine != null)
        {
            StopCoroutine(endFadeCoroutine);
            endFadeCoroutine = null;
        }

        endFadeCoroutine = StartCoroutine(FadeIdleToEndRoutine(elementName, elementEndClips[clipIndex]));
    }

    private IEnumerator FadeIdleToEndRoutine(string elementName, UnityEngine.Video.VideoClip clip)
    {
        // End 비디오 Prepare (검은 프레임 방지)
        // 이 동안에도 게임 비디오/idle은 계속 재생 중 → 화면 끊기지 않음
        endImage.gameObject.SetActive(true);
        SetImageAlpha(endImage, 0f);

        endVideoPlayer.clip = clip;
        endVideoPlayer.isLooping = true;
        endVideoPlayer.time = 0;
        endVideoPlayer.Prepare();

        float prepareElapsed = 0f;
        while (!endVideoPlayer.isPrepared && prepareElapsed < endPrepareTimeout)
        {
            prepareElapsed += Time.deltaTime;
            yield return null;
        }

        // Prepare 완료 후 재생 시작
        endVideoPlayer.Play();

        // 현재 표시 중인 게임 CanvasGroup 알파 시작값 캡처
        float gameAlphaStartA = gameCanvasGroupA != null ? gameCanvasGroupA.alpha : 0f;
        float gameAlphaStartB = gameCanvasGroupB != null ? gameCanvasGroupB.alpha : 0f;

        if (idleImage != null) SetImageAlpha(idleImage, 1f);

        float duration = Mathf.Max(0.01f, endFadeDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // SmoothStep 이징 (인게임 크로스 디졸브와 동일)
            float smoothT = t * t * (3f - 2f * t);
            SetImageAlpha(endImage, smoothT);
            if (idleImage != null) SetImageAlpha(idleImage, 1f - smoothT);
            // 게임 비디오 캔버스도 함께 페이드아웃
            if (gameCanvasGroupA != null) gameCanvasGroupA.alpha = gameAlphaStartA * (1f - smoothT);
            if (gameCanvasGroupB != null) gameCanvasGroupB.alpha = gameAlphaStartB * (1f - smoothT);
            yield return null;
        }

        // 페이드 완료: End 풀 알파, 게임/idle 정지
        SetImageAlpha(endImage, 1f);
        if (idleImage != null) SetImageAlpha(idleImage, 1f);
        StopGameVideo();
        StopIdleVideo();

        // Debug.Log($"▶️ [PlayElementEndVideo] 크로스페이드 완료 - Element: {elementName}, Clip: {clip.name}");
        endFadeCoroutine = null;
    }
    
    /// <summary>
    /// 종료 비디오를 정지합니다 (리셋 시 호출)
    /// </summary>
    void StopEndVideo()
    {
        if (endVideoPlayer != null)
        {
            if (endVideoPlayer.isPlaying)
            {
                endVideoPlayer.Stop();
                // Debug.Log("⏹️ 종료 영상 정지");
            }
            endVideoPlayer.time = 0;
            endVideoPlayer.clip = null; // 클립 해제
        }
        
        if (endImage != null)
        {
            endImage.gameObject.SetActive(false);
            // Debug.Log("🖼️ 종료 영상 화면 비활성화");
        }
    }

    void OnDestroy()
    {
        // 재연결 코루틴 중지
        if (reconnectCoroutine != null)
        {
            StopCoroutine(reconnectCoroutine);
            reconnectCoroutine = null;
        }
        isReconnecting = false;
        
        // 파티클 풀 초기화
        ResetElementParticlePool();
        
        // 결과창 닫기 (개인전 전환으로 주석 처리)
        // HideResultPanels();
        
        // 모든 비디오 정지
        StopGameVideo();
        StopIdleVideo();
        StopEndVideo();
        
        if (ws != null && isConnected)
        {
            try { ws.Close(); } catch { }
            ws = null;
        }
    }

    private void PlayFxStartTransition()
    {
        if (fxGroupStart == null) return;

        if (fxStartHideCoroutine != null)
        {
            StopCoroutine(fxStartHideCoroutine);
            fxStartHideCoroutine = null;
        }

        fxGroupStart.SetActive(false);
        fxGroupStart.SetActive(true);
        fxStartHideCoroutine = StartCoroutine(HideFxAfterDelay(fxGroupStart, true));
    }

    private void PlayFxFinishTransition()
    {
        if (fxGroupFinish == null) return;

        if (fxFinishHideCoroutine != null)
        {
            StopCoroutine(fxFinishHideCoroutine);
            fxFinishHideCoroutine = null;
        }

        fxGroupFinish.SetActive(false);
        fxGroupFinish.SetActive(true);
        fxFinishHideCoroutine = StartCoroutine(HideFxAfterDelay(fxGroupFinish, false));
    }

    private IEnumerator HideFxAfterDelay(GameObject fxGroup, bool isStartGroup)
    {
        yield return new WaitForSeconds(fxAutoHideDuration);

        if (fxGroup != null) fxGroup.SetActive(false);

        if (isStartGroup) fxStartHideCoroutine = null;
        else fxFinishHideCoroutine = null;
    }

    private void StopAllFxTransition()
    {
        if (fxStartHideCoroutine != null)
        {
            StopCoroutine(fxStartHideCoroutine);
            fxStartHideCoroutine = null;
        }
        if (fxFinishHideCoroutine != null)
        {
            StopCoroutine(fxFinishHideCoroutine);
            fxFinishHideCoroutine = null;
        }
        if (fxGroupStart != null) fxGroupStart.SetActive(false);
        if (fxGroupFinish != null) fxGroupFinish.SetActive(false);
    }

    private void StartResetFadeTransition()
    {
        if (idleImage == null || endImage == null)
        {
            // 페이드 대상 이미지가 없으면 즉시 전환(기존 방식)으로 fallback
            StopEndVideo();
            PlayIdleVideo();
            return;
        }

        // 진행 중인 Idle→End 페이드 중단
        if (endFadeCoroutine != null)
        {
            StopCoroutine(endFadeCoroutine);
            endFadeCoroutine = null;
        }

        if (resetFadeCoroutine != null)
        {
            StopCoroutine(resetFadeCoroutine);
            resetFadeCoroutine = null;
        }

        resetFadeCoroutine = StartCoroutine(FadeEndToIdleRoutine());
    }

    private IEnumerator FadeEndToIdleRoutine()
    {
        // Idle 비디오 미리 재생 시작 + 투명 상태로 표시
        if (idleVideoPlayer != null && idleVideoPlayer.clip != null)
        {
            idleImage.gameObject.SetActive(true);
            SetImageAlpha(idleImage, 0f);
            idleVideoPlayer.isLooping = true;
            idleVideoPlayer.time = 0;
            idleVideoPlayer.Play();
        }

        SetImageAlpha(endImage, 1f);

        float duration = Mathf.Max(0.01f, resetFadeDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetImageAlpha(idleImage, t);
            SetImageAlpha(endImage, 1f - t);
            yield return null;
        }

        // 페이드 종료: End 비디오 정리, 양쪽 알파 초기화
        StopEndVideo();
        SetImageAlpha(endImage, 1f);
        SetImageAlpha(idleImage, 1f);

        resetFadeCoroutine = null;
    }

    private void SetImageAlpha(UnityEngine.UI.RawImage image, float alpha)
    {
        if (image == null) return;
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}

