using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerInfoUI : MonoBehaviour
{
    [Header("UI 컴포넌트")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image teamIndicator;
    
    [Header("팀 색상")]
    [SerializeField] private Color redTeamColor = Color.red;
    [SerializeField] private Color blueTeamColor = Color.blue;
    [SerializeField] private Color adminColor = Color.yellow;
    [SerializeField] private Color observerColor = Color.gray;
    
    private string playerId;
    private string playerName;
    private string team;
    private int score;
    private bool isConnected;
    private bool isAdmin;
    private bool isObserver;
    
    public void SetPlayerInfo(string id, string name, string playerTeam, int playerScore, bool connected, bool admin = false, bool observer = false)
    {
        playerId = id;
        playerName = name;
        team = playerTeam;
        score = playerScore;
        isConnected = connected;
        isAdmin = admin;
        isObserver = observer;
        
        UpdateUI();
    }
    
    private void UpdateUI()
    {
        // 플레이어 이름 설정
        if (playerNameText != null)
        {
            string prefix = "";
            if (isAdmin) prefix = "👑 ";
            else if (isObserver) prefix = "👁️ ";
            
            playerNameText.text = $"{prefix}{playerName}";
        }
        
        // 점수 설정
        if (scoreText != null)
        {
            if (isAdmin || isObserver)
            {
                scoreText.text = "—";
            }
            else
            {
                scoreText.text = score.ToString();
            }
        }
        
        // 상태 설정
        if (statusText != null)
        {
            if (!isConnected)
            {
                statusText.text = "🔴 Offline";
                statusText.color = Color.red;
            }
            else if (isAdmin)
            {
                statusText.text = "👑 Admin";
                statusText.color = adminColor;
            }
            else if (isObserver)
            {
                statusText.text = "👁️ Observer";
                statusText.color = observerColor;
            }
            else
            {
                statusText.text = "🟢 Online";
                statusText.color = Color.green;
            }
        }
        
        // 팀 색상 설정
        UpdateTeamVisuals();
    }
    
    private void UpdateTeamVisuals()
    {
        Color teamColor = Color.white;
        
        if (isAdmin)
        {
            teamColor = adminColor;
        }
        else if (isObserver)
        {
            teamColor = observerColor;
        }
        else if (team == "Red")
        {
            teamColor = redTeamColor;
        }
        else if (team == "Blue")
        {
            teamColor = blueTeamColor;
        }
        
        // 팀 인디케이터 색상 설정
        if (teamIndicator != null)
        {
            teamIndicator.color = teamColor;
        }
        
        // 배경 색상 살짝 적용
        if (backgroundImage != null)
        {
            Color bgColor = teamColor;
            bgColor.a = 0.1f; // 투명도 조절
            backgroundImage.color = bgColor;
        }
    }
    
    public void UpdateScore(int newScore)
    {
        if (!isAdmin && !isObserver)
        {
            score = newScore;
            if (scoreText != null)
            {
                scoreText.text = score.ToString();
            }
        }
    }
    
    public void UpdateConnectionStatus(bool connected)
    {
        isConnected = connected;
        UpdateUI();
    }
    
    // Getter 메서드들
    public string GetPlayerId() => playerId;
    public string GetPlayerName() => playerName;
    public string GetTeam() => team;
    public int GetScore() => score;
    public bool IsConnected() => isConnected;
    public bool IsAdmin() => isAdmin;
    public bool IsObserver() => isObserver;
}

