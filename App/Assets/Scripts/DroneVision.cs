using UnityEngine;
using System.Collections;
using System;

public class DroneVision : MonoBehaviour
{
    [Header("Configuración de Visión")]
    [Tooltip("La cámara espía que apunta hacia abajo")]
    public Camera visionCamera;

    [Tooltip("RenderTexture donde la cámara dibuja la imagen")]
    public RenderTexture renderTexture;

    [Tooltip("Frecuencia de captura (cuántas imágenes mandamos a Python por segundo)")]
    public float framesPerSecond = 2f;

    [Range(10, 100)]
    [Tooltip("Calidad de compresión JPG (menos calidad = más velocidad en red)")]
    public int jpgQuality = 75;

    private DroneController _droneController;

    void Start()
    {
        _droneController = GetComponent<DroneController>();

        if (visionCamera == null || renderTexture == null)
        {
            Debug.LogError($"[Dron {_droneController?.myId}] Faltan asignar referencias en DroneVision. El dron está ciego.");
            enabled = false;
            return;
        }

        visionCamera.targetTexture = renderTexture;
        StartCoroutine(VisionLoop());
    }

    IEnumerator VisionLoop()
    {
        float waitTime = 1f / framesPerSecond;

        // Esperamos un poco para asegurar que todo cargue
        yield return new WaitForSeconds(3f);

        while (true)
        {
            if (_droneController != null && _droneController.wsClient != null && _droneController.wsClient.IsOpen())
            {
                // Pedimos los datos actuales al WSClient para saber si estamos volando
                var currentData = _droneController.wsClient.GetBatteryData(_droneController.myId);

                // Solo hacemos foto si está armado y a más de 5 metros de altura
                if (currentData != null && currentData.isArmed && currentData.altitude > 5f)
                {
                    yield return StartCoroutine(CaptureAndSendFrame());
                }
            }
            yield return new WaitForSeconds(waitTime);
        }
    }

    IEnumerator CaptureAndSendFrame()
    {
        // 1. Textura 2D temporal
        Texture2D tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);

        // 2. Leemos la cámara
        RenderTexture.active = renderTexture;
        tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        // 3. Comprimimos a JPG
        byte[] jpgBytes = tex.EncodeToJPG(jpgQuality);
        Destroy(tex); // Limpiar memoria de Unity

        // 4. Transformamos a texto
        string base64Image = Convert.ToBase64String(jpgBytes);

        // 5. Usamos la nueva función del DroneWSClient para enviarlo
        _droneController.wsClient.SendProcessImage(_droneController.myId, base64Image);

        yield return null;
    }
}