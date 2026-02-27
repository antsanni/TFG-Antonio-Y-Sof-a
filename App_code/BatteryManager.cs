using UnityEngine;
using System.Collections;

public class BatteryManager : MonoBehaviour
{
    public DroneWSClient droneClient;
    public float batteryThreshold = 15f;
    public float batteryLevel;
    public bool isCharging = false;
    public bool isRTLInProgress = false;

    void Update()
    {
        batteryLevel = droneClient.batteryLevel;

        bool droneFlying = droneClient.isArmed &&
                           droneClient.altitude > 2f &&
                           droneClient.flightMode != "RTL";

        if (!isCharging && !isRTLInProgress && batteryLevel <= batteryThreshold && droneFlying)
        {
            Debug.Log("Batería baja. Iniciando RTL.");
            isRTLInProgress = true;
            droneClient.SendReturnToLaunch();
            StartCoroutine(HandleBatteryRecharge());
        }
    }

    IEnumerator HandleBatteryRecharge()
    {
        Debug.Log("Esperando a que el dron inicie el RTL...");

        while (droneClient.flightMode != "RTL" && droneClient.flightMode != "LAND")
        {
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log("Esperando a que el dron aterrice...");

        while (droneClient.isArmed || droneClient.altitude > 1.0f)
        {
            yield return new WaitForSeconds(1f);
        }

        Debug.Log("Dron ha aterrizado. Iniciando carga...");

        isCharging = true;

        float startingBattery = droneClient.batteryLevel;
        float duration = 60f;
        float timer = 0f;

        while (timer < duration)
        {
            float simulatedBattery = Mathf.Lerp(startingBattery, 100f, timer / duration);
            droneClient.batteryLevel = simulatedBattery;
            timer += Time.deltaTime;
            yield return null;
        }

        droneClient.batteryLevel = 100f;
        isCharging = false;
        isRTLInProgress = false;

        droneClient.SendSetBatteryLevel(100f);

        Debug.Log("Carga completa. Relanzando misión.");
        droneClient.SendResumeMission();
    }
}