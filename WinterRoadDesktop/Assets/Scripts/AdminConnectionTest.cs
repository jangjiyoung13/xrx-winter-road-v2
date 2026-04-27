using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 관리자 연결 테스트를 위한 간단한 컴포넌트
/// </summary>
public class AdminConnectionTest : MonoBehaviour
{
    [Header("테스트 UI")]
    [SerializeField] private Button testConnectionButton;
    [SerializeField] private TMP_Text testResultText;
    
    private GameAdminManager adminManager;

    void Start()
    {
        // GameAdminManager 찾기
        adminManager = FindObjectOfType<GameAdminManager>();
        
        if (testConnectionButton != null)
        {
            testConnectionButton.onClick.AddListener(TestConnection);
        }
        
        if (testResultText != null)
        {
            testResultText.text = "연결 테스트 준비";
        }
    }

    public void TestConnection()
    {
        if (adminManager == null)
        {
            UpdateTestResult("❌ GameAdminManager를 찾을 수 없습니다.");
            return;
        }

        UpdateTestResult("🔄 서버 연결 테스트 중...");
        
        // 서버 연결 상태 확인
        if (adminManager.connectionStatusText != null)
        {
            string connectionStatus = adminManager.connectionStatusText.text;
            if (connectionStatus.Contains("연결됨"))
            {
                UpdateTestResult("✅ 서버 연결 성공!");
            }
            else if (connectionStatus.Contains("연결 중"))
            {
                UpdateTestResult("⏳ 서버 연결 중... 잠시 후 다시 시도해주세요.");
            }
            else
            {
                UpdateTestResult("❌ 서버 연결 실패. 재연결을 시도해주세요.");
            }
        }
        else
        {
            UpdateTestResult("❌ UI가 초기화되지 않았습니다.");
        }
    }

    private void UpdateTestResult(string message)
    {
        if (testResultText != null)
        {
            testResultText.text = message;
        }
        
        Debug.Log($"[ConnectionTest] {message}");
    }

    // 수동으로 재연결 시도
    public void ForceReconnect()
    {
        if (adminManager != null)
        {
            adminManager.Reconnect();
            UpdateTestResult("🔄 재연결 시도 중...");
        }
        else
        {
            UpdateTestResult("❌ GameAdminManager를 찾을 수 없습니다.");
        }
    }
}
