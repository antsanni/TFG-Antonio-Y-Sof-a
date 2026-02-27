using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class ToggleColorChanger : MonoBehaviour
{
    [SerializeField] private Color offColor = Color.green;
    [SerializeField] private Color onColor = Color.red;

    private Toggle toggle;
    private Graphic targetGraphic;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
        targetGraphic = toggle.targetGraphic;
    }

    private void Start()
    {
        toggle.onValueChanged.AddListener(OnToggleChanged);
        OnToggleChanged(toggle.isOn);
    }

    private void OnToggleChanged(bool isOn)
    {
        if (targetGraphic != null)
            targetGraphic.color = isOn ? onColor : offColor;
    }
}