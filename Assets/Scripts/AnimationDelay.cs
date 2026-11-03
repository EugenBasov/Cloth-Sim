using UnityEngine;
using System.Collections;

public class FreezeStart : MonoBehaviour
{
    private Animator _animator;
    public float delayTime = 2.0f;

    void Start()
    {
        _animator = GetComponent<Animator>();
        _animator.speed = 0f;

        StartCoroutine(StartAnimation());
    }

    IEnumerator StartAnimation()
    {
        yield return new WaitForSeconds(delayTime);
        _animator.speed = 1f;
    }
}