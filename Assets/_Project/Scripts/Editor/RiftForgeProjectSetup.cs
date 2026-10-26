using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RiftForge.Editor
{
    /// <summary>Rebuilds the playable entry scene without reverting to the retired movement-only foundation.</summary>
    public static class RiftForgeProjectSetup
    {
        private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";

        [MenuItem("Rift Forge/Rebuild Playable Main Scene")]
        public static void CreatePlayableMainScene()
        {
            if (!EditorUtility.DisplayDialog("Rebuild Rift Forge Main Scene",
                    "Replace the current Main.unity scene with the procedural game entry?", "Rebuild", "Cancel"))
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 14f, -14f);
            cameraObject.transform.LookAt(Vector3.zero);
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            new GameObject("Rift Forge Game").AddComponent<RiftGame>();
            EditorSceneManager.SaveScene(scene, MainScenePath);
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(MainScenePath, true) };
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("RIFT_FORGE_PLAYABLE_SCENE_REBUILT");
        }
    }
}
