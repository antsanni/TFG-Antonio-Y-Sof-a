using UnityEngine;

public class MeshPaintData : MonoBehaviour
{
    public float[] intensity;
    public Mesh mesh;
    public Color[] colors;
    public bool[] wasPainted;

    // Inicializa buffers de pintura para un mesh dado
    public void Initialize(Mesh m)
    {
        mesh = m;
        int n = m.vertexCount;
        wasPainted = new bool[n];
        intensity = new float[n];
        colors = new Color[n];

        for (int i = 0; i < n; i++)
        {
            intensity[i] = 0f;
            colors[i] = Color.clear;
        }
        mesh.colors = colors;
    }

    // Actualiza los colores aplicando una función gradiente
    public void UpdateColors(System.Func<float, int, Color> gradientFunc)
    {
        for (int i = 0; i < intensity.Length; i++)
        {
            colors[i] = gradientFunc(intensity[i], i);
        }
        mesh.colors = colors;
    }
}