using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PanoramaCamera : MonoBehaviour
{
    public RenderTexture targetTexture; // 3600x1647

    private Camera cam;
    private Cubemap cubemap;
    private Material convertMat;

    void Start()
    {
        cam = GetComponent<Camera>();
        cam.enabled = false; // 직접 그리지 않고 수동 렌더링

        // 큐브맵 준비 (정사각형)
        cubemap = new Cubemap(2048, TextureFormat.RGB24, false);

        // 변환 셰이더 로드 (Equirectangular 변환용)
        convertMat = new Material(Shader.Find("Hidden/CubemapToEquirectangular"));
    }

    void LateUpdate()
    {
        // 카메라에서 큐브맵으로 렌더
        cam.RenderToCubemap(cubemap);

        // 큐브맵을 파노라마로 변환해서 targetTexture에 출력
        Graphics.Blit(cubemap, targetTexture, convertMat);
    }
}
