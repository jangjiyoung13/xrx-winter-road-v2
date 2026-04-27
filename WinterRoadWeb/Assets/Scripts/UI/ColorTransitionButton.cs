using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Button을 상속받아 상태 전환 시 지정한 Graphic(Image, Text 등)의 색상을 함께 변경하는 컴포넌트.
/// Inspector에서 targetGraphics 배열에 원하는 Image나 Text를 넣고, 각 상태별 색상을 설정하면 됩니다.
/// </summary>
public class ColorTransitionButton : Button
{
    [Header("추가 색상 전환 대상")]
    [SerializeField] private Graphic[] targetGraphics;   // 색상을 변경할 Image, Text 등 Graphic 배열

    [Header("상태별 색상 설정")]
    [SerializeField] private Color normalColor      = Color.white;
    [SerializeField] private Color highlightedColor  = new Color(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color pressedColor      = new Color(0.7f, 0.7f, 0.7f, 1f);
    [SerializeField] private Color selectedColor     = new Color(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color disabledColor     = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    [SerializeField] private float fadeDuration      = 0.1f;

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(state, instant);

        if (targetGraphics == null || targetGraphics.Length == 0)
            return;

        Color targetColor;
        switch (state)
        {
            case SelectionState.Highlighted:
                targetColor = highlightedColor;
                break;
            case SelectionState.Pressed:
                targetColor = pressedColor;
                break;
            case SelectionState.Selected:
                targetColor = selectedColor;
                break;
            case SelectionState.Disabled:
                targetColor = disabledColor;
                break;
            case SelectionState.Normal:
            default:
                targetColor = normalColor;
                break;
        }

        foreach (var graphic in targetGraphics)
        {
            if (graphic == null) continue;

            if (instant)
            {
                graphic.canvasRenderer.SetColor(targetColor);
            }
            else
            {
                graphic.CrossFadeColor(targetColor, fadeDuration, true, true);
            }
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        // Inspector에서 값 변경 시 즉시 반영
        if (targetGraphics != null)
        {
            foreach (var graphic in targetGraphics)
            {
                if (graphic != null)
                    graphic.canvasRenderer.SetColor(normalColor);
            }
        }
    }
#endif
}
