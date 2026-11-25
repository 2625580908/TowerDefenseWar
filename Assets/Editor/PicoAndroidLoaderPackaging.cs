using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using Unity.XR.PXR;

namespace MRStrategyRTS.Editor
{
    public sealed class PicoAndroidLoaderPackaging : IPostGenerateGradleAndroidProject
    {
        private const string Loader = "LoaderForUnitySDK_1_1_0.aar";
        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var xr = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            var loaders = xr?.AssignedSettings?.activeLoaders;
            if (loaders == null || loaders.Count != 1 || !(loaders[0] is PXR_Loader))
                return;

            // PxrPlatform loads this AAR's native library even when Unity OpenXR is not the active provider.
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.xr.picoxr/package.json");
            var source = package == null ? null : Path.Combine(package.resolvedPath, "Runtime", "Android", Loader);
            if (!File.Exists(source))
                throw new BuildFailedException("找不到 PICO SDK 自带的运行库：" + Loader);

            var libs = Path.Combine(path, "libs");
            Directory.CreateDirectory(libs);
            File.Copy(source, Path.Combine(libs, Loader), true);

            var gradle = Path.Combine(path, "build.gradle");
            var text = File.ReadAllText(gradle);
            if (!text.Contains(Path.GetFileNameWithoutExtension(Loader)))
                File.AppendAllText(gradle, "\ndependencies { implementation files('libs/" + Loader + "') }\n");
        }
    }
}
