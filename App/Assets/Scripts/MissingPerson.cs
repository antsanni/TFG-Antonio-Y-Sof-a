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
    public DroneWSClient droneWSClient;

    [Header("Mapbox")]
    public AbstractMap map;

    [Header("UI")]
    public TMP_Text coordsText;
    public GameObject coordsTextBg;

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
                currentPlaced.name = prefabToPlace.name; // Mantiene el nombre del prefab sin el "(Clone)"
                
                // --- AUTOCORRECCIÓN DE LAYER Y COLLIDER ---
                // 1. Añadir BoxCollider si el prefab no tiene ninguno
                if (currentPlaced.GetComponentInChildren<Collider>() == null)
                {
                    BoxCollider bc = currentPlaced.AddComponent<BoxCollider>();
                    bc.size = new Vector3(2f, 2f, 2f);
                    bc.center = new Vector3(0, 1f, 0);
                }

                // 2. Asignar la capa correcta automáticamente copiándola de lo que espera el dron
                Drone dronActivo = FindObjectOfType<Drone>();
                if (dronActivo != null)
                {
                    int mask = dronActivo.layerMissingPerson.value;
                    int targetLayer = 0;
                    for (int i = 0; i < 32; i++) {
                        if ((mask & (1 << i)) != 0) { targetLayer = i; break; }
                    }
                    
                    Transform[] hijos = currentPlaced.GetComponentsInChildren<Transform>(true);
                    foreach (Transform hijo in hijos) {
                        hijo.gameObject.layer = targetLayer;
                    }
                }
                // ------------------------------------------

                placeToggle.isOn = false;
                UpdateTextCoords(hit.point);
            }
        }
    }

    private void UpdateTextCoords(Vector3 worldPos) {
        coordsTextBg.SetActive(true);
        coordsText.text = "Objetivo esperando a ser encontrado";
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
