using UnityEngine;
using WebSocketSharp;
using Newtonsoft.Json;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Cliente WebSocket para intercambio de telemetría/comandos con el servidor (DroneKit)
/// </summary>
public class DroneWSClient : MonoBehaviour {

    #region Variables y Referencias
    private WebSocket ws; // Referencia al WebSocket

    [Header("Info Display")]
    [SerializeField] private TMPro.TextMeshProUGUI infoTextL; // Texto de altitud
    [SerializeField] private TMPro.TextMeshProUGUI infoTextR; // Texto de nivel de batería

    [Header("WebSocket Settings")]
    [SerializeField] private string serverUrl = "ws://localhost:8765"; // Url y puerto del WebSocket 

    [Header("Referencias")]
    [SerializeField]
    private GameObject dronePrefab; // Prefab del dron (Para instanciar en escena)

    [Header("Mapbox")]
    [SerializeField] private Mapbox.Unity.Map.AbstractMap map; // Referencia al mapa

    private Dictionary<int, GameObject> dronesActivos = new Dictionary<int, GameObject>(); // Diccionario para llevar el control de los drones activos en la escena (ID (único) -> Instancia de PrefabDron)

    private Dictionary<int, DroneData> ultimaDataDrones = new Dictionary<int, DroneData>(); // Diccionario para guardar la informacion recibida de cada dron

    private List<DroneData> datosRecibidosPendientes = null; // Lista de drones recibidos pero aún no procesados (buzón entre el hilo del WebSocket y el Update de Unity)
    private readonly object candado = new object(); // Cerrojo para acceder a la lista de datos recibidos de forma segura entre hilos

    // Información sobre el dron (variables públicas para que sean accesibles desde scripts externos)
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
    #endregion

    #region Estructuras auxiliares
    /// <summary>
    /// Waypoint simple para misiones
    /// </summary>
    [System.Serializable]
    public class MissionWaypoint {
        public double lat; // latitud
        public double lon; // longitud
        public double alt; // altura sobre el suelo (en metros)

        /// <summary>
        /// Constructora. Se asume que la altitud es relativa al suelo (AGL) y se convertirá a absoluta (AMSL) en el servidor usando la elevación del mapa.
        /// </summary>
        /// <param name="lat">Latitud</param>
        /// <param name="lon">Longitud</param>
        /// <param name="alt">Altura sobre el suelo (en metros)</param>
        /// <returns>
        /// Objeto de tipo MissionWaypoint con los datos proporcionados.
        /// </returns>
        public MissionWaypoint(double lat, double lon, double alt) {
            this.lat = lat;
            this.lon = lon;
            this.alt = alt;
        }
    }

    // Estructuras para deserializar la telemetría

    /// <summary>
    /// Estructura de datos de la batería (voltaje, corriente, nivel y tiempo estimado restante)
    /// </summary>
    /// <param name="level"> Nivel de batería actual. </param>
    /// <param name="flightMode"> Modo de vuelo actual. </param>
    /// <param name="altitude"> Altitud del dron. </param>
    /// <param name="isArmed"> Indica si los motores del dron están armados (true) o desarmados (false). </param>
    [System.Serializable]
    public class BatteryData {
        public float level;
        public string flightMode;
        public bool isArmed;
        public float altitude;
    }

    /// <summary>
    /// Estructura de datos del dron (latitud, longitud, altitud, rumbo e ID único)
    /// </summary>
    /// <param name="latitud"> Latitud </param>
    /// <param name="longitud"> Longitud </param>
    /// <param name="altitud"> Altitud </param>
    /// <param name="rumbo"> Rumbo </param>
    /// <param name="id"> // Identicficador único (para el diccionario de drones activos) </param>
    /// <param name="battery"> // Bateria </param>
    [System.Serializable]
    public class DroneData {
        public float latitud;
        public float longitud;
        public float altitud;
        public float rumbo;
        public int id;
        public float level;
        public string flightMode;
        public bool isArmed;
        /*public float yaw;
        public float groundspeed;
        public BatteryData battery;
        public bool armed;
        public string mode;*/

    }


    #endregion

    /// <summary>
    /// Inicializa y conecta el WebSocket
    /// </summary>
    private void Start() {
        Connect();
    }

    /// <summary>
    /// Actualiza UI básica con la telemetría
    /// </summary>
    private void Update() {

        if (Input.GetKeyDown(KeyCode.Space))  {
            Debug.Log("🚀 [TECLADO] Enviando misión de despegue al Dron 5760...");
            EnviarMisionDePrueba(5760); // Mandamos volar al Dron 1
        }
        // Actualiza la UI (si las referencias están puestas en el Inspector -> no son nulas)
        //if (infoTextL != null) infoTextL.text = $"Alt: {altitude:F1} m";
        //if (infoTextR != null) infoTextR.text = $"Bat: {batteryLevel:F0}%";
        if (infoTextL != null) {
            string textoDashboard = ""; // Aquí construiremos la lista de texto

            // Recorremos todos los drones de los que tenemos datos guardados
            foreach (var kvp in ultimaDataDrones) {
                int dronId = kvp.Key;
                DroneData data = kvp.Value;

                // Aplicamos tu fórmula: (5760-5760)/10 = 0. Le sumamos 1 para que empiece en Dron 1
                int numeroDron = ((dronId - 5760) / 10) + 1;

                // Indicador visual de si está armado
                string armadoIcono = data.isArmed ? "🟢" : "🔴";

                // Añadimos la información de este dron a la lista (con \n para saltar de línea)
                // Primera línea: Dron, Altitud, Latitud, Longitud (con salto de línea al final)
                textoDashboard += $"<b>Dron {numeroDron}:</b> {armadoIcono} | Alt: {data.altitud:F1}m | Lat: {data.latitud:F5} | Lon: {data.longitud:F5}\n";

                // Segunda línea: Batería, Modo y doble salto de línea para separarlo del siguiente dron
                textoDashboard += $"Bat: {data.level:F0}% | Modo: {data.flightMode}\n\n";
            
            }

            // Imprimimos el texto final en la pantalla
            infoTextL.text = textoDashboard;
        }

        // Dejamos infoTextR libre o lo vaciamos si no lo usamos por ahora
        if (infoTextR != null) {
            infoTextR.text = $"Drones Activos: {ultimaDataDrones.Count}";
        }

        // Porcesa los datos recibidos del WebSocket (si los hay) de forma segura
        List<DroneData> datosParaProcesar = null;
        lock (candado) {
            if (datosRecibidosPendientes != null) {
                datosParaProcesar = new List<DroneData>(datosRecibidosPendientes);
                datosRecibidosPendientes = null; // Vaciamos el buzón
            }
        }

        // Muestra los datos procesados en la escena (crear/mover drones, actualizar variables serializadas para ver en el inspector, etc.)
        if (datosParaProcesar != null) {
            ProcesarDrones(datosParaProcesar);
        }
    }

    /// <summary>
    /// Genera una misión en forma de cuadrado alrededor del dron y se la envía
    /// </summary>
    public void EnviarMisionDePrueba(int droneId) {
        if (!IsOpen()) {
            Debug.LogWarning("WebSocket no conectado.");
            return;
        }

        if (!ultimaDataDrones.ContainsKey(droneId)) {
            Debug.LogWarning("Aún no tenemos la posición del dron para calcular la misión.");
            return;
        }

        // Cogemos donde está el dron ahora mismo
        DroneData data = ultimaDataDrones[droneId];
        double lat = data.latitud;
        double lon = data.longitud;

        // Creamos un cuadrado de waypoints desplazando un poquito la lat/lon
        // Altura = 15 metros
        var wps = new List<MissionWaypoint> {
            new MissionWaypoint(lat + 0.0005, lon, 15),
            new MissionWaypoint(lat, lon + 0.0005, 15),
            new MissionWaypoint(lat - 0.0005, lon, 15),
            new MissionWaypoint(lat, lon - 0.0005, 15)
        };

        // Enviamos la misión al servidor Python
        SendMission(droneId, wps);

        // Iniciamos la misión
        StartMission(droneId);
    }

    /// <summary>
    /// Procesa la lista de drones pendientes (recibidos del WebSocket pero aún no procesados)
    /// -> Crea nuevos drones si hay IDs nuevos, mueve los drones existentes a su nueva posición y rota según el rumbo.
    /// </summary>
    private void ProcesarDrones(List<DroneData> dataList) {
        // Si el mapa no está asignado, no podemos calcular posiciones -> se muestra un error
        // El resto del script seguirá funcionando, pero no se mostrarán los drones en el mapa
        if (map == null) {
            Debug.LogError("❌ [UNITY ERROR] ¡No has arrastrado el MAPA a la casilla del script!");
            return;
        }

        // Procesa cada dron recibido en la lista
        foreach (DroneData dron in dataList) {
            // Crea el dron en caso de que no exista
            if (!dronesActivos.ContainsKey(dron.id)) {
                Debug.Log($"🏭 [UNITY] Detectado Nuevo Dron ID: {dron.id}. Intentando crear...");

                // Instancia el prefab (siempre que esté asignada la referencia en el Inspector)
                if (dronePrefab != null) {
                    GameObject nuevoDron = Instantiate(dronePrefab);
                    nuevoDron.name = "Dron_" + dron.id;
                    dronesActivos.Add(dron.id, nuevoDron); // Se añade el dron a la lista de drones activos (ID -> GameObject)
                    
                    //Script para identificar el dron
                    DroneController droneController = nuevoDron.GetComponent<DroneController>();

                    if (droneController != null) { 
                        droneController.myId = dron.id;
                        droneController.wsClient = this;
                    }


                    /*// Rotación del dron según el rumbo recibido...
                    Vector3 rotacionActual = dronAActualizar.transform.rotation.eulerAngles;
                    dronAActualizar.transform.rotation = Quaternion.Euler(rotacionActual.x, dron.rumbo, rotacionActual.z);*/

                    // NUEVO: Guardamos toda la información actualizada en la agenda
                    ultimaDataDrones[dron.id] = dron;

                    Debug.Log($"✅ [UNITY] ¡Dron {dron.id} CREADO CORRECTAMENTE en la escena!");
                }
                else {
                    Debug.LogError("❌ [UNITY ERROR] ¡Falta asignar el 'Drone Prefab' en el Inspector!");
                    continue;
                }
            }

            // Actualiza la información del dron en la agenda (aunque ya exista, para tener siempre los datos más recientes)
            ultimaDataDrones[dron.id] = dron;

            // Accede al objeto (Instancia del prefab de dron)
            GameObject dronAActualizar = dronesActivos[dron.id];

            // Calculo de la posición en Unity a partir de la latitud y longitud usando Mapbox
            var mapPos = new Mapbox.Utils.Vector2d(dron.latitud, dron.longitud);
            Vector3 pos = map.GeoToWorldPosition(mapPos);

            // Calcula la altura del suelo en esa posición usando el mapa de Mapbox y ajuste de la altura del dron en consecuencia (altitud recibida + altura del suelo)
            float alturaSuelo = map.QueryElevationInUnityUnitsAt(mapPos);
            pos.y = alturaSuelo + dron.altitud;

            // Movimiento del dron en la escena (usando Lerp para suavizar el movimiento)
            dronAActualizar.transform.position = Vector3.Lerp(dronAActualizar.transform.position, pos, Time.deltaTime * 10f);

            // Rotacióin del dron según el rumbo recibido (solo rota en el eje Y, manteniendo la rotación en X y Z)
            Vector3 rotacionActual = dronAActualizar.transform.rotation.eulerAngles;
            dronAActualizar.transform.rotation = Quaternion.Euler(rotacionActual.x, dron.rumbo, rotacionActual.z);
        }

        // Actualiza las variables serializadas -> se pueden ver en el Inspector de Unity para debuggear o usarlas desde otros scripts
        // (como DroneMapController para mostrar la posición del dron principal)
        if (dataList.Count > 0) {
            var data = dataList[0];
            this.latitude = data.latitud;
            this.longitude = data.longitud;
            this.altitude = data.altitud;
            this.yawDeg = data.rumbo;
        }
    }

    /// <summary>
    /// Cierra y limpia la conexión al destruir el objeto
    /// </summary>
    private void OnDestroy() {
        if (ws != null) {
            // Desuscribe los eventos para evitar callbacks tras destruir el objeto
            ws.OnOpen -= OnWsOpen;
            ws.OnMessage -= OnWsMessage;
            ws.OnError -= OnWsError;
            ws.OnClose -= OnWsClose;

            try { ws.Close(); } catch { }
            ws = null;
        }
    }

    /// <summary>
    /// Conecta al servidor WebSocket
    /// </summary>
    private void Connect() {
        // Compueba que la URL no esté vacía o sea solo espacios
        if (string.IsNullOrWhiteSpace(serverUrl)) {
            Debug.LogError("URL de servidor WebSocket inválida.");
            return;
        }

        // Si ya hay una conexión abierta o en proceso, no hace nada, si no está abierta ni en proceso, pero existe, trata de cerrarla 
        if (ws != null) {
            if (ws.ReadyState == WebSocketState.Open || ws.ReadyState == WebSocketState.Connecting)
                return;

            try { ws.Close(); } catch { }
            ws = null;
        }

        // Crea conexión con el Sevidor WebSocket a través de la URL configurada
        ws = new WebSocket(serverUrl);

        // Gestiona los eventos del WebSocket (apertura, mensaje recibido -> JSON, error y cierre)
        ws.OnOpen += OnWsOpen;
        ws.OnMessage += OnWsMessage;
        ws.OnError += OnWsError;
        ws.OnClose += OnWsClose;

        // Intenta conectar (asíncrono, no bloquea el hilo principal de Unity)
        ws.ConnectAsync();
    }

    /// <summary>
    /// Evento: apertura de nueva conexión WebSocket (conexión establecida con el servidor)
    /// </summary>
    private void OnWsOpen(object sender, System.EventArgs e) {
        Debug.Log("🟢 [UNITY] Conectado al servidor WebSocket.");
    }

    /// <summary>
    /// Evento: mensaje recibido del servidor WebSocket (telemetría de los drones -> JSON)
    /// </summary>
    private void OnWsMessage(object sender, MessageEventArgs e) {
        if (!e.IsText) return;

        // Muestra la información de telemetría recibida (puede llenar mucho la consola)
        //Debug.Log("📩 [UNITY] Recibido JSON: " + e.Data);

        try {
            // Deserializa los datos recibidos -> JSON a una lista de objetos DroneData
            List<DroneData> incomingList = JsonConvert.DeserializeObject<List<DroneData>>(e.Data);

            // Almacena los datos en datosRecibidosPendientes de forma segura, para gestionarlos más adelante en el Update
            lock (candado) {
                datosRecibidosPendientes = incomingList;
            }
        }
        catch (System.Exception ex) {
            Debug.LogError("💥 [UNITY ERROR] Error parseando JSON: " + ex.Message);
        }
    }

    /// <summary>
    /// Evento: error en el socket
    /// </summary>
    private void OnWsError(object sender, ErrorEventArgs e) {
        Debug.LogError("🔴 [UNITY ERROR] en WebSocket: " + e.Message);
    }

    /// <summary>
    /// Evento: conexión cerrada
    /// </summary>
    private void OnWsClose(object sender, CloseEventArgs e) {
        Debug.Log("🔸 [UNITY] Conexión cerrada: " + e.Reason);
    }

    /// <summary>
    /// Envía comando de velocidad de todos los drones
    /// </summary>
    public void SendSetSpeed(float speed) {
        if (!IsOpen()) {
            Debug.LogWarning("WebSocket no conectado. No se puede enviar velocidad.");
            return;
        }

        foreach (int droneId in dronesActivos.Keys) {
            var payload = new { command = "set_speed", id = droneId, speed = speed };
            ws.Send(JsonConvert.SerializeObject(payload));
        }

        Debug.Log($"Enviada nueva velocidad: {speed} m/s a todos los drones");
    }

    /// <summary>
    /// Envía comando de velocidad
    /// </summary>
    public void SendSetSpeed(int droneId, float speed)  {
        if (!IsOpen())
        {
            Debug.LogWarning("WebSocket no conectado. No se puede enviar velocidad.");
            return;
        }

        var payload = new { command = "set_speed", id = droneId, speed = speed };
        ws.Send(JsonConvert.SerializeObject(payload));
        Debug.Log($"Enviada nueva velocidad: {speed} m/s");
    }

    /// <summary>
    /// Envía una misión (lista de WayPoints)
    /// </summary>
    public void SendMission(int droneId, IEnumerable<MissionWaypoint> waypoints) {
        if (!IsOpen()) {
            Debug.LogWarning("WebSocket no conectado. No se puede enviar la misión.");
            return;
        }

        var payload = new { command = "upload_mission", id = droneId, waypoints = waypoints };
        ws.Send(JsonConvert.SerializeObject(payload));
        Debug.Log($"Enviada misión con {waypoints.Count()} waypoints al servidor.");
    }
    /// <summary>
    /// Inicia una misión enviada
    /// </summary>
    public void StartMission(int droneId) {
        if (!IsOpen()) {
            Debug.LogWarning("WebSocket no conectado. No se puede iniciar la misión.");
            return;
        }

        var payload = new { command = "start_mission", id = droneId };
        ws.Send(JsonConvert.SerializeObject(payload));
    }

    /// <summary>
    /// Fija la base para todos los drones con los datos de latitud, longitud y altura recibidos por parámetro
    /// </summary>
    public void SendSetHome(double lat, double lon, double alt = 0) {
        if (!IsOpen()) {
            Debug.LogWarning("WebSocket no conectado. No se puede fijar la base.");
            return;
        }

        foreach (int droneId in dronesActivos.Keys) {
            var payload = new { command = "set_home", id = droneId, lat, lon, alt };
            ws.Send(JsonConvert.SerializeObject(payload));
            Debug.Log($"[Dron {droneId}] " + $"Enviado set_home -> ({lat:F6}, {lon:F6}, alt {alt} m).");
        }
            
    }

    /// <summary>
    /// Fija la base para el dron con id droneId con los datos de latitud, longitud y altura recibidos por parámetro
    /// </summary>
    public void SendSetHome(int droneId, double lat, double lon, double alt = 0)
    {
        if (!IsOpen())
        {
            Debug.LogWarning("WebSocket no conectado. No se puede fijar la base.");
            return;
        }

        var payload = new { command = "set_home", id = droneId, lat, lon, alt };
        ws.Send(JsonConvert.SerializeObject(payload));
        Debug.Log($"Enviado set_home -> ({lat:F6}, {lon:F6}, alt {alt} m).");
    }

    /// <summary>
    /// Ordena a los drones que vuelvan a la base (Return-To-Launch)
    /// </summary>
    public void SendReturnToLaunch() {

        if (!IsOpen()) {
            Debug.LogWarning("WebSocket no conectado. No se puede enviar RTL.");
            return;
        }

        foreach(int droneId in dronesActivos.Keys){
            var payload = new { command = "return_to_launch", id = droneId };
            ws.Send(JsonConvert.SerializeObject(payload));
            Debug.Log($"[Dron {droneId}] Enviada orden RTL (Return To Launch)."); 
        }
    }


    /// <summary>
    /// Ordena a los drones que vuelvan a la base (Return-To-Launch)
    /// </summary>
    public void SendReturnToLaunch(int droneId) {
        if (!IsOpen()) {
            Debug.LogWarning("WebSocket no conectado. No se puede enviar RTL.");
            return;
        }

        var payload = new { command = "return_to_launch", id = droneId };
        ws.Send(JsonConvert.SerializeObject(payload));
        Debug.Log("Enviado return_to_launch.");
    }

    /// <summary>
    /// Ajusta nivel de batería simulado al nivel recibido por parámetro (entre 0 y 100)
    /// </summary>
    public void SendSetBatteryLevel(int droneId, float level) {
        if (!IsOpen()) {
            Debug.LogWarning("WebSocket no conectado. No se puede actualizar batería.");
            return;
        }

        var payload = new { command = "set_battery_level", id = droneId, level = level };
        ws.Send(JsonConvert.SerializeObject(payload));
        Debug.Log(JsonConvert.SerializeObject(payload));
        Debug.Log($"Enviado nivel de batería simulado: {level}%");
    }

    /// <summary>
    /// Reanuda misión (desde índice guardado -> punto en el que se había quedado previamente)
    /// </summary>
    public void SendResumeMission(int droneId) {
        if (!IsOpen()) {
            Debug.LogWarning("WebSocket no conectado. No se puede reanudar la misión.");
            return;
        }

        var payload = new { command = "resume_mission", id = droneId };
        ws.Send(JsonConvert.SerializeObject(payload));
        Debug.Log("Enviado resume mission.");
    }

    /// <returns>
    /// True si el socket existe y está abierto
    /// </returns>
    private bool IsOpen() {
        return ws != null && ws.ReadyState == WebSocketState.Open;
    }

    /// <summary>
    /// Getter de la bateria
    /// </summary>
    public BatteryData GetBatteryData(int droneId) {
        if (!IsOpen()) {
            Debug.LogWarning("WebSocket no conectado. No se puede reanudar la misión.");
            return null;
        }

        // Buscamos si tenemos datos guardados para este ID
        if (ultimaDataDrones.ContainsKey(droneId)) {

            // Cogemos los datos planos del dron
            DroneData data = ultimaDataDrones[droneId];
             
            BatteryData info = new BatteryData {
                level = data.level,
                flightMode = data.flightMode,
                isArmed = data.isArmed,
                altitude = data.altitud  
            };

            return info;
        }

        // Si el ID no existe en la agenda (aún no han llegado datos de él)
        Debug.LogWarning($"[Dron {droneId}] Aún no hay datos de telemetría para este dron.");
        return null;

    }

}