using System.Collections;
using UnityEngine;

/// <summary>
/// RawImage(구슬 비디오)에 붙여서 버튼 클릭 시 타격감을 연출하는 컴포넌트.
/// Punch() 호출 시 랜덤 360° 방향으로 튀었다가 원래 위치로 탄성 복귀합니다.
/// GamePanel 등 외부에서 Punch()만 호출하면 됩니다.
/// </summary>
public class BallImpactEffect : MonoBehaviour
{
    [Header("이동 펀치")]
    [Tooltip("튕겨나가는 최대 거리 (px)")]
    [SerializeField] private float punchDistance = 20f;

    [Tooltip("이동 펀치 지속 시간 (초)")]
    [SerializeField] private float punchDuration = 0.25f;

    [Header("스케일 펀치")]
    [Tooltip("스케일이 커지는 비율 (0.15 = 15%)")]
    [SerializeField] private float punchScaleAmount = 0.12f;

    [Tooltip("스케일 펀치 지속 시간 (초)")]
    [SerializeField] private float scalePunchDuration = 0.2f;

    [Header("탄성 설정")]
    [Tooltip("바운스 횟수 (높을수록 통통 튀는 느낌)")]
    [SerializeField] private float bounceFrequency = 2.5f;

    [Tooltip("감쇠 속도 (높을수록 빠르게 멈춤)")]
    [SerializeField] private float damping = 1.0f;

    [Header("콤보 연동")]
    [Tooltip("콤보에 따라 효과 강도를 올릴지 여부")]
    [SerializeField] private bool scaleWithCombo = true;

    [Tooltip("콤보 최대 배율 (예: 2.0이면 최대 2배 강도)")]
    [SerializeField] private float maxComboMultiplier = 1.8f;

    [Tooltip("최대 배율에 도달하는 콤보 수")]
    [SerializeField] private int comboForMaxMultiplier = 20;

    // 내부 상태
    private RectTransform rectTransform;
    private Vector2 originalAnchoredPos;
    private Vector3 originalScale;
    private bool originalSaved = false;

    private Coroutine positionCoroutine;
    private Coroutine scaleCoroutine;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        SaveOriginal();
    }

    private void SaveOriginal()
    {
        if (rectTransform != null && !originalSaved)
        {
            originalAnchoredPos = rectTransform.anchoredPosition;
            originalScale = rectTransform.localScale;
            originalSaved = true;
        }
    }

    /// <summary>
    /// 타격 효과 재생 (기본 강도). 버튼 클릭 시 호출.
    /// </summary>
    public void Punch()
    {
        Punch(0);
    }

    /// <summary>
    /// 타격 효과 재생 (콤보 수에 따라 강도 조절).
    /// </summary>
    /// <param name="comboCount">현재 콤보 수 (0이면 기본 강도)</param>
    public void Punch(int comboCount)
    {
        SaveOriginal();
        if (rectTransform == null) return;

        // 콤보 배율 계산
        float multiplier = 1f;
        if (scaleWithCombo && comboCount > 0)
        {
            float t = Mathf.Clamp01((float)comboCount / comboForMaxMultiplier);
            multiplier = Mathf.Lerp(1f, maxComboMultiplier, t);
        }

        // 랜덤 360° 방향
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        // 이동 펀치
        if (positionCoroutine != null)
            StopCoroutine(positionCoroutine);
        positionCoroutine = StartCoroutine(PositionPunchCoroutine(direction, multiplier));

        // 스케일 펀치
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ScalePunchCoroutine(multiplier));
    }

    /// <summary>
    /// 랜덤 방향으로 튀었다가 탄성 복귀하는 코루틴
    /// </summary>
    private IEnumerator PositionPunchCoroutine(Vector2 direction, float multiplier)
    {
        float elapsed = 0f;
        float distance = punchDistance * multiplier;

        while (elapsed < punchDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / punchDuration;

            // 감쇠 사인파: 빠르게 튀었다가 통통 튀면서 돌아옴
            float decay = 1f - Mathf.Pow(t, damping);
            float wave = Mathf.Sin(t * Mathf.PI * bounceFrequency) * decay;

            Vector2 offset = direction * distance * wave;
            rectTransform.anchoredPosition = originalAnchoredPos + offset;

            yield return null;
        }

        rectTransform.anchoredPosition = originalAnchoredPos;
        positionCoroutine = null;
    }

    /// <summary>
    /// 스케일이 살짝 커졌다가 탄성으로 돌아오는 코루틴
    /// </summary>
    private IEnumerator ScalePunchCoroutine(float multiplier)
    {
        float elapsed = 0f;
        float scaleAmount = punchScaleAmount * multiplier;

        while (elapsed < scalePunchDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scalePunchDuration;

            // 빠르게 커졌다가 바운스하며 원래 크기로
            float bounce = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t) * scaleAmount;
            rectTransform.localScale = originalScale * (1f + bounce);

            yield return null;
        }

        rectTransform.localScale = originalScale;
        scaleCoroutine = null;
    }

    /// <summary>
    /// 위치와 스케일을 원래대로 즉시 복원
    /// </summary>
    public void ResetImmediate()
    {
        if (positionCoroutine != null)
        {
            StopCoroutine(positionCoroutine);
            positionCoroutine = null;
        }
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
            scaleCoroutine = null;
        }

        if (rectTransform != null && originalSaved)
        {
            rectTransform.anchoredPosition = originalAnchoredPos;
            rectTransform.localScale = originalScale;
        }
    }
}
