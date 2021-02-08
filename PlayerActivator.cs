using UnityEngine;
using System.Collections;
using EkumeSavedData.Player;

public class PlayerActivator : MonoBehaviour
{
    public enum WhatActivate { SpecificPlayer, SavedPlayer, AleatoryPlayer }
    
    public WhatActivate whatActivate;
    public int playerToActivate;

    void Awake ()
    {
        if (whatActivate == WhatActivate.SavedPlayer)
        {
            playerToActivate = PlayerSelection.GetPlayerSelected();
        }
        else if (whatActivate == WhatActivate.AleatoryPlayer)
        {
            playerToActivate = Random.Range(0, PlayersManager.instance.playerNames.Count);
        }

        Player[] players = GetComponentsInChildren<Player>(true);
        foreach (Player player in players)
        {
            if (player.playerIdentification != playerToActivate)
            {
                player.gameObject.SetActive(false); //This was made because the object has a delay to destroy itself (by default of Unity) and it needs to be disabled or destroyed immediately, by this reason we added this line.
                Destroy(player.gameObject);
            }
            else
            {
                player.transform.SetParent(null);
                player.gameObject.SetActive(true);
            }
        }
    }
}
