using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.Localization.Settings;

/// <summary>
/// 6가지 원소 타입 (기쁨, 슬픔, 용기, 사랑, 희망, 우정)
/// Desktop에서 해당 원소에 맞는 이펙트를 표시하는 데 사용됩니다.
/// </summary>
public enum ElementType
{
    None = 0,
    Joy = 1,        // 기쁨
    Sadness = 2,    // 슬픔
    Courage = 3,    // 용기
    Love = 4,       // 사랑
    Hope = 5,       // 희망
    Friendship = 6  // 우정
}

public class NickNamePanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private InputField nickNameInputField;
    [SerializeField] private Button joinGameButton;     // 입장 버튼 (닉네임 + 원소 선택 후 대기방 입장)
    [SerializeField] private Text statusText;
    [SerializeField] private Text errorText;

    [Header("Element Selection (원소 선택)")]
    [SerializeField] private Button joyButton;        // 기쁨
    [SerializeField] private Button sadnessButton;    // 슬픔
    [SerializeField] private Button courageButton;    // 용기
    [SerializeField] private Button loveButton;       // 사랑
    [SerializeField] private Button hopeButton;       // 희망
    [SerializeField] private Button friendshipButton; // 우정
    [SerializeField] private Text elementStatusText;  // 선택된 원소 표시 텍스트 (선택)
    
    [Header("Glow Effects (선택 효과)")]
    [SerializeField] private GlowEffect joyGlow;
    [SerializeField] private GlowEffect sadnessGlow;
    [SerializeField] private GlowEffect courageGlow;
    [SerializeField] private GlowEffect loveGlow;
    [SerializeField] private GlowEffect hopeGlow;
    [SerializeField] private GlowEffect friendshipGlow;
    
    [Header("선택 외곽 표시 오브젝트")]
    [SerializeField] private GameObject joyOutline;
    [SerializeField] private GameObject sadnessOutline;
    [SerializeField] private GameObject courageOutline;
    [SerializeField] private GameObject loveOutline;
    [SerializeField] private GameObject hopeOutline;
    [SerializeField] private GameObject friendshipOutline;

    [Header("Language (언어 설정)")]
    [SerializeField] private Button languageButton;       // 언어 설정 열기 버튼
    [SerializeField] private LanguagePanel languagePanel;  // 언어 선택 팝업

    [Header("Settings")]
    [SerializeField] private WebGLProgram webGLProgram;
    [SerializeField] private GameObject lobbyPanel; // 대기방 패널
    private const string DEFAULT_ROOM_ID = "main_room";
    
    [Header("Events")]
    public UnityEvent onNicknameSuccess;
    public UnityEvent onMoveToLobby;
    
    private bool isWaitingForResponse = false;
    private string pendingNickname = "";
    private string currentPlayerId = "";
    private ElementType selectedElement = ElementType.None;
    private Button currentSelectedButton = null;
    private GlowEffect currentGlow = null;
    private GameObject currentOutline = null;
    private readonly Vector3 selectedScale = new Vector3(1.15f, 1.15f, 1.15f);
    private readonly Vector3 normalScale = Vector3.one;
    
    // 원소별 Glow 매핑
    private System.Collections.Generic.Dictionary<ElementType, GlowEffect> glowMap;
    // 원소별 Outline 매핑
    private System.Collections.Generic.Dictionary<ElementType, GameObject> outlineMap;

    /// <summary>
    /// 현재 선택된 원소 타입을 반환합니다.
    /// </summary>
    public ElementType SelectedElement => selectedElement;

    /// <summary>
    /// ElementType의 한글 이름을 반환합니다.
    /// </summary>
    public static string GetElementName(ElementType element)
    {
        switch (element)
        {
            case ElementType.Joy: return "기쁨";
            case ElementType.Sadness: return "슬픔";
            case ElementType.Courage: return "용기";
            case ElementType.Love: return "사랑";
            case ElementType.Hope: return "희망";
            case ElementType.Friendship: return "우정";
            default: return "없음";
        }
    }
    
    void Start()
    {
        InitializeUI();
        InitializeLanguageButton();
    }
    
    void OnEnable()
    {
        Debug.Log("✅ NickNamePanel이 활성화되었습니다!");
        ResetUIState();
    }
    
    private void ResetUIState()
    {
        isWaitingForResponse = false;
        
        if (joinGameButton != null)
            joinGameButton.interactable = true;
        
        UpdateStatus("닉네임을 입력하세요");
        ClearError();
    }
    
    void OnDisable()
    {
        Debug.Log("❌ NickNamePanel이 비활성화되었습니다!");
    }
    
    void InitializeUI()
    {
        if (joinGameButton != null)
        {
            joinGameButton.onClick.AddListener(OnSetNicknameButtonClick);
            Debug.Log("✅ 입장 버튼 초기화 완료");
        }
        
        // 원소 선택 버튼 초기화
        InitializeElementButtons();
        
        // InputField Enter 키 처리
        if (nickNameInputField != null)
        {
            nickNameInputField.onEndEdit.AddListener(OnInputFieldEndEdit);
            Debug.Log("✅ 닉네임 입력 필드 초기화 완료");
        }
        else
        {
            Debug.LogError("❌ Nickname InputField가 할당되지 않았습니다!");
        }
        
        // WebGLProgram 자동 찾기
        if (webGLProgram == null)
        {
            webGLProgram = FindObjectOfType<WebGLProgram>();
            if (webGLProgram != null)
                Debug.Log("✅ WebGLProgram 자동 탐색 성공");
            else
                Debug.LogError("❌ WebGLProgram을 찾을 수 없습니다!");
        }
        
        UpdateStatus("닉네임을 입력하고 원소를 선택하세요");
        ClearError();
    }

    /// <summary>
    /// 언어 버튼 초기화: Localization 초기화 전까지 비활성화
    /// </summary>
    private void InitializeLanguageButton()
    {
        if (languageButton == null) return;

        // 클릭 시 LanguagePanel 표시
        languageButton.onClick.AddListener(() =>
        {
            if (languagePanel != null)
                languagePanel.Show();
        });

        // Localization 초기화 완료 여부 확인
        if (LocalizationSettings.InitializationOperation.IsDone)
        {
            languageButton.interactable = true;
        }
        else
        {
            languageButton.interactable = false;
            StartCoroutine(WaitForLocalizationInit());
        }
    }

    private IEnumerator WaitForLocalizationInit()
    {
        yield return LocalizationSettings.InitializationOperation;
        if (languageButton != null)
            languageButton.interactable = true;
        Debug.Log("🌐 Localization 초기화 완료 - 언어 버튼 활성화");
    }
    
    /// <summary>
    /// 원소 선택 버튼들을 초기화합니다.
    /// </summary>
    private void InitializeElementButtons()
    {
        RegisterElementButton(joyButton, ElementType.Joy);
        RegisterElementButton(sadnessButton, ElementType.Sadness);
        RegisterElementButton(courageButton, ElementType.Courage);
        RegisterElementButton(loveButton, ElementType.Love);
        RegisterElementButton(hopeButton, ElementType.Hope);
        RegisterElementButton(friendshipButton, ElementType.Friendship);
        
        // Glow 매핑 초기화
        glowMap = new System.Collections.Generic.Dictionary<ElementType, GlowEffect>
        {
            { ElementType.Joy, joyGlow },
            { ElementType.Sadness, sadnessGlow },
            { ElementType.Courage, courageGlow },
            { ElementType.Love, loveGlow },
            { ElementType.Hope, hopeGlow },
            { ElementType.Friendship, friendshipGlow }
        };
        
        // Outline 매핑 초기화
        outlineMap = new System.Collections.Generic.Dictionary<ElementType, GameObject>
        {
            { ElementType.Joy, joyOutline },
            { ElementType.Sadness, sadnessOutline },
            { ElementType.Courage, courageOutline },
            { ElementType.Love, loveOutline },
            { ElementType.Hope, hopeOutline },
            { ElementType.Friendship, friendshipOutline }
        };
        
        // 모든 Outline 초기 비활성화
        foreach (var outline in outlineMap.Values)
        {
            if (outline != null) outline.SetActive(false);
        }
        
        // 각 Glow에 원소별 색상 설정
        SetGlowColor(joyGlow, new Color(1f, 0.7f, 0.3f, 1f));         // 오렌지
        SetGlowColor(sadnessGlow, new Color(1f, 0.85f, 0.2f, 1f));    // 골드
        SetGlowColor(courageGlow, new Color(1f, 0.6f, 0.7f, 1f));     // 핑크
        SetGlowColor(loveGlow, new Color(0.5f, 0.9f, 1f, 1f));        // 시안
        SetGlowColor(hopeGlow, new Color(0.7f, 0.5f, 1f, 1f));        // 보라
        SetGlowColor(friendshipGlow, new Color(0.3f, 1f, 0.5f, 1f));  // 그린
        
        Debug.Log("✅ 원소 선택 버튼 초기화 완료");
    }
    
    private void SetGlowColor(GlowEffect glow, Color color)
    {
        if (glow != null) glow.SetColor(color);
    }
    
    private void RegisterElementButton(Button button, ElementType element)
    {
        if (button != null)
        {
            button.onClick.AddListener(() => OnElementButtonClick(element, button));
        }
    }
    
    /// <summary>
    /// 원소 버튼 클릭 핸들러
    /// </summary>
    private void OnElementButtonClick(ElementType element, Button button)
    {
        // 이전 선택 버튼 원래 크기로 복원
        if (currentSelectedButton != null)
        {
            currentSelectedButton.transform.localScale = normalScale;
        }
        
        // 이전 Glow 숨기기
        if (currentGlow != null)
        {
            currentGlow.Hide();
        }
        
        // 이전 Outline 숨기기
        if (currentOutline != null)
        {
            currentOutline.SetActive(false);
        }
        
        selectedElement = element;
        currentSelectedButton = button;
        
        // 선택된 버튼 크게 표시
        if (button != null)
        {
            button.transform.localScale = selectedScale;
        }
        
        // 새 Glow 표시
        if (glowMap != null && glowMap.TryGetValue(element, out GlowEffect glow) && glow != null)
        {
            glow.Show();
            currentGlow = glow;
        }
        
        // 새 Outline 표시
        if (outlineMap != null && outlineMap.TryGetValue(element, out GameObject outline) && outline != null)
        {
            outline.SetActive(true);
            currentOutline = outline;
        }
        
        string elementName = GetElementName(element);
        Debug.Log($"✨ 원소 선택: {elementName} ({element})");
        
        if (elementStatusText != null)
        {
            elementStatusText.text = $"선택된 원소: {elementName}";
        }
        
        ClearError();
    }
    
    /// <summary>
    /// 닉네임 설정 버튼 클릭 핸들러
    /// </summary>
    public void OnSetNicknameButtonClick()
    {
        if (isWaitingForResponse)
        {
            Debug.LogWarning("⚠️ 서버 응답 대기 중입니다...");
            return;
        }
        
        string nickname = nickNameInputField != null ? nickNameInputField.text : "";
        
        // 닉네임 유효성 검사
        if (string.IsNullOrEmpty(nickname) || nickname.Trim().Length == 0)
        {
            ShowError(LocaleHelper.Get("SYS_ERROR_NICKNAME_EMPTY", "닉네임을 입력해주세요!"));
            EnsureButtonsEnabled();
            return;
        }
        
        nickname = nickname.Trim();
        
        if (nickname.Length < 2)
        {
            ShowError(LocaleHelper.Get("SYS_ERROR_NICKNAME_TOO_SHORT", "닉네임은 최소 2자 이상이어야 합니다!"));
            EnsureButtonsEnabled();
            return;
        }
        
        if (nickname.Length > 12)
        {
            ShowError(LocaleHelper.Get("SYS_ERROR_NICKNAME_TOO_LONG", "닉네임은 최대 12자까지 가능합니다!"));
            EnsureButtonsEnabled();
            return;
        }
        
        // 특수문자 필터링: 한글, 영문, 숫자, 밑줄, 하이픈, 공백만 허용
        if (!System.Text.RegularExpressions.Regex.IsMatch(nickname, @"^[가-힣ㄱ-ㅎㅏ-ㅣa-zA-Z0-9_\- \u3040-\u309F\u30A0-\u30FF\u4E00-\u9FFF\u3400-\u4DBF]+$"))
        {
            ShowError(LocaleHelper.Get("SYS_ERROR_NICKNAME_INVALID_CHARS", "닉네임에 특수문자를 사용할 수 없습니다!\n(한글, 일본어, 한자, 영문, 숫자, 밑줄, 하이픈만 가능)"));
            EnsureButtonsEnabled();
            return;
        }
        
        // 원소 선택 검증
        if (selectedElement == ElementType.None)
        {
            ShowError(LocaleHelper.Get("SYS_ERROR_ELEMENT_NOT_SELECTED", "원소를 선택해주세요!"));
            EnsureButtonsEnabled();
            return;
        }
        
        CheckAndSetNickname(nickname);
    }
    
    private void EnsureButtonsEnabled()
    {
        if (joinGameButton != null)
            joinGameButton.interactable = true;
        isWaitingForResponse = false;
    }
    
    private void OnInputFieldEndEdit(string value)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OnSetNicknameButtonClick();
        }
    }
    
    /// <summary>
    /// 닉네임 중복 확인 후 설정
    /// </summary>
    private void CheckAndSetNickname(string nickname)
    {
        if (webGLProgram == null)
        {
            ShowError(LocaleHelper.Get("SYS_ERROR_SYSTEM_NOT_FOUND", "시스템 오류: WebGLProgram을 찾을 수 없습니다."));
            return;
        }
        
        pendingNickname = nickname;
        isWaitingForResponse = true;
        
        UpdateStatus($"'{nickname}' 닉네임 확인 중...");
        ClearError();
        
        if (joinGameButton != null)
            joinGameButton.interactable = false;
        
        Debug.Log($"🔍 닉네임 중복 확인 요청: {nickname}");
        
        string elementStr = selectedElement.ToString();
        string message = $"{{\"type\":\"checkNickname\",\"roomId\":\"{DEFAULT_ROOM_ID}\",\"nickname\":\"{nickname}\",\"element\":\"{elementStr}\"}}";
        webGLProgram.SendMessage(message);
        
        StartCoroutine(WaitForNicknameCheckTimeout());
    }
    
    private IEnumerator WaitForNicknameCheckTimeout()
    {
        yield return new WaitForSeconds(5f);
        if (isWaitingForResponse)
        {
            ShowError(LocaleHelper.Get("SYS_ERROR_SERVER_TIMEOUT", "서버 응답 시간 초과. 다시 시도해주세요."));
            ResetState();
        }
    }
    
    /// <summary>
    /// 닉네임 중복 확인 결과 처리 (WebGLProgram에서 호출)
    /// </summary>
    public void OnNicknameCheckResult(bool available, string message)
    {
        isWaitingForResponse = false;
        
        if (!available)
        {
            ShowError(message);
            ResetState();
            return;
        }
        
        Debug.Log($"✅ 닉네임 사용 가능: {pendingNickname}");
        SetNickname(pendingNickname);
    }
    
    /// <summary>
    /// 서버에 닉네임 설정 요청 (팀 없음 - 개인전)
    /// </summary>
    private void SetNickname(string nickname)
    {
        if (webGLProgram == null)
        {
            ShowError(LocaleHelper.Get("SYS_ERROR_SYSTEM_NOT_FOUND", "시스템 오류: WebGLProgram을 찾을 수 없습니다."));
            return;
        }
        
        pendingNickname = nickname;
        isWaitingForResponse = true;
        
        UpdateStatus($"'{nickname}' 대기방 입장 중...");
        ClearError();
        
        if (joinGameButton != null)
            joinGameButton.interactable = false;
        
        Debug.Log($"🎯 닉네임 설정 요청: {nickname} (개인전)");
        
        // 원소 정보와 함께 닉네임 전송
        string elementStr = selectedElement.ToString();
        Debug.Log($"✨ 원소 전송: {elementStr} ({GetElementName(selectedElement)})");
        string message = $"{{\"type\":\"setNickname\",\"roomId\":\"{DEFAULT_ROOM_ID}\",\"nickname\":\"{nickname}\",\"element\":\"{elementStr}\"}}";
        webGLProgram.SendMessage(message);
        
        StartCoroutine(WaitForSetNicknameTimeout());
    }
    
    private IEnumerator WaitForSetNicknameTimeout()
    {
        yield return new WaitForSeconds(5f);
        if (isWaitingForResponse)
        {
            ShowError(LocaleHelper.Get("SYS_ERROR_SERVER_TIMEOUT", "서버 응답 시간 초과. 다시 시도해주세요."));
            ResetState();
        }
    }
    
    /// <summary>
    /// 닉네임 설정 결과 처리 (WebGLProgram에서 호출)
    /// </summary>
    public void OnNicknameSetResult(bool success, bool inLobby, bool isWaiting, int countdown, string gameStatus, string message, string playerId)
    {
        isWaitingForResponse = false;
        
        if (!success)
        {
            ShowError(message);
            ResetState();
            return;
        }
        
        currentPlayerId = playerId;
        Debug.Log($"✅ 입장 처리: {pendingNickname}, PlayerId: {playerId}");
        Debug.Log($"   inLobby: {inLobby}, isWaiting: {isWaiting}, gameStatus: {gameStatus}");
        
        onNicknameSuccess?.Invoke();
        
        if (inLobby)
        {
            int minutes = countdown / 60;
            int seconds = countdown % 60;
            string timeText = minutes > 0 ? $"{minutes}분 {seconds}초" : $"{seconds}초";
            
            UpdateStatus($"대기방 입장 완료! 게임 시작까지 {timeText}");
            onMoveToLobby?.Invoke();
            StartCoroutine(MoveToLobby(countdown));
        }
        else if (isWaiting)
        {
            int minutes = countdown / 60;
            int seconds = countdown % 60;
            string timeText = minutes > 0 ? $"{minutes}분 {seconds}초" : $"{seconds}초";
            
            ShowError(LocaleHelper.GetFormat("SYS_ERROR_GAME_IN_PROGRESS", minutes, seconds));
            ResetState();
        }
        else
        {
            ShowError(LocaleHelper.Get("SYS_ERROR_UNKNOWN_STATE", "입장 상태 오류"));
            ResetState();
        }
    }
    
    private IEnumerator MoveToLobby(int countdown)
    {
        yield return null;
        
        Debug.Log($"🚪 대기방 패널로 이동 - 남은 시간: {countdown}초");
        
        if (webGLProgram != null)
        {
            webGLProgram.SetPlayerNickname(pendingNickname);
        }
        
        gameObject.SetActive(false);
        
        // 대기방 패널 표시 (WaitingRoomPanel 사용)
        WaitingRoomPanel waitingRoomPanel = FindObjectOfType<WaitingRoomPanel>();
        if (waitingRoomPanel != null)
        {
            waitingRoomPanel.ShowWaitingPanel(countdown, 1); // 최소 본인 1명
        }
        
        // GamePanel 참조
        GamePanel gamePanel = FindObjectOfType<GamePanel>();
        
        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(true);
        }
    }
    
    /// <summary>
    /// 게임 종료 후 닉네임 스텝으로 복귀 (WebGLProgram에서 호출)
    /// </summary>
    public void OnReturnToNicknameStep(string message, string previousNickname)
    {
        Debug.Log($"🔄 닉네임 스텝으로 복귀: {message}");
        Debug.Log($"   이전 닉네임: {previousNickname}");
        
        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(false);
        }
        
        gameObject.SetActive(true);
        
        // 이전 닉네임 복원
        if (nickNameInputField != null && !string.IsNullOrEmpty(previousNickname))
        {
            nickNameInputField.text = previousNickname;
        }
        
        // 원소 선택 초기화
        selectedElement = ElementType.None;
        if (currentSelectedButton != null)
        {
            currentSelectedButton.transform.localScale = normalScale;
            currentSelectedButton = null;
        }
        
        // Glow 초기화
        if (currentGlow != null)
        {
            currentGlow.Hide();
            currentGlow = null;
        }
        
        // Outline 초기화
        if (currentOutline != null)
        {
            currentOutline.SetActive(false);
            currentOutline = null;
        }
        
        if (elementStatusText != null) elementStatusText.text = "";
        
        UpdateStatus("게임이 종료되었습니다. 닉네임을 입력하고 원소를 선택하세요.");
        ResetState();
    }
    
    private void UpdateStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log($"📝 상태: {message}");
    }
    
    private void ShowError(string error)
    {
        if (errorText != null)
        {
            errorText.text = error;
            errorText.gameObject.SetActive(true);
        }
        Debug.LogWarning($"⚠️ 에러: {error}");
    }
    
    private void ClearError()
    {
        if (errorText != null)
        {
            errorText.text = "";
            errorText.gameObject.SetActive(false);
        }
    }
    
    private void ResetState()
    {
        isWaitingForResponse = false;
        if (joinGameButton != null) joinGameButton.interactable = true;
    }
}
