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

    [Header("Element 아이콘 (선택한 능력 표시)")]
    [SerializeField] private Image elementIcon;
    [Tooltip("순서: Joy, Sadness, Courage, Love, Hope, Friendship — ElementType enum 기준")]
    [SerializeField] private Sprite[] elementSprites = new Sprite[6];


    /// <summary>
    /// 카드 결과 데이터 설정 (개인전)
    /// </summary>
    public void SetCardData(int rank, bool isWinner, string nickname, int score, int maxCombo = 0, string element = "None")
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

        // Element 아이콘
        ApplyElementSprite(element);

        Debug.Log($"🎴 CardResult 설정 - {rank}위: {nickname} ({score}점, 최대콤보: {maxCombo}, element: {element})");
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
    /// element 문자열을 ElementType enum으로 파싱하여 elementSprites 배열에서 매핑.
    /// 알 수 없는 값이거나 "None"이면 아이콘 GameObject를 비활성화.
    /// </summary>
    private void ApplyElementSprite(string element)
    {
        if (elementIcon == null) return;

        if (string.IsNullOrEmpty(element) || element == "None")
        {
            elementIcon.gameObject.SetActive(false);
            return;
        }

        if (!System.Enum.TryParse<ElementType>(element, true, out var parsed) || parsed == ElementType.None)
        {
            Debug.LogWarning($"⚠️ CardResult: 알 수 없는 element '{element}' → 아이콘 숨김");
            elementIcon.gameObject.SetActive(false);
            return;
        }

        // ElementType: None=0, Joy=1, ..., Friendship=6 → 배열 인덱스 0~5
        int idx = (int)parsed - 1;
        if (elementSprites == null || idx < 0 || idx >= elementSprites.Length || elementSprites[idx] == null)
        {
            Debug.LogWarning($"⚠️ CardResult: elementSprites[{idx}] 미할당 (element: '{element}') → 아이콘 숨김");
            elementIcon.gameObject.SetActive(false);
            return;
        }

        elementIcon.sprite = elementSprites[idx];
        elementIcon.gameObject.SetActive(true);
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
