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
    
    public void SetData(int rank, string playerName, int score, bool isMyResult = false)
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
    }
}
