#define UNITY_5_4_PLUS
#if UNITY_4_6 || UNITY_4_7 || UNITY_4_8 || UNITY_4_9 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2 || UNITY_5_3
#undef UNITY_5_4_PLUS
#endif

#if UNITY_5_4_PLUS
using UnityEngine.SceneManagement;
#endif

using System;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;
using Debug = UnityEngine.Debug;
using UnityEngine.Events;

#if UNITY_EDITOR
// allows to use internal methods from the editor code (Prefs editor window)
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Assembly-CSharp-Editor")]
#endif

/// <summary>
/// This is an Obscured analogue of the <a href="http://docs.unity3d.com/Documentation/ScriptReference/PlayerPrefs.html">PlayerPrefs</a> class.
/// </summary>
/// Saves data in encrypted state, optionally locking it to the current device.<br/>
/// Automatically encrypts PlayerPrefs on first read (auto migration), has tampering detection and more.
public static class EkumeSecureData
{
    private const byte VERSION = 2;
    private const string RAW_NOT_FOUND = "{not_found}";
    private const string DATA_SEPARATOR = "|";

    private static bool foreignSavesReported;

    private static string cryptoKey = "e806f6";

    /// <summary>
    /// Use it to change default crypto key and / or obtain currently used crypto key.
    /// </summary>
    /// <strong>\htmlonly<font color="FF4040">WARNING:</font>\endhtmlonly Any data saved with one encryption key will not be accessible with any other encryption key!</strong>
    public static string CryptoKey
    {
        set { cryptoKey = value; }
        get { return cryptoKey; }
    }

    private static string deviceId;
    /// <summary>
    /// Allows to get current device ID or set custom device ID to lock saves to the device.
    /// </summary>
    /// <strong>\htmlonly<font color="FF4040">WARNING:</font>\endhtmlonly All data saved with previous device ID will be considered foreign!</strong>
    /// \sa lockToDevice
    public static string DeviceId
    {
        get
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = GetDeviceId();
            }
            return deviceId;
        }

        set
        {
            deviceId = value;
        }
    }

    private static uint deviceIdHash;
    private static uint DeviceIdHash
    {
        get
        {
            if (deviceIdHash == 0)
            {
                deviceIdHash = CalculateChecksum(DeviceId);
            }
            return deviceIdHash;
        }
    }

    /// <summary>
    /// Allows reacting on saves alteration. May be helpful for banning potential cheaters.
    /// </summary>
    /// Fires only once.
    public static System.Action onAlterationDetected;

    /// <summary>
    /// Allows saving original PlayerPrefs values while migrating to EkumeSecureData.
    /// </summary>
    /// In such case, original value still will be readable after switching from PlayerPrefs to 
    /// EkumeSecureData and it should be removed manually as it became unneeded.<br/>
    /// Original PlayerPrefs value will be automatically removed after read by default.
    public static bool preservePlayerPrefs = false;

#if UNITY_EDITOR
    /// <summary>
    /// Allows disabling written data obscuration. Works in Editor only.
    /// </summary>
    /// Please note, it breaks PlayerPrefs to EkumeSecureData migration (in Editor).
    public static bool unobscuredMode = false;
#endif

    /// <summary>
    /// Allows reacting on detection of possible saves from some other device. 
    /// </summary>
    /// May be helpful to ban potential cheaters, trying to use someone's purchased in-app goods for example.<br/>
    /// May fire on same device in case cheater manipulates saved data in some special way.<br/>
    /// Fires only once.
    /// 
    /// <strong>\htmlonly<font color="7030A0">NOTE:</font>\endhtmlonly May be called if same device ID was changed (pretty rare case though).</strong>
    public static System.Action onPossibleForeignSavesDetected = null;

    /// <summary>
    /// Allows locking saved data to the current device.
    /// </summary>
    /// Use it to prevent cheating via 100% game saves sharing or sharing purchased in-app items for example.<br/>
    /// Set to \link EkumeSecureData::Soft DeviceLockLevel.Soft \endlink to allow reading of not locked data.<br/>
    /// Set to \link EkumeSecureData::Strict DeviceLockLevel.Strict \endlink to disallow reading of not locked data (any not locked data will be lost).<br/>
    /// Set to \link EkumeSecureData::None DeviceLockLevel.None \endlink to disable data lock feature and to read both previously locked and not locked data.<br/>
    /// Read more in #DeviceLockLevel description.
    /// 
    /// Relies on <a href="http://docs.unity3d.com/Documentation/ScriptReference/SystemInfo-deviceUniqueIdentifier.html">SystemInfo.deviceUniqueIdentifier</a>.
    /// Please note, it may change in some rare cases, so one day all locked data may became inaccessible on same device, and here comes #emergencyMode and #readForeignSaves to rescue.<br/>
    /// 
    /// <strong>\htmlonly<font color="FF4040">WARNING:</font>\endhtmlonly On iOS use at your peril! There is no reliable way to get persistent device ID on iOS. So avoid using it or use in conjunction with ForceLockToDeviceInit() to set own device ID (e.g. user email).<br/></strong>
    /// <strong>\htmlonly<font color="7030A0">NOTE #1:</font>\endhtmlonly On iOS it tries to receive vendorIdentifier in first place, to avoid device id change while updating from iOS6 to iOS7. It leads to device ID change while updating from iOS5, but such case is lot rarer.<br/></strong>
    /// <strong>\htmlonly<font color="7030A0">NOTE #2:</font>\endhtmlonly You may use own device id via #DeviceId property. It may be useful to lock saves to the specified email for example.<br/></strong>
    /// <strong>\htmlonly<font color="7030A0">NOTE #3:</font>\endhtmlonly Main thread may lock up for a noticeable time while obtaining device ID first time on some devices (~ sec on my PC)! Consider using ForceLockToDeviceInit() to prevent undesirable behavior in such cases.</strong>
    /// \sa readForeignSaves, emergencyMode, ForceLockToDeviceInit(), DeviceId
    public static DeviceLockLevel lockToDevice = DeviceLockLevel.None;

    /// <summary>
    /// Allows reading saves locked to other device. #onPossibleForeignSavesDetected action still will be fired.
    /// </summary>
    /// \sa lockToDevice, emergencyMode
    public static bool readForeignSaves = false;

    /// <summary>
    /// Allows ignoring #lockToDevice to recover saved data in case of some unexpected issues, like unique device ID change for the same device.<br/>
    /// Similar to readForeignSaves, but doesn't fires #onPossibleForeignSavesDetected action on foreign saves detection.
    /// </summary>
    /// \sa lockToDevice, readForeignSaves
    public static bool emergencyMode = false;

    /// <summary>
    /// Allows forcing device id obtaining on demand. Otherwise, it will be obtained automatically on first usage.
    /// </summary>
    /// Device id obtaining process may be noticeably slow when called first time on some devices.<br/>
    /// This method allows you to force this process at comfortable time (while splash screen is showing for example).
    /// \sa lockToDevice
    public static void ForceLockToDeviceInit()
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            deviceId = GetDeviceId();
            deviceIdHash = CalculateChecksum(deviceId);
        }
        else
        {
            Debug.LogWarning("[Ekume]  EkumeSecureData.ForceLockToDeviceInit() is called, but device ID is already obtained!");
        }
    }

    [Obsolete("This method is obsolete, use property CryptoKey instead")]
    internal static void SetNewCryptoKey(string newKey)
    {
        CryptoKey = newKey;
    }

    #region int
    /// <summary>
    /// Sets the <c>value</c> of the preference identified by <c>key</c>.
    /// </summary>
    public static void SetInt(string key, int value)
    {
#if UNITY_EDITOR
        if (unobscuredMode) WriteUnobscured(key, value);
#endif

#if UNITY_WEBPLAYER
			try
			{
				PlayerPrefs.SetString(EncryptKey(key), EncryptIntValue(key, value));
			}
			catch (PlayerPrefsException exception)
			{
				Debug.LogException(exception);
			}
#else
        PlayerPrefs.SetString(EncryptKey(key), EncryptIntValue(key, value));
#endif
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return 0.
    /// </summary>
    public static int GetInt(string key)
    {
        return GetInt(key, 0);
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return <c>defaultValue</c>.
    /// </summary>
    public static int GetInt(string key, int defaultValue)
    {
        string encryptedKey = EncryptKey(key);

#if UNITY_EDITOR
        if (!PlayerPrefs.HasKey(encryptedKey) && !unobscuredMode)
#else
			if (!PlayerPrefs.HasKey(encryptedKey))
#endif
        {
            if (PlayerPrefs.HasKey(key))
            {
                int unencrypted = PlayerPrefs.GetInt(key, defaultValue);
                if (!preservePlayerPrefs)
                {
                    SetInt(key, unencrypted);
                    PlayerPrefs.DeleteKey(key);
                }
                return unencrypted;
            }
        }

#if UNITY_EDITOR
        if (unobscuredMode) return int.Parse(ReadUnobscured(key, defaultValue));
#endif
        string encrypted = GetEncryptedDataString(key, encryptedKey);
        return encrypted == RAW_NOT_FOUND ? defaultValue : DecryptIntValue(key, encrypted, defaultValue);
    }

    internal static string EncryptIntValue(string key, int value)
    {
        byte[] cleanBytes = BitConverter.GetBytes(value);
        return EncryptData(key, cleanBytes, DataType.Int);
    }

    internal static int DecryptIntValue(string key, string encryptedInput, int defaultValue)
    {
        if (encryptedInput.IndexOf(DEPRECATED_RAW_SEPARATOR) > -1)
        {
            string deprecatedValue = DeprecatedDecryptValue(encryptedInput);
            if (deprecatedValue == "") return defaultValue;
            int deprecatedResult;
            int.TryParse(deprecatedValue, out deprecatedResult);
            SetInt(key, deprecatedResult);
            return deprecatedResult;
        }

        byte[] cleanBytes = DecryptData(key, encryptedInput);
        if (cleanBytes == null)
        {
            return defaultValue;
        }

        int cleanValue = BitConverter.ToInt32(cleanBytes, 0);
        return cleanValue;
    }
    #endregion

    #region uint
    /// <summary>
    /// Sets the <c>value</c> of the preference identified by <c>key</c>.
    /// </summary>
    public static void SetUInt(string key, uint value)
    {
#if UNITY_EDITOR
        if (unobscuredMode) WriteUnobscured(key, value);
#endif

#if UNITY_WEBPLAYER
			try
			{
				PlayerPrefs.SetString(EncryptKey(key), EncryptUIntValue(key, value));
			}
			catch (PlayerPrefsException exception)
			{
				Debug.LogException(exception);
			}
#else
        PlayerPrefs.SetString(EncryptKey(key), EncryptUIntValue(key, value));
#endif
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return 0.
    /// </summary>
    public static uint GetUInt(string key)
    {
        return GetUInt(key, 0);
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return <c>defaultValue</c>.
    /// </summary>
    public static uint GetUInt(string key, uint defaultValue)
    {
#if UNITY_EDITOR
        if (unobscuredMode) return uint.Parse(ReadUnobscured(key, defaultValue));
#endif
        string encrypted = GetEncryptedDataString(key, EncryptKey(key));
        return encrypted == RAW_NOT_FOUND ? defaultValue : DecryptUIntValue(key, encrypted, defaultValue);
    }

    private static string EncryptUIntValue(string key, uint value)
    {
        byte[] cleanBytes = BitConverter.GetBytes(value);
        return EncryptData(key, cleanBytes, DataType.UInt);
    }

    private static uint DecryptUIntValue(string key, string encryptedInput, uint defaultValue)
    {
        if (encryptedInput.IndexOf(DEPRECATED_RAW_SEPARATOR) > -1)
        {
            string deprecatedValue = DeprecatedDecryptValue(encryptedInput);
            if (deprecatedValue == "") return defaultValue;
            uint deprecatedResult;
            uint.TryParse(deprecatedValue, out deprecatedResult);
            SetUInt(key, deprecatedResult);
            return deprecatedResult;
        }

        byte[] cleanBytes = DecryptData(key, encryptedInput);
        if (cleanBytes == null)
        {
            return defaultValue;
        }

        uint cleanValue = BitConverter.ToUInt32(cleanBytes, 0);
        return cleanValue;
    }
    #endregion

    #region string
    /// <summary>
    /// Sets the <c>value</c> of the preference identified by <c>key</c>.
    /// </summary>
    public static void SetString(string key, string value)
    {
#if UNITY_EDITOR
        if (unobscuredMode) WriteUnobscured(key, value);
#endif

#if UNITY_WEBPLAYER
			try
			{
				PlayerPrefs.SetString(EncryptKey(key), EncryptStringValue(key, value));
			}
			catch (PlayerPrefsException exception)
			{
				Debug.LogException(exception);
			}
#else
        PlayerPrefs.SetString(EncryptKey(key), EncryptStringValue(key, value));
#endif
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return "".
    /// </summary>
    public static string GetString(string key)
    {
        return GetString(key, "");
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return <c>defaultValue</c>.
    /// </summary>
    public static string GetString(string key, string defaultValue)
    {
        string encryptedKey = EncryptKey(key);

#if UNITY_EDITOR
        if (!PlayerPrefs.HasKey(encryptedKey) && !unobscuredMode)
#else
			if (!PlayerPrefs.HasKey(encryptedKey))
#endif
        {
            if (PlayerPrefs.HasKey(key))
            {
                string unencrypted = PlayerPrefs.GetString(key, defaultValue);
                if (!preservePlayerPrefs)
                {
                    SetString(key, unencrypted);
                    PlayerPrefs.DeleteKey(key);
                }
                return unencrypted;
            }
        }

#if UNITY_EDITOR
        if (unobscuredMode) return ReadUnobscured(key, defaultValue);
#endif
        string encrypted = GetEncryptedDataString(key, encryptedKey);
        return encrypted == RAW_NOT_FOUND ? defaultValue : DecryptStringValue(key, encrypted, defaultValue);
    }

    internal static string EncryptStringValue(string key, string value)
    {
        byte[] cleanBytes = Encoding.UTF8.GetBytes(value);
        return EncryptData(key, cleanBytes, DataType.String);
    }

    internal static string DecryptStringValue(string key, string encryptedInput, string defaultValue)
    {
        if (encryptedInput.IndexOf(DEPRECATED_RAW_SEPARATOR) > -1)
        {
            string deprecatedValue = DeprecatedDecryptValue(encryptedInput);
            if (deprecatedValue == "") return defaultValue;
            SetString(key, deprecatedValue);
            return deprecatedValue;
        }

        byte[] cleanBytes = DecryptData(key, encryptedInput);
        if (cleanBytes == null)
        {
            return defaultValue;
        }

        string cleanValue = Encoding.UTF8.GetString(cleanBytes, 0, cleanBytes.Length);
        return cleanValue;
    }
    #endregion

    #region float
    /// <summary>
    /// Sets the <c>value</c> of the preference identified by <c>key</c>.
    /// </summary>
    public static void SetFloat(string key, float value)
    {
#if UNITY_EDITOR
        if (unobscuredMode) WriteUnobscured(key, value);
#endif

#if UNITY_WEBPLAYER
			try
			{
				PlayerPrefs.SetString(EncryptKey(key), EncryptFloatValue(key, value));
			}
			catch (PlayerPrefsException exception)
			{
				Debug.LogException(exception);
			}
#else
        PlayerPrefs.SetString(EncryptKey(key), EncryptFloatValue(key, value));
#endif
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return 0.
    /// </summary>
    public static float GetFloat(string key)
    {
        return GetFloat(key, 0);
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return <c>defaultValue</c>.
    /// </summary>
    public static float GetFloat(string key, float defaultValue)
    {
        string encryptedKey = EncryptKey(key);

#if UNITY_EDITOR
        if (!PlayerPrefs.HasKey(encryptedKey) && !unobscuredMode)
#else
			if (!PlayerPrefs.HasKey(encryptedKey))
#endif
        {
            if (PlayerPrefs.HasKey(key))
            {
                float unencrypted = PlayerPrefs.GetFloat(key, defaultValue);
                if (!preservePlayerPrefs)
                {
                    SetFloat(key, unencrypted);
                    PlayerPrefs.DeleteKey(key);
                }
                return unencrypted;
            }
        }

#if UNITY_EDITOR
        if (unobscuredMode) return float.Parse(ReadUnobscured(key, defaultValue));
#endif
        string encrypted = GetEncryptedDataString(key, encryptedKey);
        return encrypted == RAW_NOT_FOUND ? defaultValue : DecryptFloatValue(key, encrypted, defaultValue);
    }

    internal static string EncryptFloatValue(string key, float value)
    {
        byte[] cleanBytes = BitConverter.GetBytes(value);
        return EncryptData(key, cleanBytes, DataType.Float);
    }

    internal static float DecryptFloatValue(string key, string encryptedInput, float defaultValue)
    {
        if (encryptedInput.IndexOf(DEPRECATED_RAW_SEPARATOR) > -1)
        {
            string deprecatedValue = DeprecatedDecryptValue(encryptedInput);
            if (deprecatedValue == "") return defaultValue;
            float deprecatedResult;
            float.TryParse(deprecatedValue, out deprecatedResult);
            SetFloat(key, deprecatedResult);
            return deprecatedResult;
        }

        byte[] cleanBytes = DecryptData(key, encryptedInput);
        if (cleanBytes == null)
        {
            return defaultValue;
        }

        float cleanValue = BitConverter.ToSingle(cleanBytes, 0);
        return cleanValue;
    }
    #endregion

    #region double
    /// <summary>
    /// Sets the <c>value</c> of the preference identified by <c>key</c>.
    /// </summary>
    public static void SetDouble(string key, double value)
    {
#if UNITY_EDITOR
        if (unobscuredMode) WriteUnobscured(key, value);
#endif

#if UNITY_WEBPLAYER
			try
			{
				PlayerPrefs.SetString(EncryptKey(key), EncryptDoubleValue(key, value));
			}
			catch (PlayerPrefsException exception)
			{
				Debug.LogException(exception);
			}
#else
        PlayerPrefs.SetString(EncryptKey(key), EncryptDoubleValue(key, value));
#endif
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return 0.
    /// </summary>
    public static double GetDouble(string key)
    {
        return GetDouble(key, 0);
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return <c>defaultValue</c>.
    /// </summary>
    public static double GetDouble(string key, double defaultValue)
    {
#if UNITY_EDITOR
        if (unobscuredMode) return double.Parse(ReadUnobscured(key, defaultValue));
#endif
        string encrypted = GetEncryptedDataString(key, EncryptKey(key));
        return encrypted == RAW_NOT_FOUND ? defaultValue : DecryptDoubleValue(key, encrypted, defaultValue);
    }

    private static string EncryptDoubleValue(string key, double value)
    {
        byte[] cleanBytes = BitConverter.GetBytes(value);
        return EncryptData(key, cleanBytes, DataType.Double);
    }

    private static double DecryptDoubleValue(string key, string encryptedInput, double defaultValue)
    {
        if (encryptedInput.IndexOf(DEPRECATED_RAW_SEPARATOR) > -1)
        {
            string deprecatedValue = DeprecatedDecryptValue(encryptedInput);
            if (deprecatedValue == "") return defaultValue;
            double deprecatedResult;
            double.TryParse(deprecatedValue, out deprecatedResult);
            SetDouble(key, deprecatedResult);
            return deprecatedResult;
        }

        byte[] cleanBytes = DecryptData(key, encryptedInput);
        if (cleanBytes == null)
        {
            return defaultValue;
        }

        double cleanValue = BitConverter.ToDouble(cleanBytes, 0);
        return cleanValue;
    }
    #endregion

    #region long
    /// <summary>
    /// Sets the <c>value</c> of the preference identified by <c>key</c>.
    /// </summary>
    public static void SetLong(string key, long value)
    {
#if UNITY_EDITOR
        if (unobscuredMode) WriteUnobscured(key, value);
#endif

#if UNITY_WEBPLAYER
			try
			{
				PlayerPrefs.SetString(EncryptKey(key), EncryptLongValue(key, value));
			}
			catch (PlayerPrefsException exception)
			{
				Debug.LogException(exception);
			}
#else
        PlayerPrefs.SetString(EncryptKey(key), EncryptLongValue(key, value));
#endif
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return 0.
    /// </summary>
    public static long GetLong(string key)
    {
        return GetLong(key, 0);
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return <c>defaultValue</c>.
    /// </summary>
    public static long GetLong(string key, long defaultValue)
    {
#if UNITY_EDITOR
        if (unobscuredMode) return long.Parse(ReadUnobscured(key, defaultValue));
#endif
        string encrypted = GetEncryptedDataString(key, EncryptKey(key));
        return encrypted == RAW_NOT_FOUND ? defaultValue : DecryptLongValue(key, encrypted, defaultValue);
    }

    private static string EncryptLongValue(string key, long value)
    {
        byte[] cleanBytes = BitConverter.GetBytes(value);
        return EncryptData(key, cleanBytes, DataType.Long);
    }

    private static long DecryptLongValue(string key, string encryptedInput, long defaultValue)
    {
        if (encryptedInput.IndexOf(DEPRECATED_RAW_SEPARATOR) > -1)
        {
            string deprecatedValue = DeprecatedDecryptValue(encryptedInput);
            if (deprecatedValue == "") return defaultValue;
            long deprecatedResult;
            long.TryParse(deprecatedValue, out deprecatedResult);
            SetLong(key, deprecatedResult);
            return deprecatedResult;
        }

        byte[] cleanBytes = DecryptData(key, encryptedInput);
        if (cleanBytes == null)
        {
            return defaultValue;
        }

        long cleanValue = BitConverter.ToInt64(cleanBytes, 0);
        return cleanValue;
    }
    #endregion

    #region bool
    /// <summary>
    /// Sets the <c>value</c> of the preference identified by <c>key</c>.
    /// </summary>
    public static void SetBool(string key, bool value)
    {
#if UNITY_EDITOR
        if (unobscuredMode) WriteUnobscured(key, value);
#endif

#if UNITY_WEBPLAYER
			try
			{
				PlayerPrefs.SetString(EncryptKey(key), EncryptBoolValue(key, value));
			}
			catch (PlayerPrefsException exception)
			{
				Debug.LogException(exception);
			}
#else
        PlayerPrefs.SetString(EncryptKey(key), EncryptBoolValue(key, value));
#endif
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return false.
    /// </summary>
    public static bool GetBool(string key)
    {
        return GetBool(key, false);
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return <c>defaultValue</c>.
    /// </summary>
    public static bool GetBool(string key, bool defaultValue)
    {
#if UNITY_EDITOR
        if (unobscuredMode) return bool.Parse(ReadUnobscured(key, defaultValue));
#endif
        string encrypted = GetEncryptedDataString(key, EncryptKey(key));
        return encrypted == RAW_NOT_FOUND ? defaultValue : DecryptBoolValue(key, encrypted, defaultValue);
    }

    private static string EncryptBoolValue(string key, bool value)
    {
        byte[] cleanBytes = BitConverter.GetBytes(value);
        return EncryptData(key, cleanBytes, DataType.Bool);
    }

    private static bool DecryptBoolValue(string key, string encryptedInput, bool defaultValue)
    {
        if (encryptedInput.IndexOf(DEPRECATED_RAW_SEPARATOR) > -1)
        {
            string deprecatedValue = DeprecatedDecryptValue(encryptedInput);
            if (deprecatedValue == "") return defaultValue;
            int deprecatedResult;
            int.TryParse(deprecatedValue, out deprecatedResult);
            SetBool(key, deprecatedResult == 1);
            return deprecatedResult == 1;
        }

        byte[] cleanBytes = DecryptData(key, encryptedInput);
        if (cleanBytes == null)
        {
            return defaultValue;
        }

        bool cleanValue = BitConverter.ToBoolean(cleanBytes, 0);
        return cleanValue;
    }
    #endregion

    #region byte[]
    /// <summary>
    /// Sets the <c>value</c> of the preference identified by <c>key</c>.
    /// </summary>
    public static void SetByteArray(string key, byte[] value)
    {
#if UNITY_EDITOR
        if (unobscuredMode) WriteUnobscured(key, Encoding.UTF8.GetString(value, 0, value.Length));
#endif

#if UNITY_WEBPLAYER
			try
			{
				PlayerPrefs.SetString(EncryptKey(key), EncryptByteArrayValue(key, value));
			}
			catch (PlayerPrefsException exception)
			{
				Debug.LogException(exception);
			}
#else
        PlayerPrefs.SetString(EncryptKey(key), EncryptByteArrayValue(key, value));
#endif
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return new byte[0].
    /// </summary>
    public static byte[] GetByteArray(string key)
    {
        return GetByteArray(key, 0, 0);
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return <c>byte[defaultLength]</c> filled with <c>defaultValue</c>.
    /// </summary>
    public static byte[] GetByteArray(string key, byte defaultValue, int defaultLength)
    {
#if UNITY_EDITOR
        if (unobscuredMode) return Encoding.UTF8.GetBytes(ReadUnobscured(key, RAW_NOT_FOUND));
#endif
        string encrypted = GetEncryptedDataString(key, EncryptKey(key));

        if (encrypted == RAW_NOT_FOUND)
        {
            return ConstructByteArray(defaultValue, defaultLength);
        }

        return DecryptByteArrayValue(key, encrypted, defaultValue, defaultLength);
    }

    private static string EncryptByteArrayValue(string key, byte[] value)
    {
        return EncryptData(key, value, DataType.ByteArray);
    }

    private static byte[] DecryptByteArrayValue(string key, string encryptedInput, byte defaultValue, int defaultLength)
    {
        if (encryptedInput.IndexOf(DEPRECATED_RAW_SEPARATOR) > -1)
        {
            string deprecatedValue = DeprecatedDecryptValue(encryptedInput);
            if (deprecatedValue == "")
            {
                return ConstructByteArray(defaultValue, defaultLength);
            }
            byte[] deprecatedResult = Encoding.UTF8.GetBytes(deprecatedValue);
            SetByteArray(key, deprecatedResult);
            return deprecatedResult;
        }

        byte[] cleanBytes = DecryptData(key, encryptedInput);
        if (cleanBytes == null)
        {
            return ConstructByteArray(defaultValue, defaultLength);
        }

        return cleanBytes;
    }

    private static byte[] ConstructByteArray(byte value, int length)
    {
        byte[] bytes = new byte[length];
        for (int i = 0; i < length; i++)
        {
            bytes[i] = value;
        }
        return bytes;
    }
    #endregion

    #region Vector2
    /// <summary>
    /// Sets the <c>value</c> of the preference identified by <c>key</c>.
    /// </summary>
    public static void SetVector2(string key, Vector2 value)
    {
#if UNITY_EDITOR
        if (unobscuredMode) WriteUnobscured(key, value.x + DATA_SEPARATOR + value.y);
#endif

#if UNITY_WEBPLAYER
			try
			{
				PlayerPrefs.SetString(EncryptKey(key), EncryptVector2Value(key, value));
			}
			catch (PlayerPrefsException exception)
			{
				Debug.LogException(exception);
			}
#else
        PlayerPrefs.SetString(EncryptKey(key), EncryptVector2Value(key, value));
#endif
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return Vector2.zero.
    /// </summary>
    public static Vector2 GetVector2(string key)
    {
        return GetVector2(key, Vector2.zero);
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return <c>defaultValue</c>.
    /// </summary>
    public static Vector2 GetVector2(string key, Vector2 defaultValue)
    {
#if UNITY_EDITOR
        if (unobscuredMode)
        {
            string[] values = ReadUnobscured(key, defaultValue).Split(DATA_SEPARATOR[0]);
            float x;
            float y;
            float.TryParse(values[0], out x);
            float.TryParse(values[1], out y);
            return new Vector2(x, y);
        }
#endif
        string encrypted = GetEncryptedDataString(key, EncryptKey(key));
        return encrypted == RAW_NOT_FOUND ? defaultValue : DecryptVector2Value(key, encrypted, defaultValue);
    }

    private static string EncryptVector2Value(string key, Vector2 value)
    {
        byte[] cleanBytes = new byte[8];
        Buffer.BlockCopy(BitConverter.GetBytes(value.x), 0, cleanBytes, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(value.y), 0, cleanBytes, 4, 4);
        return EncryptData(key, cleanBytes, DataType.Vector2);
    }

    private static Vector2 DecryptVector2Value(string key, string encryptedInput, Vector2 defaultValue)
    {
        if (encryptedInput.IndexOf(DEPRECATED_RAW_SEPARATOR) > -1)
        {
            string deprecatedValue = DeprecatedDecryptValue(encryptedInput);
            if (deprecatedValue == "") return defaultValue;
            string[] values = deprecatedValue.Split(DATA_SEPARATOR[0]);
            float x;
            float y;
            float.TryParse(values[0], out x);
            float.TryParse(values[1], out y);
            Vector2 deprecatedResult = new Vector2(x, y);
            SetVector2(key, deprecatedResult);
            return deprecatedResult;
        }

        byte[] cleanBytes = DecryptData(key, encryptedInput);
        if (cleanBytes == null)
        {
            return defaultValue;
        }

        Vector2 cleanValue;
        cleanValue.x = BitConverter.ToSingle(cleanBytes, 0);
        cleanValue.y = BitConverter.ToSingle(cleanBytes, 4);
        return cleanValue;
    }
    #endregion

    #region Vector3
    /// <summary>
    /// Sets the <c>value</c> of the preference identified by <c>key</c>.
    /// </summary>
    public static void SetVector3(string key, Vector3 value)
    {
#if UNITY_EDITOR
        if (unobscuredMode) WriteUnobscured(key, value.x + DATA_SEPARATOR + value.y + DATA_SEPARATOR + value.z);
#endif

#if UNITY_WEBPLAYER
			try
			{
				PlayerPrefs.SetString(EncryptKey(key), EncryptVector3Value(key, value));
			}
			catch (PlayerPrefsException exception)
			{
				Debug.LogException(exception);
			}
#else
        PlayerPrefs.SetString(EncryptKey(key), EncryptVector3Value(key, value));
#endif
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return Vector3.zero.
    /// </summary>
    public static Vector3 GetVector3(string key)
    {
        return GetVector3(key, Vector3.zero);
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return <c>defaultValue</c>.
    /// </summary>
    public static Vector3 GetVector3(string key, Vector3 defaultValue)
    {
#if UNITY_EDITOR
        if (unobscuredMode)
        {
            string[] values = ReadUnobscured(key, defaultValue).Split(DATA_SEPARATOR[0]);
            float x;
            float y;
            float z;
            float.TryParse(values[0], out x);
            float.TryParse(values[1], out y);
            float.TryParse(values[2], out z);
            return new Vector3(x, y, z);
        }
#endif
        string encrypted = GetEncryptedDataString(key, EncryptKey(key));
        return encrypted == RAW_NOT_FOUND ? defaultValue : DecryptVector3Value(key, encrypted, defaultValue);
    }

    private static string EncryptVector3Value(string key, Vector3 value)
    {
        byte[] cleanBytes = new byte[12];
        Buffer.BlockCopy(BitConverter.GetBytes(value.x), 0, cleanBytes, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(value.y), 0, cleanBytes, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(value.z), 0, cleanBytes, 8, 4);
        return EncryptData(key, cleanBytes, DataType.Vector3);
    }

    private static Vector3 DecryptVector3Value(string key, string encryptedInput, Vector3 defaultValue)
    {
        if (encryptedInput.IndexOf(DEPRECATED_RAW_SEPARATOR) > -1)
        {
            string deprecatedValue = DeprecatedDecryptValue(encryptedInput);
            if (deprecatedValue == "") return defaultValue;
            string[] values = deprecatedValue.Split(DATA_SEPARATOR[0]);
            float x;
            float y;
            float z;
            float.TryParse(values[0], out x);
            float.TryParse(values[1], out y);
            float.TryParse(values[2], out z);
            Vector3 deprecatedResult = new Vector3(x, y, z);
            SetVector3(key, deprecatedResult);
            return deprecatedResult;
        }

        byte[] cleanBytes = DecryptData(key, encryptedInput);
        if (cleanBytes == null)
        {
            return defaultValue;
        }

        Vector3 cleanValue;
        cleanValue.x = BitConverter.ToSingle(cleanBytes, 0);
        cleanValue.y = BitConverter.ToSingle(cleanBytes, 4);
        cleanValue.z = BitConverter.ToSingle(cleanBytes, 8);
        return cleanValue;
    }
    #endregion

    #region Quaternion
    /// <summary>
    /// Sets the <c>value</c> of the preference identified by <c>key</c>.
    /// </summary>
    public static void SetQuaternion(string key, Quaternion value)
    {
#if UNITY_EDITOR
        if (unobscuredMode) WriteUnobscured(key, value.x + DATA_SEPARATOR + value.y + DATA_SEPARATOR + value.z + DATA_SEPARATOR + value.w);
#endif

#if UNITY_WEBPLAYER
			try
			{
				PlayerPrefs.SetString(EncryptKey(key), EncryptQuaternionValue(key, value));
			}
			catch (PlayerPrefsException exception)
			{
				Debug.LogException(exception);
			}
#else
        PlayerPrefs.SetString(EncryptKey(key), EncryptQuaternionValue(key, value));
#endif
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return Quaternion.identity.
    /// </summary>
    public static Quaternion GetQuaternion(string key)
    {
        return GetQuaternion(key, Quaternion.identity);
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return <c>defaultValue</c>.
    /// </summary>
    public static Quaternion GetQuaternion(string key, Quaternion defaultValue)
    {
#if UNITY_EDITOR
        if (unobscuredMode)
        {
            string[] values = ReadUnobscured(key, defaultValue).Split(DATA_SEPARATOR[0]);
            float x;
            float y;
            float z;
            float w;
            float.TryParse(values[0], out x);
            float.TryParse(values[1], out y);
            float.TryParse(values[2], out z);
            float.TryParse(values[3], out w);
            return new Quaternion(x, y, z, w);
        }
#endif
        string encrypted = GetEncryptedDataString(key, EncryptKey(key));
        return encrypted == RAW_NOT_FOUND ? defaultValue : DecryptQuaternionValue(key, encrypted, defaultValue);
    }

    private static string EncryptQuaternionValue(string key, Quaternion value)
    {
        byte[] cleanBytes = new byte[16];
        Buffer.BlockCopy(BitConverter.GetBytes(value.x), 0, cleanBytes, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(value.y), 0, cleanBytes, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(value.z), 0, cleanBytes, 8, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(value.w), 0, cleanBytes, 12, 4);
        return EncryptData(key, cleanBytes, DataType.Quaternion);
    }

    private static Quaternion DecryptQuaternionValue(string key, string encryptedInput, Quaternion defaultValue)
    {
        if (encryptedInput.IndexOf(DEPRECATED_RAW_SEPARATOR) > -1)
        {
            string deprecatedValue = DeprecatedDecryptValue(encryptedInput);
            if (deprecatedValue == "") return defaultValue;
            string[] values = deprecatedValue.Split(DATA_SEPARATOR[0]);
            float x;
            float y;
            float z;
            float w;
            float.TryParse(values[0], out x);
            float.TryParse(values[1], out y);
            float.TryParse(values[2], out z);
            float.TryParse(values[3], out w);
            Quaternion deprecatedResult = new Quaternion(x, y, z, w);
            SetQuaternion(key, deprecatedResult);
            return deprecatedResult;
        }

        byte[] cleanBytes = DecryptData(key, encryptedInput);
        if (cleanBytes == null)
        {
            return defaultValue;
        }

        Quaternion cleanValue;
        cleanValue.x = BitConverter.ToSingle(cleanBytes, 0);
        cleanValue.y = BitConverter.ToSingle(cleanBytes, 4);
        cleanValue.z = BitConverter.ToSingle(cleanBytes, 8);
        cleanValue.w = BitConverter.ToSingle(cleanBytes, 12);
        return cleanValue;
    }
    #endregion

    #region Color
    /// <summary>
    /// Sets the <c>value</c> of the preference identified by <c>key</c>.
    /// </summary>
    public static void SetColor(string key, Color32 value)
    {
        uint encodedColor = (uint)((value.a << 24) | (value.r << 16) | (value.g << 8) | value.b);

#if UNITY_EDITOR
        if (unobscuredMode) WriteUnobscured(key, encodedColor);
#endif

#if UNITY_WEBPLAYER
			try
			{
				PlayerPrefs.SetString(EncryptKey(key), EncryptUIntValue(key, encodedColor));
			}
			catch (PlayerPrefsException exception)
			{
				Debug.LogException(exception);
			}
#else
        PlayerPrefs.SetString(EncryptKey(key), EncryptColorValue(key, encodedColor));
#endif
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return Color.black.
    /// </summary>
    public static Color32 GetColor(string key)
    {
        return GetColor(key, new Color32(0, 0, 0, 1));
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return <c>defaultValue</c>.
    /// </summary>
    public static Color32 GetColor(string key, Color32 defaultValue)
    {
        // 16777216u == Color32(0,0,0,1);
#if UNITY_EDITOR
        if (unobscuredMode)
        {
            uint encodedColorUnobscured;
            uint.TryParse(ReadUnobscured(key, 16777216u), out encodedColorUnobscured);

            byte aUnobscured = (byte)(encodedColorUnobscured >> 24);
            byte rUnobscured = (byte)(encodedColorUnobscured >> 16);
            byte gUnobscured = (byte)(encodedColorUnobscured >> 8);
            byte bUnobscured = (byte)(encodedColorUnobscured >> 0);
            return new Color32(rUnobscured, gUnobscured, bUnobscured, aUnobscured);
        }
#endif
        string encrypted = GetEncryptedDataString(key, EncryptKey(key));
        if (encrypted == RAW_NOT_FOUND)
        {
            return defaultValue;
        }

        uint encodedColor = DecryptUIntValue(key, encrypted, 16777216u);
        byte a = (byte)(encodedColor >> 24);
        byte r = (byte)(encodedColor >> 16);
        byte g = (byte)(encodedColor >> 8);
        byte b = (byte)(encodedColor >> 0);
        return new Color32(r, g, b, a);
    }

    private static string EncryptColorValue(string key, uint value)
    {
        byte[] cleanBytes = BitConverter.GetBytes(value);
        return EncryptData(key, cleanBytes, DataType.Color);
    }
    #endregion

    #region Rect
    /// <summary>
    /// Sets the <c>value</c> of the preference identified by <c>key</c>.
    /// </summary>
    public static void SetRect(string key, Rect value)
    {
#if UNITY_EDITOR
        if (unobscuredMode) WriteUnobscured(key, value.x + DATA_SEPARATOR + value.y + DATA_SEPARATOR + value.width + DATA_SEPARATOR + value.height);
#endif

#if UNITY_WEBPLAYER
			try
			{
				PlayerPrefs.SetString(EncryptKey(key), EncryptRectValue(key, value));
			}
			catch (PlayerPrefsException exception)
			{
				Debug.LogException(exception);
			}
#else
        PlayerPrefs.SetString(EncryptKey(key), EncryptRectValue(key, value));
#endif
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return (0,0,0,0) rect.
    /// </summary>
    public static Rect GetRect(string key)
    {
        return GetRect(key, new Rect(0, 0, 0, 0));
    }

    /// <summary>
    /// Returns the value corresponding to <c>key</c> in the preference file if it exists.
    /// If it doesn't exist, it will return <c>defaultValue</c>.
    /// </summary>
    public static Rect GetRect(string key, Rect defaultValue)
    {
#if UNITY_EDITOR
        if (unobscuredMode)
        {
            string[] values = ReadUnobscured(key, defaultValue).Split(DATA_SEPARATOR[0]);
            float x;
            float y;
            float w;
            float h;
            float.TryParse(values[0], out x);
            float.TryParse(values[1], out y);
            float.TryParse(values[2], out w);
            float.TryParse(values[3], out h);
            return new Rect(x, y, w, h);
        }
#endif
        string encrypted = GetEncryptedDataString(key, EncryptKey(key));
        return encrypted == RAW_NOT_FOUND ? defaultValue : DecryptRectValue(key, encrypted, defaultValue);
    }

    private static string EncryptRectValue(string key, Rect value)
    {
        byte[] cleanBytes = new byte[16];
        Buffer.BlockCopy(BitConverter.GetBytes(value.x), 0, cleanBytes, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(value.y), 0, cleanBytes, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(value.width), 0, cleanBytes, 8, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(value.height), 0, cleanBytes, 12, 4);
        return EncryptData(key, cleanBytes, DataType.Rect);
    }

    private static Rect DecryptRectValue(string key, string encryptedInput, Rect defaultValue)
    {
        if (encryptedInput.IndexOf(DEPRECATED_RAW_SEPARATOR) > -1)
        {
            string deprecatedValue = DeprecatedDecryptValue(encryptedInput);
            if (deprecatedValue == "") return defaultValue;
            string[] values = deprecatedValue.Split(DATA_SEPARATOR[0]);
            float x;
            float y;
            float w;
            float h;
            float.TryParse(values[0], out x);
            float.TryParse(values[1], out y);
            float.TryParse(values[2], out w);
            float.TryParse(values[3], out h);
            Rect deprecatedResult = new Rect(x, y, w, h);
            SetRect(key, deprecatedResult);
            return deprecatedResult;
        }

        byte[] cleanBytes = DecryptData(key, encryptedInput);
        if (cleanBytes == null)
        {
            return defaultValue;
        }

        Rect cleanValue = new Rect();
        cleanValue.x = BitConverter.ToSingle(cleanBytes, 0);
        cleanValue.y = BitConverter.ToSingle(cleanBytes, 4);
        cleanValue.width = BitConverter.ToSingle(cleanBytes, 8);
        cleanValue.height = BitConverter.ToSingle(cleanBytes, 12);
        return cleanValue;
    }
    #endregion

    /// <summary>
    /// Allows to set the raw encrypted value for the specified key.
    /// </summary>
    public static void SetRawValue(string key, string encryptedValue)
    {
        PlayerPrefs.SetString(EncryptKey(key), encryptedValue);
    }

    /// <summary>
    /// Allows to get the raw encrypted value for the specified key.
    /// </summary>
    /// <returns>Raw encrypted value or empty string in case there is no value for the specified key.</returns>
    public static string GetRawValue(string key)
    {
        string encryptedKey = EncryptKey(key);
        return PlayerPrefs.GetString(encryptedKey);
    }

    internal static DataType GetRawValueType(string value)
    {
        DataType result = DataType.Unknown;
        byte[] inputBytes;

        try
        {
            inputBytes = Convert.FromBase64String(value);
        }
        catch (Exception)
        {
            return result;
        }

        if (inputBytes.Length < 7)
        {
            return result;
        }

        int inputLength = inputBytes.Length;

        result = (DataType)inputBytes[inputLength - 7];

        return result;
    }

    internal static string EncryptKey(string key)
    {
        key = EkumeSecureString.EncryptDecrypt(key, cryptoKey);
        key = Convert.ToBase64String(Encoding.UTF8.GetBytes(key));
        return key;
    }

    /// <summary>
    /// Returns true if <c>key</c> exists in the EkumeSecureData or in regular PlayerPrefs.
    /// </summary>
    public static bool HasKey(string key)
    {
        return PlayerPrefs.HasKey(key) || PlayerPrefs.HasKey(EncryptKey(key));
    }

    /// <summary>
    /// Removes <c>key</c> and its corresponding value from the EkumeSecureData and regular PlayerPrefs.
    /// </summary>
    public static void DeleteKey(string key)
    {
        PlayerPrefs.DeleteKey(EncryptKey(key));
        if (!preservePlayerPrefs) PlayerPrefs.DeleteKey(key);
    }

    /// <summary>
    /// Removes all keys and values from the preferences, including anything saved with regular PlayerPrefs. Use with caution!
    /// </summary>
    public static void DeleteAll()
    {
        PlayerPrefs.DeleteAll();
    }

    /// <summary>
    /// Writes all modified preferences to disk.
    /// </summary>
    /// By default, Unity writes preferences to disk on Application Quit.<br/>
    /// In case when the game crashes or otherwise prematurely exits, you might want to write the preferences at sensible 'checkpoints' in your game.<br/>
    /// This function will write to disk potentially causing a small hiccup, therefore it is not recommended to call during actual game play.
    public static void Save()
    {
        PlayerPrefs.Save();
    }

    private static string GetEncryptedDataString(string key, string encryptedKey)
    {
        string result = PlayerPrefs.GetString(encryptedKey, RAW_NOT_FOUND);

        if (result == RAW_NOT_FOUND)
        {
            if (PlayerPrefs.HasKey(key))
            {
                Debug.LogWarning("[Ekume]  Are you trying to read regular PlayerPrefs data using EkumeSecureData (key = " + key + ")?");
            }
        }
        return result;
    }

    private static string EncryptData(string key, byte[] cleanBytes, DataType type)
    {
        int dataLength = cleanBytes.Length;
        byte[] encryptedBytes = EncryptDecryptBytes(cleanBytes, dataLength, key + cryptoKey);

        uint dataHash = xxHash.CalculateHash(cleanBytes, dataLength, 0);
        byte[] dataHashBytes = new byte[4]; // replaces BitConverter.GetBytes(hash);
        dataHashBytes[0] = (byte)(dataHash & 0xFF);
        dataHashBytes[1] = (byte)((dataHash >> 8) & 0xFF);
        dataHashBytes[2] = (byte)((dataHash >> 16) & 0xFF);
        dataHashBytes[3] = (byte)((dataHash >> 24) & 0xFF);

        byte[] deviceHashBytes = null;
        int finalBytesLength;
        if (lockToDevice != DeviceLockLevel.None)
        {
            // 4 device id hash + 1 data type + 1 device lock mode + 1 version + 4 data hash
            finalBytesLength = dataLength + 11;
            uint deviceHash = DeviceIdHash;
            deviceHashBytes = new byte[4]; // replaces BitConverter.GetBytes(hash);
            deviceHashBytes[0] = (byte)(deviceHash & 0xFF);
            deviceHashBytes[1] = (byte)((deviceHash >> 8) & 0xFF);
            deviceHashBytes[2] = (byte)((deviceHash >> 16) & 0xFF);
            deviceHashBytes[3] = (byte)((deviceHash >> 24) & 0xFF);
        }
        else
        {
            // 1 data type + 1 device lock mode + 1 version + 4 data hash
            finalBytesLength = dataLength + 7;
        }

        byte[] finalBytes = new byte[finalBytesLength];

        Buffer.BlockCopy(encryptedBytes, 0, finalBytes, 0, dataLength);
        if (deviceHashBytes != null)
        {
            Buffer.BlockCopy(deviceHashBytes, 0, finalBytes, dataLength, 4);
        }

        finalBytes[finalBytesLength - 7] = (byte)type;
        finalBytes[finalBytesLength - 6] = VERSION;
        finalBytes[finalBytesLength - 5] = (byte)lockToDevice;
        Buffer.BlockCopy(dataHashBytes, 0, finalBytes, finalBytesLength - 4, 4);

        return Convert.ToBase64String(finalBytes);
    }

    internal static byte[] DecryptData(string key, string encryptedInput)
    {
        byte[] inputBytes;

        try
        {
            inputBytes = Convert.FromBase64String(encryptedInput);
        }
        catch (Exception)
        {
            SavesTampered();
            return null;
        }

        if (inputBytes.Length <= 0)
        {
            SavesTampered();
            return null;
        }

        int inputLength = inputBytes.Length;

        // reserved for future use
        // type = (DataType)inputBytes[inputLength - 7];

        byte inputVersion = inputBytes[inputLength - 6];
        if (inputVersion != VERSION)
        {
            // in future we possibly will have some old versions fallbacks here
            SavesTampered();
            return null;
        }

        DeviceLockLevel inputLockToDevice = (DeviceLockLevel)inputBytes[inputLength - 5];

        byte[] dataHashBytes = new byte[4];
        Buffer.BlockCopy(inputBytes, inputLength - 4, dataHashBytes, 0, 4);
        uint inputDataHash = (uint)(dataHashBytes[0] | dataHashBytes[1] << 8 | dataHashBytes[2] << 16 | dataHashBytes[3] << 24);

        int dataBytesLength;
        uint inputDeviceHash = 0;

        if (inputLockToDevice != DeviceLockLevel.None)
        {
            dataBytesLength = inputLength - 11;
            if (lockToDevice != DeviceLockLevel.None)
            {
                byte[] deviceHashBytes = new byte[4];
                Buffer.BlockCopy(inputBytes, dataBytesLength, deviceHashBytes, 0, 4);
                inputDeviceHash = (uint)(deviceHashBytes[0] | deviceHashBytes[1] << 8 | deviceHashBytes[2] << 16 | deviceHashBytes[3] << 24);
            }
        }
        else
        {
            dataBytesLength = inputLength - 7;
        }

        byte[] encryptedBytes = new byte[dataBytesLength];
        Buffer.BlockCopy(inputBytes, 0, encryptedBytes, 0, dataBytesLength);
        byte[] cleanBytes = EncryptDecryptBytes(encryptedBytes, dataBytesLength, key + cryptoKey);

        uint realDataHash = xxHash.CalculateHash(cleanBytes, dataBytesLength, 0);
        if (realDataHash != inputDataHash)
        {
            SavesTampered();
            return null;
        }

        if (lockToDevice == DeviceLockLevel.Strict && inputDeviceHash == 0 && !emergencyMode && !readForeignSaves)
        {
            return null;
        }

        if (inputDeviceHash != 0 && !emergencyMode)
        {
            uint realDeviceHash = DeviceIdHash;
            if (inputDeviceHash != realDeviceHash)
            {
                PossibleForeignSavesDetected();
                if (!readForeignSaves) return null;
            }
        }

        return cleanBytes;
    }

    private static uint CalculateChecksum(string input)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input + cryptoKey);
        uint hash = xxHash.CalculateHash(inputBytes, inputBytes.Length, 0);
        return hash;
    }

    private static void SavesTampered()
    {
        if (onAlterationDetected != null)
        {
            onAlterationDetected();
            onAlterationDetected = null;
        }
    }

    private static void PossibleForeignSavesDetected()
    {
        if (onPossibleForeignSavesDetected != null && !foreignSavesReported)
        {
            foreignSavesReported = true;
            onPossibleForeignSavesDetected();
        }
    }

    private static string GetDeviceId()
    {
        string id = "";
#if UNITY_IPHONE
#if UNITY_4_3 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7
			id = iPhone.vendorIdentifier;
#else
			id = UnityEngine.iOS.Device.vendorIdentifier;
#endif
#endif

#if !ACTK_PREVENT_READ_PHONE_STATE
        if (string.IsNullOrEmpty(id)) id = SystemInfo.deviceUniqueIdentifier;
#else
			Debug.LogError("[Ekume]  Looks like you forced ACTK_PREVENT_READ_PHONE_STATE flag, but still use LockToDevice feature. It will work incorrect!");
#endif
        return id;
    }

    private static byte[] EncryptDecryptBytes(byte[] bytes, int dataLength, string key)
    {
        int encryptionKeyLength = key.Length;

        byte[] result = new byte[dataLength];

        for (int i = 0; i < dataLength; i++)
        {
            result[i] = (byte)(bytes[i] ^ key[i % encryptionKeyLength]);
        }

        return result;
    }

#if UNITY_EDITOR
    private static void WriteUnobscured<T>(string key, T value)
    {
        PlayerPrefs.SetString(key, value.ToString());
    }

    private static string ReadUnobscured<T>(string key, T defaultValueRaw)
    {
        return PlayerPrefs.GetString(key, defaultValueRaw.ToString());
    }
#endif

    internal enum DataType : byte
    {
        Unknown = 0,
        Int = 5,
        UInt = 10,
        String = 15,
        Float = 20,
        Double = 25,
        Long = 30,
        Bool = 35,
        ByteArray = 40,
        Vector2 = 45,
        Vector3 = 50,
        Quaternion = 55,
        Color = 60,
        Rect = 65,
    }

    /// <summary>
    /// Used to specify level of the device lock feature strictness.
    /// </summary>
    public enum DeviceLockLevel : byte
    {
        /// <summary>
        /// Both locked and not locked to any device data can be read (default one).
        /// </summary>
        None,

        /// <summary>
        /// Performs checks for locked data and still allows reading not locked data (useful when you decided to lock your saves in one of app updates and wish to keep user data).
        /// </summary>
        Soft,

        /// <summary>
        /// Only locked to the current device data can be read. This is a preferred mode, but it should be enabled right from the first app release. If you released app without data lock consider using Soft lock or all previously saved data will not be accessible.
        /// </summary>
        Strict
    }

    #region deprecated
    ///
    /// DEPRECATED CODE (for auto-migration from previous EkumeSecureData version
    /// 
    private const char DEPRECATED_RAW_SEPARATOR = ':';
    private static string DeprecatedDecryptValue(string value)
    {
        string[] rawParts = value.Split(DEPRECATED_RAW_SEPARATOR);

        if (rawParts.Length < 2)
        {
            SavesTampered();
            return "";
        }

        string b64EncryptedValue = rawParts[0];
        string checksum = rawParts[1];

        byte[] bytes;

        try
        {
            bytes = Convert.FromBase64String(b64EncryptedValue);
        }
        catch
        {
            SavesTampered();
            return "";
        }

        string encryptedValue = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        string clearValue = EkumeSecureString.EncryptDecrypt(encryptedValue, cryptoKey);

        // checking saves for falsification
        if (rawParts.Length == 3)
        {
            if (checksum != DeprecatedCalculateChecksum(b64EncryptedValue + DeprecatedDeviceId))
            {
                SavesTampered();
            }
        }
        else if (rawParts.Length == 2)
        {
            if (checksum != DeprecatedCalculateChecksum(b64EncryptedValue))
            {
                SavesTampered();
            }
        }
        else
        {
            SavesTampered();
        }

        // checking saves for foreignness
        if (lockToDevice != DeviceLockLevel.None && !emergencyMode)
        {
            if (rawParts.Length >= 3)
            {
                string id = rawParts[2];
                if (id != DeprecatedDeviceId)
                {
                    if (!readForeignSaves) clearValue = "";
                    PossibleForeignSavesDetected();
                }
            }
            else if (lockToDevice == DeviceLockLevel.Strict)
            {
                if (!readForeignSaves) clearValue = "";
                PossibleForeignSavesDetected();
            }
            else
            {
                if (checksum != DeprecatedCalculateChecksum(b64EncryptedValue))
                {
                    if (!readForeignSaves) clearValue = "";
                    PossibleForeignSavesDetected();
                }
            }
        }
        return clearValue;
    }

    private static string DeprecatedCalculateChecksum(string input)
    {
        int result = 0;

        byte[] inputBytes = Encoding.UTF8.GetBytes(input + cryptoKey);
        int len = inputBytes.Length;
        int encryptionKeyLen = cryptoKey.Length ^ 64;
        for (int i = 0; i < len; i++)
        {
            byte b = inputBytes[i];
            result += b + b * (i + encryptionKeyLen) % 3;
        }

        return result.ToString("X2");
    }

    private static string deprecatedDeviceId;
    private static string DeprecatedDeviceId
    {
        get
        {
            if (string.IsNullOrEmpty(deprecatedDeviceId))
            {
                deprecatedDeviceId = DeprecatedCalculateChecksum(DeviceId);
            }
            return deprecatedDeviceId;
        }
    }
    #endregion
}

//string

/// <summary>
/// Use it instead of regular <c>string</c> for any cheating-sensitive variables.
/// </summary>
/// <strong><em>Regular type is faster and memory wiser comparing to the obscured one!</em></strong>
[Serializable]
public sealed class EkumeSecureString
{
    private static string cryptoKey = "4441";

#if UNITY_EDITOR
    // For internal Editor usage only (may be useful for drawers).
    public static string cryptoKeyEditor = cryptoKey;
#endif

    [SerializeField]
    private string currentCryptoKey;

    [SerializeField]
    private byte[] hiddenValue;

    [SerializeField]
    private string fakeValue;

    [SerializeField]
    private bool inited;

    // for serialization purposes
    private EkumeSecureString() { }

    private EkumeSecureString(byte[] value)
    {
        currentCryptoKey = cryptoKey;
        hiddenValue = value;
        fakeValue = null;
        inited = true;
    }

    /// <summary>
    /// Allows to change default crypto key of this type instances. All new instances will use specified key.<br/>
    /// All current instances will use previous key unless you call ApplyNewCryptoKey() on them explicitly.
    /// </summary>
    public static void SetNewCryptoKey(string newKey)
    {
        cryptoKey = newKey;
    }

    /// <summary>
    /// Simple symmetric encryption, uses default crypto key.
    /// </summary>
    /// <returns>Encrypted or decrypted <c>string</c> (depending on what <c>string</c> was passed to the function)</returns>
    public static string EncryptDecrypt(string value)
    {
        return EncryptDecrypt(value, "");
    }

    /// <summary>
    /// Simple symmetric encryption, uses passed crypto key.
    /// </summary>
    /// <returns>Encrypted or decrypted <c>string</c> (depending on what <c>string</c> was passed to the function)</returns>
    public static string EncryptDecrypt(string value, string key)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        if (string.IsNullOrEmpty(key))
        {
            key = cryptoKey;
        }

        int keyLength = key.Length;
        int valueLength = value.Length;

        char[] result = new char[valueLength];

        for (int i = 0; i < valueLength; i++)
        {
            result[i] = (char)(value[i] ^ key[i % keyLength]);
        }

        return new string(result);
    }

    /// <summary>
    /// Use it after SetNewCryptoKey() to re-encrypt current instance using new crypto key.
    /// </summary>
    public void ApplyNewCryptoKey()
    {
        if (currentCryptoKey != cryptoKey)
        {
            hiddenValue = InternalEncrypt(InternalDecrypt());
            currentCryptoKey = cryptoKey;
        }
    }

    /// <summary>
    /// Allows to change current crypto key to the new random value and re-encrypt variable using it.
    /// Use it for extra protection against 'unknown value' search.
    /// Just call it sometimes when your variable doesn't change to fool the cheater.
    /// </summary>
    /// <strong>\htmlonly<font color="FF4040">WARNING:</font>\endhtmlonly produces some garbage, be careful when using it!</strong>
    public void RandomizeCryptoKey()
    {
        string decrypted = InternalDecrypt();

        currentCryptoKey = Random.Range(int.MinValue, int.MaxValue).ToString();
        hiddenValue = InternalEncrypt(decrypted, currentCryptoKey);
    }

    /// <summary>
    /// Allows to pick current obscured value as is.
    /// </summary>
    /// Use it in conjunction with SetEncrypted().<br/>
    /// Useful for saving data in obscured state.
    public string GetEncrypted()
    {
        ApplyNewCryptoKey();
        return GetString(hiddenValue);
    }

    /// <summary>
    /// Allows to explicitly set current obscured value.
    /// </summary>
    /// Use it in conjunction with GetEncrypted().<br/>
    /// Useful for loading data stored in obscured state.
    public void SetEncrypted(string encrypted)
    {
        inited = true;
        hiddenValue = GetBytes(encrypted);
        if (EkumeDataCheatingDetector.IsRunning)
        {
            fakeValue = InternalDecrypt();
        }
    }

    private static byte[] InternalEncrypt(string value)
    {
        return InternalEncrypt(value, cryptoKey);
    }

    private static byte[] InternalEncrypt(string value, string key)
    {
        return GetBytes(EncryptDecrypt(value, key));
    }

    private string InternalDecrypt()
    {
        if (!inited)
        {
            currentCryptoKey = cryptoKey;
            hiddenValue = InternalEncrypt("");
            fakeValue = "";
            inited = true;
        }

        string key = currentCryptoKey;
        if (string.IsNullOrEmpty(key))
        {
            key = cryptoKey;
        }

        string result = EncryptDecrypt(GetString(hiddenValue), key);

        if (EkumeDataCheatingDetector.IsRunning && !string.IsNullOrEmpty(fakeValue) && result != fakeValue)
        {
            EkumeDataCheatingDetector.Instance.OnCheatingDetected();
        }

        return result;
    }

    #region operators and overrides
    //! @cond
    public static implicit operator EkumeSecureString(string value)
    {
        if (value == null)
        {
            return null;
        }

        EkumeSecureString obscured = new EkumeSecureString(InternalEncrypt(value));
        if (EkumeDataCheatingDetector.IsRunning)
        {
            obscured.fakeValue = value;
        }
        return obscured;
    }

    public static implicit operator string (EkumeSecureString value)
    {
        if (value == null)
        {
            return null;
        }
        return value.InternalDecrypt();
    }

    /// <summary>
    /// Overrides default ToString to provide easy implicit conversion to the <c>string</c>.
    /// </summary>
    public override string ToString()
    {
        return InternalDecrypt();
    }

    /// <summary>
    /// Determines whether two specified ObscuredStrings have the same value.
    /// </summary>
    /// 
    /// <returns>
    /// true if the value of <paramref name="a"/> is the same as the value of <paramref name="b"/>; otherwise, false.
    /// </returns>
    /// <param name="a">An ObscuredString or null. </param><param name="b">An ObscuredString or null. </param><filterpriority>3</filterpriority>
    public static bool operator ==(EkumeSecureString a, EkumeSecureString b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if ((object)a == null || (object)b == null)
        {
            return false;
        }

        if (a.currentCryptoKey == b.currentCryptoKey)
        {
            return ArraysEquals(a.hiddenValue, b.hiddenValue);
        }

        return string.Equals(a.InternalDecrypt(), b.InternalDecrypt());
    }

    /// <summary>
    /// Determines whether two specified ObscuredStrings have different values.
    /// </summary>
    /// 
    /// <returns>
    /// true if the value of <paramref name="a"/> is different from the value of <paramref name="b"/>; otherwise, false.
    /// </returns>
    /// <param name="a">An ObscuredString or null. </param><param name="b">An ObscuredString or null. </param><filterpriority>3</filterpriority>
    public static bool operator !=(EkumeSecureString a, EkumeSecureString b)
    {
        return !(a == b);
    }

    /// <summary>
    /// Determines whether this instance of ObscuredString and a specified object, which must also be a ObscuredString object, have the same value.
    /// </summary>
    /// 
    /// <returns>
    /// true if <paramref name="obj"/> is a ObscuredString and its value is the same as this instance; otherwise, false.
    /// </returns>
    /// <param name="obj">An <see cref="T:System.Object"/>. </param><filterpriority>2</filterpriority>
    public override bool Equals(object obj)
    {
        if (!(obj is EkumeSecureString))
            return false;

        return Equals((EkumeSecureString)obj);
    }

    /// <summary>
    /// Determines whether this instance and another specified ObscuredString object have the same value.
    /// </summary>
    /// 
    /// <returns>
    /// true if the value of the <paramref name="value"/> parameter is the same as this instance; otherwise, false.
    /// </returns>
    /// <param name="value">A ObscuredString. </param><filterpriority>2</filterpriority>
    public bool Equals(EkumeSecureString value)
    {
        if (value == null) return false;

        if (currentCryptoKey == value.currentCryptoKey)
        {
            return ArraysEquals(hiddenValue, value.hiddenValue);
        }

        return string.Equals(InternalDecrypt(), value.InternalDecrypt());
    }

    /// <summary>
    /// Determines whether this string and a specified ObscuredString object have the same value. A parameter specifies the culture, case, and sort rules used in the comparison.
    /// </summary>
    /// 
    /// <returns>
    /// true if the value of the <paramref name="value"/> parameter is the same as this string; otherwise, false.
    /// </returns>
    /// <param name="value">An ObscuredString to compare.</param><param name="comparisonType">A value that defines the type of comparison. </param><exception cref="T:System.ArgumentException"><paramref name="comparisonType"/> is not a <see cref="T:System.StringComparison"/> value. </exception><filterpriority>2</filterpriority>
    public bool Equals(EkumeSecureString value, StringComparison comparisonType)
    {
        if (value == null) return false;

        return string.Equals(InternalDecrypt(), value.InternalDecrypt(), comparisonType);
    }

    /// <summary>
    /// Returns the hash code for this ObscuredString.
    /// </summary>
    /// 
    /// <returns>
    /// A 32-bit signed integer hash code.
    /// </returns>
    public override int GetHashCode()
    {
        return InternalDecrypt().GetHashCode();
    }
    //! @endcond
    #endregion

    private static byte[] GetBytes(string str)
    {
        byte[] bytes = new byte[str.Length * sizeof(char)];
        System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static string GetString(byte[] bytes)
    {
        char[] chars = new char[bytes.Length / sizeof(char)];
        System.Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
        return new string(chars);
    }

    private static bool ArraysEquals(byte[] a1, byte[] a2)
    {
        if (a1 == a2)
        {
            return true;
        }

        if ((a1 != null) && (a2 != null))
        {
            if (a1.Length != a2.Length)
            {
                return false;
            }
            for (int i = 0; i < a1.Length; i++)
            {
                if (a1[i] != a2[i])
                {
                    return false;
                }
            }
            return true;
        }
        return false;
    }
}

//Detectors

/// <summary>
/// Base class for all detectors.
/// </summary>
public abstract class ActDetectorBase : MonoBehaviour
{
    protected const string CONTAINER_NAME = "Anticheat Ekume Detectors";

    protected static GameObject detectorsContainer;

    /// <summary>
    /// Allows to start detector automatically.
    /// Otherwise, you'll need to call StartDetection() method to start it.
    /// </summary>
    /// Useful in conjunction with proper Detection Event configuration in the inspector.
    /// Allows to use detector without writing any code except the actual reaction on cheating.
    [Tooltip("Automatically start detector. Detection Event will be called on detection.")]
    public bool autoStart = true;

    /// <summary>
    /// Detector will survive new level (scene) load if checked. Otherwise it will be destroyed.
    /// </summary>
    /// On dispose Detector follows 2 rules:
    /// - if Game Object's name is "Anticheat Ekume Detectors": it will be automatically 
    /// destroyed if no other Detectors left attached regardless of any other components or children;<br/>
    /// - if Game Object's name is NOT "Anticheat Ekume Detectors": it will be automatically destroyed only
    /// if it has neither other components nor children attached;
    [Tooltip("Detector will survive new level (scene) load if checked.")]
    public bool keepAlive = true;

    /// <summary>
    /// Detector component will be automatically disposed after firing callback if enabled.
    /// Otherwise, it will just stop internal processes.
    /// </summary>
    /// On dispose Detector follows 2 rules:
    /// - if Game Object's name is "Anticheat Ekume Detectors": it will be automatically 
    /// destroyed if no other Detectors left attached regardless of any other components or children;<br/>
    /// - if Game Object's name is NOT "Anticheat Ekume Detectors": it will be automatically destroyed only
    /// if it has neither other components nor children attached;
    [Tooltip("Automatically dispose Detector after firing callback.")]
    public bool autoDispose = true;

    [SerializeField]
    protected UnityEvent detectionEvent = null;
    protected UnityAction detectionAction = null;

    [SerializeField]
    protected bool detectionEventHasListener = false;

    protected bool isRunning;
    protected bool started;

    #region detectors placement
#if UNITY_EDITOR
    private static void SetupDetectorInScene<T>() where T : ActDetectorBase
    {
        T component = FindObjectOfType<T>();
        string detectorName = typeof(T).Name;

        if (component != null)
        {
            if (component.gameObject.name == CONTAINER_NAME)
            {
                UnityEditor.EditorUtility.DisplayDialog(detectorName + " already exists!", detectorName + " already exists in scene and correctly placed on object \"" + CONTAINER_NAME + "\"", "OK");
            }
            else
            {
                int dialogResult = UnityEditor.EditorUtility.DisplayDialogComplex(detectorName + " already exists!", detectorName + " already exists in scene and placed on object \"" + component.gameObject.name + "\". Do you wish to move it to the Game Object \"" + CONTAINER_NAME + "\" or delete it from scene at all?", "Move", "Delete", "Cancel");
                switch (dialogResult)
                {
                    case 0:
                        GameObject container = GameObject.Find(CONTAINER_NAME);
                        if (container == null)
                        {
                            container = new GameObject(CONTAINER_NAME);
                        }
                        T newComponent = container.AddComponent<T>();
                        UnityEditor.EditorUtility.CopySerialized(component, newComponent);
                        DestroyDetectorImmediate(component);
                        break;
                    case 1:
                        DestroyDetectorImmediate(component);
                        break;
                }
            }
        }
        else
        {
            GameObject container = GameObject.Find(CONTAINER_NAME);
            if (container == null)
            {
                container = new GameObject(CONTAINER_NAME);

                UnityEditor.Undo.RegisterCreatedObjectUndo(container, "Create " + CONTAINER_NAME);
            }
            UnityEditor.Undo.AddComponent<T>(container);

            UnityEditor.EditorUtility.DisplayDialog(detectorName + " added!", detectorName + " successfully added to the object \"" + CONTAINER_NAME + "\"", "OK");
        }
    }

    private static void DestroyDetectorImmediate(ActDetectorBase component)
    {
        if (component.transform.childCount == 0 && component.GetComponentsInChildren<Component>(true).Length <= 2)
        {
            DestroyImmediate(component.gameObject);
        }
        else
        {
            DestroyImmediate(component);
        }
    }
#endif
    #endregion

    #region unity messages
    private void Start()
    {
        if (detectorsContainer == null && gameObject.name == CONTAINER_NAME)
        {
            detectorsContainer = gameObject;
        }

        if (autoStart && !started)
        {
            StartDetectionAutomatically();
        }
    }

    private void OnEnable()
    {
        if (!started || (!detectionEventHasListener && detectionAction == null))
            return;
        ResumeDetector();
    }

    private void OnDisable()
    {
        if (!started) return;
        PauseDetector();
    }

    private void OnApplicationQuit()
    {
        DisposeInternal();
    }

    protected virtual void OnDestroy()
    {
        StopDetectionInternal();

        if (transform.childCount == 0 && GetComponentsInChildren<Component>().Length <= 2)
        {
            Destroy(gameObject);
        }
        else if (name == CONTAINER_NAME && GetComponentsInChildren<ActDetectorBase>().Length <= 1)
        {
            Destroy(gameObject);
        }
    }
    #endregion

    protected virtual bool Init(ActDetectorBase instance, string detectorName)
    {
        if (instance != null && instance != this && instance.keepAlive)
        {
            Debug.LogWarning("[Ekume] " + name +
                ": self-destroying, other instance already exists & only one instance allowed!", gameObject);
            Destroy(this);
            return false;
        }

        DontDestroyOnLoad(gameObject);
        return true;
    }

    protected virtual void DisposeInternal()
    {
        Destroy(this);
    }

    internal virtual void OnCheatingDetected()
    {
        if (detectionAction != null) detectionAction();
        if (detectionEventHasListener) detectionEvent.Invoke();

        if (autoDispose)
        {
            DisposeInternal();
        }
        else
        {
            StopDetectionInternal();
        }
    }

    protected abstract void StartDetectionAutomatically();
    protected abstract void StopDetectionInternal();
    protected abstract void PauseDetector();
    protected abstract void ResumeDetector();
}

namespace Detectors2
{
    /// <summary>
    /// Base class for all detectors.
    /// </summary>
    public abstract class EkumeDetectorBase : MonoBehaviour
    {
        protected const string CONTAINER_NAME = "Anticheat Ekume Detectors";

        protected static GameObject detectorsContainer;

        /// <summary>
        /// Allows to start detector automatically.
        /// Otherwise, you'll need to call StartDetection() method to start it.
        /// </summary>
        /// Useful in conjunction with proper Detection Event configuration in the inspector.
        /// Allows to use detector without writing any code except the actual reaction on cheating.
        [Tooltip("Automatically start detector. Detection Event will be called on detection.")]
        public bool autoStart = true;

        /// <summary>
        /// Detector will survive new level (scene) load if checked. Otherwise it will be destroyed.
        /// </summary>
        /// On dispose Detector follows 2 rules:
        /// - if Game Object's name is "Anticheat Ekume Detectors": it will be automatically 
        /// destroyed if no other Detectors left attached regardless of any other components or children;<br/>
        /// - if Game Object's name is NOT "Anticheat Ekume Detectors": it will be automatically destroyed only
        /// if it has neither other components nor children attached;
        [Tooltip("Detector will survive new level (scene) load if checked.")]
        public bool keepAlive = true;

        /// <summary>
        /// Detector component will be automatically disposed after firing callback if enabled.
        /// Otherwise, it will just stop internal processes.
        /// </summary>
        /// On dispose Detector follows 2 rules:
        /// - if Game Object's name is "Anticheat Ekume Detectors": it will be automatically 
        /// destroyed if no other Detectors left attached regardless of any other components or children;<br/>
        /// - if Game Object's name is NOT "Anticheat Ekume Detectors": it will be automatically destroyed only
        /// if it has neither other components nor children attached;
        [Tooltip("Automatically dispose Detector after firing callback.")]
        public bool autoDispose = true;

        [SerializeField]
        protected UnityEvent detectionEvent = null;
        protected UnityAction detectionAction = null;

        [SerializeField]
        protected bool detectionEventHasListener = false;

        protected bool isRunning;
        protected bool started;

        #region detectors placement
#if UNITY_EDITOR
        private static void SetupDetectorInScene<T>() where T : EkumeDetectorBase
        {
            T component = FindObjectOfType<T>();
            string detectorName = typeof(T).Name;

            if (component != null)
            {
                if (component.gameObject.name == CONTAINER_NAME)
                {
                    UnityEditor.EditorUtility.DisplayDialog(detectorName + " already exists!", detectorName + " already exists in scene and correctly placed on object \"" + CONTAINER_NAME + "\"", "OK");
                }
                else
                {
                    int dialogResult = UnityEditor.EditorUtility.DisplayDialogComplex(detectorName + " already exists!", detectorName + " already exists in scene and placed on object \"" + component.gameObject.name + "\". Do you wish to move it to the Game Object \"" + CONTAINER_NAME + "\" or delete it from scene at all?", "Move", "Delete", "Cancel");
                    switch (dialogResult)
                    {
                        case 0:
                            GameObject container = GameObject.Find(CONTAINER_NAME);
                            if (container == null)
                            {
                                container = new GameObject(CONTAINER_NAME);
                            }
                            T newComponent = container.AddComponent<T>();
                            UnityEditor.EditorUtility.CopySerialized(component, newComponent);
                            DestroyDetectorImmediate(component);
                            break;
                        case 1:
                            DestroyDetectorImmediate(component);
                            break;
                    }
                }
            }
            else
            {
                GameObject container = GameObject.Find(CONTAINER_NAME);
                if (container == null)
                {
                    container = new GameObject(CONTAINER_NAME);

                    UnityEditor.Undo.RegisterCreatedObjectUndo(container, "Create " + CONTAINER_NAME);
                }
                UnityEditor.Undo.AddComponent<T>(container);

                UnityEditor.EditorUtility.DisplayDialog(detectorName + " added!", detectorName + " successfully added to the object \"" + CONTAINER_NAME + "\"", "OK");
            }
        }

        private static void DestroyDetectorImmediate(EkumeDetectorBase component)
        {
            if (component.transform.childCount == 0 && component.GetComponentsInChildren<Component>(true).Length <= 2)
            {
                DestroyImmediate(component.gameObject);
            }
            else
            {
                DestroyImmediate(component);
            }
        }
#endif
        #endregion

        #region unity messages
        private void Start()
        {
            if (detectorsContainer == null && gameObject.name == CONTAINER_NAME)
            {
                detectorsContainer = gameObject;
            }

            if (autoStart && !started)
            {
                StartDetectionAutomatically();
            }
        }

        private void OnEnable()
        {
            if (!started || (!detectionEventHasListener && detectionAction == null))
                return;
            ResumeDetector();
        }

        private void OnDisable()
        {
            if (!started) return;
            PauseDetector();
        }

        private void OnApplicationQuit()
        {
            DisposeInternal();
        }

        protected virtual void OnDestroy()
        {
            StopDetectionInternal();

            if (transform.childCount == 0 && GetComponentsInChildren<Component>().Length <= 2)
            {
                Destroy(gameObject);
            }
            else if (name == CONTAINER_NAME && GetComponentsInChildren<EkumeDetectorBase>().Length <= 1)
            {
                Destroy(gameObject);
            }
        }
        #endregion

        protected virtual bool Init(EkumeDetectorBase instance, string detectorName)
        {
            if (instance != null && instance != this && instance.keepAlive)
            {
                Debug.LogWarning("[Ekume]: self-destroying, other instance already exists & only one instance allowed!", gameObject);
                Destroy(this);
                return false;
            }

            DontDestroyOnLoad(gameObject);
            return true;
        }

        protected virtual void DisposeInternal()
        {
            Destroy(this);
        }

        internal virtual void OnCheatingDetected()
        {
            if (detectionAction != null) detectionAction();
            if (detectionEventHasListener) detectionEvent.Invoke();

            if (autoDispose)
            {
                DisposeInternal();
            }
            else
            {
                StopDetectionInternal();
            }
        }

        protected abstract void StartDetectionAutomatically();
        protected abstract void StopDetectionInternal();
        protected abstract void PauseDetector();
        protected abstract void ResumeDetector();
    }
}


//MORE DETECTORS

/// <summary>
/// Detects cheating of any Obscured type (except \link ObscuredTypes.EkumeSecureData EkumeSecureData\endlink, it has own detection features) used in project.
/// </summary>
/// It allows cheaters to find desired (fake) values in memory and change them, keeping original values secure.<br/>
/// It's like a cheese in the mouse trap - cheater tries to change some obscured value and get caught on it.
/// 
/// Just add it to any GameObject as usual or through the "GameObject > Create Other > Code Stage > Anti-Cheat Toolkit" 
/// menu to get started.<br/>
/// You can use detector completely from inspector without writing any code except the actual reaction on cheating.
/// 
/// Avoid using detectors from code at the Awake phase.

public class EkumeDataCheatingDetector : ActDetectorBase
{
    internal const string COMPONENT_NAME = "Obscured Cheating Detector";
    internal const string FINAL_LOG_PREFIX = "[Ekume] " + COMPONENT_NAME + ": ";

    private static int instancesInScene;

    #region public fields
    /// <summary>
    /// Max allowed difference between encrypted and fake values in \link ObscuredTypes.ObscuredFloat ObscuredFloat\endlink. Increase in case of false positives.
    /// </summary>
    [Tooltip("Max allowed difference between encrypted and fake values in ObscuredFloat. Increase in case of false positives.")]
    public float floatEpsilon = 0.0001f;

    /// <summary>
    /// Max allowed difference between encrypted and fake values in \link ObscuredTypes.ObscuredVector2 ObscuredVector2\endlink. Increase in case of false positives.
    /// </summary>
    [Tooltip("Max allowed difference between encrypted and fake values in ObscuredVector2. Increase in case of false positives.")]
    public float vector2Epsilon = 0.1f;

    /// <summary>
    /// Max allowed difference between encrypted and fake values in \link ObscuredTypes.ObscuredVector3 ObscuredVector3\endlink. Increase in case of false positives.
    /// </summary>
    [Tooltip("Max allowed difference between encrypted and fake values in ObscuredVector3. Increase in case of false positives.")]
    public float vector3Epsilon = 0.1f;

    /// <summary>
    /// Max allowed difference between encrypted and fake values in \link ObscuredTypes.ObscuredQuaternion ObscuredQuaternion\endlink. Increase in case of false positives.
    /// </summary>
    [Tooltip("Max allowed difference between encrypted and fake values in ObscuredQuaternion. Increase in case of false positives.")]
    public float quaternionEpsilon = 0.1f;
    #endregion

    #region public static methods
    /// <summary>
    /// Starts all Obscured types cheating detection.
    /// </summary>
    /// Make sure you have properly configured detector in scene with #autoStart disabled before using this method.
    public static void StartDetection()
    {
        if (Instance != null)
        {
            Instance.StartDetectionInternal(null);
        }
        else
        {
            Debug.LogError(FINAL_LOG_PREFIX + "can't be started since it doesn't exists in scene or not yet initialized!");
        }
    }

    /// <summary>
    /// Starts all Obscured types cheating detection with specified callback.
    /// </summary>
    /// If you have detector in scene make sure it has empty Detection Event.<br/>
    /// Creates a new detector instance if it doesn't exists in scene.
    /// <param name="callback">Method to call after detection.</param>
    public static void StartDetection(UnityAction callback)
    {
        GetOrCreateInstance.StartDetectionInternal(callback);
    }

    /// <summary>
    /// Stops detector. Detector's component remains in the scene. Use Dispose() to completely remove detector.
    /// </summary>
    public static void StopDetection()
    {
        if (Instance != null) Instance.StopDetectionInternal();
    }

    /// <summary>
    /// Stops and completely disposes detector component.
    /// </summary>
    /// On dispose Detector follows 2 rules:
    /// - if Game Object's name is "Anticheat Ekume Detectors": it will be automatically 
    /// destroyed if no other Detectors left attached regardless of any other components or children;<br/>
    /// - if Game Object's name is NOT "Anticheat Ekume Detectors": it will be automatically destroyed only
    /// if it has neither other components nor children attached;
    public static void Dispose()
    {
        if (Instance != null) Instance.DisposeInternal();
    }
    #endregion

    #region static instance
    /// <summary>
    /// Allows reaching public properties from code. Can be null.
    /// </summary>
    public static EkumeDataCheatingDetector Instance { get; private set; }

    private static EkumeDataCheatingDetector GetOrCreateInstance
    {
        get
        {
            if (Instance != null) return Instance;

            if (detectorsContainer == null)
            {
                detectorsContainer = new GameObject(CONTAINER_NAME);
            }
            Instance = detectorsContainer.AddComponent<EkumeDataCheatingDetector>();
            return Instance;
        }
    }
    #endregion

    internal static bool IsRunning
    {
        get
        {
            //object.Equals(Instance, null); 
            return ((object)Instance != null) && Instance.isRunning;
        }
    }

    private EkumeDataCheatingDetector() { } // prevents direct instantiation

    #region unity messages
    private void Awake()
    {
        instancesInScene++;
        if (Init(Instance, COMPONENT_NAME))
        {
            Instance = this;
        }

#if UNITY_5_4_PLUS
			SceneManager.sceneLoaded += OnLevelWasLoadedNew;
#endif
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        instancesInScene--;
    }

#if UNITY_5_4_PLUS
		private void OnLevelWasLoadedNew(Scene scene, LoadSceneMode mode)
		{
			OnLevelLoadedCallback();
		}
#else
    private void OnLevelWasLoaded()
    {
        OnLevelLoadedCallback();
    }
#endif

    private void OnLevelLoadedCallback()
    {
        if (instancesInScene < 2)
        {
            if (!keepAlive)
            {
                DisposeInternal();
            }
        }
        else
        {
            if (!keepAlive && Instance != this)
            {
                DisposeInternal();
            }
        }
    }
    #endregion

    private void StartDetectionInternal(UnityAction callback)
    {
        if (isRunning)
        {
            Debug.LogWarning(FINAL_LOG_PREFIX + "already running!", this);
            return;
        }

        if (!enabled)
        {
            Debug.LogWarning(FINAL_LOG_PREFIX + "disabled but StartDetection still called from somewhere (see stack trace for this message)!", this);
            return;
        }

        if (callback != null && detectionEventHasListener)
        {
            Debug.LogWarning(FINAL_LOG_PREFIX + "has properly configured Detection Event in the inspector, but still get started with Action callback. Both Action and Detection Event will be called on detection. Are you sure you wish to do this?", this);
        }

        if (callback == null && !detectionEventHasListener)
        {
            Debug.LogWarning(FINAL_LOG_PREFIX + "was started without any callbacks. Please configure Detection Event in the inspector, or pass the callback Action to the StartDetection method.", this);
            enabled = false;
            return;
        }

        detectionAction = callback;
        started = true;
        isRunning = true;
    }

    protected override void StartDetectionAutomatically()
    {
        StartDetectionInternal(null);
    }

    protected override void PauseDetector()
    {
        isRunning = false;
    }

    protected override void ResumeDetector()
    {
        if (detectionAction == null && !detectionEventHasListener) return;
        isRunning = true;
    }

    protected override void StopDetectionInternal()
    {
        if (!started)
            return;

        detectionAction = null;
        started = false;
        isRunning = false;
    }

    protected override void DisposeInternal()
    {
        base.DisposeInternal();
        if (Instance == this) Instance = null;
    }
}

/*
xxHashSharp - A pure C# implementation of xxhash
Copyright (C) 2014, Seok-Ju, Yun. (https://github.com/noricube/xxHashSharp)
Original C Implementation Copyright (C) 2012-2014, Yann Collet. (https://code.google.com/p/xxhash/)
Specific optimization and inlining by Ekume Games
BSD 2-Clause License (http://www.opensource.org/licenses/bsd-license.php)

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.
    * Redistributions in binary form must reproduce the above
      copyright notice, this list of conditions and the following
      disclaimer in the documentation and/or other materials provided
      with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

internal class xxHash
{
    private const uint PRIME32_1 = 2654435761U;
    private const uint PRIME32_2 = 2246822519U;
    private const uint PRIME32_3 = 3266489917U;
    private const uint PRIME32_4 = 668265263U;
    private const uint PRIME32_5 = 374761393U;

    public static uint CalculateHash(byte[] buf, int len, uint seed)
    {
        uint h32;
        int index = 0;

        if (len >= 16)
        {
            int limit = len - 16;
            uint v1 = seed + PRIME32_1 + PRIME32_2;
            uint v2 = seed + PRIME32_2;
            uint v3 = seed;
            uint v4 = seed - PRIME32_1;

            do
            {
                uint read_value = (uint)(buf[index++] | buf[index++] << 8 | buf[index++] << 16 | buf[index++] << 24);
                v1 += read_value * PRIME32_2;
                v1 = (v1 << 13) | (v1 >> 19);
                v1 *= PRIME32_1;

                read_value = (uint)(buf[index++] | buf[index++] << 8 | buf[index++] << 16 | buf[index++] << 24);
                v2 += read_value * PRIME32_2;
                v2 = (v2 << 13) | (v2 >> 19);
                v2 *= PRIME32_1;

                read_value = (uint)(buf[index++] | buf[index++] << 8 | buf[index++] << 16 | buf[index++] << 24);
                v3 += read_value * PRIME32_2;
                v3 = (v3 << 13) | (v3 >> 19);
                v3 *= PRIME32_1;

                read_value = (uint)(buf[index++] | buf[index++] << 8 | buf[index++] << 16 | buf[index++] << 24);
                v4 += read_value * PRIME32_2;
                v4 = (v4 << 13) | (v4 >> 19);
                v4 *= PRIME32_1;

            } while (index <= limit);

            h32 = ((v1 << 1) | (v1 >> 31)) + ((v2 << 7) | (v2 >> 25)) + ((v3 << 12) | (v3 >> 20)) + ((v4 << 18) | (v4 >> 14));
        }
        else
        {
            h32 = seed + PRIME32_5;
        }

        h32 += (uint)len;

        while (index <= len - 4)
        {
            h32 += (uint)(buf[index++] | buf[index++] << 8 | buf[index++] << 16 | buf[index++] << 24) * PRIME32_3;
            h32 = ((h32 << 17) | (h32 >> 15)) * PRIME32_4;
        }

        while (index < len)
        {
            h32 += buf[index] * PRIME32_5;
            h32 = ((h32 << 11) | (h32 >> 21)) * PRIME32_1;
            index++;
        }

        h32 ^= h32 >> 15;
        h32 *= PRIME32_2;
        h32 ^= h32 >> 13;
        h32 *= PRIME32_3;
        h32 ^= h32 >> 16;

        return h32;
    }
}


