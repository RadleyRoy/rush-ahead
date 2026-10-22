using System;
using UnityEngine;

namespace RushAhead.Net
{
    /// <summary>
    /// Android's Wi-Fi driver may drop broadcast packets not addressed to this phone unless an app holds a multicast
    /// lock, so room discovery holds one while it listens. Does nothing on other platforms. Needs the
    /// CHANGE_WIFI_MULTICAST_STATE permission, which the build adds to the manifest.
    /// </summary>
    public sealed class AndroidMulticastLock : IDisposable
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject wifiLock;

        public AndroidMulticastLock()
        {
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var context = activity.Call<AndroidJavaObject>("getApplicationContext");
                using var wifi = context.Call<AndroidJavaObject>("getSystemService", "wifi");
                wifiLock = wifi.Call<AndroidJavaObject>("createMulticastLock", "rushahead-discovery");
                wifiLock.Call("setReferenceCounted", false);
                wifiLock.Call("acquire");
            }
            catch (Exception e)
            {
                // Discovery may still work without it on many phones; joining by address always does.
                Debug.LogWarning($"[RushAhead] Couldn't take the Wi-Fi multicast lock: {e.Message}");
                wifiLock = null;
            }
        }

        public void Dispose()
        {
            if (wifiLock == null) return;
            try
            {
                wifiLock.Call("release");
            }
            catch (Exception)
            {
                // Already gone with the activity.
            }

            wifiLock.Dispose();
            wifiLock = null;
        }
#else
        public void Dispose() { }
#endif
    }
}
