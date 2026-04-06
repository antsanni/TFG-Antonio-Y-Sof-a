using UnityEngine;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using System.Collections.Generic;

public class DroneMapController : MonoBehaviour
{
    private List<Vector2> zonaXZ = new();
    public float margen = 2f;

    private void Update()
    {
        if (!zonasDefinidas) return;

        // Busca todos los objetos "Drone" de la escena (los que tienen el cono visual)
        Drone[] dronesActivos = FindObjectsOfType<Drone>();

        // Revisa uno por uno si están dentro del polígono para activarles la cámara
        foreach (Drone drone in dronesActivos)
        {
            Vector2 posXZ = new Vector2(drone.transform.position.x, drone.transform.position.z);
            bool dentro = PuntoEnPoligonoConMargen(posXZ, zonaXZ, margen);
            drone.camara_status = dentro;
        }
    }

    private bool zonasDefinidas => zonaXZ.Count >= 3;

    // Define la zona a partir del polígono de ZoneSelector
    public void SetZonaXZ(List<Vector3> topPositions)
    {
        zonaXZ.Clear();
        foreach (var pos in topPositions)
            zonaXZ.Add(new Vector2(pos.x, pos.z));
    }

    // Comprueba si un punto está dentro o cerca del polígono (con margen)
    private bool PuntoEnPoligonoConMargen(Vector2 p, List<Vector2> poligono, float margen)
    {
        int n = poligono.Count;
        bool dentro = false;

        // Comprobar si está dentro del polígono
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 pi = poligono[i];
            Vector2 pj = poligono[j];

            if (((pi.y > p.y) != (pj.y > p.y)) &&
                (p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y) + pi.x))
            {
                dentro = !dentro;
            }
        }

        if (dentro) return true;

        // Comprobar si está fuera pero dentro del margen
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float distancia = DistanciaPuntoASegmento(p, poligono[j], poligono[i]);
            if (distancia < margen)
                return true;
        }

        return false;
    }

    // Calcula la distancia de un punto a un segmento
    private float DistanciaPuntoASegmento(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        Vector2 ap = p - a;
        float t = Vector2.Dot(ap, ab) / ab.sqrMagnitude;
        t = Mathf.Clamp01(t);
        Vector2 proyeccion = a + t * ab;
        return Vector2.Distance(p, proyeccion);
    }
}