using UnityEngine;
using System.Collections;
using EkumeEnumerations;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour
{
    public static UseWeapon playerWeapon;
    public static PlayerPowers playerPowers;
    public static Transform playerTransform;
    public static PlayerLifeManager playerLifeManager;
    public int playerIdentification;
    Rigidbody2D thisRigidBody;
    PlayerHorizontalMovement playerMovement;

    public static GameObject instance
    {
        get
        {
            return playerTransform.gameObject;
        }
    }

    void Awake ()
    {
        PlayerStates.InicializeStates();
    }

    void OnEnable ()
    {
        SetValuesOfStaticVariables();
        playerMovement = GetComponent<PlayerHorizontalMovement>();
    }

    void Start ()
    {
        thisRigidBody = GetComponent<Rigidbody2D>();
        PlayerStates.SetPlayerStateValue(PlayerStatesEnum.LevelStart, true);
    }

    void Update ()
    {
        if (!PlayerStates.GetPlayerStateValue(PlayerStatesEnum.PlayerIsRidingAMount))
        {
            if (thisRigidBody.velocity.x >= 0.01f || thisRigidBody.velocity.x <= -0.01f)
                PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerIsMovingInXAxis, true);
            else
                PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerIsMovingInXAxis, false);

            //Check and change the direction of the player if the 'howToChangeDirection' option is DependingOnHisVelocity
            if (playerMovement != null)
            {
                if (thisRigidBody.velocity.x > 0.1f)
                {
                    if (playerMovement.howToChangeDirection == PlayerHorizontalMovement.HowToChangeDirection.DependingOnHisVelocity)
                    {
                        if (!PlayerStates.GetPlayerStateValue(PlayerStatesEnum.PlayerDirectionIsRight))
                            transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);

                        PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerDirectionIsRight, true);
                        PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerDirectionIsLeft, false);

                        playerMovement.currentDirection = DirectionsXAxisEnum.Right;
                    }
                }
                else if (thisRigidBody.velocity.x < -0.1f)
                {
                    if (playerMovement.howToChangeDirection == PlayerHorizontalMovement.HowToChangeDirection.DependingOnHisVelocity)
                    {
                        if (!PlayerStates.GetPlayerStateValue(PlayerStatesEnum.PlayerDirectionIsLeft))
                            transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);

                        PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerDirectionIsLeft, true);
                        PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerDirectionIsRight, false);

                        playerMovement.currentDirection = DirectionsXAxisEnum.Left;
                    }
                }
            }
            //---------------------------------------------------------------------------------------------

            if (thisRigidBody.velocity.y > 0)
            {
                PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerVelocityYPositive, true);
                PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerVelocityYNegative, false);
            }
            else if (thisRigidBody.velocity.y < 0)
            {
                PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerVelocityYNegative, true);
                PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerVelocityYPositive, false);
            }
            else
            {
                PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerVelocityYNegative, false);
                PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerVelocityYPositive, false);
            }
        }
    }

    void SetValuesOfStaticVariables ()
    {
        if (GetComponent<UseWeapon>() != null)
            playerWeapon = GetComponent<UseWeapon>();

        if (GetComponent<PlayerPowers>() != null)
            playerPowers = GetComponent<PlayerPowers>();

        if (GetComponent<PlayerLifeManager>() != null)
            playerLifeManager = GetComponent<PlayerLifeManager>();

        playerTransform = this.transform;
    }

    float[] timerToTurnFalseStates = new float[4];

    void LateUpdate ()
    {
        if(PlayerStates.GetPlayerStateValue(PlayerStatesEnum.PlayerLoseOneLive))
        {
            TurnFalseState(0);
        }
        if(PlayerStates.GetPlayerStateValue(PlayerStatesEnum.PlayerLoseHealthPoint))
        {
            TurnFalseState(1);
        }
        if(PlayerStates.GetPlayerStateValue(PlayerStatesEnum.PlayerLoseAllLives))
        {
            TurnFalseState(2);
        }
        if (PlayerStates.GetPlayerStateValue(PlayerStatesEnum.PlayerReappearInSavePoint))
        {
            TurnFalseState(3);
        }
    }

    void TurnFalseState (int index)
    {
        switch (index)
        {
            case 0:

                timerToTurnFalseStates[0] += Time.deltaTime;

                if (timerToTurnFalseStates[0] > 0.1f)
                {
                    PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerLoseOneLive, false);
                    timerToTurnFalseStates[0] = 0;
                }

                break;
            case 1:

                timerToTurnFalseStates[1] += Time.deltaTime;

                if (timerToTurnFalseStates[1] > 0.1f)
                {
                    PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerLoseHealthPoint, false);
                    timerToTurnFalseStates[1] = 0;
                }

                break;
            case 2:

                timerToTurnFalseStates[2] += Time.deltaTime;

                if (timerToTurnFalseStates[2] > 0.15f)
                {
                    PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerLoseAllLives, false);
                    timerToTurnFalseStates[2] = 0;
                }

                break;
            case 5:

                timerToTurnFalseStates[3] += Time.deltaTime;

                if (timerToTurnFalseStates[3] > 0.1f)
                {
                    PlayerStates.SetPlayerStateValue(PlayerStatesEnum.PlayerReappearInSavePoint, false);
                    timerToTurnFalseStates[3] = 0;
                }

                break;
        }

    }
}

public static class PlayerStates
{
    public static Dictionary<PlayerStatesEnum, bool> playerStatusList;
    public static Dictionary<int, bool> attackCategoriesStatusList;
    public static Dictionary<int, bool> listOfWeaponCategoriesWhoIsInUse;

    public static void InicializeStates()
    {
        playerStatusList = new Dictionary<PlayerStatesEnum, bool>();

        playerStatusList.Add(PlayerStatesEnum.IsTheSecondJump, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerStartedToMove, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerIsGrounded, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerIsMovingInXAxis, false);
        playerStatusList.Add(PlayerStatesEnum.IsUnderWater, false);
        playerStatusList.Add(PlayerStatesEnum.UsingPowerToFly, false);
        playerStatusList.Add(PlayerStatesEnum.UsingPowerObjectMagnet, false);
        playerStatusList.Add(PlayerStatesEnum.UsingPowerScoreDuplicator, false);
        playerStatusList.Add(PlayerStatesEnum.UsingPowerKillerShield, false);
        playerStatusList.Add(PlayerStatesEnum.UsingPowerProtectorShield, false);
        playerStatusList.Add(PlayerStatesEnum.UsingPowerTrapsConverter, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerDirectionIsRight, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerDirectionIsLeft, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerIsInImmunityTime, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerWinLevel, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerLoseOneLive, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerLoseHealthPoint, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerLoseAllLives, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerIsInConstantReductionOfLife, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerIsCrouchedDown, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerIsRidingAMount, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerInLadder, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerMovesInLadder, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerVelocityYPositive, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerVelocityYNegative, false);
        playerStatusList.Add(PlayerStatesEnum.UsingPowerJetpack, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerMovesInLadderToDown, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerMovesInLadderToUp, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerMovesInLadderToRight, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerMovesInLadderToLeft, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerLoseLevel, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerReappearInSavePoint, false);
        playerStatusList.Add(PlayerStatesEnum.LevelStart, false);
        playerStatusList.Add(PlayerStatesEnum.PlayerIsInParkourWall, false);

        attackCategoriesStatusList = new Dictionary<int, bool>();
        listOfWeaponCategoriesWhoIsInUse = new Dictionary<int, bool>();

        for(int i = 0; i < WeaponFactory.instance.weaponCategories.Count; i++)
        {
            attackCategoriesStatusList.Add(i, false);
            listOfWeaponCategoriesWhoIsInUse.Add(i, false);
        }
    }

    public static void SetPlayerStateValue(PlayerStatesEnum stateToChange, bool newValue)
    {
        playerStatusList[stateToChange] = newValue;
    }

    public static bool GetPlayerStateValue(PlayerStatesEnum customEvent)
    {
        return playerStatusList[customEvent];
    }

    public static void SetAttackCategoryStateValue (int attackCategorie, bool newValue)
    {
        attackCategoriesStatusList[attackCategorie] = newValue;
    }

    public static bool GetAttackCategoryStateValue(int attackCategorie)
    {
        return attackCategoriesStatusList[attackCategorie];
    }

    public static void SetStateOfUseOfWeaponCategory(int weaponCategory, bool newValue)
    {
        for (int i = 0; i < listOfWeaponCategoriesWhoIsInUse.Count; i++)
        {
            if (listOfWeaponCategoriesWhoIsInUse[i])
            {
                listOfWeaponCategoriesWhoIsInUse[i] = false;
                break;
            }
        }

        listOfWeaponCategoriesWhoIsInUse[weaponCategory] = newValue;
    }

    public static bool GetStateOfUseOfWeaponCategory(int weaponCategory)
    {
        return listOfWeaponCategoriesWhoIsInUse[weaponCategory];
    }

    public static void SetStateValueForAllAttackCategories (bool newValueForAll)
    {
        for (int i = 0; i < WeaponFactory.instance.weaponCategories.Count; i++)
        {
            attackCategoriesStatusList[i] = false;
        }
    }
}
