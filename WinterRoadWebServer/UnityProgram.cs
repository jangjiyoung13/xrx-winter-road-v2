using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HybridWebSocket;
using System.Text;
using UnityEngine.UI;

public class UnityProgram : MonoBehaviour
{
    [Header("WebSocket Settings")]
    public string serverUrl = "ws://localhost:3000";
    public string roomId = ""; // 룸 ID를 Inspector에서 설정하거나 코드에서 설정

    [Header("UI References")]
    public Text statusText;
    public Text timerText;
    public Text redScoreText;
    public Text blueScoreText;
    public Text playerInfoText;
    public Button pressButton;
    public Button joinButton;
    public InputField roomIdInput;

    [Header("Game Settings")]
    public string playerName = "UnityPlayer";

    private IWebSocket webSocket;
    private bool isConnected = false;
    private bool isInGame = false;
    private float remainingTime = 0f;
    private string currentTeam = "";
    private string currentPlayerId = "";

    // 게임 상태
    private int redScore = 0;
    private int blueScore = 0;
    private int redPlayers = 0;
    private int bluePlayers = 0;

    void Start()
    {
        InitializeUI();
        InitializeWebSocket();
    }

    void InitializeUI()
    {
        if (statusText) statusText.text = "Waiting for connection...";
        if (timerText) timerText.text = "--:--";
        if (redScoreText) redScoreText.text = "0";
        if (blueScoreText) blueScoreText.text = "0";
        if (playerInfoText) playerInfoText.text = "Not connected";
        
        if (pressButton) 
        {
            pressButton.onClick.AddListener(OnPressButton);
            pressButton.interactable = false;
        }
        
        if (joinButton) joinButton.onClick.AddListener(OnJoinButton);
    }

    void InitializeWebSocket()
    {
        try
        {
            Debug.Log($"🔄 WebSocket 초기화. URL: {serverUrl}");

            // URL 유효성 검사
            if (!IsValidWebSocketUrl(serverUrl))
            {
                            Debug.LogError($"❌ Invalid URL format: {serverUrl}");
            UpdateStatus("Invalid server URL");
                return;
            }

            Debug.Log($"✅ URL validation passed.");

            // WebSocket 인스턴스 생성
            webSocket = WebSocketFactory.CreateInstance(serverUrl);
            Debug.Log("✅ WebSocket instance created.");

            // 이벤트 핸들러 설정
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
        // 연결 성공 시
        webSocket.OnOpen += OnWebSocketOpen;

        // 메시지 수신 시
        webSocket.OnMessage += OnWebSocketMessage;

        // 오류 발생 시
        webSocket.OnError += OnWebSocketError;

        // 연결 종료 시
        webSocket.OnClose += OnWebSocketClose;
    }

    public void ConnectToServer()
    {
        if (webSocket != null && !isConnected)
        {
            try
            {
                webSocket.Connect();
                Debug.Log("Attempting to connect to server...");
                UpdateStatus("Connecting...");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Connection failed: {e.Message}");
                UpdateStatus("Connection failed");
            }
        }
    }

    public void JoinRoom(string roomId)
    {
        if (webSocket != null && isConnected && !string.IsNullOrEmpty(roomId))
        {
            try
            {
                var message = new
                {
                    type = "joinRoom",
                    roomId = roomId,
                    playerName = playerName
                };

                string jsonMessage = JsonUtility.ToJson(message);
                byte[] data = Encoding.UTF8.GetBytes(jsonMessage);
                webSocket.Send(data);
                
                Debug.Log($"Attempting to join room: {roomId}");
                UpdateStatus($"Joining room {roomId}...");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to join room: {e.Message}");
                UpdateStatus("Failed to join room");
            }
        }
    }

    public void SendPress()
    {
        if (webSocket != null && isConnected && isInGame)
        {
            try
            {
                var message = new
                {
                    type = "press"
                };

                string jsonMessage = JsonUtility.ToJson(message);
                byte[] data = Encoding.UTF8.GetBytes(jsonMessage);
                webSocket.Send(data);
                
                Debug.Log("Sending press event");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to send press: {e.Message}");
            }
        }
    }

    // UI 버튼 이벤트 핸들러
    public void OnJoinButton()
    {
        if (roomIdInput && !string.IsNullOrEmpty(roomIdInput.text))
        {
            JoinRoom(roomIdInput.text);
        }
        else if (!string.IsNullOrEmpty(roomId))
        {
            JoinRoom(roomId);
        }
        else
        {
            Debug.LogWarning("Please enter a room ID.");
            UpdateStatus("Room ID required");
        }
    }

    public void OnPressButton()
    {
        SendPress();
    }

    // WebSocket 이벤트 핸들러
    private void OnWebSocketOpen()
    {
        isConnected = true;
        Debug.Log("✅ Successfully connected to server!");
        UpdateStatus("Connected to server");
        
        // 자동으로 룸에 참가 (룸 ID가 설정된 경우)
        if (!string.IsNullOrEmpty(roomId))
        {
            JoinRoom(roomId);
        }
    }

    private void OnWebSocketMessage(byte[] data)
    {
        string message = Encoding.UTF8.GetString(data);
        Debug.Log($"📨 Message received from server: {message}");

        try
        {
            // JSON 파싱 (간단한 구조체 사용)
            ProcessReceivedMessage(message);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Message processing error: {e.Message}");
        }
    }

    private void OnWebSocketError(string error)
    {
        Debug.LogError($"❌ WebSocket error: {error}");
        UpdateStatus($"Error: {error}");
    }

    private void OnWebSocketClose(WebSocketCloseCode closeCode)
    {
        isConnected = false;
        isInGame = false;
        Debug.Log($"🔌 Server connection closed. Close code: {closeCode}");
        UpdateStatus("Connection closed");
        
        if (pressButton) pressButton.interactable = false;
    }

    private void ProcessReceivedMessage(string message)
    {
        // 간단한 JSON 파싱 (실제로는 JsonUtility나 Newtonsoft.Json 사용 권장)
        if (message.Contains("\"type\":\"joinedRoom\""))
        {
            HandleJoinedRoom(message);
        }
        else if (message.Contains("\"type\":\"gameStart\""))
        {
            HandleGameStart(message);
        }
        else if (message.Contains("\"type\":\"timeUpdate\""))
        {
            HandleTimeUpdate(message);
        }
        else if (message.Contains("\"type\":\"scoreUpdate\""))
        {
            HandleScoreUpdate(message);
        }
        else if (message.Contains("\"type\":\"gameEnd\""))
        {
            HandleGameEnd(message);
        }
        else if (message.Contains("\"type\":\"error\""))
        {
            HandleError(message);
        }
    }

    private void HandleJoinedRoom(string message)
    {
        // 간단한 파싱 (실제로는 JsonUtility 사용 권장)
        if (message.Contains("\"playerId\""))
        {
            int startIndex = message.IndexOf("\"playerId\":\"") + 12;
            int endIndex = message.IndexOf("\"", startIndex);
            currentPlayerId = message.Substring(startIndex, endIndex - startIndex);
        }

        if (message.Contains("\"team\""))
        {
            int startIndex = message.IndexOf("\"team\":\"") + 8;
            int endIndex = message.IndexOf("\"", startIndex);
            currentTeam = message.Substring(startIndex, endIndex - startIndex);
        }

        Debug.Log($"🎯 Successfully joined room! Player ID: {currentPlayerId}, Team: {currentTeam}");
        UpdateStatus($"Joined room ({currentTeam} team)");
        UpdatePlayerInfo($"{playerName} ({currentTeam} team)");
    }

    private void HandleGameStart(string message)
    {
        isInGame = true;
        Debug.Log("🚀 Game started!");
        UpdateStatus("Game in progress!");
        
        if (pressButton) pressButton.interactable = true;
        
        // 타이머 시작 (60초)
        remainingTime = 60f;
        StartCoroutine(GameTimer());
    }

    private void HandleTimeUpdate(string message)
    {
        if (message.Contains("\"remainingTime\""))
        {
            int startIndex = message.IndexOf("\"remainingTime\":") + 16;
            int endIndex = message.IndexOf(",", startIndex);
            if (endIndex == -1) endIndex = message.IndexOf("}", startIndex);
            
            string timeStr = message.Substring(startIndex, endIndex - startIndex);
            if (float.TryParse(timeStr, out float time))
            {
                remainingTime = time;
                UpdateTimer(time);
            }
        }
    }

    private void HandleScoreUpdate(string message)
    {
        // Red 팀 점수 파싱
        if (message.Contains("\"Red\""))
        {
            int redStart = message.IndexOf("\"Red\"");
            int scoreStart = message.IndexOf("\"score\":", redStart) + 8;
            int scoreEnd = message.IndexOf(",", scoreStart);
            if (scoreEnd == -1) scoreEnd = message.IndexOf("}", scoreStart);
            
            if (int.TryParse(message.Substring(scoreStart, scoreEnd - scoreStart), out int red))
            {
                redScore = red;
                if (redScoreText) redScoreText.text = redScore.ToString();
            }
        }

        // Blue 팀 점수 파싱
        if (message.Contains("\"Blue\""))
        {
            int blueStart = message.IndexOf("\"Blue\"");
            int scoreStart = message.IndexOf("\"score\":", blueStart) + 8;
            int scoreEnd = message.IndexOf(",", scoreStart);
            if (scoreEnd == -1) scoreEnd = message.IndexOf("}", scoreStart);
            
            if (int.TryParse(message.Substring(scoreStart, scoreEnd - scoreStart), out int blue))
            {
                blueScore = blue;
                if (blueScoreText) blueScoreText.text = blueScore.ToString();
            }
        }

        Debug.Log($"📊 Score update - Red: {redScore}, Blue: {blueScore}");
    }

    private void HandleGameEnd(string message)
    {
        isInGame = false;
        string winner = "Draw";
        
        if (message.Contains("\"winner\""))
        {
            int startIndex = message.IndexOf("\"winner\":\"") + 10;
            int endIndex = message.IndexOf("\"", startIndex);
            winner = message.Substring(startIndex, endIndex - startIndex);
        }

        Debug.Log($"🏁 Game ended! Winner: {winner}");
        UpdateStatus($"Game ended! Winner: {winner}");
        
        if (pressButton) pressButton.interactable = false;
    }

    private void HandleError(string message)
    {
        string errorMsg = "알 수 없는 오류";
        if (message.Contains("\"message\""))
        {
            int startIndex = message.IndexOf("\"message\":\"") + 11;
            int endIndex = message.IndexOf("\"", startIndex);
            errorMsg = message.Substring(startIndex, endIndex - startIndex);
        }
        
        Debug.LogError($"Server error: {errorMsg}");
        UpdateStatus($"Error: {errorMsg}");
    }

    private IEnumerator GameTimer()
    {
        while (remainingTime > 0 && isInGame)
        {
            UpdateTimer(remainingTime);
            yield return new WaitForSeconds(1f);
            remainingTime -= 1f;
        }
    }

    private void UpdateTimer(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60);
        int seconds = Mathf.FloorToInt(time % 60);
        
        if (timerText)
        {
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }

    private void UpdateStatus(string status)
    {
        Debug.Log($"Status update: {status}");
        if (statusText) statusText.text = status;
    }

    private void UpdatePlayerInfo(string info)
    {
        if (playerInfoText) playerInfoText.text = info;
    }

    void Update()
    {
        // 키보드 테스트
        if (Input.GetKeyDown(KeyCode.C) && !isConnected)
        {
            ConnectToServer();
        }

        if (Input.GetKeyDown(KeyCode.J) && isConnected && !string.IsNullOrEmpty(roomId))
        {
            JoinRoom(roomId);
        }

        if (Input.GetKeyDown(KeyCode.Space) && isInGame)
        {
            SendPress();
        }
    }

    void OnDestroy()
    {
        // 객체 파괴 시 WebSocket 정리
        if (webSocket != null && isConnected)
        {
            webSocket.Close();
        }
    }

    // Inspector에서 테스트용 메서드들
    [ContextMenu("Connect")]
    void ContextConnect()
    {
        ConnectToServer();
    }

    [ContextMenu("Join Room")]
    void ContextJoinRoom()
    {
        if (!string.IsNullOrEmpty(roomId))
        {
            JoinRoom(roomId);
        }
    }

    [ContextMenu("Send Press")]
    void ContextSendPress()
    {
        SendPress();
    }
}
