using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Video;

public class WebGLVideoPlayer : MonoBehaviour
{
    [DllImport("__Internal")]
    private static extern void CreateVideo(string id, string url);

    [DllImport("__Internal")]
    private static extern void RemoveVideo(string id);

    public VideoPlayer unityVideoPlayer; // 에디터/Standalone에서 쓸 VideoPlayer
    private string videoId = "myVideoPlayer";

    /// <summary>
    /// 서버 영상 URL을 재생
    /// </summary>
    public void PlayVideo(string url)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL 환경 → HTML <video> 태그 실행
        CreateVideo(videoId, url);
#else
        // 에디터 / PC 실행 → Unity VideoPlayer로 실행
        if (unityVideoPlayer == null)
        {
            unityVideoPlayer = gameObject.AddComponent<VideoPlayer>();
            unityVideoPlayer.playOnAwake = false;
            unityVideoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
            unityVideoPlayer.targetCameraAlpha = 1.0f;
        }

        unityVideoPlayer.source = VideoSource.Url;
        unityVideoPlayer.url = url;
        unityVideoPlayer.Play();
#endif
    }

    /// <summary>
    /// 영상 정지
    /// </summary>
    public void StopVideo()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        RemoveVideo(videoId);
#else
        if (unityVideoPlayer != null)
        {
            unityVideoPlayer.Stop();
        }
#endif
    }
}