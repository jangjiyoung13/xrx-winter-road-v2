using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CrystalBall : MonoBehaviour
{
    [Header("구슬 오브젝트")]
    [SerializeField] private GameObject stage1_Normal;    // 1단계: 온전한 구슬 (애니메이션 적용 대상)

    [Header("구슬 깨지는 애니메이션")]
    [SerializeField] private Image crystalBallImage; // 구슬 UI 이미지 (Canvas 안)
    [SerializeField] private Sprite[] breakSprites; // 깨지는 애니메이션 스프라이트 배열
    [SerializeField] private float frameRate = 12f; // 초당 프레임 수 (기본 12fps)

    
    [Header("흔들림 효과")]
    [SerializeField] private float shakeIntensity = 0.3f;  // 흔들림 강도
    [SerializeField] private float shakeDuration = 0.2f;   // 흔들림 지속 시간

    [Header("캐릭터")]
    [SerializeField] public CharacterAnimation characterAnimation;

    private bool isBreaking = false;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private bool isShaking = false;

    void Start()
    {
        // 원래 위치와 회전 저장
        originalPosition = transform.localPosition;
        originalRotation = transform.localRotation;

        // Image 컴포넌트가 없으면 자동으로 찾기
        if (crystalBallImage == null && stage1_Normal != null)
        {
            crystalBallImage = stage1_Normal.GetComponent<Image>();
        }

        // 초기 상태: 1단계만 활성화
        ResetCrystalBall();

        // 캐릭터 애니메이션 시작
        if (characterAnimation != null)
        {
            characterAnimation.PlayClose();
        }
        else
        {
            Debug.LogWarning("⚠️ CharacterAnimation이 할당되지 않았습니다!");
        }
    }

    /// <summary>
    /// 구슬을 초기 상태로 리셋
    /// </summary>
    public void ResetCrystalBall()
    {
        // 위치와 회전 리셋
        transform.localPosition = originalPosition;
        transform.localRotation = originalRotation;
        
        // 1단계만 활성화
        if (stage1_Normal != null) stage1_Normal.SetActive(true);
        
        // 이미지를 첫 번째 스프라이트로 리셋 (있다면)
        if (crystalBallImage != null && breakSprites != null && breakSprites.Length > 0)
        {
            crystalBallImage.sprite = breakSprites[0];
        }
        
        isBreaking = false;
        
        Debug.Log("💎 CrystalBall 초기화 - 1단계 (온전한 상태)");
    }
    
    /// <summary>
    /// 구슬을 비활성화 (현재는 사용하지 않음 - 캐릭터와 구슬이 합쳐진 스프라이트이므로)
    /// </summary>
    public void HideAllStages()
    {
        if (stage1_Normal != null) stage1_Normal.SetActive(false);
        
        Debug.Log("💎 CrystalBall 비활성화 (현재 미사용)");
    }

    /// <summary>
    /// 구슬 깨지는 애니메이션 재생 (서버에서 게임 종료 신호가 왔을 때 호출)
    /// </summary>
    public void PlayBreakAnimation()
    {
        if (!isBreaking)
        {
            StartCoroutine(PlayBreakAnimationCoroutine());
        }
        else
        {
            Debug.LogWarning("⚠️ CrystalBall 애니메이션이 이미 재생 중입니다!");
        }
    }
    
    /// <summary>
    /// 구슬 깨지는 스프라이트 애니메이션 재생 코루틴
    /// </summary>
    private IEnumerator PlayBreakAnimationCoroutine()
    {
        isBreaking = true;
        
        if (crystalBallImage == null || breakSprites == null || breakSprites.Length == 0)
        {
            Debug.LogWarning("⚠️ CrystalBall Image 또는 BreakSprites가 할당되지 않았습니다!");
            isBreaking = false;
            yield break;
        }
        
        Debug.Log($"💎 CrystalBall 깨지는 애니메이션 시작! (프레임 수: {breakSprites.Length})");
        
        // 효과음 재생 (구슬 깨짐 4종 순환)
        SoundManager.Instance.PlayCrystalBreakSFX();
        
        // 각 프레임을 순차적으로 표시
        float frameDelay = 1f / frameRate;
        for (int i = 0; i < breakSprites.Length; i++)
        {
            crystalBallImage.sprite = breakSprites[i];
            yield return new WaitForSeconds(frameDelay);
        }
        
        isBreaking = false;
        Debug.Log("💎 CrystalBall 스프라이트 애니메이션 재생 완료!");
    }

    /// <summary>
    /// 현재 단계 확인
    /// </summary>
    public int GetCurrentStage()
    {
        // 깨지는 중이면 2단계
        if (isBreaking)
        {
            return 2;
        }
        
        // 1단계 (온전한 상태)
        if (stage1_Normal != null && stage1_Normal.activeSelf) return 1;
        
        return 0;
    }

    /// <summary>
    /// 버튼을 누를 때마다 구슬을 흔들어줌
    /// </summary>
    public void Shake()
    {
        // 이미 흔들리고 있으면 무시
        if (isShaking) return;
        
        StartCoroutine(ShakeCoroutine());
    }

    /// <summary>
    /// 구슬을 흔드는 효과 코루틴
    /// </summary>
    private IEnumerator ShakeCoroutine()
    {
        isShaking = true;
        float elapsed = 0f;
        
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            
            // 랜덤한 위치 오프셋 (작게)
            float x = Random.Range(-1f, 1f) * shakeIntensity;
            float y = Random.Range(-1f, 1f) * shakeIntensity;
            
            // 랜덤한 회전 오프셋 (z축만)
            float z = Random.Range(-1f, 1f) * shakeIntensity * 30f; // 각도로 변환
            
            transform.localPosition = originalPosition + new Vector3(x, y, 0f);
            transform.localRotation = originalRotation * Quaternion.Euler(0f, 0f, z);
            
            yield return null;
        }
        
        // 원래 위치와 회전으로 복귀
        transform.localPosition = originalPosition;
        transform.localRotation = originalRotation;
        
        isShaking = false;
    }

}
