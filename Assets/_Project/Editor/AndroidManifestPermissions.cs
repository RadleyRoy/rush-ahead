#if UNITY_ANDROID // the Android build module, and so this interface, only exists where Android builds are possible
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace RushAhead.EditorTools
{
    /// <summary>
    /// Adds the permissions LAN room discovery needs to the generated Android manifest: holding a Wi-Fi multicast lock
    /// (so the driver passes broadcast adverts up to the game) and reading Wi-Fi state. Both are normal permissions,
    /// granted at install with no prompt. Done in code so it survives regenerating the Gradle project.
    /// </summary>
    public sealed class AndroidManifestPermissions : IPostGenerateGradleAndroidProject
    {
        const string AndroidNamespace = "http://schemas.android.com/apk/res/android";

        static readonly string[] Permissions =
        {
            "android.permission.CHANGE_WIFI_MULTICAST_STATE",
            "android.permission.ACCESS_WIFI_STATE",
        };

        public int callbackOrder => 0;

        /// <param name="path">The generated unityLibrary module; its manifest merges into the app's.</param>
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            var document = new XmlDocument();
            document.Load(manifestPath);
            var manifest = document.DocumentElement!;

            foreach (var permission in Permissions)
            {
                if (manifest.SelectSingleNode($"uses-permission[@*[local-name()='name']='{permission}']") != null) continue;
                var element = document.CreateElement("uses-permission");
                element.SetAttribute("name", AndroidNamespace, permission);
                manifest.PrependChild(element);
            }

            document.Save(manifestPath);
        }
    }
}
#endif
