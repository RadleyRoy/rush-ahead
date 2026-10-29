using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// Short buzzes from the phone: a tap for your own burst, a thump for a bump, a long shudder for a capsize.
    /// Android plays them at the length and strength asked for (Android 8 and later); iPhones only have one buzz, kept
    /// for the big moments. Nothing happens on a desktop, or when vibration is off in Settings.
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static int sdk;
        static bool looked;
#endif

        /// <param name="milliseconds">How long.</param>
        /// <param name="strength">0 to 1.</param>
        public static void Pulse(int milliseconds, float strength)
        {
            if (!Enabled || milliseconds <= 0 || strength <= 0f) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (!looked)
                {
                    looked = true;
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using var version = new AndroidJavaClass("android.os.Build$VERSION");
                    sdk = version.GetStatic<int>("SDK_INT");
                }

                if (vibrator == null)
                {
                    Handheld.Vibrate();
                    return;
                }

                if (sdk >= 26)
                {
                    using var effects = new AndroidJavaClass("android.os.VibrationEffect");
                    using var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, Mathf.Clamp(Mathf.RoundToInt(strength * 255f), 1, 255));
                    vibrator.Call("vibrate", effect);
                }
                else
                {
                    vibrator.Call("vibrate", (long)milliseconds);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RushAhead] Vibration failed: {e.Message}");
                Enabled = false;
            }
#elif UNITY_IOS && !UNITY_EDITOR
            if (milliseconds >= 120) Handheld.Vibrate(); // iPhones have one buzz: save it for the big moments
#endif
        }
    }
}
