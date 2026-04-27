using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// 씬 로드 전에 Localization 시스템 초기화를 강제 완료하고,
/// WebGL 빌드에서는 브라우저 언어를 감지하여 자동으로 Locale을 설정합니다.
/// </summary>
public static class LocalizationInitializer
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern string GetBrowserLanguageInternal();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ForceInitLocalization()
    {
        Debug.Log("[LocalizationInitializer] Localization 초기화 시작...");

        var initOp = LocalizationSettings.InitializationOperation;

        if (!initOp.IsDone)
        {
            Debug.Log("[LocalizationInitializer] 초기화 대기 중 (WaitForCompletion)...");
            initOp.WaitForCompletion();
        }

        if (initOp.IsDone)
        {
            Debug.Log($"[LocalizationInitializer] ✅ 초기화 완료! Locale: {LocalizationSettings.SelectedLocale?.Identifier.Code}");

            // Preload 테이블 강제 로드
            var preloadOp = LocalizationSettings.StringDatabase.PreloadOperation;
            if (!preloadOp.IsDone)
            {
                Debug.Log("[LocalizationInitializer] 테이블 프리로드 대기 중...");
                preloadOp.WaitForCompletion();
            }

            Debug.Log($"[LocalizationInitializer] ✅ 테이블 프리로드 완료!");

            // 브라우저 언어 자동 감지 및 Locale 설정
            ApplyBrowserLocale();
        }
        else
        {
            Debug.LogError("[LocalizationInitializer] ❌ 초기화 실패! Addressables 설정을 확인하세요.");
        }
    }

    /// <summary>
    /// 브라우저 언어를 감지하여 매칭되는 Locale로 자동 전환합니다.
    /// WebGL 빌드에서만 동작하며, 에디터에서는 기본 Locale을 유지합니다.
    /// </summary>
    private static void ApplyBrowserLocale()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            string browserLang = GetBrowserLanguageInternal();
            Debug.Log($"[LocalizationInitializer] 🌐 브라우저 언어: {browserLang}");

            if (string.IsNullOrEmpty(browserLang)) return;

            // 브라우저 언어 코드를 Unity Locale 코드로 매핑
            string localeCode = MapBrowserLanguageToLocale(browserLang);
            Debug.Log($"[LocalizationInitializer] 매핑된 Locale 코드: {localeCode}");

            // 현재 Locale과 동일하면 스킵
            var currentLocale = LocalizationSettings.SelectedLocale;
            if (currentLocale != null &&
                string.Equals(currentLocale.Identifier.Code, localeCode, System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[LocalizationInitializer] 이미 동일한 Locale: {localeCode}");
                return;
            }

            // 매칭되는 Locale 찾기
            var locales = LocalizationSettings.AvailableLocales.Locales;
            foreach (var locale in locales)
            {
                if (string.Equals(locale.Identifier.Code, localeCode, System.StringComparison.OrdinalIgnoreCase))
                {
                    LocalizationSettings.SelectedLocale = locale;
                    Debug.Log($"[LocalizationInitializer] ✅ 브라우저 언어에 따라 Locale 변경: {locale.LocaleName} ({localeCode})");
                    return;
                }
            }

            Debug.Log($"[LocalizationInitializer] ⚠️ 매칭되는 Locale 없음: {localeCode}, 기본 Locale 유지");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LocalizationInitializer] 브라우저 언어 감지 실패: {e.Message}");
        }
#else
        Debug.Log("[LocalizationInitializer] 에디터 모드 - 브라우저 언어 감지 스킵");
#endif
    }

    /// <summary>
    /// 브라우저 언어 코드 (BCP 47)를 Unity Locale 코드로 매핑합니다.
    /// 예: "zh-CN" → "zh-Hans", "zh-TW" → "zh-Hant", "ko-KR" → "ko"
    /// </summary>
    private static string MapBrowserLanguageToLocale(string browserLang)
    {
        // 소문자로 변환하여 비교
        string lang = browserLang.ToLowerInvariant();

        // 중국어 변종 처리
        if (lang.StartsWith("zh"))
        {
            if (lang.Contains("hant") || lang.Contains("tw") || lang.Contains("hk") || lang.Contains("mo"))
                return "zh-Hant";  // 번체
            else
                return "zh-Hans";  // 간체 (기본)
        }

        // 기본 언어 코드 추출 (예: "ko-KR" → "ko", "ja-JP" → "ja")
        string baseCode = lang.Contains("-") ? lang.Substring(0, lang.IndexOf('-')) : lang;

        // 지원 언어 매핑
        switch (baseCode)
        {
            case "ko": return "ko";
            case "ja": return "ja";
            case "en": return "en";
            default:   return "en";  // 미지원 언어는 영어로 폴백
        }
    }
}
