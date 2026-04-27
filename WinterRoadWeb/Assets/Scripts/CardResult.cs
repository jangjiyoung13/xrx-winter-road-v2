using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardResult : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Text rankText;                // 순위 텍스트 (1st, 2nd, 3rd...)
    [SerializeField] private Text nickNameText;            // 닉네임
    [SerializeField] private Text scoreText;               // 점수
    [SerializeField] private Text comboText;               // 최대 콤보
    
    [Header("등수별 카드 이미지")]
    [SerializeField] private GameObject card1stImage;      // 1등 카드 이미지
    [SerializeField] private GameObject card2ndImage;      // 2등 카드 이미지
    [SerializeField] private GameObject card3rdImage;      // 3등 카드 이미지
    [SerializeField] private GameObject cardDefaultImage;  // 4등 이후 기본 카드 이미지
    

    /// <summary>
    /// 카드 결과 데이터 설정 (개인전)
    /// </summary>
    public void SetCardData(int rank, bool isWinner, string nickname, int score, int maxCombo = 0)
    {
        // 순위 텍스트
        if (rankText != null)
        {
            rankText.text = GetRankText(rank);
        }
        
        // 닉네임
        if (nickNameText != null)
        {
            nickNameText.text = nickname;
        }
        
        // 점수
        if (scoreText != null)
        {
            scoreText.text = $"{score}";
        }
        
        // 최대 콤보
        if (comboText != null)
        {
            comboText.text = $"x{maxCombo}";
        }
        
        // 등수별 카드 이미지 전환
        SetCardImage(rank);
        

        Debug.Log($"🎴 CardResult 설정 - {rank}위: {nickname} ({score}점, 최대콤보: {maxCombo})");
    }
    
    /// <summary>
    /// 등수에 따라 카드 이미지 전환
    /// 1등/2등/3등은 각각 다른 이미지, 4등부터는 기본 이미지
    /// </summary>
    private void SetCardImage(int rank)
    {
        // 모든 카드 이미지 비활성화
        if (card1stImage != null) card1stImage.SetActive(false);
        if (card2ndImage != null) card2ndImage.SetActive(false);
        if (card3rdImage != null) card3rdImage.SetActive(false);
        if (cardDefaultImage != null) cardDefaultImage.SetActive(false);
        
        // 해당 등수 카드 이미지만 활성화
        switch (rank)
        {
            case 1:
                if (card1stImage != null) card1stImage.SetActive(true);
                break;
            case 2:
                if (card2ndImage != null) card2ndImage.SetActive(true);
                break;
            case 3:
                if (card3rdImage != null) card3rdImage.SetActive(true);
                break;
            default:
                if (cardDefaultImage != null) cardDefaultImage.SetActive(true);
                break;
        }
    }
    

    /// <summary>
    /// 순위를 텍스트로 변환 (1st, 2nd, 3rd, 4th...)
    /// </summary>
    private string GetRankText(int rank)
    {
        if (rank <= 0) return "";
        
        if (rank >= 11 && rank <= 13)
            return $"{rank}th";
        
        int lastDigit = rank % 10;
        
        switch (lastDigit)
        {
            case 1: return $"{rank}st";
            case 2: return $"{rank}nd";
            case 3: return $"{rank}rd";
            default: return $"{rank}th";
        }
    }
}
