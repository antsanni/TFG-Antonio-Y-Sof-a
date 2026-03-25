using UnityEngine;
using System.Collections;

public class BatteryManager : MonoBehaviour {
    public float batteryThreshold = 15f;
    public float batteryLevel;
    public bool isCharging = false;
    public bool isRTLInProgress = false;

    private DroneController _droneController;

    private void Start() {
        _droneController = GetComponent<DroneController>();
    }

    void Update() {
        batteryUpdate();
    }

    private void batteryUpdate() {
        DroneWSClient.BatteryData batteryData = _droneController.wsClient.GetBatteryData(_droneController.myId);
        if (batteryData == null) return; // Evitar errores si no se obtiene la información de la batería

        //Debug.Log($"[BatteryManager] Nivel de batería: {batteryData?.level}%");
        batteryLevel = batteryData.level;

        bool droneFlying = batteryData.isArmed &&
                           batteryData.altitude > 2f &&
                           batteryData.flightMode != "RTL" &&
                           batteryData.flightMode != "LAND";

        if (!isCharging && !isRTLInProgress && batteryLevel <= batteryThreshold && droneFlying)  {
            Debug.Log("Batería baja. Iniciando RTL.");
            isRTLInProgress = true;
            _droneController.Command_ReturnToLaunch();
            StartCoroutine(HandleBatteryRecharge());
        }
    }

    IEnumerator HandleBatteryRecharge()
    {
        Debug.Log("Esperando a que el dron inicie el RTL...");

        // Actualizar datos cada medio segundo para detectar el cambio a modo RTL o LAND
        while (true) {
            var currentData = _droneController.wsClient.GetBatteryData(_droneController.myId);
            if (currentData != null && (currentData.flightMode == "RTL" || currentData.flightMode == "LAND"))
                break; // Salimos del bucle cuando cambie el modo
            
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log("Esperando a que el dron aterrice...");

        while (true) {
            var currentData = _droneController.wsClient.GetBatteryData(_droneController.myId);
            // Asumimos que ha aterrizado si se desarma o la altura es menor a 1 metro
            if (currentData != null && (!currentData.isArmed || currentData.altitude <= 1.0f))
                break;
            
            yield return new WaitForSeconds(1f);
        }

        Debug.Log("Dron ha aterrizado. Iniciando carga...");

        isCharging = true;

        // Dato actual para saber desde dónde empezar la animación de carga
        var startData = _droneController.wsClient.GetBatteryData(_droneController.myId);
        float startingBattery = startData != null ? startData.level : 0f;
        float duration = 60f;
        float timer = 0f;

        while (timer < duration) {
            float simulatedBattery = Mathf.Lerp(startingBattery, 100f, timer / duration);

            // Actualizamos la variable para que la UI de Unity lo refleje
            batteryLevel = simulatedBattery;

            timer += Time.deltaTime;
            yield return null;
        }

        isCharging = false;
        isRTLInProgress = false;

        // Mandar al servidor Python la orden de reiniciar la batería real
        _droneController.Command_SetBattery(100f);

        Debug.Log("Carga completa. Relanzando misión.");
        _droneController.Command_ResumeMission();
    }
}