using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(Image))]
public class UnitIndicator : MonoBehaviour, IPointerClickHandler
{
    public Transform target;

    [Header("Sprites")]
    public Sprite normalSprite;
    public Sprite selectedSprite;

    [Header("Ajustes de posición")]
    public float worldOffset = 2f;
    public float edgePadding = 40f;
    [SerializeField] private float pixelOffsetY = 40f;

    private RectTransform rectTf;
    private Camera cam;
    private Image img;

    private void Awake()
    {
        rectTf = GetComponent<RectTransform>();
        img = GetComponent<Image>();
        cam = Camera.main;

        if (normalSprite == null)
            normalSprite = img.sprite;
    }

    private void LateUpdate()
    {
        if (!target)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 worldPos = target.position + Vector3.up * worldOffset;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        bool behindCamera = screenPos.z < 0f;
        if (behindCamera) screenPos *= -1f;

        screenPos.y += pixelOffsetY;

        bool inside =
            screenPos.x > 0 && screenPos.x < Screen.width &&
            screenPos.y > 0 && screenPos.y < Screen.height &&
            !behindCamera;

        if (inside)
        {
            rectTf.rotation = Quaternion.identity;
        }
        else
        {
            screenPos.x = Mathf.Clamp(screenPos.x, edgePadding, Screen.width - edgePadding);
            screenPos.y = Mathf.Clamp(screenPos.y, edgePadding, Screen.height - edgePadding);

            Vector2 dir = (screenPos - new Vector3(Screen.width * 0.5f, Screen.height * 0.5f)).normalized;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f;
            rectTf.rotation = Quaternion.Euler(0, 0, ang);
        }

        rectTf.position = screenPos;
    }

    public void OnPointerClick(PointerEventData _)
    {
        UnitSelectionManager.Select(this);
    }

    // Cambia sprite según si está seleccionado
    public void SetSelected(bool isSelected)
    {
        if (selectedSprite == null) return;
        img.sprite = isSelected ? selectedSprite : normalSprite;
    }
}