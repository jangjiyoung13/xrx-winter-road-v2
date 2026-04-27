using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;

/// <summary>
/// 언어 선택 팝업 패널.
/// 자식의 LanguageButtonItem 컴포넌트를 자동 수집하여 라디오 버튼으로 관리합니다.
/// Inspector에서 버튼을 하나하나 연결할 필요 없이, Prefab_Language에 LanguageButtonItem만 붙이면 됩니다.
/// </summary>
public class LanguagePanel : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;        // 팝업 전체 루트
    [SerializeField] private Button closeButton;          // X 닫기 버튼

    private List<LanguageButtonItem> languageButtons = new List<LanguageButtonItem>();
    private LanguageButtonItem currentSelected;

    private void OnEnable()
    {
        // 자식에서 LanguageButtonItem 자동 수집
        languageButtons.Clear();
        GetComponentsInChildren(true, languageButtons);

        // 버튼 이벤트 바인딩
        foreach (var item in languageButtons)
        {
            if (item.Button == null) continue;
            var captured = item;
            item.Button.onClick.AddListener(() => OnLanguageButtonClicked(captured));
        }

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        // Localization 초기화 완료 여부에 따라 처리
        if (LocalizationSettings.InitializationOperation.IsDone)
        {
            SyncWithCurrentLocale();
        }
        else
        {
            SetButtonsInteractable(false);
            StartCoroutine(WaitForInitAndSync());
        }
    }

    private System.Collections.IEnumerator WaitForInitAndSync()
    {
        yield return LocalizationSettings.InitializationOperation;
        SetButtonsInteractable(true);
        SyncWithCurrentLocale();
    }

    private void SetButtonsInteractable(bool interactable)
    {
        foreach (var item in languageButtons)
        {
            if (item.Button != null)
                item.Button.interactable = interactable;
        }
    }

    private void OnDisable()
    {
        foreach (var item in languageButtons)
        {
            if (item != null && item.Button != null)
                item.Button.onClick.RemoveAllListeners();
        }

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Hide);
    }

    /// <summary>
    /// 현재 Locale에 맞는 버튼을 선택 상태로 동기화
    /// </summary>
    private void SyncWithCurrentLocale()
    {
        var currentLocale = LocalizationSettings.SelectedLocale;
        if (currentLocale == null) return;

        string currentCode = currentLocale.Identifier.Code;

        foreach (var item in languageButtons)
        {
            if (string.Equals(item.LocaleCode, currentCode, System.StringComparison.OrdinalIgnoreCase))
            {
                SetSelected(item);
                return;
            }
        }
    }

    /// <summary>
    /// 언어 버튼 클릭 시 호출
    /// </summary>
    private void OnLanguageButtonClicked(LanguageButtonItem clicked)
    {
        if (clicked == currentSelected) return;

        SetSelected(clicked);
        ChangeLocale(clicked.LocaleCode);
    }

    /// <summary>
    /// 라디오 형식: 선택된 버튼만 Selected, 나머지는 Normal
    /// </summary>
    private void SetSelected(LanguageButtonItem selected)
    {
        currentSelected = selected;

        foreach (var item in languageButtons)
        {
            item.SetSelected(item == selected);
        }
    }

    /// <summary>
    /// Unity Localization의 Locale 변경
    /// </summary>
    private void ChangeLocale(string localeCode)
    {
        var locales = LocalizationSettings.AvailableLocales.Locales;

        foreach (var locale in locales)
        {
            if (string.Equals(locale.Identifier.Code, localeCode, System.StringComparison.OrdinalIgnoreCase))
            {
                LocalizationSettings.SelectedLocale = locale;
                Debug.Log($"🌐 언어 변경: {locale.LocaleName} ({localeCode})");
                return;
            }
        }

        Debug.LogWarning($"⚠️ Locale을 찾을 수 없습니다: {localeCode}");
    }

    /// <summary>
    /// 팝업 표시
    /// </summary>
    public void Show()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        SyncWithCurrentLocale();
    }

    /// <summary>
    /// 팝업 숨기기
    /// </summary>
    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}
