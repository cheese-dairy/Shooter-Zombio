using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zombio.Editor
{
    /// <summary>Reproducible Unity setup. No prefabs, hand wiring, NavMesh bake, or imported art.</summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        static ProjectSetup() { EditorApplication.delayCall += EnsureProject; }

        [MenuItem("Dead Signal/Configure Project")]
        public static void EnsureProject()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            PlayerSettings.companyName = "Cheese Dairy";
            PlayerSettings.productName = "Dead Signal";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.runInBackground = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            // The project supplies legacy movement and mouse axes in InputManager.asset.
            var playerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (playerAssets.Length == 0) throw new InvalidOperationException("Unity has not created PlayerSettings yet. Reopen the project and run Dead Signal/Configure Project.");
            var settings = new SerializedObject(playerAssets[0]);
            var inputHandler = settings.FindProperty("activeInputHandler");
            if (inputHandler != null && inputHandler.intValue != 0)
            {
                inputHandler.intValue = 0;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
            RetainShaders();
            if (AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Resources/GameTuning.asset") == null)
            {
                Directory.CreateDirectory("Assets/Resources");
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameTuning>(), "Assets/Resources/GameTuning.asset");
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
            AssetDatabase.SaveAssets();
        }

        static void RetainShaders()
        {
            var graphicsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphicsAssets.Length == 0) return;
            var graphics = new SerializedObject(graphicsAssets[0]);
            var shaders = graphics.FindProperty("m_AlwaysIncludedShaders");
            if (shaders == null) return;
            foreach (string shaderName in new[] { "Standard", "Sprites/Default", "Unlit/Color", "Hidden/Internal-Colored" })
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null) throw new InvalidOperationException("Missing built-in shader: " + shaderName);
                bool exists = false;
                for (int i = 0; i < shaders.arraySize; i++)
                    if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader) exists = true;
                if (exists) continue;
                int index = shaders.arraySize;
                shaders.InsertArrayElementAtIndex(index);
                shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Dead Signal/Open Main Scene")]
        public static void OpenMain()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        }

        [MenuItem("Dead Signal/Build Linux Player")]
        public static void BuildLinux()
        {
            EnsureProject();
            Directory.CreateDirectory("Builds/Linux");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = "Builds/Linux/DeadSignal.x86_64",
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Player build failed: " + report.summary.result);
        }
    }
}
