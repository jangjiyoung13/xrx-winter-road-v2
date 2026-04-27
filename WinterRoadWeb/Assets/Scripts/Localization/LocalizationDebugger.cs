using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

/// <summary>
/// Play 모드에서 Localization 시스템 상태를 진단합니다.
/// 아무 GameObject에 붙여서 Play하면 콘솔에 결과가 출력됩니다.
/// 진단 완료 후 삭제해도 됩니다.
/// </summary>
public class LocalizationDebugger : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("===== [LocalizationDebugger] 진단 시작 =====");

        // 1. 초기화 상태
        var initOp = LocalizationSettings.InitializationOperation;
        Debug.Log($"[1] 초기화 완료: {initOp.IsDone}");

        // 2. 현재 Locale
        var locale = LocalizationSettings.SelectedLocale;
        Debug.Log($"[2] 현재 Locale: {(locale != null ? locale.Identifier.Code : "NULL")}");

        // 3. 사용 가능한 Locale 목록
        var locales = LocalizationSettings.AvailableLocales.Locales;
        Debug.Log($"[3] 사용 가능한 Locale 수: {locales.Count}");
        foreach (var l in locales)
        {
            Debug.Log($"    - {l.LocaleName} ({l.Identifier.Code})");
        }

        // 4. StringTable 로드 테스트
        Debug.Log("[4] StringTable 'UI_Texts' 로드 테스트...");
        try
        {
            var tableOp = LocalizationSettings.StringDatabase.GetTableAsync("UI_Texts");
            tableOp.WaitForCompletion();

            if (tableOp.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                var table = tableOp.Result;
                if (table == null)
                {
                    Debug.LogError("[4] ❌ 테이블 로드 성공했지만 Result가 NULL!");
                }
                else
                {
                    Debug.Log($"[4] ✅ 테이블 로드 성공: {table.TableCollectionName}, Locale: {table.LocaleIdentifier.Code}");
                    Debug.Log($"    엔트리 수: {table.Count}");

                    // 5. 키별 조회 테스트
                    string[] testKeys = {
                        "LABEL_NICKNAME_NOTICE",
                        "INPUTFIELD_NICKNAME_NOTICE",
                        "SYS_ERROR_NICKNAME_EMPTY",
                        "BTN_START"
                    };

                    foreach (var key in testKeys)
                    {
                        var entry = table.GetEntry(key);
                        if (entry != null && !string.IsNullOrEmpty(entry.LocalizedValue))
                        {
                            Debug.Log($"[5] ✅ '{key}' = \"{entry.LocalizedValue}\"");
                        }
                        else if (entry != null)
                        {
                            Debug.LogWarning($"[5] ⚠️ '{key}' 엔트리 존재하지만 LocalizedValue가 비어있음!");
                        }
                        else
                        {
                            Debug.LogError($"[5] ❌ '{key}' 엔트리를 찾을 수 없음!");
                        }
                    }
                }
            }
            else
            {
                Debug.LogError($"[4] ❌ 테이블 로드 실패! Status: {tableOp.Status}");
                if (tableOp.OperationException != null)
                {
                    Debug.LogError($"    Exception: {tableOp.OperationException.Message}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[4] ❌ 예외 발생: {e.Message}\n{e.StackTrace}");
        }

        // 6. GetTable (동기) 테스트
        Debug.Log("[6] GetTable (동기) 테스트...");
        try
        {
            var table = LocalizationSettings.StringDatabase.GetTable("UI_Texts");
            if (table != null)
            {
                Debug.Log($"[6] ✅ 동기 로드 성공: 엔트리 수 = {table.Count}");
            }
            else
            {
                Debug.LogError("[6] ❌ 동기 로드 결과 NULL!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[6] ❌ 동기 로드 예외: {e.Message}");
        }

        Debug.Log("===== [LocalizationDebugger] 진단 완료 =====");
    }
}
