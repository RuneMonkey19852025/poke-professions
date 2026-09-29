using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace PokeProfessions.EditorTools
{
    /// <summary>
    /// One-time scene bootstrap. Runs automatically when the project is first opened:
    /// builds Assets/Scenes/GameScene.unity containing a single GameObject with
    /// GameBootstrap on it. Everything else in the game is created procedurally
    /// at runtime, so this guarantees the project never opens with an empty scene.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorSceneBootstrap
    {
        private const string ScenePath = "Assets/Scenes/GameScene.unity";
        private const string MarkerFile = "Library/.pokeprofessions_bootstrapped";

        static EditorSceneBootstrap()
        {
            // Defer so this never runs mid-import.
            EditorApplication.delayCall += Bootstrap;
        }

        private static void Bootstrap()
        {
            if (File.Exists(MarkerFile)) return;
            if (SceneIsAlreadySetUp()) { MarkDone(); return; }

            try
            {
                if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                    AssetDatabase.CreateFolder("Assets", "Scenes");

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var go = new GameObject("GameBootstrap");
                var cmp = go.AddComponent<GameBootstrap>();
                Undo.RegisterCreatedObjectUndo(go, "Create GameBootstrap");

                // Create the script component reference via SerializedObject so the
                // scene file references the correct script GUID after import.
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorApplication.delayCall += () =>
                {
                    AssetDatabase.SaveAssets();
                    MarkDone();
                    Debug.Log("[PokeProfessions] Scene bootstrapped at " + ScenePath +
                              ". Open it and press Play. You can delete Assets/Editor/EditorSceneBootstrap.cs afterwards if you like.");
                };
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PokeProfessions] Scene bootstrap failed: " + e.Message +
                               "\nIf this persists, create an empty scene, add a GameObject named 'GameBootstrap' with the GameBootstrap script, and save it as Assets/Scenes/GameScene.unity");
            }
        }

        private static bool SceneIsAlreadySetUp()
        {
            if (!File.Exists(ScenePath)) return false;
            // If the default SampleScene is not loaded (or ours is), treat as done.
            var active = EditorSceneManager.GetActiveScene();
            return active.path == ScenePath || string.IsNullOrEmpty(active.path);
        }

        private static void MarkDone()
        {
            Directory.CreateDirectory("Library");
            File.WriteAllText(MarkerFile, "ok");
        }
    }
}
