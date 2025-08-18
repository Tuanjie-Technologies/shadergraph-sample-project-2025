using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using System.Linq;

namespace ShaderGraphDemo.DependencyChecker
{
    [InitializeOnLoad]
    public class CinemachineDependencyChecker
    {
        private const string CinemachinePackageId = "com.unity.cinemachine";
        private const string CinemachineVersion = "2.10.3";

        static CinemachineDependencyChecker()
        {
            // Defer the check to ensure the Package Manager client is ready.
            EditorApplication.delayCall += CheckAndInstallCinemachine;
        }

        private static void CheckAndInstallCinemachine()
        {
            // Use Client.List to check for installed packages.
            var listRequest = Client.List(offlineMode: true); // Check local cache first.
            while (!listRequest.IsCompleted) { /* Wait for sync */ }

            if (listRequest.Status == StatusCode.Success)
            {
                if (listRequest.Result.Any(p => p.name == CinemachinePackageId))
                {
                    Debug.Log("Cinemachine is already installed.");
                    return;
                }
            }
            else if (listRequest.Status >= StatusCode.Failure)
            {
                Debug.LogError("Failed to check packages: " + listRequest.Error.message);
                return;
            }

            Debug.Log("Cinemachine not found. Starting installation...");
            InstallCinemachine();
        }

        private static void InstallCinemachine()
        {
            string packageToAdd = $"{CinemachinePackageId}@{CinemachineVersion}";
            var addRequest = Client.Add(packageToAdd);
            while (!addRequest.IsCompleted) { /* Wait for sync */ }

            if (addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"Successfully installed Cinemachine: {addRequest.Result.displayName}");
                // Force a script reload to make the new package available to other scripts.
                AssetDatabase.Refresh();
            }
            else if (addRequest.Status >= StatusCode.Failure)
            {
                Debug.LogError($"Failed to install Cinemachine: {addRequest.Error.message}");
            }
        }
    }
}
