using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Drone : MonoBehaviour
{
    public Transform visionCone;
    public LayerMask layerMallaModificable;
    public LayerMask layerMissingPerson;
    public DroneWSClient droneWSClient;

    public Color colorMarcado = new Color(0f, 0f, 1f, 0.4f);
    public bool camara_status = true;
    public ZoneProgressManager zoneMgr;
    public Slider progresoSlider;
    public TextMeshProUGUI progresoTexto;

    private MeshCollider coneCollider;
    private GameObject reportedTarget;

    [Header("UI Found Banner")]
    [SerializeField] private CanvasGroup foundBanner;
    [SerializeField] private float visibleSeconds = 5f;
    [SerializeField] private float fadeSeconds = 2f;
    private Coroutine bannerRoutine;

    private readonly Collider[] missingPersonBuffer = new Collider[1];

    private void Start()
    {
        reportedTarget = null;

        if (visionCone != null)
        {
            coneCollider = visionCone.GetComponent<MeshCollider>();
            if (coneCollider == null)
                coneCollider = visionCone.gameObject.AddComponent<MeshCollider>();

            coneCollider.convex = true;
            coneCollider.isTrigger = true;
        }

        if (foundBanner != null)
        {
            foundBanner.alpha = 0f;
            foundBanner.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!camara_status || coneCollider == null)
            return;

        Bounds bounds = coneCollider.bounds;

        Collider[] posibles = Physics.OverlapBox(bounds.center, bounds.extents, visionCone.rotation, layerMallaModificable);
        for (int c = 0; c < posibles.Length; c++)
        {
            var col = posibles[c];
            if (col is MeshCollider meshCol && meshCol.sharedMesh != null)
            {
                Mesh mesh = meshCol.sharedMesh;
                Transform t = meshCol.transform;
                int[] triangles = mesh.triangles;
                Vector3[] vertices = mesh.vertices;

                if (!col.TryGetComponent<MeshPaintData>(out var paintData))
                    continue;

                var intensidad = paintData.intensity;
                var fuePintado = paintData.wasPainted;

                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int i0 = triangles[i];
                    int i1 = triangles[i + 1];
                    int i2 = triangles[i + 2];

                    Vector3 v0 = t.TransformPoint(vertices[i0]);
                    Vector3 v1 = t.TransformPoint(vertices[i1]);
                    Vector3 v2 = t.TransformPoint(vertices[i2]);

                    if (VertexInVolume(v0) || VertexInVolume(v1) || VertexInVolume(v2))
                    {
                        intensidad[i0] = 1f; fuePintado[i0] = true;
                        intensidad[i1] = 1f; fuePintado[i1] = true;
                        intensidad[i2] = 1f; fuePintado[i2] = true;

                        if (zoneMgr != null)
                        {
                            float antes = zoneMgr.Porcentaje();
                            zoneMgr.RegistrarTrianguloPintado(mesh, i);
                            float despues = zoneMgr.Porcentaje();

                            if (despues > antes)
                            {
                                if (progresoSlider != null) progresoSlider.value = despues * 100.0f;
                                if (progresoTexto != null) progresoTexto.text = $"{despues * 100f:0.0}%";
                            }
                        }
                    }
                }
            }
        }

        DetectMissingPerson(bounds);
    }

    // Comprueba si hay una persona desaparecida dentro del volumen
    private void DetectMissingPerson(Bounds bounds)
    {
        int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, missingPersonBuffer, visionCone.rotation, layerMissingPerson);

        GameObject detectado = null;
        if (count > 0)
        {
            var c = missingPersonBuffer[0];
            Vector3 p = c.bounds.center;
            if (VertexInVolume(p))
                detectado = c.gameObject;
        }

        if (detectado != null && detectado != reportedTarget)
        {
            Debug.Log("Missing person Found!");
            ShowFoundBanner();
            reportedTarget = detectado;

            if (droneWSClient != null &&
                droneWSClient.isArmed &&
                droneWSClient.altitude > 2f &&
                droneWSClient.flightMode != "RTL")
            {
                droneWSClient.SendReturnToLaunch();
            }
        }
    }

    // Muestra un banner en la interfaz cuando se detecta una persona
    private void ShowFoundBanner()
    {
        if (foundBanner == null) return;

        if (bannerRoutine != null)
            StopCoroutine(bannerRoutine);

        bannerRoutine = StartCoroutine(BannerRoutine());
    }

    // Controla visibilidad y fade del banner
    private IEnumerator BannerRoutine()
    {
        foundBanner.gameObject.SetActive(true);
        foundBanner.alpha = 1f;

        yield return new WaitForSeconds(visibleSeconds);

        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeSeconds);
            foundBanner.alpha = 1f - k;
            yield return null;
        }

        foundBanner.alpha = 0f;
        foundBanner.gameObject.SetActive(false);
        bannerRoutine = null;
    }

    // Comprueba si un punto está dentro del volumen del cono
    private bool VertexInVolume(Vector3 punto)
    {
        if (!coneCollider.bounds.Contains(punto))
            return false;

        Vector3 puntoMasCercano = coneCollider.ClosestPoint(punto);
        return Vector3.Distance(punto, puntoMasCercano) < 0.01f;
    }
}
