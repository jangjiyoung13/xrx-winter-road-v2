using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class CrystalBallAnim : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        transform.DOLocalMove(new Vector3(0, -1, 0), 1f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
    }
}
