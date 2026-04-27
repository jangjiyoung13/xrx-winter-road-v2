using UnityEngine;

public class Program : MonoBehaviour
{
    [Header("게임 관리")]
    [SerializeField] private GameAdminManager gameAdminManager;
    [SerializeField] private AdminUIController adminUIController;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log("Winter Road Desktop 게임 관리 시스템 시작");
        
        // AdminUIController 먼저 초기화 (UI 생성)
        InitializeAdminUI();
        
        // GameAdminManager 초기화 (UI가 생성된 후)
        InitializeGameAdminManager();
    }
    
    private void InitializeAdminUI()
    {
        if (adminUIController == null)
        {
            adminUIController = FindObjectOfType<AdminUIController>();
            
            if (adminUIController == null)
            {
                // AdminUIController 컴포넌트를 동적으로 추가
                GameObject uiObject = new GameObject("AdminUIController");
                adminUIController = uiObject.AddComponent<AdminUIController>();
                Debug.Log("AdminUIController가 동적으로 생성되었습니다.");
            }
            else
            {
                Debug.Log("기존 AdminUIController를 찾았습니다.");
            }
        }
    }
    
    private void InitializeGameAdminManager()
    {
        if (gameAdminManager == null)
        {
            gameAdminManager = FindObjectOfType<GameAdminManager>();
            
            if (gameAdminManager == null)
            {
                Debug.Log("GameAdminManager를 찾을 수 없습니다. AdminUIController가 생성할 것입니다.");
            }
            else
            {
                Debug.Log("기존 GameAdminManager를 찾았습니다.");
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        // 현재는 특별한 업데이트 로직이 필요하지 않음
        // GameAdminManager가 자체적으로 업데이트를 처리함
    }
    
    void OnApplicationQuit()
    {
        Debug.Log("Winter Road Desktop 게임 관리 시스템 종료");
    }
}
