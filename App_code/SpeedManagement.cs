using TMPro;
using UnityEngine;

public class SpeedManagement : MonoBehaviour
{
    public DroneWSClient client;
    public TMP_Text DesiredSpeedTxt;
    public float currentSpeed = 5f;

    public float STEP = 5f;
    public float MIN_SPEED = 1f;
    public float MAX_SPEED = 20f;

    public void IncreaseSpeed()
    {
        currentSpeed = Mathf.Min(currentSpeed + STEP, MAX_SPEED);
        client.SendSetSpeed(currentSpeed);
        UpdateText();
    }

    public void DecreaseSpeed()
    {
        currentSpeed = Mathf.Max(currentSpeed - STEP, MIN_SPEED);
        client.SendSetSpeed(currentSpeed);
        UpdateText();
    }

    private void UpdateText()
    {
        if (DesiredSpeedTxt != null)
            DesiredSpeedTxt.text = $"{currentSpeed:0.0} m/s";
    }

    private void Start()
    {
        UpdateText();
    }
}