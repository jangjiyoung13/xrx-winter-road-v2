using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대기방 패널을 관리하는 스크립트.
/// GamePanel에서 분리된 대기 화면 로직을 담당합니다.
/// 대기 인원 수와 게임 시작까지 남은 시간을 표시합니다.
/// </summary>
public class WaitingRoomPanel : MonoBehaviour
{
    [Header("대기 패널")]
    [SerializeField] private GameObject waitingPanel;

    [Header("대기 인원 표시")]
    [SerializeField] private Text playerCountText;       // 대기 인원 수 텍스트 (숫자만)

    [Header("남은 시간 표시")]
    [SerializeField] private Text waitingText;            // 남은 시간 텍스트 (MM:SS)

    // 현재 상태 캐싱
    private int currentPlayerCount = 0;
    private int currentCountdown = 0;

    /// <summary>
    /// 현재 대기 인원 수를 반환합니다.
    /// </summary>
    public int CurrentPlayerCount => currentPlayerCount;

    /// <summary>
    /// 현재 남은 시간(초)을 반환합니다.
    /// </summary>
    public int CurrentCountdown => currentCountdown;

    /// <summary>
    /// 대기 패널을 표시하고 남은 시간과 대기 인원을 업데이트합니다.
    /// </summary>
    /// <param name="seconds">게임 시작까지 남은 시간 (초)</param>
    /// <param name="playerCount">대기 인원 수</param>
    public void ShowWaitingPanel(int seconds, int playerCount = 0)
    {
        if (waitingPanel != null)
        {
            waitingPanel.SetActive(true);
        }
        gameObject.SetActive(true);
        
        UpdateWaitingText(seconds);
        UpdatePlayerCount(playerCount);
        
        Debug.Log($"⏳ 대기 패널 표시 - 게임 시작까지 {seconds}초, 대기 인원: {playerCount}명");
    }

    /// <summary>
    /// 대기 패널의 남은 시간 텍스트를 업데이트합니다.
    /// </summary>
    /// <param name="seconds">게임 시작까지 남은 시간 (초)</param>
    public void UpdateWaitingText(int seconds)
    {
        currentCountdown = seconds;
        
        if (waitingText != null)
        {
            int minutes = seconds / 60;
            int secs = seconds % 60;
            waitingText.text = string.Format("{0:00}:{1:00}", minutes, secs);
        }
    }

    /// <summary>
    /// 대기 인원 수를 업데이트합니다.
    /// </summary>
    /// <param name="count">대기 인원 수</param>
    public void UpdatePlayerCount(int count)
    {
        currentPlayerCount = count;
        
        if (playerCountText != null)
        {
            playerCountText.text = count.ToString();
        }
        
        Debug.Log($"👥 대기 인원 업데이트: {count}명");
    }

    /// <summary>
    /// 대기 패널의 남은 시간과 인원 수를 동시에 업데이트합니다.
    /// </summary>
    /// <param name="seconds">남은 시간 (초)</param>
    /// <param name="playerCount">대기 인원 수</param>
    public void UpdateWaitingInfo(int seconds, int playerCount)
    {
        UpdateWaitingText(seconds);
        UpdatePlayerCount(playerCount);
    }

    /// <summary>
    /// 대기 패널을 숨깁니다.
    /// </summary>
    public void HideWaitingPanel()
    {
        if (waitingPanel != null)
        {
            waitingPanel.SetActive(false);
        }
        Debug.Log("🙈 대기 패널 숨김");
    }
}
