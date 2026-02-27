using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RouteManager : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(EsperarYDuplicar());
    }

    private IEnumerator EsperarYDuplicar()
    {
        GameObject mapaOriginal;

        // Espera hasta que exista el objeto "Map" y tenga hijos útiles
        do
        {
            mapaOriginal = GameObject.Find("Map");
            yield return null;
        }
        while (mapaOriginal == null || mapaOriginal.transform.childCount <= 1);

        yield return null;

        GameObject mapaCopia = new GameObject("MapModificable");

        foreach (Transform hijo in mapaOriginal.transform)
        {
            if (hijo.name == "TileProvider")
                continue;

            MeshFilter mfOriginal = hijo.GetComponent<MeshFilter>();
            MeshRenderer mrOriginal = hijo.GetComponent<MeshRenderer>();

            if (mfOriginal != null && mrOriginal != null && mfOriginal.sharedMesh != null)
            {
                Mesh meshBase = Instantiate(mfOriginal.sharedMesh);
                Mesh meshCopia = MeshSubdivide(meshBase, 3);

                GameObject copia = new GameObject(hijo.name + "_Modificable");
                var paintData = copia.AddComponent<MeshPaintData>();
                paintData.Initialize(meshCopia);

                Vector3 posicionElevada = hijo.position;
                posicionElevada.y += 0.5f;
                copia.transform.SetPositionAndRotation(posicionElevada, hijo.rotation);
                copia.transform.localScale = hijo.localScale;
                copia.transform.parent = mapaCopia.transform;
                copia.AddComponent<MeshCollider>().sharedMesh = meshCopia;
                copia.layer = LayerMask.NameToLayer("MallaModificable");

                MeshFilter mfNuevo = copia.AddComponent<MeshFilter>();
                mfNuevo.mesh = meshCopia;

                MeshRenderer mrNuevo = copia.AddComponent<MeshRenderer>();
                Material mat = new Material(Shader.Find("Custom/VertexColorShader"));
                mrNuevo.material = mat;
                mrNuevo.enabled = true;

                Color[] colores = new Color[meshCopia.vertexCount];
                for (int i = 0; i < colores.Length; i++)
                    colores[i] = new Color(1f, 0f, 0f, 0f);

                meshCopia.colors = colores;
            }
        }
    }

    // Subdivide la malla varias veces
    private Mesh MeshSubdivide(Mesh originalMesh, int subdivisiones = 1)
    {
        Mesh m = originalMesh;
        for (int s = 0; s < subdivisiones; s++)
            m = SubdivideOnce(m);
        return m;
    }

    // Subdivide la malla una sola vez
    private Mesh SubdivideOnce(Mesh mesh)
    {
        Vector3[] oldVertices = mesh.vertices;
        int[] oldTriangles = mesh.triangles;
        Color[] oldColors = mesh.colors.Length == oldVertices.Length ? mesh.colors : new Color[oldVertices.Length];
        Vector2[] oldUVs = mesh.uv.Length == oldVertices.Length ? mesh.uv : new Vector2[oldVertices.Length];

        List<Vector3> newVertices = new();
        List<Color> newColors = new();
        List<Vector2> newUVs = new();
        List<int> newTriangles = new();

        Dictionary<Vector3, int> vertexMap = new();

        int GetOrAddVertex(Vector3 pos, Color color, Vector2 uv)
        {
            if (vertexMap.TryGetValue(pos, out int index))
                return index;

            index = newVertices.Count;
            newVertices.Add(pos);
            newColors.Add(color);
            newUVs.Add(uv);
            vertexMap[pos] = index;
            return index;
        }

        for (int i = 0; i < oldTriangles.Length; i += 3)
        {
            int i0 = oldTriangles[i];
            int i1 = oldTriangles[i + 1];
            int i2 = oldTriangles[i + 2];

            Vector3 v0 = oldVertices[i0];
            Vector3 v1 = oldVertices[i1];
            Vector3 v2 = oldVertices[i2];

            Vector3 m0 = (v0 + v1) * 0.5f;
            Vector3 m1 = (v1 + v2) * 0.5f;
            Vector3 m2 = (v2 + v0) * 0.5f;

            Color c0 = oldColors[i0];
            Color c1 = oldColors[i1];
            Color c2 = oldColors[i2];

            Color cm0 = (c0 + c1) * 0.5f;
            Color cm1 = (c1 + c2) * 0.5f;
            Color cm2 = (c2 + c0) * 0.5f;

            Vector2 uv0 = oldUVs[i0];
            Vector2 uv1 = oldUVs[i1];
            Vector2 uv2 = oldUVs[i2];

            Vector2 mu0 = (uv0 + uv1) * 0.5f;
            Vector2 mu1 = (uv1 + uv2) * 0.5f;
            Vector2 mu2 = (uv2 + uv0) * 0.5f;

            int vi0 = GetOrAddVertex(v0, c0, uv0);
            int vi1 = GetOrAddVertex(v1, c1, uv1);
            int vi2 = GetOrAddVertex(v2, c2, uv2);
            int mi0 = GetOrAddVertex(m0, cm0, mu0);
            int mi1 = GetOrAddVertex(m1, cm1, mu1);
            int mi2 = GetOrAddVertex(m2, cm2, mu2);

            newTriangles.AddRange(new[] { vi0, mi0, mi2 });
            newTriangles.AddRange(new[] { mi0, vi1, mi1 });
            newTriangles.AddRange(new[] { mi2, mi1, vi2 });
            newTriangles.AddRange(new[] { mi0, mi1, mi2 });
        }

        Mesh subdividida = new Mesh
        {
            vertices = newVertices.ToArray(),
            triangles = newTriangles.ToArray(),
            colors = newColors.ToArray(),
            uv = newUVs.ToArray()
        };
        subdividida.RecalculateNormals();
        subdividida.RecalculateBounds();

        return subdividida;
    }
}
