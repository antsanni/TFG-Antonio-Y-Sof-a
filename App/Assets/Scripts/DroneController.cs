using UnityEngine;

public class DroneController : MonoBehaviour {
    [Header("Identidad del Dron")]
    public int myId;

    [HideInInspector] 
    public DroneWSClient wsClient; 

    public void Command_ReturnToLaunch() {
        if (wsClient != null) {
            Debug.Log($"[Dron {myId}] Solicitando RTL...");
            wsClient.SendReturnToLaunch(myId);
        }
    }

    public void Command_SetSpeed(float speed) {
        if (wsClient != null) wsClient.SendSetSpeed(myId, speed);
    }

    public void Command_SetBattery(float level) {
        if (wsClient != null) wsClient.SendSetBatteryLevel(myId, level);
    }

    public void Command_ResumeMission() {
        if (wsClient != null) wsClient.SendResumeMission(myId);
    }
}