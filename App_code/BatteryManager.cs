using UnityEngine;
using System.Collections;

public class BatteryManager : MonoBehaviour
{
    [Header("Cálculo Dinámico de Batería")]
    [Tooltip("Velocidad de vuelta a casa en m/s (ArduPilot suele volver a 5 m/s)")]
    public float rtlSpeed = 5.0f;
    [Tooltip("Porcentaje de batería que gasta por segundo (0.033 * 10 ticks = 0.33)")]
    public float batteryDrainPerSec = 0.33f;
    [Tooltip("Margen de seguridad extra en % (para el descenso y maniobras)")]
    public float safetyMargin = 8.0f;

    public float batteryLevel;
    public bool isCharging = false;
    public bool isRTLInProgress = false;

    private DroneController _droneController;
    private Transform _baseTransform;

    private void Start()
    {
        _droneController = GetComponent<DroneController>();
        StartCoroutine(FindBaseCoroutine());
    }

    private IEnumerator FindBaseCoroutine()
    {
        yield return new WaitForSeconds(2f);

        GameObject baseObj = GameObject.Find("Base_Inicial_Dron_" + _droneController.myId);
        if (baseObj != null)
        {
            _baseTransform = baseObj.transform;
            Debug.Log($"[Dron {_droneController.myId}] 🎯 Base localizada para cálculos de batería.");
        }
    }

    void Update()
    {
        batteryUpdate();
    }

    private void batteryUpdate()
    {
        DroneWSClient.BatteryData batteryData = _droneController.wsClient.GetBatteryData(_droneController.myId);
        if (batteryData == null) return;

        batteryLevel = batteryData.level;

        bool droneFlying = batteryData.isArmed &&
                           batteryData.altitude > 2f &&
                           batteryData.flightMode != "RTL" &&
                           batteryData.flightMode != "LAND";

        float umbralDinamico = 20f;

        if (_baseTransform != null)
        {
            float distance = Vector3.Distance(transform.position, _baseTransform.position);
            float timeToHome = distance / rtlSpeed;
            float batteryNeeded = timeToHome * batteryDrainPerSec;
            umbralDinamico = batteryNeeded + safetyMargin;
        }

        if (!isCharging && !isRTLInProgress && batteryLevel <= umbralDinamico && droneFlying)
        {
            Debug.Log($"[Dron {_droneController.myId}] Batería ({batteryLevel:F1}%). Necesita {umbralDinamico:F1}% para volver a salvo. ¡Iniciando RTL inteligente!");
            isRTLInProgress = true;

            _droneController.wsClient.SendReturnToLaunch(_droneController.myId);

            StartCoroutine(HandleBatteryRecharge());
        }

        bool droneLanded = !batteryData.isArmed || batteryData.altitude <= 1.0f;
        bool isAtBase = _baseTransform != null && Vector3.Distance(transform.position, _baseTransform.position) < 10.0f;

        if (droneLanded && isAtBase && !isCharging && !isRTLInProgress && batteryLevel < 99.0f)
        {
            Debug.Log($"[Dron {_droneController.myId}] Detectado en base con batería al {batteryLevel:F1}%. Iniciando recarga automática.");
            StartCoroutine(ChargeBatteryRoutine());
        }
    }

    IEnumerator ChargeBatteryRoutine()
    {
        isCharging = true;
        var startData = _droneController.wsClient.GetBatteryData(_droneController.myId);
        float startingBattery = startData != null ? startData.level : 0f;

        float duration = 15f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += 1f;
            float progress = timer / duration;
            float simulatedBattery = Mathf.Lerp(startingBattery, 100f, progress);
            _droneController.wsClient.SendSetBatteryLevel(_droneController.myId, simulatedBattery);
            yield return new WaitForSeconds(1f);
        }

        _droneController.wsClient.SendSetBatteryLevel(_droneController.myId, 100f);
        isCharging = false;
        Debug.Log($"[Dron {_droneController.myId}] 🔋 Batería recargada al 100% en la base.");
    }

    IEnumerator HandleBatteryRecharge()
    {
        Debug.Log($"[Dron {_droneController.myId}] Esperando a que aterrice en la base...");

        while (true)
        {
            var currentData = _droneController.wsClient.GetBatteryData(_droneController.myId);

            if (currentData != null && (!currentData.isArmed || currentData.altitude <= 1.0f))
            {
                break;
            }
            yield return new WaitForSeconds(1f);
        }

        Debug.Log($"[Dron {_droneController.myId}] ¡Aterrizado! Iniciando recarga poco a poco...");
        yield return StartCoroutine(ChargeBatteryRoutine());

        isRTLInProgress = false;

        Debug.Log($"[Dron {_droneController.myId}] 🔋 Carga completada. Relanzando misión.");

        _droneController.wsClient.SendResumeMission(_droneController.myId);
    }
}