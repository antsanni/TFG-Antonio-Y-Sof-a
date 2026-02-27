using System.Collections.Generic;
using UnityEngine;

public class ZoneProgressManager : MonoBehaviour
{
    [Header("Capas")]
    public LayerMask layerMallaModificable;

    private GameObject wallsGo;
    private readonly HashSet<(Mesh, int)> triDentro = new();
    private readonly HashSet<(Mesh, int)> triVisitados = new();
    private bool listo = false;

    // Inicia el censo de triángulos dentro del polígono
    public void ComenzarConParedes()
    {
        wallsGo = GameObject.Find("PolygonWalls");
        if (wallsGo == null)
        {
            Debug.LogWarning("[Zone] No se encontró 'PolygonWalls' en la escena.");
            return;
        }

        triDentro.Clear();
        triVisitados.Clear();
        CensarTriangulosInicial();
        listo = true;

        Debug.Log($"[Zone] Censo completo. Triángulos en polígono: {triDentro.Count}");
    }

    // Registra un triángulo pintado por el dron
    public void RegistrarTrianguloPintado(Mesh m, int triIndex)
    {
        if (!listo) return;
        var clave = (m, triIndex);
        if (triDentro.Contains(clave))
            triVisitados.Add(clave);
    }

    // Devuelve el porcentaje de triángulos visitados
    public float Porcentaje() => listo && triDentro.Count > 0
                                ? (float)triVisitados.Count / triDentro.Count
                                : 0f;

    // Censa los triángulos dentro del polígono inicial
    private void CensarTriangulosInicial()
    {
        Mesh wallMesh = wallsGo.GetComponent<MeshFilter>().sharedMesh;
        List<Vector3> polyXZ = new();
        foreach (Vector3 v in wallMesh.vertices)
        {
            Vector3 p = wallsGo.transform.TransformPoint(v);
            polyXZ.Add(new Vector3(p.x, 0, p.z));
        }

        foreach (MeshCollider col in FindObjectsOfType<MeshCollider>())
        {
            if ((layerMallaModificable.value & (1 << col.gameObject.layer)) == 0) continue;

            Mesh m = col.sharedMesh;
            int[] tris = m.triangles;
            Vector3[] verts = m.vertices;
            Transform t = col.transform;

            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 c = (t.TransformPoint(verts[tris[i]]) +
                             t.TransformPoint(verts[tris[i + 1]]) +
                             t.TransformPoint(verts[tris[i + 2]])) / 3f;
                c.y = 0;

                if (PuntoEnPoligonoXZ(c, polyXZ))
                    triDentro.Add((m, i));
            }
        }
    }

    // Comprueba si un punto está dentro del polígono 2D
    private bool PuntoEnPoligonoXZ(Vector3 p, List<Vector3> poly)
    {
        bool inside = false;
        int j = poly.Count - 1;
        for (int i = 0; i < poly.Count; j = i++)
        {
            Vector3 pi = poly[i];
            Vector3 pj = poly[j];
            if (((pi.z > p.z) != (pj.z > p.z)) &&
                (p.x < (pj.x - pi.x) * (p.z - pi.z) / (pj.z - pi.z) + pi.x))
                inside = !inside;
        }
        return inside;
    }
}