using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// Localize String Event 컴포넌트가 있는 Text에 자동으로 연결하여
/// 리터럴 \n, \t를 실제 줄바꿈/탭으로 치환합니다.
///
/// 사용법: 씬의 아무 GameObject에 이 컴포넌트를 하나 추가하면
/// 씬 내 모든 LocalizeStringEvent를 자동으로 찾아서 처리합니다.
/// </summary>
public class EscapeSequenceHandler : MonoBehaviour
{
    private void Start()
    {
        RegisterAllInScene();
    }

    /// <summary>
    /// 씬 내 모든 LocalizeStringEvent를 찾아 이스케이프 핸들러를 등록합니다.
    /// </summary>
    private void RegisterAllInScene()
    {
        var events = FindObjectsByType<LocalizeStringEvent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;

        foreach (var localizeEvent in events)
        {
            RegisterEscapeHandler(localizeEvent);
            count++;
        }

        Debug.Log($"[EscapeSequenceHandler] ✅ {count}개의 LocalizeStringEvent에 이스케이프 치환 핸들러 등록 완료");
    }

    /// <summary>
    /// LocalizeStringEvent에 이스케이프 시퀀스 치환 핸들러를 등록합니다.
    /// </summary>
    private void RegisterEscapeHandler(LocalizeStringEvent localizeEvent)
    {
        // 현재 연결된 Text 컴포넌트 찾기
        var text = localizeEvent.GetComponent<Text>();
        if (text == null) return;

        // OnUpdateString 이벤트에 후처리 핸들러 추가
        localizeEvent.OnUpdateString.AddListener((localizedValue) =>
        {
            if (text != null && localizedValue != null)
            {
                // 리터럴 \n → 실제 줄바꿈, \t → 실제 탭으로 치환
                text.text = localizedValue.Replace("\\n", "\n").Replace("\\t", "\t");
            }
        });
    }
}
