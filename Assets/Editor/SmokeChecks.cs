using UnityEditor;
using UnityEditor.SceneManagement;

namespace Zombio.Editor
{
    public static class SmokeChecks
    {
        // Invoke with -batchmode -executeMethod Zombio.Editor.SmokeChecks.Run --zombio-smoke.
        // Do not pass -quit: the runtime suite exits with its actual result.
        public static void Run()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--zombio-smoke") < 0)
                throw new System.InvalidOperationException("Supply --zombio-smoke to run the runtime suite.");
            ProjectSetup.EnsureProject();
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            EditorApplication.isPlaying = true;
        }
    }
}
