using UnityEngine;
using System.Collections;
using EkumeEnumerations;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

//namespace that saves the data inside the game, coins, ammon, etc...
namespace EkumeSavedData
{
    // Gets and sets for the Scores accumulated, you can access from any script
    namespace Scores
    {
        public class Accumulated
        {
            public static float GetAccumulated(int ScoreNumber)
            {
                return EkumeSecureData.GetFloat("Accumulated-" + ScoreNumber,
                    ScoreTypesManager.instance.ScoresData[ScoreNumber].defaultValue);
            }

            public static void SetAccumulated(int ScoreNumber, float newValue)
            {
                EkumeSecureData.SetFloat("Accumulated-" + ScoreNumber, newValue);
            }
        }

        // Provides control score, about best score and score for level
        public class Score
        {
            // Key:     id level
            // Value:   Score 
            public static Dictionary<int, float> scoresInLevel = new Dictionary<int, float>();

            // Set the score of level
            // Key:     ID of type of Score
            // Value:   Score 
            public static void SetScoreOfLevel(int ScoreNumber, float newValue)
            {
                if (scoresInLevel.ContainsKey(ScoreNumber))   // If already exist, update the value
                {
                    scoresInLevel[ScoreNumber] = newValue;
                }
                else                                                // Else, add the register
                {
                    scoresInLevel.Add(ScoreNumber, newValue);
                }
            }

            // Get the score by level Score number
            // if the Score number dont exist return 0
            public static float GetScoreOfLevel(int ScoreNumber)
            {
                return (scoresInLevel.ContainsKey(ScoreNumber)) ? scoresInLevel[ScoreNumber] : 0;
            }

            // --------------------------------------------------------------------------------- /

            public static void SetBestScoreOfLevel(int ScoreNumber, string levelIdentification, float newValue)
            {
                EkumeSecureData.SetFloat("BestScoreOfLevel-" + levelIdentification + "-" + ScoreNumber, newValue);
            }

            public static float GetBestScoreOfLevel(int ScoreNumber, string levelIdentification)
            {
                return EkumeSecureData.GetFloat("BestScoreOfLevel-" + levelIdentification + "-" + ScoreNumber, 0);
            }

            public static float GetTotalBestScoreOfWorld(int worldNumber, int ScoreNumber)
            {
                float totalScore = 0;
                for (int i = 1; i < Levels.GetNumberOfLevelsOfWorld(worldNumber); i++)
                {
                    totalScore += GetBestScoreOfLevel(ScoreNumber, Levels.ConvertToLevelIdentification(worldNumber, i));
                }
                return totalScore;
            }
        }
    }

    namespace WeaponInventory
    {
        public class Bullets
        {
            public static void SetBulletsToGun(int gunNumber, int newValue)
            {
                PlayerPrefs.SetInt("BulletsOfGun" + gunNumber, newValue);
            }

            public static int GetBulletsOfGun(int gunNumber)
            {
                return PlayerPrefs.GetInt("BulletsOfGun" + gunNumber,
                    WeaponFactory.instance.weaponsData[gunNumber].defaultBulletQuantity);
            }
        }

        public class Weapons
        {
            public static void SetWeaponToInventory(int weaponNumber)
            {
                PlayerPrefs.SetInt("WeaponInInventory" + weaponNumber, 1);
            }

            public static bool IsWeaponInInventory(int weaponNumber)
            {
                return PlayerPrefs.HasKey("WeaponInInventory" + weaponNumber);
            }

            public static void DeleteWeaponOfInventory(int weaponNumber)
            {
                PlayerPrefs.DeleteKey("WeaponInInventory" + weaponNumber);
            }

            public static void SetWeaponThatIsUsing(int weaponNumber)
            {
                PlayerPrefs.SetInt("WeaponInUse", weaponNumber);
            }

            public static int GetWeaponThatIsUsing()
            {
                return PlayerPrefs.GetInt("WeaponInUse",
                    (WeaponFactory.instance.startWithGunTheFirsTime) ?
                    WeaponFactory.instance.gunToStartByFirstTime : -1);
            }
        }
    }

    public class ObjectsInGame
    {
        //--- Used by: DestroyIfWasObtainedBefore.cs

        public static bool IsThisObjectObtainedBefore(string objectCode)
        {
            return EkumeSecureData.HasKey(objectCode);
        }

        public static void SetObjectObtained(string objectCode)
        {
            EkumeSecureData.SetInt(objectCode, 1);
        }

        //-----------------------------------------
    }

    public class ClothingInventory
    {
        public static void SetItemToInventory(int itemID)
        {
            PlayerPrefs.SetInt("ClothingItem-" + itemID, 1);
        }

        public static bool IsItemInInventory(int itemID)
        {
            return PlayerPrefs.HasKey("ClothingItem-" + itemID);
        }

        public static void PutItem(int itemID)
        {
            PlayerPrefs.SetInt("ItemPlaced-" + ClothingFactory.instance.items[itemID].category, itemID);
        }

        /// <summary>
        /// Returns the ID of the item placed, of the corresponding category
        /// </summary>
        /// <param name="category">Category of the object</param>
        /// <returns></returns>
        public static int GetItemPlacedInCategory(int category)
        {
            return PlayerPrefs.GetInt("ItemPlaced-" + category, 0);
        }
    }

    public class SavePoints
    {
        public static void SetSavePoint(int savePointNumber)
        {
            PlayerPrefs.SetInt(SceneManager.GetActiveScene().name + "SavedPoint", savePointNumber);
        }

        public static int GetSavePointNumber()
        {
            return PlayerPrefs.GetInt(SceneManager.GetActiveScene().name + "SavedPoint", 0);
        }

        public static bool ExistSavePoint()
        {
            return PlayerPrefs.HasKey(SceneManager.GetActiveScene().name + "SavedPoint");
        }

        public static void DeleteSavePoint()
        {
            if (PlayerPrefs.HasKey(SceneManager.GetActiveScene().name + "SavedPoint"))
            {
                PlayerPrefs.DeleteKey(SceneManager.GetActiveScene().name + "SavedPoint");
            }
        }

        public static void SetCurrentTimeOfSavePoint(float currentTime)
        {
            PlayerPrefs.SetFloat("CurrentTimeOfSavePoint", currentTime);
        }

        public static float GetLastTimeOfSavePoint()
        {
            return PlayerPrefs.GetFloat("CurrentTimeOfSavePoint", 0);
        }
    }

    public class Levels
    {
        public static List<World> worldsInformation = new List<World>();

        public static int GetNumberOfLevels()
        {
            worldsInformation = LevelsManager.instance.world;

            int numberOfLevels = 0;
            for (int i = 1; i < worldsInformation.Count; i++)
            {
                for (int j = 1; j < worldsInformation[i].level.Count; j++)
                {
                    numberOfLevels++;
                }
            }
            return numberOfLevels;
        }

        public static string ConvertToLevelIdentification (int worldNumber, int levelNumber)
        {
            return "World " + worldNumber + " - Level " + levelNumber;
        }

        public static int GetNumberOfLevelsOfWorld (int worldNumber)
        {
            return LevelsManager.instance.world[worldNumber].level.Count;
        }

        public static int GetNumberOfLevelsClearedOfWorld (int worldNumber)
        {
            int levelsCleared = 0;

            for(int i = 1; i < GetNumberOfLevelsOfWorld(worldNumber); i++)
            {
                if (IsLevelCleared(ConvertToLevelIdentification(worldNumber, i)))
                {
                    levelsCleared++;
                }
            }

            return levelsCleared;
        }

        public static int GetTotalOfLevelsCleared ()
        {
            int levelsCleared = 0;

            for (int world = 1; world < GetNumberOfWorlds(); world++)
            {
                for (int level = 1; level < GetNumberOfLevelsOfWorld(world); level++)
                {
                    if (IsLevelCleared(ConvertToLevelIdentification(world, level)))
                    {
                        levelsCleared++;
                    }
                }
            }

            return levelsCleared;
        }

        public static string[] GetListOfWorlds ()
        {
            worldsInformation = LevelsManager.instance.world;
            string[] worldsList = new string[worldsInformation.Count];
            worldsList[0] = "Select a world";

            for (int i = 1; i < worldsInformation.Count; i++)
            {
                worldsList[i] = "World " + i;
            }

            return worldsList;
        }

        public static int GetNumberOfWorlds ()
        {
            return LevelsManager.instance.world.Count;
        }

        public static int SceneNameToWorldNumber(string sceneName)
        {
            worldsInformation = LevelsManager.instance.world;

            int worldNumberToReturn = 0;
            for (int i = 1; i < worldsInformation.Count; i++)
            {
                for (int j = 1; j < worldsInformation[i].level.Count; j++)
                {
                    if (worldsInformation[i].level[j].sceneName == sceneName)
                    {
                        worldNumberToReturn = i;
                        break;
                    }
                }
                if (worldNumberToReturn != 0)
                    break;
            }
#if UNITY_EDITOR
            if (worldNumberToReturn == 0)
                Debug.LogError("The scene name don't exist in the list of levels");
#endif
            return worldNumberToReturn;
        }

        public static int GetLevelNumberOfSceneName(string sceneName)
        {
            worldsInformation = LevelsManager.instance.world;
            
            int levelNumberToReturn = 0;
            for (int i = 1; i < worldsInformation.Count; i++)
            {
                for (int j = 1; j < worldsInformation[i].level.Count; j++)
                {
                    if (worldsInformation[i].level[j].sceneName == sceneName)
                    {
                        levelNumberToReturn = j;
                        break;
                    }
                }
                if (levelNumberToReturn != 0)
                    break;
            }
#if UNITY_EDITOR
            if (levelNumberToReturn == 0)
                Debug.LogError("The scene name don't exist in the list of levels");
#endif
            return levelNumberToReturn;
        }


        public static string GetLevelIdentificationOfSceneName(string sceneName)
        {

            worldsInformation = LevelsManager.instance.world;


            string levelIDToReturn = "";
            for (int i = 1; i < worldsInformation.Count; i++)
            {
                for (int j = 1; j < worldsInformation[i].level.Count; j++)
                {
                    if (worldsInformation[i].level[j].sceneName == sceneName)
                    {
                        levelIDToReturn = "World " + i + " - Level " + j;
                        break;
                    }
                }
                if (levelIDToReturn != "")
                    break;
            }
#if UNITY_EDITOR
            if (levelIDToReturn == "")
                Debug.LogError("The scene name don't exist in the list of levels");
#endif
            return levelIDToReturn;
        }

        public static string GetLevelIdentificationOfCurrentScene()
        {
            worldsInformation = LevelsManager.instance.world;

            string levelIDToReturn = "";
            for (int i = 1; i < worldsInformation.Count; i++)
            {
                for (int j = 1; j < worldsInformation[i].level.Count; j++)
                {
                    if (worldsInformation[i].level[j].sceneName == SceneManager.GetActiveScene().name)
                    {
                        levelIDToReturn = "World " + i + " - Level " + j;
                        break;
                    }
                }
                if (levelIDToReturn != "")
                    break;
            }
#if UNITY_EDITOR
            if (levelIDToReturn == "")
                Debug.LogError("The scene name does not exist in the list of levels or you changed the name of the scene before. Add the current scene to the \"Levels Manager\" window or remove and add again if you changed the name.");
#endif
            return levelIDToReturn;
        }

        public static string GetSceneNameOfLevelIdentification(string levelIdentification)
        {

            worldsInformation = LevelsManager.instance.world;


            string sceneNameToReturn = "";
            for (int i = 1; i < worldsInformation.Count; i++)
            {
                for (int j = 1; j < worldsInformation[i].level.Count; j++)
                {
                    if (levelIdentification == "World " + i + " - Level " + j)
                    {
                        sceneNameToReturn = worldsInformation[i].level[j].sceneName;
                        break;
                    }
                }
                if (sceneNameToReturn != "")
                    break;
            }

            return sceneNameToReturn;
        }

        public static string GetLevelIdentificationOfNumberOfList(int levelNumber)
        {
            if (levelNumber >= GetListOfLevelIdentifications().Length)
            {
                Debug.LogError("Error: Some script is trying to access to a level that does not exist. Please reset the saved data or add the level that was recently deleted from the Levels Manager.");
            }

            return GetListOfLevelIdentifications()[levelNumber];
        }

        public static string GetSceneNameOfNumberOfList(int levelNumber)
        {

            worldsInformation = LevelsManager.instance.world;

            string sceneNameToReturn = "";
            int iterationNumber = 1;
            for (int i = 1; i < worldsInformation.Count; i++)
            {
                for (int j = 1; j < worldsInformation[i].level.Count; j++)
                {
                    if (levelNumber == iterationNumber)
                    {
                        sceneNameToReturn = LevelsManager.instance.world[i].level[j].sceneName;
                        break;
                    }

                    iterationNumber++;
                }

                if (sceneNameToReturn != "")
                    break;
            }

            return sceneNameToReturn;
        }

        public static string[] GetListOfLevelIdentifications()
        {
            worldsInformation = LevelsManager.instance.world;

            string[] levelList = new string[GetNumberOfLevels() + 1];

            int iterationNumber = 1;
            levelList[0] = "Select a level";
            for (int i = 1; i < LevelsManager.instance.world.Count; i++)
            {
                for (int j = 1; j < LevelsManager.instance.world[i].level.Count; j++)
                {
                    levelList[iterationNumber] = "World " + i + " - Level " + j;
                    iterationNumber++;
                }
            }

            return levelList;
        }

        public static void DisableAllLevelsCleared()
        {

            worldsInformation = LevelsManager.instance.world;


            for (int i = 1; i < worldsInformation.Count; i++) //levelsCleared.Count return the quantity of worlds
            {
                for (int j = 1; j < worldsInformation[i].level.Count; j++) //levelsCleared[world].count return the quantity of levels of the world
                {
                    if (!worldsInformation[i].level[j].startUnlocked) //If the value by defauld of the level is false means that the level is not available when start the first time
                        SetLevelCleared("World " + i + " - Level " + j, false);
                }
            }
        }

        public static void DisableAllLevelsClearedOfAWorld(int worldNumber)
        {

            worldsInformation = LevelsManager.instance.world;


            for (int j = 1; j < worldsInformation[worldNumber].level.Count; j++) //levelsCleared[world].count return the quantity of levels of the world
            {
                if (!worldsInformation[worldNumber].level[j].startUnlocked) //If the value by defauld of the level is false means that the level is not available when start the first time
                    SetLevelCleared("World " + worldNumber + " - Level " + j, false);
            }
        }

        public static void SetLevelCleared(string levelIdentification, bool levelCleared)
        {
            if (levelCleared)
                EkumeSecureData.SetInt(levelIdentification, 1); //If levelCleared==true save 1
            else
                EkumeSecureData.SetInt(levelIdentification, 0); //If levelCleared==false save 0
        }

        public static bool IsLevelCleared(string levelIdentification)
        {
            if (EkumeSecureData.GetInt(levelIdentification, 0) == 1) //If the level is cleared return true
                return true;
            else //If the level is not cleared (Because have save 0) then return false
                return false;
        }

        public static string GetLevelIdentificationOfNextLevel() //This function returns the ID of next level. This search what is the last level that cleared and if the next level is not clear will return that level.
        {
            worldsInformation = LevelsManager.instance.world;

            string levelIdentification = "";
            for (int i = 1; i < worldsInformation.Count; i++) //world.Count return the quantity of worlds
            {
                for (int j = 1; j < worldsInformation[i].level.Count; j++) // world[i].level.count return the quantity of levels of the world
                {
                    if (!IsLevelCleared("World " + i + " - Level " + j)) //If level is not cleared
                    {
                        levelIdentification = "World " + i + " - Level " + j;
                        break;
                    }
                }

                if (levelIdentification != "")
                    break;
            }

            return levelIdentification;
        }

        public static int GetCurrentWorldNumber()
        {
            worldsInformation = LevelsManager.instance.world;

            int worldNumber = 0;
            for (int i = 1; i < worldsInformation.Count; i++) //worldsInformation.Count return the quantity of worlds
            {
                for (int j = 1; j < worldsInformation[i].level.Count; j++) // worldsInformation[i].level.count return the quantity of levels of the world
                {
                    if (!IsLevelCleared("World " + i + " - Level " + j)) //If level is not cleared
                    {
                        worldNumber = i;
                        break;
                    }
                }

                if (worldNumber != 0)
                    break;
            }

            return worldNumber;
        }

        public static int LevelIdentificationToWorldNumber (string levelIdentification)
        {
            worldsInformation = LevelsManager.instance.world;

            int worldNumber = 0;
            for (int i = 1; i < worldsInformation.Count; i++)
            {
                for (int j = 1; j < worldsInformation[i].level.Count; j++) 
                {
                    if (levelIdentification == ("World " + i + " - Level " + j))
                    {
                        worldNumber = i;
                        break;
                    }
                }

                if (worldNumber != 0)
                    break;
            }

            return worldNumber;
        }

        public static string GetSceneNameOfNextLevel()
        {
            worldsInformation = LevelsManager.instance.world;
            
            string sceneNameOfNextLevel = "";
            for (int i = 1; i < worldsInformation.Count; i++) //worldsInformation.Count return the quantity of worlds
            {
                for (int j = 1; j < worldsInformation[i].level.Count; j++) //worldsInformation[world].count return the quantity of levels of the world
                {
                    if (!IsLevelCleared("World " + i + " - Level " + j)) //If level is not cleared
                    {
                        sceneNameOfNextLevel = GetSceneNameOfLevelIdentification("World " + i + " - Level " + j);
                        break;
                    }
                }

                if (sceneNameOfNextLevel != "")
                    break;
            }

            return sceneNameOfNextLevel;
        }
    }

    namespace Player
    {
        public class PlayerSelection
        {
            public static void SetPlayerUnlocked(int playerID)
            {
                PlayerPrefs.SetInt("PlayerUnlocked-" + playerID, 1);
            }

            public static bool PlayerIsUnlocked(int playerID)
            {
                return PlayerPrefs.HasKey("PlayerUnlocked-" + playerID);
            }

            public static void SetPlayerSelected(int playerID)
            {
                PlayerPrefs.SetInt("PlayerSelected", playerID);
            }

            static PlayersManager playersManager = PlayersManager.instance;
            public static int GetPlayerSelected()
            {
                return PlayerPrefs.GetInt("PlayerSelected", playersManager.defaultPlayer);
            }
        }

        public class PlayerStats
        {
            public static int _defaultTotalLives;
            public static float _defaultHealth;
            public static bool _immunityTimeActivated;

            public static int GetTotalLives()
            {
                return EkumeSecureData.GetInt("TotalLives", _defaultTotalLives);
            }

            public static void SetTotalLives(int newValueOfLives)
            {
                EkumeSecureData.SetInt("TotalLives", newValueOfLives);
            }

            public static float GetHealth()
            {
                return EkumeSecureData.GetFloat("Health", _defaultHealth);
            }

            public static void SetHealth(float newHealth, bool ignoreImmunityTime)
            {
                if ((!_immunityTimeActivated || ignoreImmunityTime) || (newHealth > 0))
                    EkumeSecureData.SetFloat("Health", newHealth);
            }

            public static void SetHealth(float newHealth)
            {
                if ((!_immunityTimeActivated) || (newHealth > 0))
                    EkumeSecureData.SetFloat("Health", newHealth);
            }

        }

        public class PowerStats
        {
            public static float GetTimeOfPower(PowersEnum power)
            {
                float timeOfPower = 0;
                switch (power)
                {
                    case PowersEnum.FlyingPower:
                        timeOfPower = PlayerPrefs.GetFloat("PowerToFlyTime", OptionsOfPowers.instance.defaultTimeForPower.powerToFly);
                        break;
                    case PowersEnum.ObjectMagnet:
                        timeOfPower = PlayerPrefs.GetFloat("PowerCoinMagnetTime", OptionsOfPowers.instance.defaultTimeForPower.coinMagnet);
                        break;
                    case PowersEnum.ScoreDuplicator:
                        timeOfPower = PlayerPrefs.GetFloat("CoinsDuplicatorTime", OptionsOfPowers.instance.defaultTimeForPower.coinsDuplicator);
                        break;
                    case PowersEnum.KillerShield:
                        timeOfPower = PlayerPrefs.GetFloat("KillerShieldTime", OptionsOfPowers.instance.defaultTimeForPower.killerShield);
                        break;
                    case PowersEnum.ProtectorShield:
                        timeOfPower = PlayerPrefs.GetFloat("ProtectorShieldTime", OptionsOfPowers.instance.defaultTimeForPower.protectorShield);
                        break;
                    case PowersEnum.TrapsConverter:
                        timeOfPower = PlayerPrefs.GetFloat("TrapsConverterTime", OptionsOfPowers.instance.defaultTimeForPower.trapsConverter);
                        break;
                    case PowersEnum.Jetpack:
                        timeOfPower = PlayerPrefs.GetFloat("JetpackTime", OptionsOfPowers.instance.defaultTimeForPower.jetpack);
                        break;

                    default:
#if UNITY_EDITOR
                        Debug.LogError("Error: are you calling power that is not added in the cases?.");
#endif
                        timeOfPower = 0;
                        break;
                }
                return timeOfPower;
            }

            public static int GetQuantityOfTimeUpgrades(PowersEnum power)
            {
                int quantity = 0;
                switch (power)
                {
                    case PowersEnum.FlyingPower:
                        quantity = PlayerPrefs.GetInt("PowerToFlyTimeUpgrades", 0);
                        break;
                    case PowersEnum.ObjectMagnet:
                        quantity = PlayerPrefs.GetInt("PowerCoinMagnetTimeUpgrades", 0);
                        break;
                    case PowersEnum.ScoreDuplicator:
                        quantity = PlayerPrefs.GetInt("CoinsDuplicatorTimeUpgrades", 0);
                        break;
                    case PowersEnum.KillerShield:
                        quantity = PlayerPrefs.GetInt("KillerShieldTimeUpgrades", 0);
                        break;
                    case PowersEnum.ProtectorShield:
                        quantity = PlayerPrefs.GetInt("ProtectorShieldTimeUpgrades", 0);
                        break;
                    case PowersEnum.TrapsConverter:
                        quantity = PlayerPrefs.GetInt("TrapsConverterTimeUpgrades", 0);
                        break;
                    case PowersEnum.Jetpack:
                        quantity = PlayerPrefs.GetInt("JetpackTimeUpgrades", 0);
                        break;

                    default:
#if UNITY_EDITOR
                        Debug.LogError("Error: are you calling power that is not added in the cases?.");
#endif
                        quantity = 0;
                        break;
                }
                return quantity;
            }

            public static void UpgradeTimeOfPower(PowersEnum power, float timeToIncrease)
            {
                switch (power)
                {
                    case PowersEnum.FlyingPower:
                        PlayerPrefs.SetFloat("PowerToFlyTime", GetTimeOfPower(power) + timeToIncrease);
                        PlayerPrefs.SetInt("PowerToFlyTimeUpgrades", GetQuantityOfTimeUpgrades(power) + 1);
                        break;
                    case PowersEnum.ObjectMagnet:
                        PlayerPrefs.SetFloat("PowerCoinMagnetTime", GetTimeOfPower(power) + timeToIncrease);
                        PlayerPrefs.SetInt("PowerCoinMagnetTimeUpgrades", GetQuantityOfTimeUpgrades(power) + 1);
                        break;
                    case PowersEnum.ScoreDuplicator:
                        PlayerPrefs.SetFloat("CoinsDuplicatorTime", GetTimeOfPower(power) + timeToIncrease);
                        PlayerPrefs.SetInt("CoinsDuplicatorTimeUpgrades", GetQuantityOfTimeUpgrades(power) + 1);
                        break;
                    case PowersEnum.KillerShield:
                        PlayerPrefs.SetFloat("KillerShieldTime", GetTimeOfPower(power) + timeToIncrease);
                        PlayerPrefs.SetInt("KillerShieldTimeUpgrades", GetQuantityOfTimeUpgrades(power) + 1);
                        break;
                    case PowersEnum.ProtectorShield:
                        PlayerPrefs.SetFloat("ProtectorShieldTime", GetTimeOfPower(power) + timeToIncrease);
                        PlayerPrefs.SetInt("ProtectorShieldTimeUpgrades", GetQuantityOfTimeUpgrades(power) + 1);
                        break;
                    case PowersEnum.TrapsConverter:
                        PlayerPrefs.SetFloat("TrapsConverterTime", GetTimeOfPower(power) + timeToIncrease);
                        PlayerPrefs.SetInt("TrapsConverterTimeUpgrades", GetQuantityOfTimeUpgrades(power) + 1);
                        break;
                    case PowersEnum.Jetpack:
                        PlayerPrefs.SetFloat("JetpackTime", GetTimeOfPower(power) + timeToIncrease);
                        PlayerPrefs.SetInt("JetpackTimeUpgrades", GetQuantityOfTimeUpgrades(power) + 1);
                        break;
                    default:
#if UNITY_EDITOR
                        Debug.LogError("Error: are you calling power that is not added in the cases?.");
#endif
                        break;
                }
            }



            //-------------------------------------------------------------------------------------- /

            public static int GetQuantityOfPower(PowersEnum power)
            {
                int quantityOfPower = 0;
                switch (power)
                {
                    case PowersEnum.FlyingPower:
                        quantityOfPower = PlayerPrefs.GetInt("AmountOfUsesOfFly", OptionsOfPowers.instance.defaultQuantityForPower.powerToFly);
                        break;
                    case PowersEnum.ObjectMagnet:
                        quantityOfPower = PlayerPrefs.GetInt("AmountOfUsesOfCoinMagnet", OptionsOfPowers.instance.defaultQuantityForPower.coinMagnet);
                        break;
                    case PowersEnum.ScoreDuplicator:
                        quantityOfPower = PlayerPrefs.GetInt("AmountOfUsesOfCoinsDuplicator", OptionsOfPowers.instance.defaultQuantityForPower.coinsDuplicator);
                        break;
                    case PowersEnum.KillerShield:
                        quantityOfPower = PlayerPrefs.GetInt("AmountOfUsesOfKillerShield", OptionsOfPowers.instance.defaultQuantityForPower.killerShield);
                        break;
                    case PowersEnum.ProtectorShield:
                        quantityOfPower = PlayerPrefs.GetInt("AmountOfUsesOfProtectorShield", OptionsOfPowers.instance.defaultQuantityForPower.protectorShield);
                        break;
                    case PowersEnum.TrapsConverter:
                        quantityOfPower = PlayerPrefs.GetInt("AmountOfUsesOfTrapsConverter", OptionsOfPowers.instance.defaultQuantityForPower.trapsConverter);
                        break;
                    case PowersEnum.Jetpack:
                        quantityOfPower = PlayerPrefs.GetInt("AmountOfUsesOfJetpack", OptionsOfPowers.instance.defaultQuantityForPower.jetpack);
                        break;
                    default:
#if UNITY_EDITOR
                        Debug.LogError("Error: are you calling power that is not added in the cases?.");
#endif
                        quantityOfPower = 0;
                        break;
                }

                return quantityOfPower;
            }

            public static void AddQuantityOfPower(PowersEnum power, int quantityToAdd)
            {
                switch (power)
                {
                    case PowersEnum.FlyingPower:
                        PlayerPrefs.SetInt("AmountOfUsesOfFly", GetQuantityOfPower(power) + quantityToAdd);
                        break;
                    case PowersEnum.ObjectMagnet:
                        PlayerPrefs.SetInt("AmountOfUsesOfCoinMagnet", GetQuantityOfPower(power) + quantityToAdd);
                        break;
                    case PowersEnum.ScoreDuplicator:
                        PlayerPrefs.SetInt("AmountOfUsesOfCoinsDuplicator", GetQuantityOfPower(power) + quantityToAdd);
                        break;
                    case PowersEnum.KillerShield:
                        PlayerPrefs.SetInt("AmountOfUsesOfKillerShield", GetQuantityOfPower(power) + quantityToAdd);
                        break;
                    case PowersEnum.ProtectorShield:
                        PlayerPrefs.SetInt("AmountOfUsesOfProtectorShield", GetQuantityOfPower(power) + quantityToAdd);
                        break;
                    case PowersEnum.TrapsConverter:
                        PlayerPrefs.SetInt("AmountOfUsesOfTrapsConverter", GetQuantityOfPower(power) + quantityToAdd);
                        break;
                    case PowersEnum.Jetpack:
                        PlayerPrefs.SetInt("AmountOfUsesOfJetpack", GetQuantityOfPower(power) + quantityToAdd);
                        break;
                    default:
#if UNITY_EDITOR
                        Debug.LogError("Error: are you calling power that is not added in the cases?.");
#endif
                        break;
                }

            }
        }
    }
}