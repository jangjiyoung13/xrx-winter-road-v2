using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using LeTai.TrueShadow;

public class GamePanel : MonoBehaviour
{
    [Header("닉네임 설정")]
    [SerializeField] private NickNamePanel nicknamePanel;


    [Header("게임화면 UI")]
    [SerializeField] private GameObject gameUiContent;
    [SerializeField] private GameObject gameCrystal;
    [SerializeField] private Button pressButton;
    
    public Button PressButton => pressButton;

    [Header("참여자 영역")]
    [SerializeField] private Transform playerContent; // 개인전 참여 인원 Instantiate용 Content
    [SerializeField] private ScrollRect playerScrollRect; // 플레이어 목록 스크롤뷰
    
    [Header("스크롤 설정")]
    [SerializeField] private int visiblePlayerCount = 5; // 화면에 보이는 플레이어 수
    
    [Header("게임 상태")]
    [SerializeField] private Text gameTimerText;
    [SerializeField] private Text totalPlayersText;
    
    [Header("내 순위 표시")]
    [SerializeField] private Text myRankText;           // 내 현재 등수 표시 텍스트

    [Header("게임 진행 프로그레스 바")]
    [SerializeField] private Slider gameProgressBar;

    [Header("터치 파티클")]
    [SerializeField] private ParticleSystem touchParticle;   // UIParticle이 붙은 파티클
    [SerializeField] private ParticleSystem[] breakParticles;  // 구슬 깨지는 파티클 배열 (랜덤 재생)
    [SerializeField] private RectTransform canvasRectTransform;  // Canvas_UI의 RectTransform
    [SerializeField] private Camera uiCamera;               // Screen Space - Camera일 때 사용하는 카메라 (Overlay면 null)

    [Header("터치 사운드")]
    [SerializeField] private AudioSource touchAudioSource;   // 버튼 터치 사운드 AudioSource
    [SerializeField] private float touchPitchMin = 0.9f;     // 피치 랜덤 최소값
    [SerializeField] private float touchPitchMax = 1.1f;     // 피치 랜덤 최대값

    [Header("추월 연출 오브젝트")]
    [SerializeField] private GameObject rankUpEffectObject;        // 추월 시 활성화되는 오브젝트
    [SerializeField] private GameObject rankDownEffectObject;      // 추월당함 시 활성화되는 오브젝트
    [SerializeField] private float overtakeEffectDuration = 2f;    // 오브젝트 활성화 지속 시간 (초)

    [Header("비네팅 오버레이")]
    [SerializeField] private Image vignetteOverlay;           // VignetteOverlay UI Image
    [SerializeField] private float vignetteFlashDuration = 0.5f;  // 플래시 지속 시간


    [Header("구체 스피어 (크리스탈볼)")]
    [SerializeField] private RectTransform crystalBall;      // 크리스탈볼 RawImage의 RectTransform
    [SerializeField] private BallImpactEffect ballImpactEffect;  // 구슬 타격 펀치 효과

    [Header("구슬 컨테이너 쉐이크")]
    [SerializeField] private RectTransform ballContainer;    // 쉐이크시킬 구슬 컨테이너 (구슬의 부모 권장)
    [SerializeField] private float containerShakeAmount = 2f;    // 쉐이크 최대 이동 거리 (px)
    [SerializeField] private float containerShakeDuration = 0.1f; // 쉐이크 지속 시간 (초)
    
    [Header("스피어 진동 설정")]
    [SerializeField] private float vibrateIntensity = 10f;    // 진동 강도 (px)
    [SerializeField] private float vibrateDuration = 0.3f;   // 진동 지속 시간
    [SerializeField] private AnimationCurve vibrateCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);  // 진동 곡선

    [Header("콤보 시스템")]
    [SerializeField] private GameObject comboUI;              // 콤보 표시 UI 루트 (2콤보 이상일 때 active)
    [SerializeField] private Text comboText;       // 콤보 숫자 텍스트
    [SerializeField] private TrueShadow comboGlowShadow;      // 콤보 UI의 TrueShadow (글로우 효과)
    [Tooltip("콤보 유지 시간 (초) - 이 시간 내에 다시 누르지 않으면 초기화")]
    [SerializeField] private float comboDuration = 2.0f;
    
    // 콤보 내부 상태
    private int comboCount = 0;
    private int maxComboCount = 0;
    private float comboTimer = 0f;
    private bool comboActive = false;
    // 서버가 마지막으로 확정한 내 점수 — 콤보는 이 값의 delta로 증가. 로컬 터치는 카운트 안 함.
    private int lastConfirmedScore = 0;

    /// <summary>
    /// 현재 게임에서 달성한 최대 콤보 수
    /// </summary>
    public int MaxCombo => maxComboCount;

    [Header("프리팹")]
    [SerializeField] private GameObject playerInfoPrefab;
    
    // 플레이어 정보 관리
    private Dictionary<string, PlayerInfoUI> playerInfoUIs = new Dictionary<string, PlayerInfoUI>();
    
    // 현재 플레이어 정보
    private string currentPlayerId = "";

    
    // 현재 게임 상태
    private int remainingTime = 0;
    private int totalGameDuration = 0;
    
    // 스피어 진동 관련
    private Vector2 crystalBallOriginalPos;
    private Vector3 crystalBallOriginalScale;
    private bool isVibrating = false;
    private Coroutine sphereBounceCoroutine;

    // 구슬 컨테이너 쉐이크 관련
    private Vector2 ballContainerOriginalPos;
    private bool ballContainerOriginalPosSaved = false;
    private Coroutine ballContainerShakeCoroutine;
    
    // 타이머 떨림 관련
    private int previousTimerSecond = -1;
    private Coroutine timerShakeCoroutine;
    private Vector2 timerOriginalPos;
    private Vector3 timerOriginalScale;
    private bool timerOriginalPosSaved = false;
    private Color timerOriginalColor;
    
    // 터치 카운트 (구슬 깨짐 파티클용)
    private int touchPressCount = 0;
    
    // 등수 변화 감지
    private int previousRank = -1;
    private Coroutine vignetteFlashCoroutine;
    private Coroutine rankUpEffectCoroutine;
    private Coroutine rankDownEffectCoroutine;
    
    private void Start()
    {
        InitializeUI();
    }
    
    private void Update()
    {
        // 콤보 타이머 처리
        if (comboActive)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f)
            {
                ResetCombo();
            }
        }
    }
    
    private void InitializeUI()
    {
        // 초기 상태 설정
        UpdateTimer(0);
        UpdateTotalPlayersCount(0, 0);
        
        // 결과창 초기에 숨기기
        HideResultPanel();

        // 콤보 UI 초기 비활성화
        ResetCombo();

        // 게임 UI 오브젝트 초기에 숨기기
        DeactivateGameUI();
        
        // 스피어 원래 위치 저장
        if (crystalBall != null)
        {
            crystalBallOriginalPos = crystalBall.anchoredPosition;
            crystalBallOriginalScale = crystalBall.localScale;
            Debug.Log($"🌍 크리스탈볼 원래 위치 저장: {crystalBallOriginalPos}");
        }
    }
    

    
    public void UpdateTimer(int timeInSeconds)
    {
        remainingTime = timeInSeconds;
        
        if (gameTimerText != null)
        {
            int minutes = timeInSeconds / 60;
            int seconds = timeInSeconds % 60;
            gameTimerText.text = $"{minutes:00}:{seconds:00}";
            
            // 10초 이하일 때 떨림 효과
            if (timeInSeconds <= 10 && timeInSeconds > 0)
            {
                // 초가 바뀔 때만 떨림 발동
                if (timeInSeconds != previousTimerSecond)
                {
                    ShakeTimerText(timeInSeconds);
                }
            }
            else
            {
                // 10초 초과이면 원래 상태로 복원
                ResetTimerAppearance();
            }
            
            previousTimerSecond = timeInSeconds;
        }
        
        // 프로그레스 바 업데이트
        UpdateProgressBar();
    }
    
    /// <summary>
    /// 전체 게임 시간을 설정합니다 (프로그레스 바 비율 계산용).
    /// </summary>
    /// <param name="duration">전체 게임 시간 (초)</param>
    public void SetTotalGameDuration(int duration)
    {
        totalGameDuration = duration;
        Debug.Log($"⏱️ 전체 게임 시간 설정: {totalGameDuration}초");
        
        // 프로그레스 바 초기값 설정 (0에서 시작)
        if (gameProgressBar != null)
        {
            gameProgressBar.value = 0f;
        }
    }
    
    /// <summary>
    /// 프로그레스 바 값을 업데이트합니다 (0에서 1로 증가).
    /// </summary>
    private void UpdateProgressBar()
    {
        if (gameProgressBar == null || totalGameDuration <= 0) return;
        
        // 경과 시간 비율로 계산 (0 → 1로 증가)
        float elapsed = 1f - ((float)remainingTime / totalGameDuration);
        gameProgressBar.value = Mathf.Clamp01(elapsed);
    }
    
    public void UpdateTotalPlayersCount(int activePlayers, int observers)
    {
        if (totalPlayersText != null)
        {
            totalPlayersText.text = $"👥 플레이어: {activePlayers}명 | 👁️ 관전자: {observers}명";
        }
        
        Debug.Log($"👥 플레이어 수 업데이트 - 활성: {activePlayers}, 관전자: {observers}");
    }

    public void ResetScore()
    {
        // 개인전 모드에서는 팀 점수 초기화 불필요
    }
    
    /// <summary>
    /// 현재 플레이어 ID를 설정합니다.
    /// </summary>
    /// <param name="playerId">현재 플레이어의 ID</param>
    public void SetCurrentPlayerId(string playerId)
    {
        currentPlayerId = playerId;
        Debug.Log($"🆔 현재 플레이어 ID 설정: {currentPlayerId}");
        
        // 기존의 모든 플레이어 UI에 대해 나 태그 업데이트
        foreach (var kvp in playerInfoUIs)
        {
            if (kvp.Value != null)
            {
                kvp.Value.UpdateMeTag(kvp.Key == currentPlayerId);
            }
        }
    }
    
    /// <summary>
    /// 현재 플레이어의 팀을 설정합니다.
    /// </summary>
    /// <param name="team">현재 플레이어의 팀</param>
    public void SetCurrentPlayerTeam(string team)
    {
        Debug.Log($"👥 현재 플레이어 팀 설정: {team}");
    }
    
    // 간단한 플레이어 정보 업데이트 (WebGL에서는 복잡한 JSON 파싱 대신 간단한 방식 사용)
    public void AddOrUpdatePlayer(string playerId, string playerName, string team, int score, bool connected, bool isAdmin = false, bool isObserver = false)
    {
        Debug.Log($"AddOrUpdatePlayer called - ID: {playerId}, Name: {playerName}, Team: {team}, Score: {score}, Connected: {connected}, Admin: {isAdmin}, Observer: {isObserver}");
        
        // 컨테이너 선택 (개인전 - 모든 플레이어를 playerContent에 배치)
        Transform targetContainer = playerContent;
        
        if (targetContainer == null)
        {
            Debug.LogError($"❌ Target container is null for player {playerName} (team: {team})");
            return;
        }
        
        if (playerInfoPrefab == null)
        {
            Debug.LogError($"❌ PlayerInfo prefab is null!");
            return;
        }
        
        Debug.Log($"✅ Container and prefab check passed for {playerName}");
        
        // 기존 플레이어 UI 업데이트 또는 새로 생성
        if (playerInfoUIs.ContainsKey(playerId))
        {
            Debug.Log($"🔄 Updating existing player UI for {playerName}");
            PlayerInfoUI existingUI = playerInfoUIs[playerId];
            if (existingUI != null)
            {
                bool isMe = playerId == currentPlayerId;
                Debug.Log($"🔍 기존 플레이어 UI 업데이트 - currentPlayerId: '{currentPlayerId}', playerId: '{playerId}', isMe: {isMe}");

                // 현재 플레이어의 팀 정보 저장
                if (isMe)
                {
                    SetCurrentPlayerTeam(team);
                    // 서버 확정 점수로 콤보 업데이트 (delta만큼 콤보 증가, "🔥 N COMBO!" 텍스트)
                    ConfirmComboFromServer(score);
                }

                existingUI.SetPlayerInfo(playerId, playerName, team, score, connected, isAdmin, isObserver);
                existingUI.UpdateMeTag(isMe);
                
                // 점수 변경 시 정렬 및 등수 업데이트
                SortPlayersByScore();
                UpdateMyRank();
                
                Debug.Log($"✅ Player UI updated for {playerName} (isMe: {isMe})");
            }
            else
            {
                Debug.LogWarning($"⚠️ Existing UI is null for {playerName}, removing from dictionary");
                playerInfoUIs.Remove(playerId);
            }
        }
        else
        {
            Debug.Log($"🆕 Creating new player UI for {playerName} in container: {targetContainer.name}");
            
            try
            {
                GameObject playerObj = Instantiate(playerInfoPrefab, targetContainer);
                Debug.Log($"✅ GameObject instantiated for {playerName}");
                
                PlayerInfoUI playerUI = playerObj.GetComponent<PlayerInfoUI>();
                
            if (playerUI != null)
            {
                bool isMe = playerId == currentPlayerId;
                Debug.Log($"🔍 새 플레이어 UI 생성 - currentPlayerId: '{currentPlayerId}', playerId: '{playerId}', isMe: {isMe}");

                // 현재 플레이어의 팀 정보 저장
                if (isMe)
                {
                    SetCurrentPlayerTeam(team);
                    // 서버 확정 점수로 콤보 업데이트
                    ConfirmComboFromServer(score);
                }

                playerUI.SetPlayerInfo(playerId, playerName, team, score, connected, isAdmin, isObserver);
                playerUI.UpdateMeTag(isMe);
                playerInfoUIs[playerId] = playerUI;
                Debug.Log($"✅ Player UI created and configured for {playerName} (isMe: {isMe})");
            }
                else
                {
                    Debug.LogError($"❌ PlayerInfoUI component not found on prefab for {playerName}");
                    Destroy(playerObj);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Error creating player UI for {playerName}: {e.Message}");
                Debug.LogError($"❌ Stack trace: {e.StackTrace}");
            }
        }
        
        // 점수순 정렬 후 내 등수 업데이트 및 스크롤
        SortPlayersByScore();
        UpdateMyRank();
        ScrollToMyPlayer();
    }
    
    public void RemovePlayer(string playerId)
    {
        if (playerInfoUIs.ContainsKey(playerId))
        {
            PlayerInfoUI playerUI = playerInfoUIs[playerId];
            if (playerUI != null && playerUI.gameObject != null)
            {
                Destroy(playerUI.gameObject);
            }
            playerInfoUIs.Remove(playerId);
        }
    }
    
    public void ClearAllPlayers()
    {
        foreach (var playerUI in playerInfoUIs.Values)
        {
            if (playerUI != null && playerUI.gameObject != null)
            {
                Destroy(playerUI.gameObject);
            }
        }
        playerInfoUIs.Clear();
    }
    
    /// <summary>
    /// 게임 종료 시 결과 로그 (실제 결과창은 ResultPanel 컴포넌트에서 처리)
    /// </summary>
    /// <param name="winner">우승자 이름 (개인전)</param>
    public void ShowGameResult(string winner)
    {
        // 개인전 우승자 메시지
        string resultMessage = "";
        if (winner == "Draw" || string.IsNullOrEmpty(winner))
        {
            resultMessage = "DRAW!";
        }
        else
        {
            resultMessage = $"{winner} WIN!";
        }
        
        Debug.Log($"🏆 게임 결과 표시 - 우승자: {winner}, 메시지: {resultMessage} (개인전)");
        
        // 실제 결과창 표시는 ResultPanel 컴포넌트에서 처리
        // (WebGLProgram.HandleGameEnd에서 resultPanel.ShowResults() 호출)
    }
    
    /// <summary>
    /// 결과창을 숨깁니다. (실제 결과창 숨김은 ResultPanel 컴포넌트에서 처리)
    /// </summary>
    public void HideResultPanel()
    {
        // 실제 결과창 숨김은 ResultPanel 컴포넌트에서 처리
        Debug.Log("🙈 결과창 숨김 요청 (ResultPanel.Hide() 호출 필요)");
    }
    

    
    /// <summary>
    /// 게임 UI를 활성화합니다.
    /// </summary>
    public void ActivateGameUI()
    {
        if (gameUiContent != null) gameUiContent.SetActive(true);
        if (gameCrystal != null) gameCrystal.SetActive(true);
        
        // 프로그레스 바 활성화
        if (gameProgressBar != null) gameProgressBar.gameObject.SetActive(true);
        
        // 최대 콤보 초기화 (새 게임 시작)
        maxComboCount = 0;
        
        Debug.Log("✅ 게임 UI 활성화 완료");
    }
    
    /// <summary>
    /// 게임 UI를 비활성화합니다.
    /// </summary>
    public void DeactivateGameUI()
    {
        if (gameUiContent != null) gameUiContent.SetActive(false);
        if (gameCrystal != null) gameCrystal.SetActive(false);
        
        // 프로그레스 바 비활성화
        if (gameProgressBar != null) gameProgressBar.gameObject.SetActive(false);
        
        // 콤보 초기화
        ResetCombo();
        
        Debug.Log("🙈 게임 UI 비활성화 완료");
    }
    
    /// <summary>
    /// 터치/클릭 위치에 파티클을 재생합니다.
    /// </summary>
    public void PlayTouchParticle()
    {
        if (touchParticle == null) return;
        
        // 마우스/터치 위치를 Canvas 로컬 좌표로 변환
        Vector2 screenPos = Input.mousePosition;
        Vector2 localPoint;
        
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRectTransform, screenPos, uiCamera, out localPoint);
        
        // 파티클 위치 설정
        touchParticle.transform.localPosition = new Vector3(localPoint.x, localPoint.y, 0f);
        
        // 파티클 재생
        touchParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        touchParticle.Play();

        PlayTouchSound();

        // 타격감: 구슬 컨테이너 미세 쉐이크
        ShakeBallContainer();

        // 5번째 터치마다 구슬 깨지는 파티클 랜덤 재생
        touchPressCount++;
        if (touchPressCount % 5 == 0 && breakParticles != null && breakParticles.Length > 0)
        {
            int idx = Random.Range(0, breakParticles.Length);
            if (breakParticles[idx] != null)
            {
                breakParticles[idx].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                breakParticles[idx].Play();
                SoundManager.Instance.PlayCrystalBreakSFX();
                Debug.Log($"💎 구슬 깨짐 파티클[{idx}] 재생! (터치 {touchPressCount}회)");
            }
        }
    }
    
    private void PlayTouchSound()
    {
        if (touchAudioSource == null) return;

        touchAudioSource.pitch = Random.Range(touchPitchMin, touchPitchMax);
        touchAudioSource.Stop();
        touchAudioSource.Play();
    }

    /// <summary>
    /// 구슬 컨테이너를 짧게 미세 쉐이크. 터치 타격감 연출.
    /// </summary>
    private void ShakeBallContainer()
    {
        if (ballContainer == null) return;

        if (!ballContainerOriginalPosSaved)
        {
            ballContainerOriginalPos = ballContainer.anchoredPosition;
            ballContainerOriginalPosSaved = true;
        }

        if (ballContainerShakeCoroutine != null) StopCoroutine(ballContainerShakeCoroutine);
        ballContainerShakeCoroutine = StartCoroutine(ShakeBallContainerCoroutine());
    }

    private IEnumerator ShakeBallContainerCoroutine()
    {
        float elapsed = 0f;
        while (elapsed < containerShakeDuration)
        {
            elapsed += Time.deltaTime;
            float decay = 1f - (elapsed / containerShakeDuration);
            float ox = Random.Range(-1f, 1f) * containerShakeAmount * decay;
            float oy = Random.Range(-1f, 1f) * containerShakeAmount * decay;
            ballContainer.anchoredPosition = ballContainerOriginalPos + new Vector2(ox, oy);
            yield return null;
        }

        ballContainer.anchoredPosition = ballContainerOriginalPos;
        ballContainerShakeCoroutine = null;
    }

    /// <summary>
    /// 스피어를 진동시킵니다.
    /// </summary>
    public void VibrateSphere()
    {
        if (crystalBall == null)
        {
            Debug.LogWarning("⚠ CrystalBall이 설정되지 않았습니다!");
            return;
        }
        
        if (isVibrating)
        {
            Debug.Log(" 이미 진동 중입니다. 기존 진동을 중단하고 새로 시작합니다.");
            StopAllCoroutines();
        }
        
        StartCoroutine(VibrateCoroutine());
        BounceSphere();
        
        // 방향 펀치 효과 (콤보 연동)
        if (ballImpactEffect != null)
        {
            ballImpactEffect.Punch(comboCount);
        }
    }
    
    /// <summary>
    /// 스피어 진동 코루틴 (UI RectTransform 기반)
    /// </summary>
    private IEnumerator VibrateCoroutine()
    {
        isVibrating = true;
        float elapsedTime = 0f;
        
        while (elapsedTime < vibrateDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / vibrateDuration;
            
            float curveValue = vibrateCurve.Evaluate(progress);
            
            // 랜덤한 방향으로 UI 진동 (px 단위)
            Vector2 randomOffset = new Vector2(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ) * vibrateIntensity * curveValue;
            
            crystalBall.anchoredPosition = crystalBallOriginalPos + randomOffset;
            
            yield return null;
        }
        
        crystalBall.anchoredPosition = crystalBallOriginalPos;
        isVibrating = false;
    }
    
    /// <summary>
    /// 스피어 터치 시 통통 튀는 스케일 바운스 효과
    /// </summary>
    private void BounceSphere()
    {
        if (crystalBall == null) return;
        
        if (sphereBounceCoroutine != null)
            StopCoroutine(sphereBounceCoroutine);
        
        sphereBounceCoroutine = StartCoroutine(SphereBounceCoroutine());
    }
    
    private IEnumerator SphereBounceCoroutine()
    {
        float duration = 0.3f;
        float punchScale = 0.15f;  // 15% 커졌다 돌아옴
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // 탄성 바운스: 빠르게 커졌다가 통통 튀면서 돌아오는 느낌
            float bounce = Mathf.Sin(t * Mathf.PI * 2.5f) * (1f - t) * punchScale;
            crystalBall.localScale = crystalBallOriginalScale * (1f + bounce);
            
            yield return null;
        }
        
        crystalBall.localScale = crystalBallOriginalScale;
        sphereBounceCoroutine = null;
    }
    
    // 현재 상태 반환 메서드들
    public int GetRemainingTime() => remainingTime;
    public int GetPlayerCount() => playerInfoUIs.Count;
    public string GetCurrentPlayerId() => currentPlayerId;
    
    #region 타이머 떨림 효과
    
    /// <summary>
    /// 타이머 텍스트를 흔들어 긴박감을 줍니다. 초가 낮을수록 강하게 떨립니다.
    /// </summary>
    private void ShakeTimerText(int secondsLeft)
    {
        if (gameTimerText == null) return;
        
        RectTransform rt = gameTimerText.GetComponent<RectTransform>();
        if (rt == null) return;
        
        // 원래 위치/스케일/색상 최초 1회 저장
        if (!timerOriginalPosSaved)
        {
            timerOriginalPos = rt.anchoredPosition;
            timerOriginalScale = rt.localScale;
            timerOriginalColor = gameTimerText.color;
            timerOriginalPosSaved = true;
        }
        
        // 기존 코루틴 중단
        if (timerShakeCoroutine != null)
            StopCoroutine(timerShakeCoroutine);
        
        // 초가 낮을수록 강하게 (10초: 약하게, 1초: 강하게)
        float intensityMult = Mathf.Lerp(1.0f, 2.5f, 1f - (secondsLeft - 1f) / 9f);
        
        timerShakeCoroutine = StartCoroutine(TimerShakeCoroutine(rt, intensityMult, secondsLeft));
    }
    
    /// <summary>
    /// 타이머 떨림 코루틴 - 스케일 펀치 + 위치 진동 + 색상 변화
    /// </summary>
    private IEnumerator TimerShakeCoroutine(RectTransform rt, float intensityMult, int secondsLeft)
    {
        float shakeDuration = 0.5f;
        float shakeIntensity = 4f * intensityMult;   // 위치 진동 강도 (px)
        float punchScale = 0.15f * intensityMult;     // 스케일 펀치 크기
        float elapsed = 0f;
        
        // 위급함에 따른 색상 (10초: 주황, 1초: 빨강)
        float urgency = 1f - (secondsLeft - 1f) / 9f; // 0.0(10초) ~ 1.0(1초)
        Color urgentColor = Color.Lerp(new Color(1f, 0.6f, 0f), Color.red, urgency);
        
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shakeDuration;
            
            // 감쇠 곡선: 시작에 강하고 점점 약해짐
            float decay = 1f - (t * t);
            
            // 위치 진동
            Vector2 offset = new Vector2(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ) * shakeIntensity * decay;
            rt.anchoredPosition = timerOriginalPos + offset;
            
            // 스케일 펀치 (빠르게 커졌다 돌아오기)
            float scaleBounce = Mathf.Sin(t * Mathf.PI) * punchScale * decay;
            rt.localScale = timerOriginalScale * (1f + scaleBounce);
            
            // 색상 보간 (떨리는 동안 위급 색상 → 유지)
            gameTimerText.color = Color.Lerp(urgentColor, Color.white, t * 0.3f);
            
            yield return null;
        }
        
        // 떨림 종료 후: 위치 복원, 스케일 복원, 색상은 위급 색상 유지
        rt.anchoredPosition = timerOriginalPos;
        rt.localScale = timerOriginalScale;
        gameTimerText.color = urgentColor;
        
        timerShakeCoroutine = null;
    }
    
    /// <summary>
    /// 타이머 외형을 원래 상태로 복원합니다.
    /// </summary>
    private void ResetTimerAppearance()
    {
        if (gameTimerText == null) return;
        if (!timerOriginalPosSaved) return;
        
        RectTransform rt = gameTimerText.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = timerOriginalPos;
            rt.localScale = timerOriginalScale;
        }
        gameTimerText.color = timerOriginalColor;
    }
    
    #endregion
    
    #region 콤보 시스템
    
    /// <summary>
    /// 로컬 터치 시 호출 — 콤보 카운트는 증가하지 않음(서버 확정 시에만 증가).
    /// 이미 콤보 UI가 떠 있으면 텍스트를 "PRESS!"로 바꿔서 터치가 인식됐음을 즉시 피드백.
    /// </summary>
    public void RegisterCombo()
    {
        comboTimer = comboDuration;
        comboActive = true;

        // 콤보 UI가 떠 있는 상태에서만 PRESS! 텍스트로 전환
        // (아직 서버 확정이 한 번도 안 된 상태라면 UI 자체가 숨겨져 있으므로 건드리지 않음)
        if (comboUI != null && comboUI.activeSelf && comboText != null)
        {
            comboText.text = "PRESS!";
            BounceComboText();
        }
    }

    /// <summary>
    /// 서버가 내 점수를 확정했을 때 호출. delta만큼 콤보 카운트를 올리고 "🔥 N COMBO!" 표시.
    /// AddOrUpdatePlayer의 isMe 분기에서 호출됨.
    /// </summary>
    private void ConfirmComboFromServer(int newScore)
    {
        int delta = newScore - lastConfirmedScore;
        lastConfirmedScore = newScore;

        if (delta <= 0) return; // 점수 증가 없음 (초기 세팅 또는 재연결 시 동일 값)

        comboCount += delta;
        comboTimer = comboDuration;
        comboActive = true;

        if (comboCount > maxComboCount)
            maxComboCount = comboCount;

        if (comboCount >= 2)
        {
            if (comboUI != null && !comboUI.activeSelf)
            {
                comboUI.SetActive(true);
            }

            if (comboText != null)
            {
                comboText.text = $"🔥 {comboCount} COMBO!";
                BounceComboText();
            }

            if (comboGlowShadow != null)
            {
                float glowSize = Mathf.Lerp(8f, 30f, Mathf.Clamp01((comboCount - 2f) / 20f));
                comboGlowShadow.Size = glowSize;
            }
        }
    }
    
    /// <summary>
    /// 콤보 텍스트를 살짝 위로 튀어오르게 합니다.
    /// </summary>
    private Coroutine comboBounceCoroutine;
    private Vector2 comboOriginalPos;
    private bool comboOriginalPosSaved = false;
    
    private void BounceComboText()
    {
        if (comboUI == null) return;
        
        RectTransform rt = comboUI.GetComponent<RectTransform>();
        if (rt == null) return;
        
        // 원래 위치를 최초 1회만 저장
        if (!comboOriginalPosSaved)
        {
            comboOriginalPos = rt.anchoredPosition;
            comboOriginalPosSaved = true;
        }
        
        if (comboBounceCoroutine != null)
            StopCoroutine(comboBounceCoroutine);
        
        // 바운스 시작 전 원래 위치로 리셋
        rt.anchoredPosition = comboOriginalPos;
        comboBounceCoroutine = StartCoroutine(ComboBounceAnimation());
    }
    
    private IEnumerator ComboBounceAnimation()
    {
        RectTransform rt = comboUI.GetComponent<RectTransform>();
        if (rt == null) yield break;
        
        float bounceHeight = 20f;   // 위로 튀어오르는 높이 (px)
        float duration = 0.25f;     // 애니메이션 시간
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // 위로 튀었다가 원래 위치로 돌아오는 커브
            float bounce = Mathf.Sin(t * Mathf.PI) * bounceHeight * (1f - t);
            rt.anchoredPosition = comboOriginalPos + Vector2.up * bounce;
            
            yield return null;
        }
        
        rt.anchoredPosition = comboOriginalPos;
        comboBounceCoroutine = null;
    }
    
    /// <summary>
    /// 콤보 초기화 - 카운트 0, UI 비활성화
    /// </summary>
    public void ResetCombo()
    {
        comboCount = 0;
        comboTimer = 0f;
        comboActive = false;
        lastConfirmedScore = 0;  // 새 게임 시작/종료 시 서버 기준 점수도 0에서 다시 추적

        if (comboUI != null)
        {
            comboUI.SetActive(false);
        }
    }
    
    #endregion
    
    #region 내 순위 표시
    
    /// <summary>
    /// 내 플레이어의 현재 순위를 계산하여 UI에 표시합니다.
    /// </summary>
    private void UpdateMyRank()
    {
        if (myRankText == null) return;
        if (string.IsNullOrEmpty(currentPlayerId)) return;
        if (!playerInfoUIs.ContainsKey(currentPlayerId)) return;
        
        PlayerInfoUI myUI = playerInfoUIs[currentPlayerId];
        if (myUI == null) return;
        
        // 일반 플레이어 수 카운트
        int totalPlayers = 0;
        foreach (var kvp in playerInfoUIs)
        {
            if (kvp.Value != null && !kvp.Value.IsAdmin() && !kvp.Value.IsObserver())
                totalPlayers++;
        }
        
        int myRank = myUI.transform.GetSiblingIndex() + 1;
        myRankText.text = $"{myRank}";
        
        // 등수 변화 감지 → 비네팅 플래시
        if (previousRank > 0 && myRank != previousRank)
        {
            if (myRank < previousRank)
            {
                // 추월! (등수가 낮아짐 = 순위 올라감)
                FlashVignette(new Color(0f, 1f, 0.3f, 1f));  // 초록색
                myUI.ShowRankUp();
                ShowOvertakeEffect(true);
                Debug.Log($"🟢 추월! {previousRank}위 → {myRank}위");
            }
            else
            {
                // 추월당함 (등수가 높아짐 = 순위 내려감)
                FlashVignette(new Color(1f, 0.2f, 0.2f, 1f));  // 빨간색
                myUI.ShowRankDown();
                ShowOvertakeEffect(false);
                Debug.Log($"🔴 추월당함! {previousRank}위 → {myRank}위");
            }
        }
        previousRank = myRank;
    }
    
    /// <summary>
    /// 비네팅 오버레이를 지정 색상으로 플래시합니다.
    /// </summary>
    private void FlashVignette(Color color)
    {
        if (vignetteOverlay == null) return;
        
        if (vignetteFlashCoroutine != null)
            StopCoroutine(vignetteFlashCoroutine);
        
        vignetteFlashCoroutine = StartCoroutine(VignetteFlashAnimation(color));
    }
    
    private IEnumerator VignetteFlashAnimation(Color color)
    {
        vignetteOverlay.gameObject.SetActive(true);
        
        // Material의 _Color를 변경 (인스턴스화)
        Material mat = vignetteOverlay.material;
        
        float elapsed = 0f;
        
        while (elapsed < vignetteFlashDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / vignetteFlashDuration;
            
            // 빠르게 나타나고 천천히 사라짐
            float alpha = 1f - (t * t);  // ease-out
            Color c = color;
            c.a = alpha;
            mat.SetColor("_Color", c);
            
            yield return null;
        }
        
        // 완전히 투명하게
        Color final_c = color;
        final_c.a = 0f;
        mat.SetColor("_Color", final_c);
        vignetteOverlay.gameObject.SetActive(false);
        
        vignetteFlashCoroutine = null;
    }
    
    /// <summary>
    /// 추월 연출 오브젝트를 활성화하고 지정된 시간 후 자동으로 비활성화합니다.
    /// </summary>
    /// <param name="isRankUp">true: 추월(순위 상승), false: 추월당함(순위 하락)</param>
    private void ShowOvertakeEffect(bool isRankUp)
    {
        if (isRankUp)
        {
            if (rankUpEffectObject != null)
            {
                if (rankUpEffectCoroutine != null)
                    StopCoroutine(rankUpEffectCoroutine);
                rankUpEffectCoroutine = StartCoroutine(OvertakeEffectCoroutine(rankUpEffectObject, (c) => rankUpEffectCoroutine = c));
            }
        }
        else
        {
            if (rankDownEffectObject != null)
            {
                if (rankDownEffectCoroutine != null)
                    StopCoroutine(rankDownEffectCoroutine);
                rankDownEffectCoroutine = StartCoroutine(OvertakeEffectCoroutine(rankDownEffectObject, (c) => rankDownEffectCoroutine = c));
            }
        }
    }
    
    /// <summary>
    /// 오브젝트를 활성화했다가 지정 시간 후 비활성화하는 코루틴
    /// </summary>
    private IEnumerator OvertakeEffectCoroutine(GameObject effectObj, System.Action<Coroutine> clearRef)
    {
        effectObj.SetActive(true);
        yield return new WaitForSeconds(overtakeEffectDuration);
        effectObj.SetActive(false);
        clearRef(null);
    }
    
    #endregion
    
    /// <summary>
    /// 플레이어 목록을 점수 내림차순으로 정렬합니다.
    /// </summary>
    private void SortPlayersByScore()
    {
        if (playerContent == null || playerInfoUIs.Count <= 1) return;
        
        // 현재 플레이어들을 리스트로 수집
        List<PlayerInfoUI> sortedPlayers = new List<PlayerInfoUI>();
        foreach (var kvp in playerInfoUIs)
        {
            if (kvp.Value != null && !kvp.Value.IsAdmin() && !kvp.Value.IsObserver())
            {
                sortedPlayers.Add(kvp.Value);
            }
        }
        
        // 점수 내림차순 정렬 (동점이면 이름 오름차순)
        sortedPlayers.Sort((a, b) =>
        {
            int scoreCompare = b.GetScore().CompareTo(a.GetScore());
            if (scoreCompare != 0) return scoreCompare;
            return string.Compare(a.GetPlayerName(), b.GetPlayerName(), System.StringComparison.Ordinal);
        });
        
        // Transform 순서 재배치 및 등수 표시 업데이트
        for (int i = 0; i < sortedPlayers.Count; i++)
        {
            sortedPlayers[i].transform.SetSiblingIndex(i);
            sortedPlayers[i].UpdateRank(i + 1);  // 1부터 시작하는 등수
        }
    }
    
    /// <summary>
    /// 내 플레이어가 스크롤뷰 중앙(3번째)에 보이도록 스크롤 위치를 조정합니다.
    /// 조건: 전체 플레이어 6명 이상 AND 내 등수 4등 이하
    /// </summary>
    private void ScrollToMyPlayer()
    {
        if (playerScrollRect == null || playerContent == null) return;
        if (string.IsNullOrEmpty(currentPlayerId)) return;
        if (!playerInfoUIs.ContainsKey(currentPlayerId)) return;
        
        PlayerInfoUI myUI = playerInfoUIs[currentPlayerId];
        if (myUI == null) return;
        
        // 일반 플레이어만 카운트 (Admin/Observer 제외)
        int totalPlayers = 0;
        foreach (var kvp in playerInfoUIs)
        {
            if (kvp.Value != null && !kvp.Value.IsAdmin() && !kvp.Value.IsObserver())
                totalPlayers++;
        }
        
        // 내 현재 순위 (sibling index 기준, 0부터 시작)
        int myIndex = myUI.transform.GetSiblingIndex();
        int myRank = myIndex + 1; // 1부터 시작하는 등수
        
        // 조건: 6명 이상이고 내 등수가 4등 이하일 때만 스크롤
        if (totalPlayers < 6 || myRank <= 3)
        {
            // 상위권이면 스크롤 맨 위로
            playerScrollRect.verticalNormalizedPosition = 1f;
            return;
        }
        
        // 중앙(3번째, 인덱스 2)에 오도록 스크롤 위치 계산
        // 스크롤 가능한 아이템 수 = 전체 - 화면에 보이는 수
        int scrollableCount = totalPlayers - visiblePlayerCount;
        if (scrollableCount <= 0)
        {
            playerScrollRect.verticalNormalizedPosition = 1f;
            return;
        }
        
        // 내 플레이어가 중앙(3번째 = 인덱스 2)에 오려면 스크롤 시작점
        int centerOffset = visiblePlayerCount / 2; // 2 (5개 중 3번째)
        int scrollToIndex = myIndex - centerOffset;
        
        // 범위 클램프
        scrollToIndex = Mathf.Clamp(scrollToIndex, 0, scrollableCount);
        
        // normalizedPosition: 1 = 맨 위, 0 = 맨 아래
        float normalizedPos = 1f - ((float)scrollToIndex / scrollableCount);
        playerScrollRect.verticalNormalizedPosition = normalizedPos;
        
        Debug.Log($"📜 스크롤 조정 - 내 등수: {myRank}/{totalPlayers}, 스크롤 위치: {normalizedPos:F2}");
    }
}
