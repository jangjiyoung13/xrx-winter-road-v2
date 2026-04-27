using UnityEngine;

public class FullScreenResize : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Screen.SetResolution(1920, 928, FullScreenMode.FullScreenWindow);
        //Screen.SetResolutions

    }

  
}
