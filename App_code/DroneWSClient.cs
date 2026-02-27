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

        if (infoTextL != null)
        {
            string etaTxt = (float.IsNaN(batteryEtaMin) || batteryEtaMin <= 0f)
            ? "--"
            : $"{batteryEtaMin:F0} min";
            infoTextL.text =
                $"Altitude:\t{altitude:F1} m\n" +
                $"Speed:\t{groundspeed:F1} m/s\n" +
                $"ETA:\t{etaTxt}\n";
        }

        if (infoTextR != null)
        {
            infoTextR.text =
                $"Battery:\t{batteryLevel:F0}%\n" +
                $"Voltage:\t{batteryVoltage:F1} V\n" +
                $"Current:\t{batteryCurrent:F1} A";
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
        Debug.Log("Conectado al servidor WebSocket.");
    }

    // Evento: mensaje entrante (JSON de telemetría)
    private void OnWsMessage(object sender, MessageEventArgs e)
    {
        if (!e.IsText || string.IsNullOrEmpty(e.Data)) return;

        try
        {
            List<DroneData> dataList = JsonConvert.DeserializeObject<List<DroneData>>(e.Data);
            if(dataList == null || dataList.Count == 0) return;
            DroneData data = dataList[0];
            
            if (data == null) return;

            this.latitude = data.latitud;
            this.longitude = data.longitud;
            this.altitude = data.altitud;
            this.yawDeg = data.rumbo;

            Debug.Log($"Dron {data.id} -> Lat: {this.latitude}, Lon: {this.longitude}");

        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error al parsear mensaje: " + ex.Message);
        }
    }

    // Evento: error en el socket
    private void OnWsError(object sender, ErrorEventArgs e)
    {
        Debug.LogError("Error en WebSocket: " + e.Message);
    }

    // Evento: conexión cerrada
    private void OnWsClose(object sender, CloseEventArgs e)
    {
        Debug.Log("Conexión cerrada: " + e.Reason);
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