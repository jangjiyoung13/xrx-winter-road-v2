using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사운드를 관리하는 싱글톤 매니저
/// BGM과 효과음을 제어합니다.
/// </summary>
public class SoundManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    private static SoundManager instance;
    public static SoundManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("SoundManager");
                instance = go.AddComponent<SoundManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    [Header("BGM 설정")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip[] bgmClips;
    [SerializeField] private bool playBGMOnStart = true;
    
    [Header("효과음 설정")]
    [SerializeField] private AudioSource[] sfxSources;
    [SerializeField] private int sfxSourceCount = 5; // 동시에 재생할 수 있는 효과음 개수
    [SerializeField] private AudioClip[] sfxClips; // 효과음 클립 배열 (인덱스로 재생)

    [Header("구슬 깨짐 효과음 (호출마다 순환 재생)")]
    [SerializeField] private AudioClip[] crystalBreakClips; // 구슬 깨짐 효과음 4종
    private int crystalBreakIndex = 0;
    
    [Header("볼륨 설정")]
    [SerializeField] [Range(0f, 1f)] private float bgmVolume = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float sfxVolume = 1f;
    
    private bool isBGMPlaying = true;
    private int currentBGMIndex = 0;

    void Awake()
    {
        // 싱글톤 체크
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        InitializeAudioSources();
    }

    void Start()
    {
        // AudioListener 확인
        AudioListener listener = FindObjectOfType<AudioListener>();
        if (listener == null)
        {
            Debug.LogError("❌ AudioListener가 씬에 없습니다! Main Camera에 AudioListener를 추가하세요.");
        }
        else
        {
            Debug.Log($"✅ AudioListener 발견: {listener.gameObject.name}");
        }
        
        Debug.Log($"🔊 SoundManager Start - playBGMOnStart: {playBGMOnStart}, bgmClips 개수: {(bgmClips != null ? bgmClips.Length : 0)}");
        
        if (bgmClips != null && bgmClips.Length > 0)
        {
            Debug.Log($"🎵 BGM Clip[0]: {(bgmClips[0] != null ? bgmClips[0].name : "null")}");
        }
        
        Debug.Log($"🔊 BGM Volume: {bgmVolume}, BGM Source Volume: {bgmSource.volume}");
        
        if (playBGMOnStart && bgmClips != null && bgmClips.Length > 0)
        {
            PlayBGM(0);
        }
        else if (bgmClips == null || bgmClips.Length == 0)
        {
            Debug.LogWarning("⚠️ BGM 클립이 설정되지 않았습니다! Inspector에서 BGM Clips를 할당하세요.");
        }
    }

    /// <summary>
    /// AudioSource 초기화
    /// </summary>
    private void InitializeAudioSources()
    {
        // BGM AudioSource 생성
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.volume = bgmVolume;
            bgmSource.spatialBlend = 0f; // 2D 사운드
            bgmSource.priority = 0; // 최고 우선순위
        }
        else
        {
            // 이미 할당된 경우에도 설정 적용
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.volume = bgmVolume;
            bgmSource.spatialBlend = 0f;
            bgmSource.priority = 0;
        }
        
        Debug.Log($"🔊 BGM Source 설정 - Volume: {bgmSource.volume}, Mute: {bgmSource.mute}, SpatialBlend: {bgmSource.spatialBlend}");
        
        // 효과음 AudioSource 생성
        if (sfxSources == null || sfxSources.Length == 0)
        {
            sfxSources = new AudioSource[sfxSourceCount];
            for (int i = 0; i < sfxSourceCount; i++)
            {
                sfxSources[i] = gameObject.AddComponent<AudioSource>();
                sfxSources[i].playOnAwake = false;
                sfxSources[i].volume = sfxVolume;
                sfxSources[i].spatialBlend = 0f; // 2D 사운드
                sfxSources[i].priority = 128; // 중간 우선순위
            }
        }
        else
        {
            // 이미 할당된 경우에도 설정 적용
            foreach (var source in sfxSources)
            {
                if (source != null)
                {
                    source.playOnAwake = false;
                    source.volume = sfxVolume;
                    source.spatialBlend = 0f;
                    source.priority = 128;
                }
            }
        }
        
        Debug.Log($"🔊 SoundManager 초기화 완료 - BGM Source: 1개, SFX Sources: {sfxSources.Length}개");
    }

    #region BGM 제어

    /// <summary>
    /// BGM 재생 (인덱스로 선택)
    /// </summary>
    public void PlayBGM(int index)
    {
        Debug.Log($"🎵 PlayBGM 호출됨 - 인덱스: {index}");
        
        if (bgmClips == null || bgmClips.Length == 0)
        {
            Debug.LogWarning("⚠️ BGM 클립이 설정되지 않았습니다!");
            return;
        }
        
        if (index < 0 || index >= bgmClips.Length)
        {
            Debug.LogWarning($"⚠️ BGM 인덱스가 범위를 벗어났습니다: {index}");
            return;
        }
        
        if (bgmClips[index] == null)
        {
            Debug.LogWarning($"⚠️ BGM 클립이 null입니다: 인덱스 {index}");
            return;
        }
        
        currentBGMIndex = index;
        bgmSource.clip = bgmClips[index];
        
        Debug.Log($"🎵 BGM 설정 완료 - Clip: {bgmClips[index].name}, Volume: {bgmSource.volume}, Loop: {bgmSource.loop}");
        
        bgmSource.Play();
        isBGMPlaying = true;
        
        Debug.Log($"🎵 BGM 재생 시작됨 - isPlaying: {bgmSource.isPlaying}, time: {bgmSource.time}");
    }

    /// <summary>
    /// BGM 재생 (AudioClip으로 선택)
    /// </summary>
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("⚠️ BGM 클립이 null입니다!");
            return;
        }
        
        bgmSource.clip = clip;
        bgmSource.Play();
        isBGMPlaying = true;
        
        Debug.Log($"🎵 BGM 재생: {clip.name}");
    }

    /// <summary>
    /// BGM 일시 정지
    /// </summary>
    public void PauseBGM()
    {
        if (bgmSource.isPlaying)
        {
            bgmSource.Pause();
            isBGMPlaying = false;
            Debug.Log("⏸️ BGM 일시 정지");
        }
    }

    /// <summary>
    /// BGM 재개
    /// </summary>
    public void ResumeBGM()
    {
        if (!bgmSource.isPlaying && isBGMPlaying)
        {
            bgmSource.UnPause();
            Debug.Log("▶️ BGM 재개");
        }
    }

    /// <summary>
    /// BGM 중지
    /// </summary>
    public void StopBGM()
    {
        bgmSource.Stop();
        isBGMPlaying = false;
        Debug.Log("⏹️ BGM 중지");
    }

    /// <summary>
    /// BGM 켜기/끄기 토글
    /// </summary>
    public void ToggleBGM()
    {
        if (bgmSource.isPlaying)
        {
            PauseBGM();
        }
        else
        {
            ResumeBGM();
        }
    }

    /// <summary>
    /// BGM 볼륨 설정
    /// </summary>
    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        bgmSource.volume = bgmVolume;
        Debug.Log($"🔊 BGM 볼륨 설정: {bgmVolume}");
    }

    /// <summary>
    /// BGM이 재생 중인지 확인
    /// </summary>
    public bool IsBGMPlaying()
    {
        return bgmSource.isPlaying;
    }

    /// <summary>
    /// BGM 볼륨을 지정 시간에 걸쳐 페이드합니다.
    /// </summary>
    /// <param name="targetVolume">목표 볼륨 (0~1)</param>
    /// <param name="duration">페이드 시간 (초)</param>
    private Coroutine bgmFadeCoroutine;
    
    public void FadeBGMVolume(float targetVolume, float duration = 1f)
    {
        if (bgmFadeCoroutine != null)
            StopCoroutine(bgmFadeCoroutine);
        
        bgmFadeCoroutine = StartCoroutine(FadeBGMCoroutine(targetVolume, duration));
    }
    
    private IEnumerator FadeBGMCoroutine(float targetVolume, float duration)
    {
        float startVolume = bgmSource.volume;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            bgmSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }
        
        bgmSource.volume = targetVolume;
        bgmFadeCoroutine = null;
        Debug.Log($"🔊 BGM 볼륨 페이드 완료: {targetVolume}");
    }
    
    /// <summary>
    /// BGM 볼륨을 원래 설정값으로 페이드 복원합니다.
    /// </summary>
    public void RestoreBGMVolume(float duration = 1f)
    {
        FadeBGMVolume(bgmVolume, duration);
    }

    #endregion

    #region 효과음 제어

    /// <summary>
    /// 효과음 재생 (AudioClip)
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("⚠️ 효과음 클립이 null입니다!");
            return;
        }
        
        // 사용 가능한 AudioSource 찾기
        AudioSource availableSource = GetAvailableSFXSource();
        
        if (availableSource != null)
        {
            availableSource.PlayOneShot(clip, sfxVolume);
            Debug.Log($"🔔 효과음 재생: {clip.name}");
        }
        else
        {
            Debug.LogWarning("⚠️ 사용 가능한 효과음 AudioSource가 없습니다!");
        }
    }

    /// <summary>
    /// 효과음 재생 (AudioClip, 볼륨 지정)
    /// </summary>
    public void PlaySFX(AudioClip clip, float volumeScale)
    {
        if (clip == null)
        {
            Debug.LogWarning("⚠️ 효과음 클립이 null입니다!");
            return;
        }
        
        AudioSource availableSource = GetAvailableSFXSource();
        
        if (availableSource != null)
        {
            availableSource.PlayOneShot(clip, sfxVolume * volumeScale);
            Debug.Log($"🔔 효과음 재생: {clip.name} (볼륨: {sfxVolume * volumeScale})");
        }
        else
        {
            Debug.LogWarning("⚠️ 사용 가능한 효과음 AudioSource가 없습니다!");
        }
    }
    
    /// <summary>
    /// 효과음 재생 (인덱스로 선택)
    /// </summary>
    public void PlaySFXByIndex(int index)
    {
        if (sfxClips == null || sfxClips.Length == 0)
        {
            Debug.LogWarning("⚠️ SFX 클립 배열이 설정되지 않았습니다!");
            return;
        }
        
        if (index < 0 || index >= sfxClips.Length)
        {
            Debug.LogWarning($"⚠️ SFX 인덱스가 범위를 벗어났습니다: {index} (배열 크기: {sfxClips.Length})");
            return;
        }
        
        if (sfxClips[index] == null)
        {
            Debug.LogWarning($"⚠️ SFX 클립이 null입니다: 인덱스 {index}");
            return;
        }
        
        PlaySFX(sfxClips[index]);
    }
    
    /// <summary>
    /// 효과음 재생 (인덱스로 선택, 볼륨 지정)
    /// </summary>
    public void PlaySFXByIndex(int index, float volumeScale)
    {
        if (sfxClips == null || sfxClips.Length == 0)
        {
            Debug.LogWarning("⚠️ SFX 클립 배열이 설정되지 않았습니다!");
            return;
        }
        
        if (index < 0 || index >= sfxClips.Length)
        {
            Debug.LogWarning($"⚠️ SFX 인덱스가 범위를 벗어났습니다: {index} (배열 크기: {sfxClips.Length})");
            return;
        }
        
        if (sfxClips[index] == null)
        {
            Debug.LogWarning($"⚠️ SFX 클립이 null입니다: 인덱스 {index}");
            return;
        }
        
        PlaySFX(sfxClips[index], volumeScale);
    }

    /// <summary>
    /// 구슬 깨짐 효과음 순차 재생.
    /// 호출될 때마다 crystalBreakClips 배열의 다음 클립을 재생하고,
    /// 마지막 클립 이후에는 다시 첫 번째로 순환합니다.
    /// </summary>
    public void PlayCrystalBreakSFX()
    {
        if (crystalBreakClips == null || crystalBreakClips.Length == 0)
        {
            Debug.LogWarning("⚠️ crystalBreakClips가 설정되지 않았습니다!");
            return;
        }

        AudioClip clip = crystalBreakClips[crystalBreakIndex];
        crystalBreakIndex = (crystalBreakIndex + 1) % crystalBreakClips.Length;

        if (clip == null)
        {
            Debug.LogWarning($"⚠️ crystalBreakClips[{crystalBreakIndex}] 클립이 null입니다!");
            return;
        }

        PlaySFX(clip);
    }

    /// <summary>
    /// 구슬 깨짐 효과음 순환 인덱스를 처음으로 리셋.
    /// 새 라운드 시작 시 호출하면 항상 첫 번째 사운드부터 시작합니다.
    /// </summary>
    public void ResetCrystalBreakSequence()
    {
        crystalBreakIndex = 0;
    }

    /// <summary>
    /// 사용 가능한 효과음 AudioSource 찾기
    /// </summary>
    private AudioSource GetAvailableSFXSource()
    {
        // 재생 중이지 않은 AudioSource 찾기
        foreach (var source in sfxSources)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }
        
        // 모두 재생 중이면 첫 번째 것 반환 (덮어쓰기)
        return sfxSources[0];
    }

    /// <summary>
    /// 모든 효과음 중지
    /// </summary>
    public void StopAllSFX()
    {
        foreach (var source in sfxSources)
        {
            source.Stop();
        }
        Debug.Log("⏹️ 모든 효과음 중지");
    }

    /// <summary>
    /// 효과음 볼륨 설정
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        
        foreach (var source in sfxSources)
        {
            source.volume = sfxVolume;
        }
        
        Debug.Log($"🔊 효과음 볼륨 설정: {sfxVolume}");
    }

    #endregion

    #region 유틸리티

    /// <summary>
    /// 모든 사운드 중지
    /// </summary>
    public void StopAllSounds()
    {
        StopBGM();
        StopAllSFX();
        Debug.Log("⏹️ 모든 사운드 중지");
    }

    /// <summary>
    /// 전체 볼륨 설정 (BGM + SFX)
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        SetBGMVolume(volume);
        SetSFXVolume(volume);
        Debug.Log($"🔊 전체 볼륨 설정: {volume}");
    }

    #endregion
}

