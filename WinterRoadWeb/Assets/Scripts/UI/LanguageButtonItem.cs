using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 각 언어 프리팹(Prefab_Language)에 붙이는 스크립트.
/// 자식에서 "Normal"과 "Selected" 오브젝트를 자동으로 찾아 상태를 관리합니다.
/// </summary>
public class LanguageButtonItem : MonoBehaviour
{
    [Header("Locale 설정")]
    [SerializeField] private string localeCode;  // "ko", "en", "zh-Hans", "zh-Hant", "ja"

    [Header("상태 오브젝트")]
    [SerializeField] private GameObject normalState;    // Normal 상태 오브젝트
    [SerializeField] private GameObject selectedState;  // Selected 상태 오브젝트

    private Button _button;

    public string LocaleCode => localeCode;
    public Button Button
    {
        get
        {
            if (_button == null)
                _button = GetComponent<Button>();
            return _button;
        }
    }

    /// <summary>
    /// 선택/해제 상태를 설정합니다.
    /// </summary>
    public void SetSelected(bool isSelected)
    {
        if (normalState != null)
            normalState.SetActive(!isSelected);

        if (selectedState != null)
            selectedState.SetActive(isSelected);
    }
}
