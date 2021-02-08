using UnityEngine;
using System.Collections;
using EkumeEnumerations;
using System.Collections.Generic;

[RequireComponent(typeof (Animator))]
public class AnimatorManagerOfPlayer : MonoBehaviour
{
    public List<PlayerStatesEnum> playerStates = new List<PlayerStatesEnum>();
    public List<int> selectedCategoryOfAttack = new List<int>();
    public List<int> categoryOfWeaponSelected = new List<int>();
    public List<string> parameterNames = new List<string>();
    public Animator currentAnimator;

    bool objectDisabled = false;
    
    void Awake ()
    {
        currentAnimator = GetComponent<Animator>();

        if (gameObject.tag == "Mount")
        {
            this.enabled = false;
        }
    }

    void Start ()
    {
        StartCoroutine("StatusUpdate");
    }

    void OnEnable ()
    {
        if (objectDisabled)
        {
            StartCoroutine("StatusUpdate");
            objectDisabled = false;
        }
    }

    IEnumerator StatusUpdate()
    {
        for (;;)
        {
            RefreshAnimations();
            yield return new WaitForSeconds(0.075f);
        }
    }

    void OnDisable()
    {
        RefreshAnimations();
        objectDisabled = true;
    }

    void RefreshAnimations()
    {
#if UNITY_EDITOR
        if (currentAnimator.isInitialized)
        {
#endif
            if (this.gameObject.tag != "Mount" || (DismountOfPlayer.currentMount == this.gameObject)) //Avoid to play the animations in the mounts that the player is not riding
            {
                for (int i = 0; i < playerStates.Count; i++)
                {
                    if (playerStates[i] == PlayerStatesEnum.PlayerAttackWithWeapon)
                        currentAnimator.SetBool(parameterNames[i], PlayerStates.GetAttackCategoryStateValue(selectedCategoryOfAttack[i]));
                    else if (playerStates[i] == PlayerStatesEnum.PlayerIsUsingSpecificWeaponCategory)
                        currentAnimator.SetBool(parameterNames[i], PlayerStates.GetStateOfUseOfWeaponCategory(categoryOfWeaponSelected[i]));
                    else
                        currentAnimator.SetBool(parameterNames[i], PlayerStates.GetPlayerStateValue(playerStates[i]));
                }
            }
#if UNITY_EDITOR
        }
#endif
    }
}