using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class AssignWolfPrefab
{
    static AssignWolfPrefab()
    {
        EditorApplication.delayCall += AssignWolf;
    }

    static void AssignWolf()
    {
        if (EditorPrefs.GetBool("WolfAssigned", false)) return;

        MissingPerson mp = Object.FindObjectOfType<MissingPerson>();
        if (mp != null)
        {
            string prefabPath = "Assets/Wolf/LRP(Built-in)/Wolf/Prefab/Wolf_LRP_Animal.prefab";
            GameObject wolfPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            if (wolfPrefab != null)
            {
                mp.prefabToPlace = wolfPrefab;
                EditorUtility.SetDirty(mp);
                
                // Save the scene if possible
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(currentScene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(currentScene);
                
                Debug.Log("¡El prefab del Lobo ha sido asignado correctamente al script MissingPerson!");
                EditorPrefs.SetBool("WolfAssigned", true);
            }
            else
            {
                Debug.LogError("No se encontró el prefab del lobo en la ruta esperada.");
            }
        }
    }
}
