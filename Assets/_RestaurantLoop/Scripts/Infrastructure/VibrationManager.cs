using UnityEngine;

namespace RestaurantLoop.Infrastructure
{
    public enum VibrationPreset
    {
        None,
        Light,
        Soft,
        Medium,
        Heavy,
        Failure
    }

    /// <summary>
    /// Persistent, zero-dependency haptic service. Android Java objects, vibration effects, waveforms,
    /// and invocation arguments are cached during initialization so PlayPreset creates no managed garbage.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-900)]
    public sealed class VibrationManager : MonoBehaviour
    {
        private const string VibrationEnabledPreferenceKey = "VibrationEnabled";
        private const string LegacyVibrationPreferenceKey = "Vibration";

        [Header("Event Presets")]
        [SerializeField] private VibrationPreset winVibration = VibrationPreset.Heavy;
        [SerializeField] private VibrationPreset loseVibration = VibrationPreset.Failure;
        [SerializeField] private VibrationPreset itemTapVibration = VibrationPreset.Light;
        [SerializeField] private VibrationPreset timedWarningVibration = VibrationPreset.Soft;

        [Header("Startup")]
        [SerializeField] private bool vibrationEnabledByDefault = true;

        public static VibrationManager Instance { get; private set; }
        public bool IsVibrationEnabled { get; private set; }

#if UNITY_ANDROID
        private const int AndroidOreoApiLevel = 26;
        private static readonly long[] FailurePattern = { 0L, 50L, 40L, 80L };
        private static readonly int[] FailureAmplitudes = { 0, 190, 0, 255 };

        private AndroidJavaObject vibrator;
        private AndroidJavaObject[] cachedEffects;
        private jvalue[][] cachedEffectArguments;
        private jvalue[][] cachedLegacyArguments;
        private jvalue[] cachedFailureLegacyArguments;
        private System.IntPtr vibrationEffectMethodId;
        private System.IntPtr vibrationDurationMethodId;
        private System.IntPtr vibrationPatternMethodId;
        private System.IntPtr failurePatternGlobalReference;
        private bool nativeVibrationAvailable;
        private bool usesVibrationEffects;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (Instance != null) return;
            GameObject serviceObject = new GameObject(nameof(VibrationManager));
            serviceObject.AddComponent<VibrationManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            IsVibrationEnabled = LoadVibrationPreference();

#if UNITY_ANDROID
            if (Application.platform == RuntimePlatform.Android) InitializeAndroidVibrator();
#endif
        }

        public void SetVibrationEnabled(bool enabled)
        {
            IsVibrationEnabled = enabled;
            PlayerPrefs.SetInt(VibrationEnabledPreferenceKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        private bool LoadVibrationPreference()
        {
            if (PlayerPrefs.HasKey(VibrationEnabledPreferenceKey))
            {
                return PlayerPrefs.GetInt(VibrationEnabledPreferenceKey) != 0;
            }

            bool enabled = PlayerPrefs.HasKey(LegacyVibrationPreferenceKey)
                ? PlayerPrefs.GetFloat(LegacyVibrationPreferenceKey) > 0.5f
                : vibrationEnabledByDefault;
            PlayerPrefs.SetInt(VibrationEnabledPreferenceKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            return enabled;
        }

        public void PlayWinVibration() => PlayPreset(winVibration);

        public void PlayLoseVibration() => PlayPreset(loseVibration);

        public void PlayTapVibration() => PlayPreset(itemTapVibration);

        public void PlayTimedWarningVibration() => PlayPreset(timedWarningVibration);

        public void PlayPreset(VibrationPreset preset)
        {
            if (!IsVibrationEnabled || preset == VibrationPreset.None) return;

#if UNITY_ANDROID
            if (nativeVibrationAvailable)
            {
                int index = (int)preset;
                if (usesVibrationEffects)
                {
                    AndroidJNI.CallVoidMethod(
                        vibrator.GetRawObject(), vibrationEffectMethodId, cachedEffectArguments[index]);
                }
                else if (preset == VibrationPreset.Failure)
                {
                    AndroidJNI.CallVoidMethod(
                        vibrator.GetRawObject(), vibrationPatternMethodId, cachedFailureLegacyArguments);
                }
                else
                {
                    AndroidJNI.CallVoidMethod(
                        vibrator.GetRawObject(), vibrationDurationMethodId, cachedLegacyArguments[index]);
                }

                return;
            }
#endif

            Handheld.Vibrate();
        }

#if UNITY_ANDROID
        private void InitializeAndroidVibrator()
        {
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity =
                       unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }

                nativeVibrationAvailable = vibrator != null && vibrator.Call<bool>("hasVibrator");
                if (!nativeVibrationAvailable) return;

                int apiLevel;
                using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    apiLevel = version.GetStatic<int>("SDK_INT");
                }

                usesVibrationEffects = apiLevel >= AndroidOreoApiLevel;
                if (usesVibrationEffects) CacheVibrationEffects();
                else CacheLegacyArguments();
            }
            catch (AndroidJavaException exception)
            {
                nativeVibrationAvailable = false;
                Debug.LogWarning($"Android vibration initialization failed; using platform fallback. {exception.Message}");
            }
        }

        private void CacheVibrationEffects()
        {
            int presetCount = System.Enum.GetValues(typeof(VibrationPreset)).Length;
            cachedEffects = new AndroidJavaObject[presetCount];
            cachedEffectArguments = new jvalue[presetCount][];

            using (AndroidJavaClass effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
            {
                cachedEffects[(int)VibrationPreset.Light] =
                    effectClass.CallStatic<AndroidJavaObject>("createOneShot", 12L, 55);
                cachedEffects[(int)VibrationPreset.Soft] =
                    effectClass.CallStatic<AndroidJavaObject>("createOneShot", 20L, 90);
                cachedEffects[(int)VibrationPreset.Medium] =
                    effectClass.CallStatic<AndroidJavaObject>("createOneShot", 35L, 150);
                cachedEffects[(int)VibrationPreset.Heavy] =
                    effectClass.CallStatic<AndroidJavaObject>("createOneShot", 60L, 230);
                cachedEffects[(int)VibrationPreset.Failure] =
                    effectClass.CallStatic<AndroidJavaObject>(
                        "createWaveform", FailurePattern, FailureAmplitudes, -1);
            }

            vibrationEffectMethodId = AndroidJNI.GetMethodID(
                vibrator.GetRawClass(), "vibrate", "(Landroid/os/VibrationEffect;)V");
            for (int i = 1; i < presetCount; i++)
            {
                cachedEffectArguments[i] = new[]
                {
                    new jvalue { l = cachedEffects[i].GetRawObject() }
                };
            }
        }

        private void CacheLegacyArguments()
        {
            int presetCount = System.Enum.GetValues(typeof(VibrationPreset)).Length;
            cachedLegacyArguments = new jvalue[presetCount][];
            cachedLegacyArguments[(int)VibrationPreset.Light] = CreateDurationArgument(12L);
            cachedLegacyArguments[(int)VibrationPreset.Soft] = CreateDurationArgument(20L);
            cachedLegacyArguments[(int)VibrationPreset.Medium] = CreateDurationArgument(35L);
            cachedLegacyArguments[(int)VibrationPreset.Heavy] = CreateDurationArgument(60L);

            vibrationDurationMethodId = AndroidJNI.GetMethodID(vibrator.GetRawClass(), "vibrate", "(J)V");
            vibrationPatternMethodId = AndroidJNI.GetMethodID(vibrator.GetRawClass(), "vibrate", "([JI)V");
            System.IntPtr localPatternReference = AndroidJNI.ToLongArray(FailurePattern);
            failurePatternGlobalReference = AndroidJNI.NewGlobalRef(localPatternReference);
            AndroidJNI.DeleteLocalRef(localPatternReference);
            cachedFailureLegacyArguments = new[]
            {
                new jvalue { l = failurePatternGlobalReference },
                new jvalue { i = -1 }
            };
        }

        private static jvalue[] CreateDurationArgument(long duration)
        {
            return new[] { new jvalue { j = duration } };
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            if (cachedEffects != null)
            {
                for (int i = 0; i < cachedEffects.Length; i++) cachedEffects[i]?.Dispose();
            }

            if (failurePatternGlobalReference != System.IntPtr.Zero)
            {
                AndroidJNI.DeleteGlobalRef(failurePatternGlobalReference);
                failurePatternGlobalReference = System.IntPtr.Zero;
            }

            vibrator?.Dispose();
            Instance = null;
        }
#else
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
#endif
    }
}
