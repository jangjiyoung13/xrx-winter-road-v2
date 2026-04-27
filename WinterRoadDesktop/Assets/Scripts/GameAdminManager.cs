using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using WebSocketSharp;

[System.Serializable]
public class TeamInfo
{
    public int score;
    public int playerCount;
}

[System.Serializable]
public class GameData
{
    public TeamInfo Red;
    public TeamInfo Blue;
}

[System.Serializable]
public class RoomInfo
{
    public string id;
    public string status;
    public GameData teams;
    public int remainingTime;
    public string qrCodeUrl;
}

[System.Serializable]
public class AdminMessage
{
    public string type;
    public AdminMessageData data;
}

[System.Serializable]
public class AdminMessageData
{
    public string playerId;
    public string playerName;
    public string team;
    public RoomInfo room;
    public GameData teams;
    public int remainingTime;
    public int duration;
    public string winner;
    public GameData finalScores;
    public string message;
}

public class GameAdminManager : MonoBehaviour
{
    [Header("서버 설정")]
    [SerializeField] private string serverUrl = "ws://127.0.0.1:8080";
    [SerializeField] private string roomId = "main_room";

    [Header("UI 컴포넌트")]
    [SerializeField] public TMP_Text roomStatusText;
    [SerializeField] public TMP_Text redTeamCountText;
    [SerializeField] public TMP_Text blueTeamCountText;
    [SerializeField] public TMP_Text redTeamScoreText;
    [SerializeField] public TMP_Text blueTeamScoreText;
    [SerializeField] public TMP_Text gameTimerText;
    [SerializeField] public TMP_Text winnerText;
    [SerializeField] public Button startGameButton;
    [SerializeField] public Button resetGameButton;
    [SerializeField] public TMP_Text connectionStatusText;
    [SerializeField] public TMP_Text logText;

    [Header("게임 상태 패널")]
    [SerializeField] public GameObject waitingPanel;
    [SerializeField] public GameObject playingPanel;
    [SerializeField] public GameObject finishedPanel;

    private WebSocket ws;
    private readonly Queue<Action> mainThreadActions = new Queue<Action>();
    private RoomInfo currentRoom;
    private bool isConnected = false;

    void Start()
    {
       InitializeUI();
       ConnectToServer();
    }

    void Update()
    {
       // 메인 스레드에서 UI 업데이트 처리
       lock (mainThreadActions)
       {
           while (mainThreadActions.Count > 0)
           {
               var action = mainThreadActions.Dequeue();
               action?.Invoke();
           }
       }
    }

    private void InitializeUI()
    {
       if (startGameButton != null)
           startGameButton.onClick.AddListener(StartGame);

       if (resetGameButton != null)
           resetGameButton.onClick.AddListener(ResetGame);

       UpdateConnectionStatus("연결 시도 중...");
       ShowWaitingUI();
    }

    private void ConnectToServer()
    {
       try
       {
                       ws = new WebSocket(serverUrl);

           ws.OnOpen += (sender, e) =>
           {
               EnqueueMainThreadAction(() =>
               {
                   isConnected = true;
                   UpdateConnectionStatus("서버에 연결됨");
                   LogMessage("서버에 연결되었습니다.");
                    
                   // 관리자로 방에 입장
                   JoinRoomAsAdmin();
               });
           };

           ws.OnMessage += (sender, e) =>
           {
               EnqueueMainThreadAction(() =>
               {
                   HandleServerMessage(e.Data);
               });
           };

           ws.OnClose += (sender, e) =>
           {
               EnqueueMainThreadAction(() =>
               {
                   isConnected = false;
                   UpdateConnectionStatus("서버 연결 끊어짐");
                   LogMessage($"서버 연결이 끊어졌습니다. 코드: {e.Code}");
               });
           };

           ws.OnError += (sender, e) =>
           {
               EnqueueMainThreadAction(() =>
               {
                   UpdateConnectionStatus("연결 오류");
                   LogMessage($"연결 오류: {e.Message}");
               });
           };

           ws.Connect();
       }
       catch (Exception ex)
       {
           UpdateConnectionStatus("연결 실패");
           LogMessage($"연결 실패: {ex.Message}");
       }
    }

    private void JoinRoomAsAdmin()
    {
       var joinMessage = new
       {
           type = "joinRoom",
           roomId = roomId,
           playerName = "Admin"
       };

       SendMessage(joinMessage);
    }

    private void HandleServerMessage(string messageJson)
    {
       try
       {
           var message = JsonUtility.FromJson<AdminMessage>(messageJson);
           LogMessage($"메시지 수신: {message.type}");

           switch (message.type)
           {
               case "joinedRoom":
                   HandleJoinedRoom(message.data);
                   break;
               case "gameStart":
                   HandleGameStart(message.data);
                   break;
               case "scoreUpdate":
                   HandleScoreUpdate(message.data);
                   break;
               case "timeUpdate":
                   HandleTimeUpdate(message.data);
                   break;
               case "gameEnd":
                   HandleGameEnd(message.data);
                   break;
               case "playerJoined":
               case "playerLeft":
                   HandlePlayerUpdate(message.data);
                   break;
               case "gameReset":
                   HandleGameReset(message.data);
                   break;
               case "error":
                   LogMessage($"서버 오류: {message.data.message}");
                   break;
           }
       }
       catch (Exception ex)
       {
           LogMessage($"메시지 파싱 오류: {ex.Message}");
       }
    }

    private void HandleJoinedRoom(AdminMessageData data)
    {
       currentRoom = data.room;
       UpdateRoomUI();
       LogMessage($"방 '{roomId}'에 관리자로 입장했습니다.");
    }

    private void HandleGameStart(AdminMessageData data)
    {
       if (currentRoom != null)
       {
           currentRoom.status = "playing";
           currentRoom.teams = data.teams;
       }
       UpdateRoomUI();
       ShowPlayingUI();
       LogMessage("게임이 시작되었습니다!");
    }

    private void HandleScoreUpdate(AdminMessageData data)
    {
       if (currentRoom != null)
       {
           currentRoom.teams = data.teams;
       }
       UpdateTeamScores();
    }

    private void HandleTimeUpdate(AdminMessageData data)
    {
       if (currentRoom != null)
       {
           currentRoom.remainingTime = data.remainingTime;
       }
       UpdateTimer();
    }

    private void HandleGameEnd(AdminMessageData data)
    {
       if (currentRoom != null)
       {
           currentRoom.status = "finished";
           currentRoom.teams = data.teams;
       }
       UpdateRoomUI();
       ShowFinishedUI(data.winner, data.finalScores);
       LogMessage($"게임 종료! 승자: {data.winner}");
    }

    private void HandlePlayerUpdate(AdminMessageData data)
    {
       if (currentRoom != null)
       {
           currentRoom.teams = data.teams;
       }
       UpdateTeamCounts();
       LogMessage($"플레이어 업데이트: {data.playerName} ({data.team})");
    }

    private void HandleGameReset(AdminMessageData data)
    {
       if (currentRoom != null)
       {
           currentRoom.status = "waiting";
           currentRoom.teams = data.teams;
           currentRoom.remainingTime = 300; // 기본 5분
       }
       UpdateRoomUI();
       ShowWaitingUI();
       LogMessage("게임이 리셋되었습니다.");
    }

    private void UpdateRoomUI()
    {
       if (currentRoom == null) return;

       UpdateConnectionStatus($"방: {currentRoom.id} | 상태: {GetStatusText(currentRoom.status)}");
       UpdateTeamCounts();
       UpdateTeamScores();
       UpdateTimer();
    }

    private void UpdateTeamCounts()
    {
       if (currentRoom?.teams == null) return;

       if (redTeamCountText != null)
           redTeamCountText.text = $"레드팀: {currentRoom.teams.Red.playerCount}명";

       if (blueTeamCountText != null)
           blueTeamCountText.text = $"블루팀: {currentRoom.teams.Blue.playerCount}명";
    }

    private void UpdateTeamScores()
    {
       if (currentRoom?.teams == null) return;

       if (redTeamScoreText != null)
           redTeamScoreText.text = $"점수: {currentRoom.teams.Red.score}";

       if (blueTeamScoreText != null)
           blueTeamScoreText.text = $"점수: {currentRoom.teams.Blue.score}";
    }

    private void UpdateTimer()
    {
       if (currentRoom == null) return;

       if (gameTimerText != null)
       {
           int minutes = currentRoom.remainingTime / 60;
           int seconds = currentRoom.remainingTime % 60;
           gameTimerText.text = $"{minutes:00}:{seconds:00}";
       }
    }

    private void ShowWaitingUI()
    {
       SetPanelVisibility(waitingPanel, true);
       SetPanelVisibility(playingPanel, false);
       SetPanelVisibility(finishedPanel, false);

       if (startGameButton != null)
           startGameButton.interactable = isConnected;
       if (resetGameButton != null)
           resetGameButton.interactable = false;
    }

    private void ShowPlayingUI()
    {
       SetPanelVisibility(waitingPanel, false);
       SetPanelVisibility(playingPanel, true);
       SetPanelVisibility(finishedPanel, false);

       if (startGameButton != null)
           startGameButton.interactable = false;
       if (resetGameButton != null)
           resetGameButton.interactable = isConnected;
    }

    private void ShowFinishedUI(string winner, GameData finalScores)
    {
       SetPanelVisibility(waitingPanel, false);
       SetPanelVisibility(playingPanel, false);
       SetPanelVisibility(finishedPanel, true);

       if (winnerText != null)
       {
           string resultText = winner == "Draw" ? "무승부!" : $"{winner} 팀 승리!";
           if (finalScores != null)
           {
               resultText += $"\n최종 점수 - 레드: {finalScores.Red.score}, 블루: {finalScores.Blue.score}";
           }
           winnerText.text = resultText;
       }

       if (startGameButton != null)
           startGameButton.interactable = false;
       if (resetGameButton != null)
           resetGameButton.interactable = isConnected;
    }

    private void SetPanelVisibility(GameObject panel, bool visible)
    {
       if (panel != null)
           panel.SetActive(visible);
    }

    private string GetStatusText(string status)
    {
       switch (status)
       {
           case "waiting": return "대기 중";
           case "playing": return "게임 중";
           case "finished": return "게임 종료";
           default: return status;
       }
    }

    public void StartGame()
    {
       if (!isConnected)
       {
           LogMessage("서버에 연결되지 않았습니다.");
           return;
       }

       var startMessage = new { type = "startGame" };
       SendMessage(startMessage);
       LogMessage("게임 시작 요청을 보냈습니다.");
    }

    public void ResetGame()
    {
       if (!isConnected)
       {
           LogMessage("서버에 연결되지 않았습니다.");
           return;
       }

       var resetMessage = new { type = "resetGame" };
       SendMessage(resetMessage);
       LogMessage("게임 리셋 요청을 보냈습니다.");
    }

    private void SendMessage(object message)
    {
       if (ws == null || !isConnected) return;

       try
       {
           string json = JsonUtility.ToJson(message);
           ws.Send(json);
       }
       catch (Exception ex)
       {
           LogMessage($"메시지 전송 오류: {ex.Message}");
       }
    }

    private void UpdateConnectionStatus(string status)
    {
       if (connectionStatusText != null)
           connectionStatusText.text = status;
    }

    private void LogMessage(string message)
    {
       string timeStamp = DateTime.Now.ToString("HH:mm:ss");
       string logEntry = $"[{timeStamp}] {message}";
        
       Debug.Log($"[GameAdmin] {logEntry}");
        
       if (logText != null)
       {
           logText.text += (logText.text.Length > 0 ? "\n" : "") + logEntry;
            
           // 로그가 너무 길어지면 처음 부분 삭제
           string[] lines = logText.text.Split('\n');
           if (lines.Length > 50)
           {
               string[] lastLines = new string[30];
               Array.Copy(lines, lines.Length - 30, lastLines, 0, 30);
               logText.text = string.Join("\n", lastLines);
           }
       }
    }

    private void EnqueueMainThreadAction(Action action)
    {
       lock (mainThreadActions)
       {
           mainThreadActions.Enqueue(action);
       }
    }

    void OnDestroy()
    {
       if (ws != null)
       {
           try
           {
               ws.Close();
           }
           catch { }
           ws = null;
       }
    }

    // 재연결 기능
    public void Reconnect()
    {
       if (ws != null)
       {
           try { ws.Close(); } catch { }
           ws = null;
       }
        
       LogMessage("서버 재연결 시도 중...");
       ConnectToServer();
    }
}
