using UnityEngine;
using System.Collections;

public class BatteryManager : MonoBehaviour {
    public float batteryThreshold = 15f;
    public float batteryLevel;
    public bool isCharging = false;
    public bool isRTLInProgress = false;

    private DroneController _droneController;

    void Update() {
        batteryUpdate();
    }

    private void batteryUpdate() {
        DroneWSClient.BatteryData batteryData = _droneController.wsClient.GetBatteryData(_droneController.myId);
        batteryLevel = batteryData.level;

        bool droneFlying = batteryData.isArmed &&
                           batteryData.altitude > 2f &&
                           batteryData.flightMode != "RTL";

        if (!isCharging && !isRTLInProgress && batteryLevel <= batteryThreshold && droneFlying)
        {
            Debug.Log("Batería baja. Iniciando RTL.");
            isRTLInProgress = true;
            _droneController.Command_ReturnToLaunch();
            StartCoroutine(HandleBatteryRecharge());
        }
    }
    IEnumerator HandleBatteryRecharge() {
        DroneWSClient.BatteryData batteryData = _droneController.wsClient.GetBatteryData(_droneController.myId);

        Debug.Log("Esperando a que el dron inicie el RTL...");

        while (batteryData.flightMode != "RTL" && batteryData.flightMode != "LAND") {
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log("Esperando a que el dron aterrice...");

        while (batteryData.isArmed || batteryData.altitude > 1.0f) {
            yield return new WaitForSeconds(1f);
        }

        Debug.Log("Dron ha aterrizado. Iniciando carga...");

        isCharging = true;

        float startingBattery = batteryData.level;
        float duration = 60f;
        float timer = 0f;

        while (timer < duration) {
            float simulatedBattery = Mathf.Lerp(startingBattery, 100f, timer / duration);
            batteryData.level = simulatedBattery;
            timer += Time.deltaTime;
            yield return null;
        }

        batteryData.level = 100f;
        isCharging = false;
        isRTLInProgress = false;

        _droneController.Command_SetBattery(100f);

        Debug.Log("Carga completa. Relanzando misión.");
        _droneController.Command_ResumeMission();
    }
}