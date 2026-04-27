using UnityEngine;

[RequireComponent(typeof(Camera))]
public class SpherePreview : MonoBehaviour
{
    public RenderTexture previewRT; // 1000x1000
    private Camera cam;
    private Cubemap cubemap;
    private Material convertMat;

    void Start()
    {
        cam = GetComponent<Camera>();
        cam.enabled = false;

        cubemap = new Cubemap(512, TextureFormat.RGB24, false);
        convertMat = new Material(Shader.Find("Hidden/CubemapToEquirectangular"));
    }

    void LateUpdate()
    {
        cam.RenderToCubemap(cubemap);
        Graphics.Blit(cubemap, previewRT, convertMat);
    }
}
