using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;

    void Awake()
    {        
        if (animator == null)
        {
            Debug.LogWarning($"⚠️ Animator 컴포넌트를 찾을 수 없습니다: {gameObject.name}");
        }
    }

    void Start()
    {
        // close로 시작 (Animator Controller의 기본 상태이므로 생략 가능)
        PlayClose();
    }

    public void PlayIdle()
    {
        if (animator == null)
        {
            Debug.LogWarning("⚠️ Animator가 없어서 PlayIdle을 실행할 수 없습니다.");
            return;
        }
        
        Debug.Log("PlayIdle");
        animator.Play("yeti_idle"); // Idle 애니메이션 이름과 일치해야 함
    }

    public void PlayHappy()
    {
        if (animator == null)
        {
            Debug.LogWarning("⚠️ Animator가 없어서 PlayHappy를 실행할 수 없습니다.");
            return;
        }
        
        Debug.Log("PlayHappy");
        animator.Play("yeti_happy");
    }

    public void PlayClose()
    {
        if (animator == null)
        {
            Debug.LogWarning("⚠️ Animator가 없어서 PlayClose를 실행할 수 없습니다.");
            return;
        }
        
        Debug.Log("PlayClose");
        animator.Play("yeti_close");
    }
}
