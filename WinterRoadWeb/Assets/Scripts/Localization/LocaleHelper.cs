using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

/// <summary>
/// Unity Localization String Table에서 키로 번역 문자열을 조회하는 헬퍼 유틸리티.
/// UI_Texts 테이블을 사용합니다.
/// </summary>
public static class LocaleHelper
{
    private const string TABLE_NAME = "UI_Texts";

    /// <summary>
    /// 키에 해당하는 번역 문자열을 반환합니다.
    /// 키가 없거나 로딩 전이면 fallback을 반환합니다.
    /// </summary>
    /// <param name="key">로컬라이제이션 키 (예: SYS_ERROR_NICKNAME_EMPTY)</param>
    /// <param name="fallback">키가 없을 때 반환할 기본 문자열 (null이면 키 자체를 반환)</param>
    /// <returns>번역된 문자열</returns>
    public static string Get(string key, string fallback = null)
    {
        if (!LocalizationSettings.InitializationOperation.IsDone)
        {
            Debug.LogWarning($"[LocaleHelper] Localization not initialized yet. Key: {key}");
            return fallback ?? key;
        }

        var table = LocalizationSettings.StringDatabase.GetTable(TABLE_NAME);
        if (table == null)
        {
            Debug.LogWarning($"[LocaleHelper] Table '{TABLE_NAME}' not found. Key: {key}");
            return fallback ?? key;
        }

        var entry = table.GetEntry(key);
        if (entry == null || string.IsNullOrEmpty(entry.LocalizedValue))
        {
            Debug.LogWarning($"[LocaleHelper] Key '{key}' not found in table '{TABLE_NAME}'.");
            return fallback ?? key;
        }

        return ProcessEscapes(entry.LocalizedValue);
    }

    /// <summary>
    /// 키에 해당하는 번역 문자열을 포맷 파라미터와 함께 반환합니다.
    /// string.Format()과 동일하게 {0}, {1}, ... 치환됩니다.
    /// </summary>
    /// <param name="key">로컬라이제이션 키</param>
    /// <param name="args">포맷 파라미터</param>
    /// <returns>포맷이 적용된 번역 문자열</returns>
    public static string GetFormat(string key, params object[] args)
    {
        string template = Get(key);
        // Get()에서 이미 ProcessEscapes 처리됨

        try
        {
            return string.Format(template, args);
        }
        catch (System.FormatException e)
        {
            Debug.LogWarning($"[LocaleHelper] Format error for key '{key}': {e.Message}");
            return template;
        }
    }

    /// <summary>
    /// 리터럴 "\n" 등 이스케이프 시퀀스를 실제 문자로 치환
    /// </summary>
    private static string ProcessEscapes(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input.Replace("\\n", "\n").Replace("\\t", "\t");
    }
}
