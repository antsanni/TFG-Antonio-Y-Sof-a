using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class FixWhiteWolf
{
    static FixWhiteWolf()
    {
        EditorApplication.delayCall += AssignTextures;
    }

    static void AssignTextures()
    {
        if (EditorPrefs.GetBool("WhiteWolfFixed", false)) return;

        Debug.Log("Arreglando texturas en blanco del lobo...");

        // Paths of materials
        string matPathWolf = "Assets/Wolf/LRP(Built-in)/Wolf/Materials/Wolf_LRP.mat";
        string matPathFur = "Assets/Wolf/LRP(Built-in)/Wolf/Materials/FurWolf_LRP.mat";
        string matPathHair = "Assets/Wolf/LRP(Built-in)/Wolf/Materials/HairWolf_LRP.mat";

        // Paths of textures
        string texPathWolf = "Assets/Wolf/Textures/Wolf_Textures/wolf_AlbedoAlpha.png";
        string texPathFur = "Assets/Wolf/Textures/Wolf_Textures/WolfFur2_AlbedoAlpha.png";
        string texPathHair = "Assets/Wolf/Textures/Wolf_Textures/WolfFiber_AlphaAlbedo.png";

        // Load Textures
        Texture2D texWolf = AssetDatabase.LoadAssetAtPath<Texture2D>(texPathWolf);
        Texture2D texFur = AssetDatabase.LoadAssetAtPath<Texture2D>(texPathFur);
        Texture2D texHair = AssetDatabase.LoadAssetAtPath<Texture2D>(texPathHair);

        // Assign to Materials
        Material matWolf = AssetDatabase.LoadAssetAtPath<Material>(matPathWolf);
        if (matWolf != null && texWolf != null)
        {
            matWolf.shader = Shader.Find("Standard");
            matWolf.SetTexture("_MainTex", texWolf);
            EditorUtility.SetDirty(matWolf);
        }

        Material matFur = AssetDatabase.LoadAssetAtPath<Material>(matPathFur);
        if (matFur != null && texFur != null)
        {
            matFur.shader = Shader.Find("Standard");
            matFur.SetTexture("_MainTex", texFur);
            EditorUtility.SetDirty(matFur);
        }

        Material matHair = AssetDatabase.LoadAssetAtPath<Material>(matPathHair);
        if (matHair != null && texHair != null)
        {
            matHair.shader = Shader.Find("Standard");
            matHair.SetTexture("_MainTex", texHair);
            EditorUtility.SetDirty(matHair);
        }

        AssetDatabase.SaveAssets();
        EditorPrefs.SetBool("WhiteWolfFixed", true);
        Debug.Log("¡Texturas del lobo reasignadas correctamente!");
    }
}
