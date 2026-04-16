using UnityEngine;
using System.Collections;

public class BatteryManager : MonoBehaviour
{
    // 1. Umbral cambiado a 30%
    public float batteryThreshold = 30f;
    public float batteryLevel;
    public bool isCharging = false;
    public bool isRTLInProgress = false;

    private DroneController _droneController;

    private void Start()
    {
        _droneController = GetComponent<DroneController>();
    }

    void Update()
    {
        batteryUpdate();
    }

    private void batteryUpdate()
    {
        DroneWSClient.BatteryData batteryData = _droneController.wsClient.GetBatteryData(_droneController.myId);
        if (batteryData == null) return; // Evitar errores si no se obtiene la información de la batería

        batteryLevel = batteryData.level;

        bool droneFlying = batteryData.isArmed &&
                           batteryData.altitude > 2f &&
                           batteryData.flightMode != "RTL" &&
                           batteryData.flightMode != "LAND";

        // 2. Dispara el RTL si baja del 30%
        if (!isCharging && !isRTLInProgress && batteryLevel <= batteryThreshold && droneFlying)
        {
            Debug.Log($"[Dron {_droneController.myId}] Batería baja ({batteryLevel:F0}%). Iniciando RTL para recargar.");
            isRTLInProgress = true;

            // Forzamos el envío de RTL desde el cliente directamente por seguridad
            _droneController.wsClient.SendReturnToLaunch(_droneController.myId);

            StartCoroutine(HandleBatteryRecharge());
        }
    }

    IEnumerator HandleBatteryRecharge()
    {
        Debug.Log($"[Dron {_droneController.myId}] Esperando a que aterrice en la base...");

        // Esperamos hasta que el dron toque el suelo (desarmado o altura muy baja)
        while (true)
        {
            var currentData = _droneController.wsClient.GetBatteryData(_droneController.myId);

            // Si aterriza y se desarma, o se queda a menos de 1m, consideramos que ha llegado
            if (currentData != null && (!currentData.isArmed || currentData.altitude <= 1.0f))
            {
                break;
            }
            yield return new WaitForSeconds(1f);
        }

        Debug.Log($"[Dron {_droneController.myId}] ¡Aterrizado! Iniciando recarga poco a poco...");
        isCharging = true;

        // Miramos cuánta batería le queda al llegar (probablemente menos del 30%)
        var startData = _droneController.wsClient.GetBatteryData(_droneController.myId);
        float startingBattery = startData != null ? startData.level : 0f;

        // 3. ¿Cuánto quieres que tarde en recargar? (He puesto 15 segundos para no hacerte esperar mucho en pruebas)
        float duration = 15f;
        float timer = 0f;

        // 4. Bucle de recarga gradual sincronizado con el servidor
        while (timer < duration)
        {
            timer += 1f; // Avanzamos 1 segundo
            float progress = timer / duration;

            // Calculamos el porcentaje actual (ej: pasa del 25% al 100% de forma suave)
            float simulatedBattery = Mathf.Lerp(startingBattery, 100f, progress);

            // IMPORTANTE: Le enviamos la recarga parcial a Python para que la UI y los demás scripts se enteren
            _droneController.wsClient.SendSetBatteryLevel(_droneController.myId, simulatedBattery);

            // Esperamos un segundo antes del siguiente "chute" de energía
            yield return new WaitForSeconds(1f);
        }

        // Por si acaso los decimales bailan, aseguramos el 100% clavado al final
        _droneController.wsClient.SendSetBatteryLevel(_droneController.myId, 100f);

        isCharging = false;
        isRTLInProgress = false;

        Debug.Log($"[Dron {_droneController.myId}] 🔋 Carga completada. Relanzando misión.");

        // Asumiendo que tu método resume mission está así (o usa la función wrapper de tu DroneController)
        _droneController.wsClient.SendResumeMission(_droneController.myId);
    }
}