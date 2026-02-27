using UnityEngine;
using WebSocketSharp;
using Newtonsoft.Json;
using System.Linq;
using System.Collections.Generic;

// Cliente WebSocket para intercambio de telemetría/comandos con el servidor (DroneKit)
public class DroneWSClient : MonoBehaviour
{
    private WebSocket ws;

    [Header("Info Display")]
    public TMPro.TextMeshProUGUI infoTextL;
    public TMPro.TextMeshProUGUI infoTextR;

    [Header("WebSocket Settings")]
    public string serverUrl = "ws://localhost:8765";

    [Header("Referencias")]
    public GameObject dronePrefab;

    [Header("Mapbox")]
    public Mapbox.Unity.Map.AbstractMap map;

    private Dictionary<int, GameObject> dronesActivos = new Dictionary<int, GameObject>();

    private List<DroneData> datosRecibidosPendientes = null;
    private readonly object candado = new object();

    [Header("Drone Data")]
    public float latitude;
    public float longitude;
    public float altitude;
    public float yawDeg;
    public float groundspeed;
    public float batteryVoltage;
    public float batteryCurrent;
    public float batteryLevel;
    public float batteryEtaMin;
    public string flightMode;
    public bool isArmed;

    // Waypoint simple para misiones
    [System.Serializable]
    public class MissionWaypoint
    {
        public double lat;
        public double lon;
        public double alt;

        public MissionWaypoint(double lat, double lon, double alt)
        {
            this.lat = lat;
            this.lon = lon;
            this.alt = alt;
        }
    }

    // Inicializa y conecta el WebSocket
    private void Start()
    {
        Connect();
    }

    // Actualiza UI básica con la telemetría
    private void Update()
    {
        // 1. Actualizar textos (como antes)
        if (infoTextL != null) infoTextL.text = $"Alt: {altitude:F1} m";
        if (infoTextR != null) infoTextR.text = $"Bat: {batteryLevel:F0}%";

        // 2. PROCESAR DATOS PENDIENTES (La lógica de los drones)
        List<DroneData> datosParaProcesar = null;

        // Sacamos los datos del "buzón" de forma segura
        lock (candado)
        {
            if (datosRecibidosPendientes != null)
            {
                datosParaProcesar = new List<DroneData>(datosRecibidosPendientes);
                datosRecibidosPendientes = null; // Vaciamos el buzón
            }
        }

        // Si había datos, los procesamos (Crear y Mover)
        if (datosParaProcesar != null)
        {
            ProcesarDrones(datosParaProcesar);
        }
    }

    // Esta función hace el trabajo sucio (Instantiate, Move, etc.)
    private void ProcesarDrones(List<DroneData> dataList)
    {
        // --- DEBUG: Comprobamos si el mapa está puesto ---
        if (map == null) 
        {
            Debug.LogError("❌ [UNITY ERROR] ¡No has arrastrado el MAPA a la casilla del script!");
            return;
        }

        foreach (DroneData dron in dataList)
        {
            // A) ¿Es nuevo? -> Lo creamos
            if (!dronesActivos.ContainsKey(dron.id))
            {
                // --- DEBUG: Avisamos que hemos visto un ID nuevo ---
                Debug.Log($"🏭 [UNITY] Detectado Nuevo Dron ID: {dron.id}. Intentando crear...");

                if (dronePrefab != null)
                {
                    GameObject nuevoDron = Instantiate(dronePrefab);
                    nuevoDron.name = "Dron_" + dron.id;
                    dronesActivos.Add(dron.id, nuevoDron);
                    
                    // --- DEBUG: Confirmamos creación ---
                    Debug.Log($"✅ [UNITY] ¡Dron {dron.id} CREADO CORRECTAMENTE en la escena!");
                }
                else
                {
                    Debug.LogError("❌ [UNITY ERROR] ¡Falta asignar el 'Drone Prefab' en el Inspector!");
                    continue;
                }
            }

            // B) Recuperamos el objeto
            GameObject dronAActualizar = dronesActivos[dron.id];

            // C) Calculamos posición en el mapa
            var mapPos = new Mapbox.Utils.Vector2d(dron.latitud, dron.longitud);
            Vector3 pos = map.GeoToWorldPosition(mapPos);

            // D) Altura
            float alturaSuelo = map.QueryElevationInUnityUnitsAt(mapPos);
            pos.y = alturaSuelo + dron.altitud;

            // E) Mover y Rotar
            // Usamos Lerp para que sea suave (opcional, pero queda mejor)
            dronAActualizar.transform.position = Vector3.Lerp(dronAActualizar.transform.position, pos, Time.deltaTime * 10f);

            Vector3 rotacionActual = dronAActualizar.transform.rotation.eulerAngles;
            dronAActualizar.transform.rotation = Quaternion.Euler(rotacionActual.x, dron.rumbo, rotacionActual.z);
        }

        // Actualizamos las variables públicas solo para ver algo en el inspector (del primero)
        if (dataList.Count > 0)
        {
            var data = dataList[0];
            this.latitude = data.latitud;
            this.longitude = data.longitud;
            this.altitude = data.altitud;
            this.yawDeg = data.rumbo;
        }
    }

    // Cierra y limpia la conexión al destruir el objeto
    private void OnDestroy()
    {
        if (ws != null)
        {
            // Desuscribir para evitar callbacks tras destruir el objeto
            ws.OnOpen -= OnWsOpen;
            ws.OnMessage -= OnWsMessage;
            ws.OnError -= OnWsError;
            ws.OnClose -= OnWsClose;

            try { ws.Close(); } catch { /* noop */ }
            ws = null;
        }
    }

    // Conecta al servidor WebSocket
    private void Connect()
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            Debug.LogError("URL de servidor WebSocket inválida.");
            return;
        }

        if (ws != null)
        {
            if (ws.ReadyState == WebSocketState.Open || ws.ReadyState == WebSocketState.Connecting)
                return;

            try { ws.Close(); } catch { /* noop */ }
            ws = null;
        }

        ws = new WebSocket(serverUrl);

        ws.OnOpen += OnWsOpen;
        ws.OnMessage += OnWsMessage;
        ws.OnError += OnWsError;
        ws.OnClose += OnWsClose;

        ws.ConnectAsync();
    }

    // Evento: conexión abierta
    private void OnWsOpen(object sender, System.EventArgs e)
    {
        Debug.Log("🟢 [UNITY] Conectado al servidor WebSocket.");
    }

    // Evento: mensaje entrante (JSON de telemetría)
    private void OnWsMessage(object sender, MessageEventArgs e)
    {
        if (!e.IsText) return;

        // --- DEBUG: Ver qué llega exactamente (puede llenar mucho la consola) ---
        Debug.Log("📩 [UNITY] Recibido JSON: " + e.Data);

        try
        {
            // 1. Solo deserializamos los datos
            List<DroneData> incomingList = JsonConvert.DeserializeObject<List<DroneData>>(e.Data);
            
            // 2. Los metemos en el "buzón" (Thread Safe)
            lock (candado)
            {
                datosRecibidosPendientes = incomingList;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("💥 [UNITY ERROR] Error parseando JSON: " + ex.Message);
        }
    }

    // Evento: error en el socket
    private void OnWsError(object sender, ErrorEventArgs e)
    {
        Debug.LogError("🔴 [UNITY ERROR] en WebSocket: " + e.Message);
    }

    // Evento: conexión cerrada
    private void OnWsClose(object sender, CloseEventArgs e)
    {
        Debug.Log("🔸 [UNITY] Conexión cerrada: " + e.Reason);
    }

    // Envía comando de velocidad
    public void SendSetSpeed(float speed)
    {
        if (!IsOpen())
        {
            Debug.LogWarning("WebSocket no conectado. No se puede enviar velocidad.");
            return;
        }

        var payload = new { command = "set_speed", speed = speed };
        ws.Send(JsonConvert.SerializeObject(payload));
        Debug.Log($"Enviada nueva velocidad: {speed} m/s");
    }

    // Envía una misión (lista de WPs)
    public void SendMission(IEnumerable<MissionWaypoint> waypoints)
    {
        if (!IsOpen())
        {
            Debug.LogWarning("WebSocket no conectado. No se puede enviar la misión.");
            return;
        }

        var payload = new { command = "upload_mission", waypoints = waypoints };
        ws.Send(JsonConvert.SerializeObject(payload));
        Debug.Log($"Enviada misión con {waypoints.Count()} waypoints al servidor.");
    }

    // Fija home en el servidor (DroneKit)
    public void SendSetHome(double lat, double lon, double alt = 0)
    {
        if (!IsOpen())
        {
            Debug.LogWarning("WebSocket no conectado. No se puede fijar la base.");
            return;
        }

        var payload = new { command = "set_home", lat, lon, alt };
        ws.Send(JsonConvert.SerializeObject(payload));
        Debug.Log($"Enviado set_home -> ({lat:F6}, {lon:F6}, alt {alt} m).");
    }

    // Ordena Return-To-Launch
    public void SendReturnToLaunch()
    {
        if (!IsOpen())
        {
            Debug.LogWarning("WebSocket no conectado. No se puede enviar RTL.");
            return;
        }

        var payload = new { command = "return_to_launch" };
        ws.Send(JsonConvert.SerializeObject(payload));
        Debug.Log("Enviado return_to_launch.");
    }

    // Ajusta nivel de batería simulado
    public void SendSetBatteryLevel(float level)
    {
        if (!IsOpen())
        {
            Debug.LogWarning("WebSocket no conectado. No se puede actualizar batería.");
            return;
        }

        var payload = new { command = "set_battery_level", level = level };
        ws.Send(JsonConvert.SerializeObject(payload));
        Debug.Log(JsonConvert.SerializeObject(payload));
        Debug.Log($"Enviado nivel de batería simulado: {level}%");
    }

    // Reanuda misión desde índice guardado
    public void SendResumeMission()
    {
        if (!IsOpen())
        {
            Debug.LogWarning("WebSocket no conectado. No se puede reanudar la misión.");
            return;
        }

        var payload = new { command = "resume_mission" };
        ws.Send(JsonConvert.SerializeObject(payload));
        Debug.Log("Enviado resume mission.");
    }

    // Comprueba si el socket está abierto
    private bool IsOpen()
    {
        return ws != null && ws.ReadyState == WebSocketState.Open;
    }
}

// Estructuras para deserializar la telemetría
[System.Serializable]
public class BatteryData
{
    public float voltage;
    public float current;
    public float level;
    public float? eta_min;
}

[System.Serializable]
public class DroneData
{
    public float latitud;
    public float longitud;
    public float altitud;
    public float rumbo;
    public int id;
    /*public float yaw;
    public float groundspeed;
    public BatteryData battery;
    public bool armed;
    public string mode;*/
    
}