using UnityEngine;
using System.Collections;
using EkumeEnumerations;
using System.Collections.Generic;

[RequireComponent(typeof (Animator))]
[RequireComponent(typeof (Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class AnimatorManagerOnCollision : MonoBehaviour
{
    public List<string> parameterNames = new List<string>();
    public List<string> tags = new List<string>();

    public Animator currentAnimator;
    
    void Awake ()
    {
        currentAnimator = GetComponent<Animator>();
    }

    void OnCollisionEnter2D (Collision2D other)
    {
        if(tags.Contains(other.collider.tag))
        {
            currentAnimator.SetBool(parameterNames[tags.IndexOf(other.collider.tag)], true);
        }
    }

    void OnCollisionExit2D (Collision2D other)
    {
        if (tags.Contains(other.collider.tag))
        {
            currentAnimator.SetBool(parameterNames[tags.IndexOf(other.collider.tag)], false);
        }
    }
}