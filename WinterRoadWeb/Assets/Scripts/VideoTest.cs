using UnityEngine;

public class VideoTest : MonoBehaviour
{
    public WebGLVideoPlayer player;

    public void Play()
    {
        //player.PlayVideo("https://example.com/video.mp4");
        player.unityVideoPlayer.Play();
    }

    public void Stop()
    {
        player.StopVideo();
    }
}