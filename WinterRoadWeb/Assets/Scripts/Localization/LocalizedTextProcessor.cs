using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Localize String Event의 Update String 이벤트에 연결하여
/// 리터럴 "\n"을 실제 줄바꿈으로 치환한 뒤 Text에 적용합니다.
///
/// 사용법:
/// 1. 이 컴포넌트를 Text가 있는 GameObject에 추가
/// 2. Localize String Event의 Update String 이벤트에서:
///    - Target: 이 컴포넌트의 GameObject
///    - Function: LocalizedTextProcessor.SetLocalizedText (string)
/// </summary>
[RequireComponent(typeof(Text))]
public class LocalizedTextProcessor : MonoBehaviour
{
    private Text targetText;

    private void Awake()
    {
        targetText = GetComponent<Text>();
    }

    /// <summary>
    /// Localize String Event의 Update String 이벤트에서 호출됩니다.
    /// 리터럴 \n → 실제 줄바꿈, \t → 실제 탭으로 치환합니다.
    /// </summary>
    public void SetLocalizedText(string localizedValue)
    {
        if (targetText == null)
            targetText = GetComponent<Text>();

        if (targetText != null && localizedValue != null)
        {
            targetText.text = localizedValue.Replace("\\n", "\n").Replace("\\t", "\t");
        }
    }
}
