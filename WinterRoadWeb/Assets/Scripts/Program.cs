using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HybridWebSocket;
using System.Text;
using UnityEngine.UI;
using TMPro;

public class Program : MonoBehaviour
{
    [Header("WebSocket Settings")]
    public string serverUrl = "ws://59.16.72.37:3000";
    public string playerName = "Player";
    private const string DEFAULT_ROOM_ID = "main_room"; // 고정된 룸 ID

    private IWebSocket webSocket;
    private bool isConnected = false;
    private bool isInGame = false;
    private int remainingTime = 0;

    [Header("UI Elements")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI redScoreText;
    public TextMeshProUGUI blueScoreText;
    public TextMeshProUGUI playerInfoText;
    public Button pressButton;
    public Button joinButton;
    
    [Header("Team Selection")]
    public Button redTeamButton;
    public Button blueTeamButton;
    public TextMeshProUGUI selectedTeamText;
    private string selectedTeam = "";

    [Header("UI Test")]
    public string testMessage = "Hello Server!";

    // Start is called before the first frame update
    void Start()
    {
        InitializeUI();
        InitializeWebSocket();

        // Attempt to auto-connect after 3 seconds (for testing)
        Invoke("AutoConnect", 3f);
        
        // Test ping after 5 seconds to verify connection
        Invoke("TestPing", 5f);
    }

    void InitializeUI()
    {
        if (statusText) statusText.text = "Waiting for connection...";
        if (timerText) timerText.text = "--:--";
        if (redScoreText) redScoreText.text = "0";
        if (blueScoreText) blueScoreText.text = "0";
        if (playerInfoText) playerInfoText.text = "Not connected";
        if (selectedTeamText) selectedTeamText.text = "Select your team";
        
        if (pressButton) 
        {
            pressButton.onClick.AddListener(OnPressButton);
            //pressButton.interactable = false;
        }
        
        if (joinButton) 
        {
            joinButton.onClick.AddListener(OnJoinButton);
            joinButton.GetComponentInChildren<TextMeshProUGUI>().text = "Join Game";
        }
        
        // Team selection buttons
        if (redTeamButton)
        {
            redTeamButton.onClick.AddListener(OnRedTeamSelected);
        }
        
        if (blueTeamButton)
        {
            blueTeamButton.onClick.AddListener(OnBlueTeamSelected);
        }
    }

    void AutoConnect()
    {
        if (!isConnected)
        {
            Debug.Log("🔄 Attempting to auto-connect...");
            ConnectToServer();
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Simple keyboard tests
        if (Input.GetKeyDown(KeyCode.C) && !isConnected)
        {
            ConnectToServer();
        }

        if (Input.GetKeyDown(KeyCode.D) && isConnected)
        {
            DisconnectFromServer();
        }

        if (Input.GetKeyDown(KeyCode.S) && isConnected)
        {
            SendTestMessage();
        }

        // Game controls
        if (Input.GetKeyDown(KeyCode.J) && isConnected)
        {
            JoinRoom(DEFAULT_ROOM_ID);
        }

        if (Input.GetKeyDown(KeyCode.Space) && isInGame)
        {
            SendPress();
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
        if (webSocket != null && isConnected)
        {
            // Include team selection in join message if team is selected
            string teamInfo = "";
            if (!string.IsNullOrEmpty(selectedTeam))
            {
                teamInfo = $",\"team\":\"{selectedTeam}\"";
            }
            
            string message = $"{{\"type\":\"joinRoom\",\"roomId\":\"{roomId}\",\"playerName\":\"{playerName}\"{teamInfo}}}";
            SendMessage(message);
            Debug.Log($"Joining room: {roomId} with team: {selectedTeam}");
        }
    }

    public void SendPress()
    {
        Debug.Log($"🔍 SendPress called - WebSocket: {webSocket != null}, Connected: {isConnected}, InGame: {isInGame}");
        
        if (webSocket != null && isConnected)
        {
            string message = "{\"type\":\"press\"}";
            SendMessage(message);
            Debug.Log("✅ Press event sent successfully");
        }
        else
        {
            Debug.LogWarning("❌ Cannot send press - WebSocket not ready or not connected");
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

    // UI Button Handlers
    public void OnJoinButton()
    {
        JoinRoom(DEFAULT_ROOM_ID);
    }

    public void OnPressButton()
    {
        Debug.Log("🎯 Press Button clicked!");
        SendPress();
    }

    // Team Selection Button Handlers
    public void OnRedTeamSelected()
    {
        selectedTeam = "Red";
        UpdateTeamSelectionUI();
        Debug.Log("🔴 Red team selected!");
    }

    public void OnBlueTeamSelected()
    {
        selectedTeam = "Blue";
        UpdateTeamSelectionUI();
        Debug.Log("🔵 Blue team selected!");
    }

    private void UpdateTeamSelectionUI()
    {
        if (selectedTeamText)
        {
            if (selectedTeam == "Red")
            {
                selectedTeamText.text = "🔴 Red Team Selected";
                selectedTeamText.color = Color.red;
            }
            else if (selectedTeam == "Blue")
            {
                selectedTeamText.text = "🔵 Blue Team Selected";
                selectedTeamText.color = Color.blue;
            }
        }
        
        // Update button states
        if (redTeamButton)
        {
            redTeamButton.interactable = (selectedTeam != "Red");
        }
        
        if (blueTeamButton)
        {
            blueTeamButton.interactable = (selectedTeam != "Blue");
        }
    }

    // WebSocket event handlers
    private void OnWebSocketOpen()
    {
        Debug.Log("🎉 OnWebSocketOpen called!");
        isConnected = true;
        Debug.Log("✅ Successfully connected to server!");
        UpdateStatus("Connected to server");
        
        // Auto-join the main room
        JoinRoom(DEFAULT_ROOM_ID);
        
        // Request room info after joining
        Invoke("RequestRoomInfo", 1f);
    }

    private void OnWebSocketMessage(byte[] data)
    {
        Debug.Log($"📨 OnWebSocketMessage called with {data?.Length ?? 0} bytes");
        
        if (data == null || data.Length == 0)
        {
            Debug.LogWarning("⚠️ Received null or empty data in OnWebSocketMessage");
            return;
        }
        
        try
        {
            string message = Encoding.UTF8.GetString(data);
            Debug.Log($"📨 Message received from server: {message}");
            Debug.Log($"📨 Message length: {data.Length} bytes");

            // Process the received message here
            ProcessReceivedMessage(message);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error in OnWebSocketMessage: {e.Message}");
            Debug.LogError($"❌ Data length: {data?.Length ?? 0}");
        }
    }

    private void OnWebSocketError(string error)
    {
        Debug.LogError($"❌ OnWebSocketError called: {error}");
        UpdateStatus("Connection error");
    }

    private void OnWebSocketClose(WebSocketCloseCode closeCode)
    {
        Debug.Log($"🔌 OnWebSocketClose called with code: {closeCode}");
        isConnected = false;
        isInGame = false;
        Debug.Log($"🔌 Server connection closed. Close code: {closeCode}");
        UpdateStatus("Disconnected");
        
      //  if (pressButton) pressButton.interactable = false;
    }

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
            // Handle different message types
            if (message.Contains("\"type\":\"joinedRoom\""))
            {
                Debug.Log("🎯 Processing joinedRoom message");
                HandleJoinedRoom(message);
            }
            else if (message.Contains("\"type\":\"gameStart\""))
            {
                Debug.Log("🚀 Processing gameStart message");
                HandleGameStart(message);
            }
            else if (message.Contains("\"type\":\"timeUpdate\""))
            {
                Debug.Log("⏰ Processing timeUpdate message");
                HandleTimeUpdate(message);
            }
            else if (message.Contains("\"type\":\"scoreUpdate\""))
            {
                Debug.Log("📊 Processing scoreUpdate message");
                HandleScoreUpdate(message);
            }
            else if (message.Contains("\"type\":\"gameEnd\""))
            {
                Debug.Log("🏁 Processing gameEnd message");
                HandleGameEnd(message);
            }
            else if (message.Contains("\"type\":\"error\""))
            {
                Debug.Log("❌ Processing error message");
                HandleError(message);
            }
            else if (message.Contains("\"type\":\"roomInfo\""))
            {
                Debug.Log("📋 Processing roomInfo message");
                HandleRoomInfo(message);
            }
            else
            {
                Debug.LogWarning($"⚠️ Unknown message type: {message}");
            }

            // Ping-pong for testing
            if (message.Contains("ping"))
            {
                SendMessage("pong");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing message: {e.Message}");
        }
    }

    private void HandleJoinedRoom(string message)
    {
        Debug.Log("🎯 Joined room successfully!");
        UpdateStatus("Joined room");
        UpdatePlayerInfo();
    }

    private void HandleGameStart(string message)
    {
        Debug.Log("🚀 Game started!");
        Debug.Log($"🚀 Game start message: {message}");
        isInGame = true;
        UpdateStatus("Game in progress");
        
        if (pressButton) 
        {
            pressButton.interactable = true;
            Debug.Log("✅ Press button activated!");
        }
        else
        {
            Debug.LogWarning("⚠️ Press button not found!");
        }
        
        // Start local timer
        StartCoroutine(GameTimer());
    }

    private void HandleTimeUpdate(string message)
    {
        // Extract remaining time from message
        int timeIndex = message.IndexOf("\"remainingTime\":") + 16;
        int endIndex = message.IndexOf(",", timeIndex);
        if (endIndex == -1) endIndex = message.IndexOf("}", timeIndex);
        
        if (timeIndex > 15 && endIndex > timeIndex)
        {
            string timeStr = message.Substring(timeIndex, endIndex - timeIndex);
            if (int.TryParse(timeStr, out remainingTime))
            {
                UpdateTimer();
            }
        }
    }

    private void HandleScoreUpdate(string message)
    {
        // Extract scores from message (simple parsing)
        if (message.Contains("\"Red\""))
        {
            int redScore = ExtractScore(message, "Red");
            int blueScore = ExtractScore(message, "Blue");
            
            if (redScoreText) redScoreText.text = redScore.ToString();
            if (blueScoreText) blueScoreText.text = blueScore.ToString();
        }
    }

    private void HandleGameEnd(string message)
    {
        Debug.Log("🏁 Game ended!");
        isInGame = false;
        UpdateStatus("Game ended");
        
       // if (pressButton) pressButton.interactable = false;
        
        // Extract winner information
        if (message.Contains("\"winner\""))
        {
            string winner = ExtractWinner(message);
            string redScore = ExtractFinalScore(message, "Red");
            string blueScore = ExtractFinalScore(message, "Blue");
            
            if (statusText)
            {
                if (winner == "Draw")
                {
                    statusText.text = $"🏆 Draw! ({redScore} - {blueScore})";
                }
                else
                {
                    statusText.text = $"🏆 {winner} Team Wins! ({redScore} - {blueScore})";
                }
            }
        }
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

    private void HandleError(string message)
    {
        Debug.LogError($"Server error: {message}");
        UpdateStatus("Server error");
    }

    private void HandleRoomInfo(string message)
    {
        Debug.Log($"📋 Room info received: {message}");
        
        // Check if game is already in progress
        if (message.Contains("\"status\":\"playing\""))
        {
            Debug.Log("🎮 Game is already in progress!");
            isInGame = true;
            UpdateStatus("Game in progress");
            
            if (pressButton) 
            {
                pressButton.interactable = true;
                Debug.Log("✅ Press button activated from room info!");
            }
            
            // Extract remaining time
            int timeIndex = message.IndexOf("\"remainingTime\":") + 16;
            int endIndex = message.IndexOf(",", timeIndex);
            if (endIndex == -1) endIndex = message.IndexOf("}", timeIndex);
            
            if (timeIndex > 15 && endIndex > timeIndex)
            {
                string timeStr = message.Substring(timeIndex, endIndex - timeIndex);
                if (int.TryParse(timeStr, out remainingTime))
                {
                    Debug.Log($"⏰ Remaining time from room info: {remainingTime}");
                    UpdateTimer();
                    StartCoroutine(GameTimer());
                }
            }
        }
        else
        {
            Debug.Log("⏳ Game is waiting to start");
            isInGame = false;
            UpdateStatus("Waiting for game to start");
            
            if (pressButton) pressButton.interactable = false;
        }
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
    }

    private void UpdateStatus()
    {
        if (statusText)
        {
            if (!isConnected)
                statusText.text = "Disconnected";
            else if (!isInGame)
                statusText.text = "Connected - Waiting for game";
            else
                statusText.text = "Game in progress";
        }
    }

    private void UpdateStatus(string status)
    {
        if (statusText) statusText.text = status;
    }

    private void UpdatePlayerInfo()
    {
        if (playerInfoText)
        {
            string teamInfo = "";
            if (!string.IsNullOrEmpty(selectedTeam))
            {
                teamInfo = $" ({selectedTeam} Team)";
            }
            playerInfoText.text = $"Player: {playerName}{teamInfo}";
        }
    }

    private void UpdateTimer()
    {
        if (timerText)
        {
            int minutes = remainingTime / 60;
            int seconds = remainingTime % 60;
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
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

    [ContextMenu("Select Red Team")]
    void ContextSelectRedTeam()
    {
        OnRedTeamSelected();
    }

    [ContextMenu("Select Blue Team")]
    void ContextSelectBlueTeam()
    {
        OnBlueTeamSelected();
    }

    void OnDestroy()
    {
        // Clean up WebSocket on object destruction
        if (webSocket != null && isConnected)
        {
            webSocket.Close();
        }
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
}