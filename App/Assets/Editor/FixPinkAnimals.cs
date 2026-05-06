using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class FixPinkAnimals
{
    static FixPinkAnimals()
    {
        EditorApplication.delayCall += RunFix;
    }

    static void RunFix()
    {
        if (EditorPrefs.GetBool("AnimalsFixed", false)) return;

        Debug.Log("Iniciando arreglo automático de animales...");
        
        bool somethingChanged = false;

        // Arreglar Lobo
        string[] wolfGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Wolf" });
        foreach (string guid in wolfGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                Texture baseMap = mat.GetTexture("_BaseColorMap"); // HDRP/URP
                if (baseMap == null) baseMap = mat.GetTexture("_BaseMap");
                if (baseMap == null) baseMap = mat.mainTexture;

                mat.shader = Shader.Find("Standard");
                if (baseMap != null) mat.SetTexture("_MainTex", baseMap);
                EditorUtility.SetDirty(mat);
                somethingChanged = true;
            }
        }

        // Arreglar Perros
        string[] dogGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/RSG_DogsPack" });
        foreach (string guid in dogGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                Texture baseMap = mat.GetTexture("_BaseColorMap");
                if (baseMap == null) baseMap = mat.GetTexture("_BaseMap");
                if (baseMap == null) baseMap = mat.mainTexture;

                mat.shader = Shader.Find("Standard");
                if (baseMap != null) mat.SetTexture("_MainTex", baseMap);
                EditorUtility.SetDirty(mat);
                somethingChanged = true;
            }
        }

        // Renombrar Prefabs para que contengan la palabra "Animal"
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Wolf", "Assets/RSG_DogsPack" });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && !prefab.name.ToLower().Contains("animal"))
            {
                AssetDatabase.RenameAsset(path, prefab.name + "_Animal");
                somethingChanged = true;
            }
        }

        if (somethingChanged)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("¡Animales arreglados y renombrados con éxito!");
        }

        EditorPrefs.SetBool("AnimalsFixed", true);
    }
}
