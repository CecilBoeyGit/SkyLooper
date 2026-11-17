using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

namespace MiniPlanes.Editor
{
    /// <summary>
    /// Keeps 32-bit libraries out of the APK. The build already targets arm64-v8a only, which strips the
    /// 32-bit native libraries, but Meta's OVRPlugin.aar also ships 32-bit copies of its telemetry libraries
    /// as plain assets (assets/lib/armeabi-v7a), which no ABI filter touches. Meta's store upload then warns
    /// that the app contains 32-bit libraries. This adds armeabi-v7a to the Gradle asset ignore pattern.
    /// </summary>
    public class MiniPlanesGradle64Only : IPostGenerateGradleAndroidProject
    {
        const string Ignore = ":!armeabi-v7a";

        // After Meta's own Gradle processing.
        public int callbackOrder => 10000;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            Patch(Path.Combine(unityLibraryPath, "build.gradle"));
            Patch(Path.Combine(unityLibraryPath, "..", "launcher", "build.gradle"));
        }

        static void Patch(string file)
        {
            if (!File.Exists(file)) return;
            string text = File.ReadAllText(file);
            var pattern = new Regex("(ignoreAssetsPattern\\s*=?\\s*\")([^\"]*)(\")");
            if (!pattern.IsMatch(text))
            {
                Debug.LogWarning("[MiniPlanes] No ignoreAssetsPattern in " + file + "; 32-bit asset libraries may be packaged.");
                return;
            }
            string patched = pattern.Replace(text, m => m.Groups[2].Value.Contains("!armeabi-v7a")
                ? m.Value
                : m.Groups[1].Value + m.Groups[2].Value + Ignore + m.Groups[3].Value);
            if (patched != text) File.WriteAllText(file, patched);
        }
    }
}
