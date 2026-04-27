using UnityEngine;

public class PanoramaOutput : MonoBehaviour
{
    public Camera sourceCamera;
    public RenderTexture panoramaRT; // 3600x1647

    private Cubemap cubemap;
    private Material convertMat;

    void Start()
    {
        cubemap = new Cubemap(2048, TextureFormat.RGB24, false);
        convertMat = new Material(Shader.Find("Hidden/CubemapToEquirectangular"));
    }

    void LateUpdate()
    {
        // 1. 카메라 → 큐브맵
        sourceCamera.RenderToCubemap(cubemap);

        // 2. 큐브맵 → Equirectangular(RenderTexture)
        Graphics.Blit(cubemap, panoramaRT, convertMat);
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        // 3. 최종 출력 = panoramaRT
        Graphics.Blit(panoramaRT, dest);
    }
}