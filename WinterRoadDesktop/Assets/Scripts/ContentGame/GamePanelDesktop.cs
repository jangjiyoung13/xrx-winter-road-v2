using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

public class GamePanelDesktop : MonoBehaviour
{
    [Header("팀 영역")]
    [SerializeField] private Transform whiteTeamContainer;
    [SerializeField] private Transform blueTeamContainer;
    [SerializeField] private Transform observerContainer;
    
    [Header("팀 헤더")]
    [SerializeField] private TextMeshProUGUI whiteTeamHeader;
    [SerializeField] private TextMeshProUGUI blueTeamHeader;
    [SerializeField] private TextMeshProUGUI observerHeader;
    
    [Header("팀 점수")]
    [SerializeField] private TextMeshProUGUI whiteTeamScore;
    [SerializeField] private TextMeshProUGUI blueTeamScore;
    
    [Header("게임 상태")]
    [SerializeField] private TextMeshProUGUI gameStatusText;
    [SerializeField] private TextMeshProUGUI gameTimerText;
    [SerializeField] private TextMeshProUGUI totalPlayersText;
    
    [Header("프리팹")]
    [SerializeField] private PlayerInfoUI playerInfoPrefab;
    
    // 플레이어 정보 관리
    private Dictionary<string, PlayerInfoUI> playerInfoUIs = new Dictionary<string, PlayerInfoUI>();
    
    // 현재 게임 상태
    private string currentGameStatus = "waiting";
    private int remainingTime = 0;
    
    private void Start()
    {
        InitializeUI();
    }
    
    private void InitializeUI()
    {
        // 컨테이너 참조 확인
        Debug.Log($"🔍 컨테이너 참조 확인:");
        Debug.Log($"   - whiteTeamContainer (Red용): {whiteTeamContainer != null} - {whiteTeamContainer?.name}");
        Debug.Log($"   - blueTeamContainer: {blueTeamContainer != null} - {blueTeamContainer?.name}");
        Debug.Log($"   - observerContainer: {observerContainer != null} - {observerContainer?.name}");
        Debug.Log($"   - playerInfoPrefab: {playerInfoPrefab != null} - {playerInfoPrefab?.name}");
        
        // 헤더 텍스트 설정
        if (whiteTeamHeader != null)
            whiteTeamHeader.text = "🔴 Red Team";
        
        if (blueTeamHeader != null)
            blueTeamHeader.text = "🔵 Blue Team";
        
        if (observerHeader != null)
            observerHeader.text = " Observers";
        
        // 초기 점수 설정
        UpdateTeamScores(0, 0);
        
        // 초기 상태 설정
        UpdateGameStatus("waiting");
        UpdateTimer(0);
        UpdateTotalPlayersCount(0, 0);
        
        Debug.Log($"✅ GamePanelDesktop UI 초기화 완료");
    }
    
    public void UpdateGameState(JToken gameData)
    {
        try
        {
            // 게임 상태 업데이트
            var roomInfo = gameData["room"];
            if (roomInfo != null)
            {
                string status = roomInfo["status"]?.ToString() ?? "waiting";
                int time = roomInfo["remainingTime"]?.Value<int>() ?? 0;
                
                UpdateGameStatus(status);
                UpdateTimer(time);
            }
            
            // 개인전 - 팀 점수 파싱 제거
            // 플레이어 수는 playerCount로 직접 받음
            
            // 플레이어 정보 업데이트
            var players = gameData["players"];
            if (players != null)
            {
                UpdatePlayerList(players);
            }
            
            // 플레이어 수 업데이트
            var playerCount = gameData["playerCount"];
            if (playerCount != null)
            {
                int active = playerCount["active"]?.Value<int>() ?? 0;
                int observers = playerCount["observers"]?.Value<int>() ?? 0;
                UpdateTotalPlayersCount(active, observers);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($" GamePanel 업데이트 오류: {ex.Message}");
        }
    }
    
    private void UpdatePlayerList(JToken playersData)
    {
        Debug.Log($"🔄 UpdatePlayerList 호출됨 - 개인전 모드");
        
        // 개인전 - players 배열을 직접 처리
        if (playersData is JArray playerArray)
        {
            Debug.Log($"👤 플레이어 수: {playerArray.Count}");
            foreach (var player in playerArray)
            {
                CreatePlayerUI(player, whiteTeamContainer, "");
            }
        }
        else
        {
            // 호환성 유지 - 기존 팀별 구조가 올 경우
            var allPlayers = playersData["Red"] ?? playersData["players"];
            if (allPlayers != null)
            {
                foreach (var player in allPlayers)
                {
                    CreatePlayerUI(player, whiteTeamContainer, "");
                }
            }
            
            var observers = playersData["Observers"];
            if (observers != null && observerContainer != null)
            {
                foreach (var observer in observers)
                {
                    CreatePlayerUI(observer, observerContainer, "Observer");
                }
            }
        }
    }
    
    private void CreatePlayerUI(JToken playerData, Transform container, string team)
    {
        if (playerInfoPrefab == null || container == null) return;
        
        try
        {
            string playerId = playerData["id"]?.ToString() ?? "";
            string playerName = playerData["name"]?.ToString() ?? "Unknown";
            int score = playerData["score"]?.Value<int>() ?? 0;
            bool connected = playerData["connected"]?.Value<bool>() ?? true;
            
            // Admin/Observer 여부 확인
            bool isAdmin = playerName.Contains("Admin");
            bool isObserver = playerName.Contains("Observer") || playerName.Contains("Admin");
            
            Debug.Log($"🔧 CreatePlayerUI → AddOrUpdatePlayer: {playerName} ({team})");
            
            // 기존 AddOrUpdatePlayer 메서드 사용 (중복 생성 방지)
            AddOrUpdatePlayer(playerId, playerName, team, score, connected, isAdmin, isObserver);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ CreatePlayerUI 오류: {ex.Message}");
        }
    }
    
    private void ClearAllPlayerUIs()
    {
        // 기존 플레이어 UI 오브젝트 제거
        foreach (var playerUI in playerInfoUIs.Values)
        {
            if (playerUI != null && playerUI.gameObject != null)
            {
                Destroy(playerUI.gameObject);
            }
        }
        playerInfoUIs.Clear();
    }
    
    public void UpdateTeamScores(int redScore, int blueScore)
    {
        if (whiteTeamScore != null)
            whiteTeamScore.text = $"Score: {redScore}";
        
        if (blueTeamScore != null)
            blueTeamScore.text = $"Score: {blueScore}";
        
        Debug.Log($"📊 팀 점수 업데이트 - Red: {redScore}, Blue: {blueScore}");
    }
    
    public void UpdateGameStatus(string status)
    {
        currentGameStatus = status;
        
        if (gameStatusText != null)
        {
            switch (status)
            {
                case "waiting":
                    gameStatusText.text = "게임 대기 중";
                    gameStatusText.color = Color.yellow;
                    break;
                case "playing":
                    gameStatusText.text = "게임 진행 중";
                    gameStatusText.color = Color.green;
                    break;
                case "finished":
                    gameStatusText.text = "게임 종료";
                    gameStatusText.color = Color.red;
                    break;
                default:
                    gameStatusText.text = status;
                    gameStatusText.color = Color.white;
                    break;
            }
        }
        
        Debug.Log($"게임 상태 업데이트: {status}");
    }
    
    public void UpdateTimer(int timeInSeconds)
    {
        remainingTime = timeInSeconds;
        
        if (gameTimerText != null)
        {
            int minutes = timeInSeconds / 60;
            int seconds = timeInSeconds % 60;
            gameTimerText.text = $" {minutes:00}:{seconds:00}";
            
            // 시간에 따른 색상 변경
            if (timeInSeconds <= 10 && timeInSeconds > 0)
            {
                gameTimerText.color = Color.red;
            }
            else if (timeInSeconds <= 30)
            {
                gameTimerText.color = Color.yellow;
            }
            else
            {
                gameTimerText.color = Color.white;
            }
        }
    }
    
    public void UpdateTotalPlayersCount(int activePlayers, int observers)
    {
        if (totalPlayersText != null)
        {
            totalPlayersText.text = $" 플레이어: {activePlayers}명 |  관전자: {observers}명";
        }
        
        Debug.Log($"👥 플레이어 수 업데이트 - 활성: {activePlayers}, 관전자: {observers}");
    }
    
    public void UpdateTeamHeaders(int redCount, int blueCount)
    {
        // 팀 헤더에 플레이어 수 표시
        if (whiteTeamHeader != null)
        {
            whiteTeamHeader.text = $"🔴 Red Team ({redCount})";
        }
        
        if (blueTeamHeader != null)
        {
            blueTeamHeader.text = $"🔵 Blue Team ({blueCount})";
        }
        
        Debug.Log($"👥 팀별 플레이어 수 헤더 업데이트 - Red: {redCount}, Blue: {blueCount}");
    }
    
    public void AddOrUpdatePlayer(string playerId, string playerName, string team, int score, bool connected, bool isAdmin = false, bool isObserver = false)
    {
        Debug.Log($"AddOrUpdatePlayer called - ID: {playerId}, Name: {playerName}, Team: {team}, Score: {score}, Connected: {connected}, Admin: {isAdmin}, Observer: {isObserver}");
        
        // 중요한 참조들 확인
        Debug.Log($" 참조 확인 - redContainer: {whiteTeamContainer != null}, blueContainer: {blueTeamContainer != null}, observerContainer: {observerContainer != null}, prefab: {playerInfoPrefab != null}");
        
        Transform targetContainer = null;
        // 전달받은 매개변수 사용, fallback으로 기존 로직 유지
        if (!isAdmin) isAdmin = playerName.Contains("Admin");
        if (!isObserver) isObserver = playerName.Contains("Observer");
        
        Debug.Log($" 최종 플래그 - isAdmin: {isAdmin}, isObserver: {isObserver}");
        
        // 컨테이너 선택 (개인전)
        if (isAdmin || isObserver)
        {
            targetContainer = observerContainer;
            Debug.Log($"🔍 Observer 컨테이너 선택 - Container: {targetContainer != null}");
        }
        else
        {
            // 개인전: 모든 플레이어를 하나의 컨테이너에 배치
            targetContainer = whiteTeamContainer;
            Debug.Log($"👤 Player 컨테이너 선택 (개인전) - Container: {targetContainer != null} - Name: {targetContainer?.name}");
        }
        
        if (targetContainer == null || playerInfoPrefab == null) 
        {
            Debug.LogError($"❌ UI 생성 불가 - targetContainer: {targetContainer != null}, prefab: {playerInfoPrefab != null}");
            if (targetContainer == null) Debug.LogError($"❌ 타겟 컨테이너가 null입니다 (team: {team}, isAdmin: {isAdmin}, isObserver: {isObserver})");
            if (playerInfoPrefab == null) Debug.LogError($"❌ playerInfoPrefab이 null입니다");
            return;
        }
        
        // 기존 플레이어 UI 업데이트 또는 새로 생성 (안전한 방식)
        PlayerInfoUI existingUI = null;
        bool needsNewUI = false;
        
        if (playerInfoUIs.ContainsKey(playerId))
        {
            existingUI = playerInfoUIs[playerId];
            
            // UI가 null이거나 gameObject가 파괴되었는지 확인
            if (existingUI == null || existingUI.gameObject == null)
            {
                Debug.LogWarning($"⚠️ 기존 UI가 null이거나 파괴됨: {playerId} - 새로 생성합니다");
                Debug.LogWarning($"⚠️ existingUI null?: {existingUI == null}, gameObject null?: {existingUI?.gameObject == null}");
                if (existingUI != null && existingUI.gameObject == null)
                {
                    Debug.LogError($"❌ PlayerInfoUI는 존재하지만 gameObject가 파괴됨! 무언가가 GameObject를 삭제했습니다!");
                }
                playerInfoUIs.Remove(playerId); // 잘못된 참조 제거
                needsNewUI = true;
            }
            else
            {
                // 기존 UI 업데이트
                existingUI.SetPlayerInfo(playerId, playerName, team, score, connected, isAdmin, isObserver);
                Debug.Log($"✅ 기존 플레이어 UI 업데이트: {playerName} ({team}) - Score: {score}");
            }
        }
        else
        {
            needsNewUI = true;
        }
        
        // 새 UI 생성이 필요한 경우
        if (needsNewUI)
        {
            Debug.Log($"🆕 새 플레이어 UI 생성 시작 - {playerName} ({team})");
            Debug.Log($"🆕 Prefab: {playerInfoPrefab?.name}, Container: {targetContainer?.name}");
            Debug.Log($"🆕 Container Active: {targetContainer?.gameObject.activeInHierarchy}, Container Parent: {targetContainer?.parent?.name}");
            
            try
            {
                PlayerInfoUI playerUI = Instantiate(playerInfoPrefab, targetContainer);
                Debug.Log($"✅ PlayerInfoUI 오브젝트 생성 완료: {playerUI.name}");
                Debug.Log($"✅ 생성된 UI의 부모: {playerUI.transform.parent?.name}");
                Debug.Log($"✅ 생성된 UI Active: {playerUI.gameObject.activeInHierarchy}");
                
                playerUI.SetPlayerInfo(playerId, playerName, team, score, connected, isAdmin, isObserver);
                Debug.Log($"✅ PlayerInfoUI.SetPlayerInfo 설정 완료");
                
                playerInfoUIs[playerId] = playerUI;
                Debug.Log($"✅ 플레이어 UI 딕셔너리에 추가 완료. 총 개수: {playerInfoUIs.Count}");
                Debug.Log($"🎉 새 플레이어 UI 생성 완료: {playerName} ({team}) - Container: {targetContainer.name}");
                
                // 1초 후 다시 확인
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ UI 생성 중 예외 발생: {ex.Message}");
                Debug.LogError($"❌ 스택 트레이스: {ex.StackTrace}");
            }
        }
    }
    
    public void RemovePlayer(string playerId)
    {
        Debug.Log($"🗑️ RemovePlayer 호출됨 - PlayerId: {playerId}");
        Debug.Log($"🗑️ 호출 스택: {System.Environment.StackTrace}");
        
        if (playerInfoUIs.ContainsKey(playerId))
        {
            PlayerInfoUI playerUI = playerInfoUIs[playerId];
            if (playerUI != null && playerUI.gameObject != null)
            {
                string playerName = playerUI.GetPlayerName();
                string team = playerUI.GetTeam();
                Debug.Log($"🗑️ 플레이어 UI 제거 중: {playerId} - Name: {playerName}, Team: {team}");
                Destroy(playerUI.gameObject);
                Debug.Log($"🗑️ Destroy 완료: {playerName} ({team})");
            }
            else
            {
                Debug.LogWarning($"⚠️ PlayerUI가 이미 null이거나 파괴됨: {playerId}");
            }
            playerInfoUIs.Remove(playerId);
            Debug.Log($"🗑️ 딕셔너리에서 제거 완료. 남은 플레이어 수: {playerInfoUIs.Count}");
        }
        else
        {
            Debug.LogWarning($"⚠️ 제거할 플레이어 UI를 찾을 수 없음: {playerId}");
        }
    }
    
    public void UpdateSinglePlayerScore(string playerId, int newScore)
    {
        if (playerInfoUIs.ContainsKey(playerId))
        {
            PlayerInfoUI playerUI = playerInfoUIs[playerId];
            if (playerUI != null && playerUI.gameObject != null)
            {
                playerUI.UpdateScore(newScore);
                Debug.Log($"✅ 플레이어 점수 업데이트 완료: {playerId} -> {newScore}점");
            }
            else
            {
                Debug.LogWarning($"⚠️ 플레이어 UI가 null이거나 파괴됨: {playerId} - 점수 업데이트 실패");
                playerInfoUIs.Remove(playerId); // 잘못된 참조 제거
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ 플레이어 UI를 찾을 수 없음: {playerId} - 점수 업데이트 실패");
        }
    }
    
    public void UpdatePlayerConnection(string playerId, bool connected)
    {
        if (playerInfoUIs.ContainsKey(playerId))
        {
            playerInfoUIs[playerId].UpdateConnectionStatus(connected);
        }
    }
    
    // 현재 상태 반환 메서드들
    public string GetCurrentGameStatus() => currentGameStatus;
    public int GetRemainingTime() => remainingTime;
    public int GetPlayerCount() => playerInfoUIs.Count;
}

