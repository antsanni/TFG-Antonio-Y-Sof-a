using System.Net.WebSockets;
using TMPro;
using UnityEngine;

public class SpeedManagement : MonoBehaviour {
    public TMP_Text DesiredSpeedTxt;
    public float currentSpeed = 5f;

    public float STEP = 5f;
    public float MIN_SPEED = 1f;
    public float MAX_SPEED = 20f;

    public DroneWSClient _ws;

    public void IncreaseSpeed() {
        currentSpeed = Mathf.Min(currentSpeed + STEP, MAX_SPEED);
        _ws.SendSetSpeed(currentSpeed);
        UpdateText();
    }

    public void DecreaseSpeed() {
        currentSpeed = Mathf.Max(currentSpeed - STEP, MIN_SPEED);
        _ws.SendSetSpeed(currentSpeed);
        UpdateText();
    }

    private void UpdateText()
    {
        if (DesiredSpeedTxt != null)
            DesiredSpeedTxt.text = $"{currentSpeed:0.0} m/s";
    }

    private void Start() {
        UpdateText();
    }
}