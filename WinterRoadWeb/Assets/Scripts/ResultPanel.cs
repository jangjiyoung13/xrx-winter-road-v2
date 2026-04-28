using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Runtime.InteropServices;

public class ResultPanel : MonoBehaviour
{
    // WebGL JavaScript 함수 선언
#if UNITY_WEBGL && !UNITY_EDITOR
    // [공통] iOS 플랫폼 감지 (1 = iOS, 0 = 그 외)
    [DllImport("__Internal")]
    private static extern int IsIOSPlatform();

    // [iOS 전용] 스크린샷 URL 사전 준비 → HTML 링크에 바인딩
    [DllImport("__Internal")]
    private static extern void PrepareScreenshotURL_iOS(string base64);

    // [iOS 전용] 저장 링크 숨기기 및 Blob URL 정리
    [DllImport("__Internal")]
    private static extern void HideScreenshotLink_iOS();

    // [Android / Desktop 전용] <a download> 방식 즉시 다운로드
    [DllImport("__Internal")]
    private static extern void DownloadScreenshot(string base64);
#endif

    /// <summary>
    /// 런타임 iOS 플랫폼 판별
    /// </summary>
    private bool IsRuntimeIOS()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { return IsIOSPlatform() == 1; }
        catch { return false; }
#else
        return false;
#endif
    }

    [Header("내 결과 카드 (상단)")]
    [SerializeField] private CardResult myCardResult;       // 내 등수 카드 (1장만 사용)

    [Header("순위 결과 리스트 (하단)")]
    [SerializeField] private Transform resultContainer;     // 랭킹 리스트 부모
    [SerializeField] private GameObject resultTextPrefab;   // 랭킹 항목 프리팹 (ResultTextPrefab)

    [Header("버튼")]
    [SerializeField] private Button returnToLobbyButton;
    [SerializeField] private Button captureToScreenButton;
    
    [Header("컨페티 파티클")]
    [SerializeField] private GameObject confettiObject;       // ShinyConfettiRainbowRain 오브젝트
    [SerializeField] private Camera particleCamera;           // ParticleCamera
    
    [Header("Settings")]
    [SerializeField] private float buttonActivationDelay = 2f;
    [SerializeField] private GamePanel gamePanel;              // 게임 패널 (최대콤보 조회용)
    
    private string myPlayerId;
    private List<GameObject> spawnedResultItems = new List<GameObject>();
    private Coroutine buttonActivationCoroutine;
    private Coroutine iosCaptureCoroutine;
    private bool isCapturing = false;
    
    private void Start()
    {
        if (returnToLobbyButton != null)
        {
            returnToLobbyButton.onClick.AddListener(OnReturnToLobby);
        }
        
        if (captureToScreenButton != null)
        {
            captureToScreenButton.onClick.AddListener(OnCaptureScreenshot);
        }
    }
    
    /// <summary>
    /// 게임 결과 표시 (개인전)
    /// </summary>
    public void ShowResults(string winner, List<PlayerRankData> rankings, string currentPlayerId)
    {
        myPlayerId = currentPlayerId;
        
        // 패널 활성화
        gameObject.SetActive(true);
        
        // BGM 페이드아웃 + 결과 효과음 재생
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.FadeBGMVolume(0f, 1.5f);
            SoundManager.Instance.PlaySFXByIndex(0);
        }
        
        // 컨페티 파티클 시작
        PlayConfetti();
        
        // GamePanel 비활성화
        GamePanel gamePanel = FindObjectOfType<GamePanel>(true);
        if (gamePanel != null)
        {
            gamePanel.gameObject.SetActive(false);
            Debug.Log("📋 ResultPanel 표시 → GamePanel 비활성화");
        }
        
        // NicknamePanel 비활성화
        NickNamePanel nickNamePanel = FindObjectOfType<NickNamePanel>(true);
        if (nickNamePanel != null)
        {
            nickNamePanel.gameObject.SetActive(false);
            Debug.Log("📋 ResultPanel 표시 → NickNamePanel 비활성화");
        }
        
#if UNITY_WEBGL && !UNITY_EDITOR
        returnToLobbyButton.gameObject.SetActive(false);

        // ===========================================
        // 플랫폼 분기: iOS는 HTML 링크 방식, 그 외는 Unity 버튼 방식
        // ===========================================
        if (IsRuntimeIOS())
        {
            // [iOS] Unity 버튼 숨기고 HTML 오버레이 링크 사용 (자동 캡처)
            Debug.Log("📱 [iOS] HTML 링크 방식으로 저장 기능 활성화");
            captureToScreenButton.gameObject.SetActive(false);

            if (iosCaptureCoroutine != null)
                StopCoroutine(iosCaptureCoroutine);
            iosCaptureCoroutine = StartCoroutine(PrepareIOSScreenshotCoroutine());
        }
        else
        {
            // [Android / Desktop WebGL] 기존 Unity 버튼 + <a download> 방식
            Debug.Log("📱 [Android/Desktop] Unity 캡처 버튼 방식 유지");
            captureToScreenButton.gameObject.SetActive(true);
        }
#else
        captureToScreenButton.gameObject.SetActive(false);
        returnToLobbyButton.gameObject.SetActive(true);
#endif
        
        // 돌아가기 버튼 딜레이
        if (returnToLobbyButton != null)
        {
            returnToLobbyButton.interactable = false;
            
            if (buttonActivationCoroutine != null)
                StopCoroutine(buttonActivationCoroutine);
            
            buttonActivationCoroutine = StartCoroutine(ActivateButtonAfterDelay());
        }

        // 이전 결과 정리
        ClearResults();

        // ====================================
        // 상단: 내 등수 카드
        // ====================================
        Debug.Log($"🔍 내 플레이어 ID: '{myPlayerId}' (길이: {myPlayerId?.Length ?? 0})");
        for (int i = 0; i < rankings.Count; i++)
        {
            Debug.Log($"   rankings[{i}].playerId: '{rankings[i].playerId}' (길이: {rankings[i].playerId?.Length ?? 0}) == myPlayerId? {rankings[i].playerId == myPlayerId}");
        }
        
        PlayerRankData myData = rankings.FirstOrDefault(r => r.playerId == myPlayerId);
        int myRank = 0;
        
        if (myData != null)
        {
            myRank = rankings.IndexOf(myData) + 1;
            Debug.Log($"🎯 내 순위: {myRank}위, 점수: {myData.score}점");
        }
        else
        {
            Debug.LogWarning($"⚠️ rankings에서 내 playerId '{myPlayerId}'를 찾을 수 없습니다!");
        }
        
        if (myCardResult != null)
        {
            if (myData != null)
            {
                bool isWinner = (myRank == 1);
                int maxCombo = (gamePanel != null) ? gamePanel.MaxCombo : 0;
                myCardResult.SetCardData(myRank, isWinner, myData.nickname, myData.score, maxCombo, myData.element);
                myCardResult.gameObject.SetActive(true);
            }
            else
            {
                // 내 데이터를 찾지 못한 경우
                myCardResult.SetCardData(0, false, "Unknown", 0, 0, "None");
                myCardResult.gameObject.SetActive(true);
            }
        }

        // ====================================
        // 하단: 순위 결과 리스트
        // ====================================
        if (resultTextPrefab == null)
        {
            Debug.LogError("❌ resultTextPrefab이 할당되지 않았습니다! Inspector에서 확인하세요.");
        }
        else if (resultContainer == null)
        {
            Debug.LogError("❌ resultContainer가 할당되지 않았습니다! Inspector에서 확인하세요.");
        }
        else
        {
            Debug.Log($"📋 순위 리스트 생성 시작 - {rankings.Count}명");
            
            for (int i = 0; i < rankings.Count; i++)
            {
                PlayerRankData rankData = rankings[i];
                int rank = i + 1;
                
                Debug.Log($"   [{rank}위] {rankData.nickname} - {rankData.score}점 (playerId: {rankData.playerId})");
                
                GameObject resultItem = Instantiate(resultTextPrefab, resultContainer);
                ResultTextPrefab resultText = resultItem.GetComponent<ResultTextPrefab>();
                
                if (resultText != null)
                {
                    bool isMyResult = (rankData.playerId == myPlayerId);
                    resultText.SetData(rank, rankData.nickname, rankData.score, isMyResult, rankData.element);
                }
                else
                {
                    Debug.LogError($"❌ resultTextPrefab에서 ResultTextPrefab 컴포넌트를 찾을 수 없습니다!");
                }
                
                spawnedResultItems.Add(resultItem);
            }
        }
        
        Debug.Log($"✅ 결과 화면 표시 완료 - 내 순위: {myRank}위, 전체 {rankings.Count}명");
    }
    
    /// <summary>
    /// 이전 결과 정리
    /// </summary>
    private void ClearResults()
    {
        foreach (GameObject item in spawnedResultItems)
        {
            if (item != null)
                Destroy(item);
        }
        spawnedResultItems.Clear();
    }
    
    /// <summary>
    /// 일정 시간 후 돌아가기 버튼 활성화
    /// </summary>
    private IEnumerator ActivateButtonAfterDelay()
    {
        yield return new WaitForSeconds(buttonActivationDelay);
        
        if (returnToLobbyButton != null)
        {
            returnToLobbyButton.gameObject.SetActive(true);
            returnToLobbyButton.interactable = true;
        }
        
        buttonActivationCoroutine = null;
    }
    
    /// <summary>
    /// 대기방으로 돌아가기
    /// </summary>
    private void OnReturnToLobby()
    {
        Debug.Log("🔄 결과창 닫기");

        // BGM 볼륨 복원
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.RestoreBGMVolume(1f);
        }

        if (buttonActivationCoroutine != null)
        {
            StopCoroutine(buttonActivationCoroutine);
            buttonActivationCoroutine = null;
        }

        // [iOS 전용] 캡처 코루틴 취소 + HTML 링크 정리
        CleanupIOSScreenshotLink();
        
        GamePanel gamePanel = FindObjectOfType<GamePanel>(true);
        
        WebGLProgram webGLProgram = FindObjectOfType<WebGLProgram>(true);
        if (webGLProgram != null)
            webGLProgram.OnResultPanelClosed();
        
        // NicknamePanel 활성화
        NickNamePanel nickNamePanel = FindObjectOfType<NickNamePanel>(true);
        if (nickNamePanel != null)
        {
            nickNamePanel.gameObject.SetActive(true);
            Debug.Log("📋 ResultPanel 닫기 → NickNamePanel 활성화");
        }
        
        StopConfetti();
        gameObject.SetActive(false);
    }
    
    /// <summary>
    /// 결과 패널 숨기기
    /// </summary>
    public void Hide()
    {
        if (buttonActivationCoroutine != null)
        {
            StopCoroutine(buttonActivationCoroutine);
            buttonActivationCoroutine = null;
        }

        // BGM 볼륨 복원
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.RestoreBGMVolume(1f);
        }

        // [iOS 전용] 캡처 코루틴 취소 + HTML 링크 정리
        CleanupIOSScreenshotLink();

        StopConfetti();
        gameObject.SetActive(false);
        ClearResults();
    }

    /// <summary>
    /// [iOS 전용] 자동 캡처 코루틴 중단 및 HTML 저장 링크 숨김 + Blob URL 정리
    /// </summary>
    private void CleanupIOSScreenshotLink()
    {
        if (iosCaptureCoroutine != null)
        {
            StopCoroutine(iosCaptureCoroutine);
            iosCaptureCoroutine = null;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        if (IsRuntimeIOS())
        {
            try { HideScreenshotLink_iOS(); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"⚠️ [iOS] 저장 링크 정리 중 오류(무시 가능): {e.Message}");
            }
        }
#endif
    }
    
    /// <summary>
    /// [Android / Desktop 전용] Unity 캡처 버튼 클릭 핸들러
    /// iOS는 버튼 자체가 비활성 상태이며 HTML 링크를 통해 저장함
    /// </summary>
    private void OnCaptureScreenshot()
    {
        if (isCapturing) return;

        Debug.Log("📸 [Android/Desktop] 화면 캡처 시작...");
        StartCoroutine(CaptureScreenCoroutine());
    }

    /// <summary>
    /// [iOS 전용] 결과창 표시 직후 자동으로 캡처를 수행하여
    /// HTML 오버레이 링크에 Blob URL을 미리 바인딩한다.
    /// Unity 버튼 클릭 → window.open 경로가 Safari 팝업 차단에 걸리기 때문에
    /// 사용자가 HTML <a> 태그를 직접 탭하도록 사전 준비하는 방식.
    /// </summary>
    private IEnumerator PrepareIOSScreenshotCoroutine()
    {
        // 컨페티 연출 + 결과 UI 스폰 완료 대기
        yield return new WaitForSeconds(1.5f);
        yield return new WaitForEndOfFrame();

        Debug.Log("📸 [iOS] 자동 캡처 시작");

        Texture2D screenshot = null;
        try
        {
            screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [iOS] 캡처 실패: {e.Message}");
            iosCaptureCoroutine = null;
            yield break;
        }

        if (screenshot == null)
        {
            Debug.LogError("❌ [iOS] 캡처 결과 텍스처가 null입니다");
            iosCaptureCoroutine = null;
            yield break;
        }

        byte[] pngData = screenshot.EncodeToPNG();
        string base64 = System.Convert.ToBase64String(pngData);
        Destroy(screenshot);

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            PrepareScreenshotURL_iOS(base64);
            Debug.Log("✅ [iOS] HTML 저장 링크 활성화 완료");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [iOS] URL 준비 중 오류: {e.Message}");
        }
#endif

        iosCaptureCoroutine = null;
    }
    
    private IEnumerator CaptureScreenCoroutine()
    {
        isCapturing = true;
        
        if (captureToScreenButton != null)
            captureToScreenButton.interactable = false;
        
        yield return new WaitForEndOfFrame();
        
        try
        {
            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            
            if (screenshot != null)
            {
                byte[] pngData = screenshot.EncodeToPNG();
                string base64 = System.Convert.ToBase64String(pngData);
                Destroy(screenshot);
                
#if UNITY_WEBGL && !UNITY_EDITOR
                try
                {
                    DownloadScreenshot(base64);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ WebGL 스크린샷 실패: {e.Message}");
                }
#else
                string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string path = System.IO.Path.Combine(Application.persistentDataPath, $"WinterRoad_{timestamp}.png");
                System.IO.File.WriteAllBytes(path, pngData);
                Debug.Log($"💾 스크린샷 저장: {path}");
#endif
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 캡처 오류: {e.Message}");
        }
        
        yield return new WaitForSeconds(1f);
        
        if (captureToScreenButton != null)
            captureToScreenButton.interactable = true;
        
        if (returnToLobbyButton != null)
        {
            returnToLobbyButton.gameObject.SetActive(true);
            returnToLobbyButton.interactable = true;
        }
        
        isCapturing = false;
    }
    
    /// <summary>
    /// 컨페티 파티클과 파티클 카메라를 활성화합니다.
    /// </summary>
    private void PlayConfetti()
    {
        if (confettiObject != null)
        {
            confettiObject.SetActive(true);
            
            ParticleSystem ps = confettiObject.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Clear();
                ps.Play(true);
            }
        }
        
        if (particleCamera != null)
        {
            particleCamera.gameObject.SetActive(true);
        }
        
        Debug.Log("🎊 컨페티 파티클 시작!");
    }
    
    /// <summary>
    /// 컨페티 파티클과 파티클 카메라를 비활성화합니다.
    /// </summary>
    private void StopConfetti()
    {
        if (confettiObject != null)
        {
            ParticleSystem ps = confettiObject.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true);
            }
            confettiObject.SetActive(false);
        }
        
        if (particleCamera != null)
        {
            particleCamera.gameObject.SetActive(false);
        }
        
        Debug.Log("🎊 컨페티 파티클 정지!");
    }
    
    private void OnDestroy()
    {
        if (buttonActivationCoroutine != null)
        {
            StopCoroutine(buttonActivationCoroutine);
            buttonActivationCoroutine = null;
        }

        if (iosCaptureCoroutine != null)
        {
            StopCoroutine(iosCaptureCoroutine);
            iosCaptureCoroutine = null;
        }
    }
}

/// <summary>
/// 플레이어 순위 데이터 클래스 (개인전)
/// </summary>
[System.Serializable]
public class PlayerRankData
{
    public string playerId;
    public string nickname;
    public string element; // 서버에서 전송: "Joy" | "Sadness" | "Courage" | "Love" | "Hope" | "Friendship" | "None"
    public int score;
    public int pressCount;

    public PlayerRankData(string id, string name, int playerScore, int count)
    {
        playerId = id;
        nickname = name;
        score = playerScore;
        pressCount = count;
        element = "None";
    }
}
