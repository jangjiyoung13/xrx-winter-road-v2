using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerInfoPrefab : MonoBehaviour
{
    public TextMeshProUGUI _playerName;
    public TextMeshProUGUI _playerScore;
    
    [Header("UI 컴포넌트")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image teamIndicator;
    
    [Header("팀 색상")]
    [SerializeField] private Color whiteTeamColor = Color.red;
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
    
    void Start()
    {
        // 기존 참조가 있다면 새 참조로 매핑
        if (_playerName != null && playerNameText == null)
            playerNameText = _playerName;
        if (_playerScore != null && scoreText == null)
            scoreText = _playerScore;
    }
    
    public void SetPlayerInfo(string id, string name, string playerTeam, int playerScore, bool connected, bool admin = false, bool observer = false)
    {
        playerId = id;
        playerName = name;
        team = playerTeam;
        score = playerScore;
        isConnected = connected;
        isAdmin = admin;
        isObserver = observer;
        
        Debug.Log($"🎯 PlayerInfoPrefab.SetPlayerInfo 호출됨 - {name} ({playerTeam})");
        
        UpdateUI();
    }
    
    private void UpdateUI()
    {
        Debug.Log($"🎨 UI 업데이트 시작 - {playerName}");
        
        // 플레이어 이름 설정 (기존 방식과 새 방식 모두 지원)
        if (playerNameText != null)
        {
            string prefix = "";
            if (isAdmin) prefix = "👑 ";
            else if (isObserver) prefix = "👁️ ";
            
            playerNameText.text = $"{prefix}{playerName}";
            Debug.Log($"✅ 플레이어 이름 설정 완료: {playerNameText.text}");
        }
        else if (_playerName != null)
        {
            string prefix = "";
            if (isAdmin) prefix = "👑 ";
            else if (isObserver) prefix = "👁️ ";
            
            _playerName.text = $"{prefix}{playerName}";
            Debug.Log($"✅ 플레이어 이름 설정 완료 (기존 방식): {_playerName.text}");
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
            Debug.Log($"✅ 점수 설정 완료: {scoreText.text}");
        }
        else if (_playerScore != null)
        {
            if (isAdmin || isObserver)
            {
                _playerScore.text = "—";
            }
            else
            {
                _playerScore.text = score.ToString();
            }
            Debug.Log($"✅ 점수 설정 완료 (기존 방식): {_playerScore.text}");
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
            Debug.Log($"✅ 상태 설정 완료: {statusText.text}");
        }
        
        // 팀 색상 설정
        UpdateTeamVisuals();
        
        Debug.Log($"🎨 UI 업데이트 완료 - {playerName}");
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
        else if (team == "White")
        {
            teamColor = whiteTeamColor;
        }
        else if (team == "Blue")
        {
            teamColor = blueTeamColor;
        }
        
        // 팀 인디케이터 색상 설정
        if (teamIndicator != null)
        {
            teamIndicator.color = teamColor;
            Debug.Log($"✅ 팀 인디케이터 색상 설정: {teamColor}");
        }
        
        // 배경 색상 살짝 적용
        if (backgroundImage != null)
        {
            Color bgColor = teamColor;
            bgColor.a = 0.1f; // 투명도 조절
            backgroundImage.color = bgColor;
            Debug.Log($"✅ 배경 색상 설정: {bgColor}");
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
            else if (_playerScore != null)
            {
                _playerScore.text = score.ToString();
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
