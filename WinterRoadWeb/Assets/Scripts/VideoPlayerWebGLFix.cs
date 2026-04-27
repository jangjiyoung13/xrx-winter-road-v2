using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// WebGL에서 VideoPlayer가 Video Clip 대신 StreamingAssets URL을 사용하도록 자동 전환합니다.
/// VideoPlayer와 같은 GameObject에 추가하세요.
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public class VideoPlayerWebGLFix : MonoBehaviour
{
    [Tooltip("StreamingAssets 폴더 내 영상 파일명 (예: video_bg_renewal.mp4)")]
    [SerializeField] private string videoFileName = "video_bg_renewal.mp4";
    
    private void Awake()
    {
        VideoPlayer vp = GetComponent<VideoPlayer>();
        if (vp == null) return;
        
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL: Video Clip은 지원 안 됨 → StreamingAssets URL로 전환
        string url = System.IO.Path.Combine(Application.streamingAssetsPath, videoFileName);
        Debug.Log($"🎬 [WebGL] VideoPlayer를 URL 모드로 전환: {url}");
        
        vp.source = VideoSource.Url;
        vp.url = url;
        
        // Play On Awake가 켜져 있으면 수동으로 재생
        if (vp.playOnAwake)
        {
            vp.playOnAwake = false;  // 중복 재생 방지
            vp.Prepare();
            vp.prepareCompleted += (source) =>
            {
                Debug.Log("🎬 [WebGL] 영상 준비 완료, 재생 시작!");
                vp.Play();
            };
        }
#endif
        // 에디터/Standalone에서는 기존 Video Clip 모드 그대로 사용
    }
}
