using UnityEngine;

public class ResultPanel : MonoBehaviour
{
    [SerializeField] private GameObject redWinObject;
    [SerializeField] private GameObject blueWinObject;
    private void Awake()
    {
        redWinObject.gameObject.SetActive(false);
        blueWinObject.gameObject.SetActive(false);
    }

    public void WinRed()
    {
        redWinObject.gameObject.SetActive(true);
        blueWinObject.gameObject.SetActive(false);
    }

    public void WinBlue()
    {
        redWinObject.gameObject.SetActive(false);
        blueWinObject.gameObject.SetActive(true);
    }
}
