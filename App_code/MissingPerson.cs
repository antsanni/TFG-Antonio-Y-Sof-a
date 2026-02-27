using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Mapbox.Unity.Map;
using Mapbox.Utils;

public class MissingPerson : MonoBehaviour
{
    [Header("Refs")]
    public LineRenderer line;
    public Toggle placeToggle;
    public GameObject prefabToPlace;
    public Camera cam;
    public LayerMask hitMask;
    public float maxRayDistance = 500f;

    [Header("Mapbox")]
    public AbstractMap map;

    [Header("UI")]
    public TMP_Text coordsText;

    private Vector3[] verts;
    private GameObject currentPlaced;

    private void Awake()
    {
        if (!cam) cam = Camera.main;
    }

    private void Update()
    {
        if (placeToggle == null || !placeToggle.isOn) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (Input.GetMouseButtonDown(0) &&
            Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, maxRayDistance, hitMask))
        {
            int n = line.positionCount;
            if (verts == null || verts.Length != n) verts = new Vector3[n];
            line.GetPositions(verts);

            if (PointInPolygonXZ(hit.point, verts))
            {
                if (currentPlaced != null)
                    Destroy(currentPlaced);

                currentPlaced = Instantiate(prefabToPlace, hit.point, Quaternion.identity);
                placeToggle.isOn = false;
                UpdateTextCoords(hit.point);
            }
        }
    }

    // Convierte posición world a lat/lon y actualiza el texto
    private void UpdateTextCoords(Vector3 worldPos)
    {

        Vector2d lonLat = map.WorldToGeoPosition(worldPos);
        double lon = lonLat.x;
        double lat = lonLat.y;

        coordsText.text = $"Heat signal detected! Possible person found at lat: {lat:F6} lon: {lon:F6}";
    }

    // Comprueba si un punto está dentro de un polígono en XZ
    private bool PointInPolygonXZ(Vector3 p, Vector3[] poly)
    {
        bool inside = false;
        int n = poly.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector3 a = poly[i];
            Vector3 b = poly[j];
            bool intersect = ((a.z > p.z) != (b.z > p.z)) &&
                             (p.x < (b.x - a.x) * (p.z - a.z) / (b.z - a.z + Mathf.Epsilon) + a.x);
            if (intersect) inside = !inside;
        }
        return inside;
    }
}
