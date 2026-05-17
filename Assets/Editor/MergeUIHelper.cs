using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MergeUIHelper
{
    public static void ExecuteMerge()
    {
        Debug.Log("Starting UI Merge into TesteVoz...");

        // Open TesteVoz (Destination)
        Scene mainScene = EditorSceneManager.OpenScene("Assets/Scenes/TesteVoz.unity", OpenSceneMode.Single);

        // Open testeConectaIpAutomatico (Source) additively
        Scene uiScene = EditorSceneManager.OpenScene("Assets/Scenes/testeConectaIpAutomatico.unity", OpenSceneMode.Additive);

        int movedCount = 0;

        // Move UI and Connection manager objects to TesteVoz
        foreach (GameObject go in uiScene.GetRootGameObjects())
        {
            if (go.name == "Canvas" || go.name == "Manual Connector" || go.name == "EventSystem")
            {
                SceneManager.MoveGameObjectToScene(go, mainScene);
                movedCount++;
                Debug.Log($"Moved {go.name} to TesteVoz.");
            }
        }

        // Close the source scene without saving it
        EditorSceneManager.CloseScene(uiScene, true);

        // Deduplicate EventSystem in TesteVoz just in case
        var eventSystems = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
        if (eventSystems.Length > 1)
        {
            for (int i = 1; i < eventSystems.Length; i++)
            {
                GameObject.DestroyImmediate(eventSystems[i].gameObject);
                Debug.Log("Removed duplicated EventSystem.");
            }
        }

        if (movedCount > 0)
        {
            // Save the modified TesteVoz scene
            EditorSceneManager.SaveScene(mainScene);
            Debug.Log("Successfully saved TesteVoz with new UI!");
        }
        else
        {
            Debug.LogError("Failed to find Canvas or Manual Connector in source scene.");
        }

        // Quit Unity batchmode
        EditorApplication.Exit(0);
    }
}
