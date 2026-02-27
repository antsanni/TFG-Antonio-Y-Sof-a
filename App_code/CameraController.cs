using UnityEngine;
using UnityEngine.UI;

public class CameraController : MonoBehaviour
{
    [Header("Referencias")]
    public Transform pivot;
    public Camera cam;
    public Toggle toggle;

    [Header("Inclinación (botón central)")]
    public float tiltSpeed = 50f;
    public float minTiltAngle = 45f;
    public float maxTiltAngle = 90f;

    [Header("Desplazamiento (botón derecho)")]
    public float panSpeed = 10f;

    [Header("Zoom con Scroll (en el eje Y del pivot)")]
    public float zoomSpeed = 200f;
    public float minPivotHeight = 50f;
    public float maxPivotHeight = 500f;
    public float minOrtoHeight = 50f;
    public float maxOrtoHeight = 500f;

    private float currentTilt;
    private Transform cameraTransform;

    [Header("Modo Ortográfico (Cenital)")]
    public Vector3 cenitalPivotPosition = new Vector3(0f, 200f, -10f);
    public Vector3 cenitalCameraRotation = new Vector3(90f, 0f, 0f);
    public float orthoSize = 255f;

    [Header("Modo Perspectiva (Original)")]
    private bool originalOrthographic;
    private float originalFieldOfView;

    private bool isToggleOn;

    [Header("Seleccion de unidad")]
    public float followHeight = 15f;
    public Vector3 followOffset = new(0, 15, -20);
    public float followTilt = 45f;

    private Transform followTarget;
    private bool isFollowing;

    private void Awake()
    {
        cameraTransform = cam.transform;
        originalOrthographic = cam.orthographic;
        originalFieldOfView = cam.fieldOfView;
        toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    private void Start()
    {
        currentTilt = cameraTransform.localEulerAngles.x;
    }

    private void Update()
    {
        HandleTilt();
        HandlePan();
        HandleZoom();
    }

    private void LateUpdate()
    {
        if (isFollowing && followTarget)
        {
            Vector3 desired = followTarget.position + followOffset;
            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * 5f);
        }
    }

    private void OnDestroy()
    {
        toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }

    // Activa/desactiva modo ortográfico cenital
    private void OnToggleChanged(bool isOn)
    {
        isToggleOn = isOn;
        if (isOn)
        {
            pivot.position = cenitalPivotPosition;
            cam.transform.localEulerAngles = cenitalCameraRotation;
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
        }
        else
        {
            cam.orthographic = originalOrthographic;
            cam.fieldOfView = originalFieldOfView;
        }
    }

    // Inclinación de la cámara con botón central
    private void HandleTilt()
    {
        if (isToggleOn) return;
        if (!Input.GetMouseButton(2)) return;

        isFollowing = false;
        UnitSelectionManager.Clear();

        currentTilt = cameraTransform.localEulerAngles.x;
        float mouseY = Input.GetAxis("Mouse Y");
        float tiltDelta = -mouseY * tiltSpeed * Time.deltaTime;
        currentTilt = Mathf.Clamp(currentTilt + tiltDelta, minTiltAngle, maxTiltAngle);

        Vector3 localRotation = cameraTransform.localEulerAngles;
        localRotation.x = currentTilt;
        cameraTransform.localEulerAngles = localRotation;
    }

    // Desplazamiento de la cámara con botón derecho
    private void HandlePan()
    {
        if (!Input.GetMouseButton(1)) return;

        isFollowing = false;
        UnitSelectionManager.Clear();

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        Vector3 move = new Vector3(-mouseX, 0, -mouseY) * panSpeed * Time.deltaTime;
        transform.Translate(move, Space.Self);
    }

    // Zoom con la rueda del ratón
    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Approximately(scroll, 0f)) return;

        isFollowing = false;
        UnitSelectionManager.Clear();

        if (isToggleOn)
        {
            float newSize = cam.orthographicSize - (scroll * zoomSpeed * 1000f * Time.deltaTime);
            newSize = Mathf.Clamp(newSize, minOrtoHeight, maxOrtoHeight);
            cam.orthographicSize = newSize;
        }
        else
        {
            float currentHeight = transform.position.y;
            currentHeight -= scroll * zoomSpeed * 1000f * Time.deltaTime;
            currentHeight = Mathf.Clamp(currentHeight, minPivotHeight, maxPivotHeight);

            Vector3 newPos = transform.position;
            newPos.y = currentHeight;
            transform.position = newPos;
        }
    }

    // Activa seguimiento a una unidad
    public void StartFollowing(Transform t)
    {
        Vector3 eul = cam.transform.localEulerAngles;
        eul.x = followTilt;
        cam.transform.localEulerAngles = eul;

        followTarget = t;
        isFollowing = true;
    }
}
