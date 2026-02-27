using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using Clipper2Lib;
using System.Collections.Generic;
using System.Linq;

public class ZoneSelector : MonoBehaviour
{
    [Header("DroneWSClient")]
    public DroneWSClient client;

    [Header("Prefabs / Visuals")]
    public GameObject basePrefab;
    public GameObject nodePrefab;
    public LineRenderer walls_lineRenderer;
    public Material borderMaterial;

    [Header("Reference")]
    public AbstractMap map;
    [SerializeField] private DroneMapController droneMapController;

    [Header("UI")]
    public Toggle toggleDrawMode;
    public Toggle placeBaseToggle;



    [Header("Path rendering")]
    [SerializeField] private Color pathStartColor = Color.cyan;
    [SerializeField] private Color pathEndColor = Color.green;
    [SerializeField, Min(0.01f)] private float pathWidth = 1f;

    [Header("Limits & Params")]
    [Tooltip("Desired height Above Ground Level that the drone must keep.")]
    public float agl = 20f;
    [Header("Terrain following")]
    [Tooltip("0 = plano; 1 = sigue el terreno. Valores intermedios suavizan.")]
    [Range(0f, 1f)][SerializeField] private float adaptationIntensity = 1f;
    [Tooltip("Separación máxima entre muestras consecutivas de terreno.")]
    [Min(0.1f)][SerializeField] private float sampleSpacing = 5.0f;

    [Header("Path simplification / densify")]
    [Tooltip("Factor para ε (Douglas-Peucker). 0 = nada, 1 = agresivo.")]
    [Range(0f, 1f)] public float simplificationFactor = 0.40f;
    [Tooltip("Salto vertical máximo permitido entre muestras sucesivas (m).")]
    [Min(0.1f)] public float maxVerticalStep = 1.5f;
    [Tooltip("Si Δaltura entre dos WPs supera este valor (m), se inserta uno intermedio.")]
    public float slopeWaypointThreshold = 3f;

    [Header("DroneKit limits")]
    [Tooltip("Máximo de waypoints que DroneKit acepta (<= 650).")]
    [SerializeField, Min(2)] private int maxWaypoints = 650;

    private LineRenderer _pathLR;
    private LineRenderer PathRenderer => _pathLR;

    private readonly List<Vector2d> geoPoints = new();
    private readonly List<GameObject> nodes = new();
    private readonly List<Vector2> lastCoveragePath = new();

    private GameObject wallsGo;
    private bool canPlace = false;
    private bool isDrawing = false;
    private float maxNodeHeight = float.NegativeInfinity;
    private GameObject drone_base;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        float halfH = 500.0f * 0.5f;

        Gizmos.color = Color.yellow;
        foreach (Vector2 p in lastCoveragePath)
        {
            Vector3 v = new Vector3(p.x, 0f, p.y);
            Gizmos.DrawSphere(v, 1.0f);
            Vector3 vBottom = v + Vector3.down * halfH;
            Vector3 vTop = v + Vector3.up * halfH;
            Gizmos.DrawLine(vBottom, vTop);
        }

        if (_pathLR && _pathLR.positionCount > 0)
        {
            Gizmos.color = Color.red;
            Vector3[] pts = new Vector3[_pathLR.positionCount];
            _pathLR.GetPositions(pts);
            foreach (Vector3 v in pts) Gizmos.DrawSphere(v, 1.0f);
        }
    }
#endif

    private void Awake()
    {
        if (toggleDrawMode) toggleDrawMode.onValueChanged.AddListener(ToggleDrawingMode);
        if (placeBaseToggle) placeBaseToggle.onValueChanged.AddListener(ToggleBase);

        var go = new GameObject("CoveragePath");
        go.transform.SetParent(transform, false);

        _pathLR = go.AddComponent<LineRenderer>();
        _pathLR.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
        _pathLR.alignment = LineAlignment.View;
        _pathLR.widthMultiplier = pathWidth;
        _pathLR.startColor = pathStartColor;
        _pathLR.endColor = pathEndColor;
    }

    private void Start()
    {
        canPlace = placeBaseToggle != null && placeBaseToggle.isOn;
        if (walls_lineRenderer)
        {
            walls_lineRenderer.useWorldSpace = true;
            walls_lineRenderer.positionCount = 0;
        }
        if (!client) client = FindObjectOfType<DroneWSClient>();
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        if (!EventSystem.current.IsPointerOverGameObject() && canPlace)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (drone_base) Destroy(drone_base);
                drone_base = Instantiate(basePrefab, hit.point, Quaternion.identity);

                if (client && map)
                {
                    Vector2d geo = map.WorldToGeoPosition(hit.point);
                    double terrainMSL = 0;
                    client.SendSetHome(geo.x, geo.y, terrainMSL);
                }
                else
                {
                    Debug.LogWarning("[ZoneSelector] No DroneWSClient or Map assigned to send set_home.");
                }
                return;
            }
        }

        if (isDrawing && GetMousePositionOnMap(out Vector3 worldPos))
        {
            AddPoint(worldPos);
        }
    }

    private void OnDestroy()
    {
        if (toggleDrawMode) toggleDrawMode.onValueChanged.RemoveListener(ToggleDrawingMode);
        if (placeBaseToggle) placeBaseToggle.onValueChanged.RemoveListener(ToggleBase);
        if (_pathLR) Destroy(_pathLR.gameObject);
    }

    // Devuelve una copia del último path 2D (XZ)
    public List<Vector2> GetLastCoveragePath() => new(lastCoveragePath);

    // Alterna modo de dibujo; al desactivar con >=3 puntos, cierra polígono y genera ruta
    private void ToggleDrawingMode(bool value)
    {
        isDrawing = value;

        if (isDrawing)
        {
            DeletePath();
            DeleteNodes();
        }
        else if (geoPoints.Count > 2)
        {
            ClosePolygon();
        }
    }

    // Alterna modo de colocación de base
    private void ToggleBase(bool value) => canPlace = value;

    // Borra la ruta renderizada y su caché
    private void DeletePath()
    {
        lastCoveragePath.Clear();
        if (_pathLR) _pathLR.positionCount = 0;
    }

    // Elimina nodos y paredes y limpia colecciones
    private void DeleteNodes()
    {
        if (wallsGo) Destroy(wallsGo);
        foreach (GameObject node in nodes) if (node) Destroy(node);
        nodes.Clear();
        geoPoints.Clear();
        if (walls_lineRenderer) walls_lineRenderer.positionCount = 0;
        maxNodeHeight = float.NegativeInfinity;
    }

    // Crea paredes verticales del polígono
    private void CreateWalls(List<Vector3> topPositions, Material borderMat)
    {
        if (topPositions == null || topPositions.Count < 2) return;

        wallsGo = new GameObject("PolygonWalls");
        MeshFilter mf = wallsGo.AddComponent<MeshFilter>();
        MeshRenderer mr = wallsGo.AddComponent<MeshRenderer>();
        mr.material = borderMat;

        List<Vector3> vertices = new();
        List<Vector2> uvs = new();
        List<int> triangles = new();

        const float groundY = 0f;
        int n = topPositions.Count;

        for (int i = 0; i < n; i++)
        {
            int i2 = (i + 1) % n;

            Vector3 top1 = topPositions[i];
            Vector3 top2 = topPositions[i2];
            Vector3 bot1 = new(top1.x, groundY, top1.z);
            Vector3 bot2 = new(top2.x, groundY, top2.z);

            int idx = vertices.Count;
            vertices.AddRange(new[] { top1, top2, bot1, bot2 });

            uvs.AddRange(new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 0) });

            triangles.AddRange(new[] { idx, idx + 1, idx + 2, idx + 2, idx + 1, idx + 3 });
        }

        Mesh mesh = new() { name = "WallsMesh" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.mesh = mesh;
    }

    // Añade un vértice al polígono y re-nivela todos los nodos
    private void AddPoint(Vector3 worldPos)
    {
        if (!map || !walls_lineRenderer || !nodePrefab) return;

        Vector2d geoPos = map.WorldToGeoPosition(worldPos);
        geoPoints.Add(geoPos);

        float nodeH = nodePrefab.transform.localScale.y;
        maxNodeHeight = Mathf.Max(maxNodeHeight, worldPos.y + nodeH);

        foreach (GameObject n in nodes)
        {
            Vector3 p = n.transform.position;
            p.y = maxNodeHeight;
            n.transform.position = p;
        }

        walls_lineRenderer.positionCount = geoPoints.Count;
        for (int i = 0; i < geoPoints.Count; i++)
        {
            Vector3 p = map.GeoToWorldPosition(geoPoints[i]);
            p.y = maxNodeHeight;
            walls_lineRenderer.SetPosition(i, p);
        }

        Vector3 finalPos = new(worldPos.x, maxNodeHeight, worldPos.z);
        nodes.Add(Instantiate(nodePrefab, finalPos, Quaternion.identity));
    }

    // Cierra el polígono, crea paredes y genera la ruta de cobertura
    private void ClosePolygon()
    {
        if (geoPoints.Count < 3 || !map || !walls_lineRenderer) return;

        Vector3 first = map.GeoToWorldPosition(geoPoints[0]);
        Vector3 last = map.GeoToWorldPosition(geoPoints[^1]);
        bool merge = Mathf.Abs(first.x - last.x) < 10f && Mathf.Abs(first.z - last.z) < 10f;

        List<Vector3> topPositions = new();

        if (merge)
        {
            Vector3 mid = (first + last) * 0.5f;
            mid.y = maxNodeHeight;

            Vector2d midGeo = map.WorldToGeoPosition(mid);
            geoPoints[0] = midGeo;
            geoPoints[^1] = midGeo;

            if (nodes.Count > 0) Destroy(nodes[0]);
            if (nodes.Count > 1) Destroy(nodes[^1]);
            if (nodes.Count > 0) nodes.RemoveAt(nodes.Count - 1);
            if (nodes.Count > 0) nodes[0] = Instantiate(nodePrefab, mid, Quaternion.identity);

            walls_lineRenderer.positionCount = geoPoints.Count;
            for (int i = 0; i < geoPoints.Count; i++)
            {
                Vector3 p = map.GeoToWorldPosition(geoPoints[i]);
                p.y = maxNodeHeight;
                walls_lineRenderer.SetPosition(i, p);
                topPositions.Add(p);
            }
        }
        else
        {
            walls_lineRenderer.positionCount = geoPoints.Count + 1;
            for (int i = 0; i < geoPoints.Count; i++)
            {
                Vector3 p = map.GeoToWorldPosition(geoPoints[i]);
                p.y = maxNodeHeight;
                walls_lineRenderer.SetPosition(i, p);
                topPositions.Add(p);
            }
            Vector3 firstClosed = first; firstClosed.y = maxNodeHeight;
            walls_lineRenderer.SetPosition(geoPoints.Count, firstClosed);
        }

        if (droneMapController) droneMapController.SetZonaXZ(topPositions);
        CreateWalls(topPositions, borderMaterial);
        GenerateCoveragePath();
    }

    // Proyecta click del ratón al mapa en world space
    private static bool GetMousePositionOnMap(out Vector3 worldPos)
    {
        worldPos = Vector3.zero;
        if (EventSystem.current.IsPointerOverGameObject()) return false;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            worldPos = hit.point;
            return true;
        }
        return false;
    }

    // Inserta puntos intermedios cuando el desnivel supera el umbral
    private List<Vector2> AddSlopeBreaks(List<Vector2> inPath, float maxVertStep, float sampleStep)
    {
        if (inPath == null || inPath.Count < 2) return inPath;

        List<Vector2> outPts = new() { inPath[0] };

        for (int i = 1; i < inPath.Count; ++i)
        {
            Vector2 A = inPath[i - 1];
            Vector2 B = inPath[i];

            float segLen = Vector2.Distance(A, B);
            int slices = Mathf.Max(1, Mathf.CeilToInt(segLen / sampleStep));

            float prevH = SampleGroundY(A);

            for (int s = 1; s <= slices; ++s)
            {
                float t = (float)s / slices;
                Vector2 P = Vector2.Lerp(A, B, t);
                float h = SampleGroundY(P);

                if (Mathf.Abs(h - prevH) >= maxVertStep)
                {
                    outPts.Add(P);
                    prevH = h;
                }
            }

            outPts.Add(B);
        }

        return outPts;
    }

    // Simplificación Douglas-Peucker sobre XY
    private static List<Vector2> DouglasPeucker(List<Vector2> src, float eps)
    {
        if (src == null || src.Count < 3) return src;

        bool[] keep = new bool[src.Count];
        keep[0] = keep[^1] = true;
        Stack<(int s, int e)> st = new();
        st.Push((0, src.Count - 1));

        while (st.Count > 0)
        {
            var (s, e) = st.Pop();
            float maxD = 0; int idx = -1;

            Vector2 A = src[s], B = src[e];
            for (int i = s + 1; i < e; i++)
            {
                float d = DistPointLine(src[i], A, B);
                if (d > maxD) { maxD = d; idx = i; }
            }

            if (maxD > eps)
            {
                keep[idx] = true;
                st.Push((s, idx));
                st.Push((idx, e));
            }
        }

        List<Vector2> dst = new();
        for (int i = 0; i < src.Count; ++i) if (keep[i]) dst.Add(src[i]);
        return dst;
    }

    // Distancia punto-segmento AB
    private static float DistPointLine(Vector2 P, Vector2 A, Vector2 B)
    {
        float len2 = (B - A).sqrMagnitude;
        if (len2 < 1e-6f) return Vector2.Distance(P, A);
        float t = Vector2.Dot(P - A, B - A) / len2;
        Vector2 proj = A + Mathf.Clamp01(t) * (B - A);
        return Vector2.Distance(P, proj);
    }

    // Densifica por distancia y por desnivel máximo
    private List<Vector2> DensifyForHeight(IReadOnlyList<Vector2> xy, float baseStep, float maxVertStep)
    {
        List<Vector2> outPts = new() { xy[0] };

        for (int i = 1; i < xy.Count; ++i)
        {
            Vector2 a = xy[i - 1];
            Vector2 b = xy[i];

            float ya = SampleGroundY(a);
            float yb = SampleGroundY(b);

            int cutsVert = Mathf.CeilToInt(Mathf.Abs(yb - ya) / maxVertStep);
            int cutsHorz = Mathf.CeilToInt(Vector2.Distance(a, b) / baseStep);
            int splits = Mathf.Max(1, Mathf.Max(cutsVert, cutsHorz));

            for (int s = 1; s <= splits; ++s)
            {
                float t = (float)s / splits;
                outPts.Add(Vector2.Lerp(a, b, t));
            }
        }
        return outPts;
    }

    // Convierte XY en 3D siguiendo terreno con suavizado vertical
    private Vector3[] BuildPath3D(IReadOnlyList<Vector2> xy)
    {
        Vector3[] p3 = new Vector3[xy.Count];

        float prevY = SampleGroundY(xy[0]) + agl;
        p3[0] = new Vector3(xy[0].x, prevY, xy[0].y);

        for (int i = 1; i < xy.Count; ++i)
        {
            float targetY = SampleGroundY(xy[i]) + agl;
            prevY = Mathf.Lerp(prevY, targetY, adaptationIntensity);
            p3[i] = new Vector3(xy[i].x, prevY, xy[i].y);
        }
        return p3;
    }

    // Genera ruta de cobertura tipo “lawn-mower” dentro del polígono y actualiza el LineRenderer
    public List<Vector2> GenerateCoveragePath()
    {
        const double SCALE = 1000.0;
        float fovSide = agl * 1.2f;
        float step = fovSide * 0.8f;

        List<Vector2> rawPoly = nodes.Select(n => new Vector2(n.transform.position.x, n.transform.position.z)).ToList();
        if (rawPoly.Count > 1 && Vector2.Distance(rawPoly[0], rawPoly[^1]) < 0.01f)
            rawPoly.RemoveAt(rawPoly.Count - 1);

        List<List<Vector2>> allRings = new() { rawPoly };
        Paths64 paths = new() { ToPath64(rawPoly, SCALE) };

        double inset = step;
        while (paths.Count > 0)
        {
            Paths64 inner = Clipper.InflatePaths(paths, -inset * SCALE, Clipper2Lib.JoinType.Miter, EndType.Polygon);
            if (inner.Count == 0) break;

            foreach (Path64 p in inner) allRings.Add(ToVector2(p, SCALE));
            paths = inner;
        }

        List<Vector2> path = new();
        bool cw = true;
        foreach (List<Vector2> ringSrc in allRings)
        {
            List<Vector2> ring = cw ? ringSrc : ringSrc.AsEnumerable().Reverse().ToList();

            if (path.Count > 0)
            {
                Vector2 last = path[^1];
                Vector2 next = ring.OrderBy(p => Vector2.Distance(p, last)).First();
                if (Vector2.Distance(next, last) > 1e-4f) path.Add(next);

                int idx = ring.IndexOf(next);
                ring = ring.Skip(idx).Concat(ring.Take(idx)).ToList();
            }

            path.AddRange(ring);
            path.Add(ring[0]);
            cw = !cw;
        }

        if (Vector2.Distance(path[^1], path[0]) > step * 0.5f) path.Add(path[0]);

        List<Vector2> denseXY = ResamplePath(path, Mathf.Max(0.01f, sampleSpacing));

        float eps = fovSide * simplificationFactor;
        List<Vector2> path2D = DouglasPeucker(denseXY, eps);
        path2D = AddSlopeBreaks(path2D, slopeWaypointThreshold, sampleSpacing);

        List<Vector2> zSupport = DensifyForHeight(path2D, sampleSpacing, maxVerticalStep);

        Vector3[] path3D = BuildPath3D(zSupport);
        PathRenderer.positionCount = path3D.Length;
        PathRenderer.SetPositions(path3D);

        lastCoveragePath.Clear();
        lastCoveragePath.AddRange(path2D);
        return lastCoveragePath;
    }

    // Exporta la ruta actual a waypoints y la envía al backend (DroneKit)
    public void ExportMission()
    {
        if (lastCoveragePath == null || lastCoveragePath.Count == 0)
        {
            Debug.LogWarning("ZoneSelector.ExportMission › No simplified path to export.");
            return;
        }

        List<DroneWSClient.MissionWaypoint> wps = new(lastCoveragePath.Count);

        foreach (Vector2 p in lastCoveragePath)
        {
            Vector3 world = new Vector3(p.x, 0f, p.y);
            Vector2d geo = map.WorldToGeoPosition(world);
            double terrainMSL = map.QueryElevationInUnityUnitsAt(geo);
            wps.Add(new DroneWSClient.MissionWaypoint(geo.x, geo.y, terrainMSL + agl));
        }

        if (wps.Count > maxWaypoints)
        {
            float ratio = (float)(wps.Count - 1) / (maxWaypoints - 1);
            List<DroneWSClient.MissionWaypoint> reduced = new(maxWaypoints);

            for (int i = 0; i < maxWaypoints; i++)
            {
                int idx = Mathf.RoundToInt(i * ratio);
                if (idx >= wps.Count) idx = wps.Count - 1;
                reduced.Add(wps[idx]);
            }
            wps = reduced;
            Debug.Log($"[ZoneSelector] Ruta recortada de {lastCoveragePath.Count} → {wps.Count} (límite DroneKit).");
        }

        if (client) client.SendMission(wps);
        else Debug.LogWarning("ZoneSelector.ExportMission › No DroneWSClient found in the scene.");
    }

    // Re-muestrea el camino para limitar longitud de segmentos
    private static List<Vector2> ResamplePath(IReadOnlyList<Vector2> src, float maxSegLen)
    {
        if (src == null || src.Count == 0) return new List<Vector2>();

        List<Vector2> res = new() { src[0] };
        for (int i = 1; i < src.Count; i++)
        {
            Vector2 a = src[i - 1];
            Vector2 b = src[i];
            float dist = Vector2.Distance(a, b);
            if (dist < 1e-4f) continue;

            int slices = Mathf.CeilToInt(dist / maxSegLen);
            for (int s = 1; s <= slices; s++)
            {
                float t = (float)s / slices;
                res.Add(Vector2.Lerp(a, b, t));
            }
        }
        return res;
    }

    // Altura del terreno (unidades de Unity) en XZ
    private float SampleGroundY(Vector2 pXZ)
    {
        Vector3 world = new(pXZ.x, 0f, pXZ.y);
        Vector2d latLon = map.WorldToGeoPosition(world);
        return map.QueryElevationInUnityUnitsAt(latLon);
    }

    // Convierte lista de Vector2 (XZ) a Path64 de Clipper con escala
    private static Path64 ToPath64(IEnumerable<Vector2> poly, double s)
    {
        Path64 p = new();
        foreach (Vector2 v in poly) p.Add(new Point64((long)(v.x * s), (long)(v.y * s)));
        return p;
    }

    // Convierte Path64 a lista de Vector2 usando escala
    private static List<Vector2> ToVector2(Path64 p, double s)
        => p.Select(q => new Vector2((float)(q.X / s), (float)(q.Y / s))).ToList();
}