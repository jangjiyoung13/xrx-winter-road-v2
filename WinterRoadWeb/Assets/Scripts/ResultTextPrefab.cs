using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResultTextPrefab : MonoBehaviour
{
    [SerializeField] private GameObject[] _rankObject;
    [SerializeField] private Text rankText;
    [SerializeField] private Text nameText;
    [SerializeField] private Text scoreText;
    [SerializeField] private GameObject meHighlight;      // 자기 자신일 때 활성화할 오브젝트
    [SerializeField] private Text meNameText;              // meHighlight 안 플레이어 이름 텍스트
    [SerializeField] private Text meScoreText;             // meHighlight 안 점수 텍스트

    [Header("Element 아이콘 (선택한 능력 표시)")]
    [SerializeField] private Image elementIcon;
    [Tooltip("순서: Joy, Sadness, Courage, Love, Hope, Friendship — ElementType enum 기준")]
    [SerializeField] private Sprite[] elementSprites = new Sprite[6];

    public void SetData(int rank, string playerName, int score, bool isMyResult = false, string element = "None")
    {
        for (int i = 0; i < _rankObject.Length; i++)
        {
            _rankObject[i].gameObject.SetActive(false);
        }

        if (rankText != null)
        {
            if(rank <= 3)
            {
                _rankObject[rank - 1].gameObject.SetActive(true);
                rankText.text = "";
            }
            else
            {
                rankText.text = rank.ToString();
            }
        }

        if (nameText != null)
        {
            nameText.text = playerName;
        }

        if (scoreText != null)
        {
            scoreText.text = score.ToString();
        }

        // 자기 자신 하이라이트
        if (meHighlight != null)
        {
            meHighlight.SetActive(isMyResult);

            // meHighlight 안 텍스트도 동일한 값으로 세팅
            if (isMyResult)
            {
                if (meNameText != null)
                    meNameText.text = playerName;
                if (meScoreText != null)
                    meScoreText.text = score.ToString();
            }
        }

        // Element 아이콘
        ApplyElementSprite(element);
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
            Debug.LogWarning($"⚠️ ResultTextPrefab: 알 수 없는 element '{element}' → 아이콘 숨김");
            elementIcon.gameObject.SetActive(false);
            return;
        }

        // ElementType: None=0, Joy=1, ..., Friendship=6 → 배열 인덱스 0~5
        int idx = (int)parsed - 1;
        if (elementSprites == null || idx < 0 || idx >= elementSprites.Length || elementSprites[idx] == null)
        {
            Debug.LogWarning($"⚠️ ResultTextPrefab: elementSprites[{idx}] 미할당 (element: '{element}') → 아이콘 숨김");
            elementIcon.gameObject.SetActive(false);
            return;
        }

        elementIcon.sprite = elementSprites[idx];
        elementIcon.gameObject.SetActive(true);
    }
}
