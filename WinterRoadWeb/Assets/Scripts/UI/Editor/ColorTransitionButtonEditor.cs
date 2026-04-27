#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(ColorTransitionButton), true)]
[CanEditMultipleObjects]
public class ColorTransitionButtonEditor : ButtonEditor
{
    private SerializedProperty _targetGraphics;
    private SerializedProperty _normalColor;
    private SerializedProperty _highlightedColor;
    private SerializedProperty _pressedColor;
    private SerializedProperty _selectedColor;
    private SerializedProperty _disabledColor;
    private SerializedProperty _fadeDuration;

    protected override void OnEnable()
    {
        base.OnEnable();
        _targetGraphics  = serializedObject.FindProperty("targetGraphics");
        _normalColor     = serializedObject.FindProperty("normalColor");
        _highlightedColor = serializedObject.FindProperty("highlightedColor");
        _pressedColor    = serializedObject.FindProperty("pressedColor");
        _selectedColor   = serializedObject.FindProperty("selectedColor");
        _disabledColor   = serializedObject.FindProperty("disabledColor");
        _fadeDuration    = serializedObject.FindProperty("fadeDuration");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("추가 색상 전환 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_targetGraphics, true);
        EditorGUILayout.PropertyField(_normalColor);
        EditorGUILayout.PropertyField(_highlightedColor);
        EditorGUILayout.PropertyField(_pressedColor);
        EditorGUILayout.PropertyField(_selectedColor);
        EditorGUILayout.PropertyField(_disabledColor);
        EditorGUILayout.PropertyField(_fadeDuration);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
