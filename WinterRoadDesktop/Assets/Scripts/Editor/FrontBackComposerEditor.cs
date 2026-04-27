using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(FrontBackComposer))]
public class FrontBackComposerEditor : Editor
{
    private FrontBackComposer composer;
    
    void OnEnable()
    {
        composer = (FrontBackComposer)target;
    }
    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        GUILayout.Space(10);
        GUILayout.Label("실시간 텍스처 미리보기", EditorStyles.boldLabel);
        
        if (Application.isPlaying && composer != null)
        {
            // 런타임 상태 표시
            EditorGUILayout.LabelField("시스템 상태", "실행 중");
            
            // 텍스처 미리보기
            DrawTexturePreview("전면 구형 왜곡", composer.GetFrontSphericalTexture());
            DrawTexturePreview("후면 구형 왜곡", composer.GetBackSphericalTexture());
            DrawTexturePreview("최종 합성 (1920x928)", composer.GetFinalComposedTexture());
            
            GUILayout.Space(10);
            
            // Asset 복사 버튼
            if (GUILayout.Button("현재 텍스처를 Asset에 저장"))
            {
                composer.CopyRuntimeTexturesToAssets();
            }
            
            // 실시간 업데이트를 위한 Repaint
            if (Event.current.type == EventType.Layout)
            {
                Repaint();
            }
        }
        else
        {
            EditorGUILayout.LabelField("시스템 상태", "정지됨 (Play 모드에서 확인 가능)");
            
            // Asset 생성 버튼
            if (GUILayout.Button("미리보기용 Asset 생성"))
            {
                composer.CreatePreviewTextures();
            }
        }
        
        GUILayout.Space(10);
        GUILayout.Label("에셋 폴더 확인", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "실행 후 'Assets/RenderTexture/Generated/' 폴더에서\n" +
            "구형 왜곡된 텍스처들을 직접 확인할 수 있습니다.",
            MessageType.Info
        );
        
        // 폴더 열기 버튼
        if (GUILayout.Button("Generated 폴더 열기"))
        {
            string folderPath = Application.dataPath + "/RenderTexture/Generated";
            if (System.IO.Directory.Exists(folderPath))
            {
                EditorUtility.RevealInFinder(folderPath);
            }
            else
            {
                Debug.LogWarning("Generated 폴더가 아직 생성되지 않았습니다. 먼저 Play 모드를 실행해주세요.");
            }
        }
    }
    
    void DrawTexturePreview(string label, RenderTexture texture)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(150));
        
        if (texture != null)
        {
            EditorGUILayout.LabelField($"{texture.width}x{texture.height}", GUILayout.Width(100));
            
            // 작은 미리보기 이미지
            Rect previewRect = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64));
            EditorGUI.DrawPreviewTexture(previewRect, texture);
            
            // 텍스처 선택 버튼
            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                Selection.activeObject = texture;
            }
        }
        else
        {
            EditorGUILayout.LabelField("생성되지 않음", GUILayout.Width(100));
        }
        
        EditorGUILayout.EndHorizontal();
    }
}



