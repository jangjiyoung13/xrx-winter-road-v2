using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerInfoUI : MonoBehaviour
{
    [Header("UI 컴포넌트")]
    [SerializeField] private Text playerNameText;
    [SerializeField] private Text playerNameText2;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text scoreText2;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject meBackground;        // 내 플레이어일 때 표시하는 배경
    
    [Header("등수 표시")]
    [SerializeField] private GameObject rank1stObject;   // 1등 오브젝트 (메달/뱃지 등)
    [SerializeField] private GameObject rank2ndObject;   // 2등 오브젝트
    [SerializeField] private GameObject rank3rdObject;   // 3등 오브젝트
    [SerializeField] private Text rankText;              // 4등 이하 텍스트 표시
    
    [Header("추월 연출")]
    [SerializeField] private GameObject rankUpObject;         // 추월 시 0.5초 플래시
    [SerializeField] private GameObject rankDownObject;       // 추월당함 시 0.5초 플래시
    
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
        if (playerNameText2 != null)
        {
            string prefix = "";
            if (isAdmin) prefix = "👑 ";
            else if (isObserver) prefix = "👁️ ";
            
            playerNameText2.text = $"{prefix}{playerName}";
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
        if (scoreText2 != null)
        {
            if (isAdmin || isObserver)
            {
                scoreText2.text = "—";
            }
            else
            {
                scoreText2.text = score.ToString();
            }
        }
    }
    
    /// <summary>
    /// 등수에 따라 1~3등 오브젝트 또는 4등+ 텍스트를 표시합니다.
    /// </summary>
    /// <param name="rank">현재 등수 (1부터 시작)</param>
    public void UpdateRank(int rank)
    {
        // 모든 등수 표시 초기화
        if (rank1stObject != null) rank1stObject.SetActive(false);
        if (rank2ndObject != null) rank2ndObject.SetActive(false);
        if (rank3rdObject != null) rank3rdObject.SetActive(false);
        if (rankText != null) rankText.gameObject.SetActive(false);
        
        // Admin/Observer는 등수 표시 안함
        if (isAdmin || isObserver) return;
        
        switch (rank)
        {
            case 1:
                if (rank1stObject != null) rank1stObject.SetActive(true);
                break;
            case 2:
                if (rank2ndObject != null) rank2ndObject.SetActive(true);
                break;
            case 3:
                if (rank3rdObject != null) rank3rdObject.SetActive(true);
                break;
            default:
                if (rankText != null)
                {
                    rankText.gameObject.SetActive(true);
                    rankText.text = $"{rank}";
                }
                break;
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
            if (scoreText2 != null)
            {
                scoreText2.text = score.ToString();
            }
        }
    }
    
    public void UpdateConnectionStatus(bool connected)
    {
        isConnected = connected;
        UpdateUI();
    }
    
    /// <summary>
    /// 나 태그 업데이트 - 내 플레이어면 배경 표시
    /// </summary>
    public void UpdateMeTag(bool isMe)
    {
        if (meBackground != null)
        {
            meBackground.SetActive(isMe);
        }
        
        if (isMe)
        {
            Debug.Log($"✅ 이 플레이어가 나입니다: [{playerId}] {playerName}");
        }
    }
    
    /// <summary>
    /// 추월 연출 (0.5초 플래시)
    /// </summary>
    public void ShowRankUp()
    {
        if (rankUpObject != null)
            StartCoroutine(FlashObject(rankUpObject, 0.5f));
    }
    
    /// <summary>
    /// 추월당함 연출 (0.5초 플래시)
    /// </summary>
    public void ShowRankDown()
    {
        if (rankDownObject != null)
            StartCoroutine(FlashObject(rankDownObject, 0.5f));
    }
    
    private IEnumerator FlashObject(GameObject obj, float duration)
    {
        obj.SetActive(true);
        yield return new WaitForSeconds(duration);
        obj.SetActive(false);
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
