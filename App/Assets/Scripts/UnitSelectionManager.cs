using UnityEngine;

public class UnitSelectionManager : MonoBehaviour
{
    [SerializeField] private CameraController cam;

    private static UnitIndicator current;
    public static UnitSelectionManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    public static void Select(UnitIndicator ui)
    {
        if (current) current.SetSelected(false);
        current = ui;
        current.SetSelected(true);

        Instance.cam.StartFollowing(ui.target);
    }

    public static void Clear()
    {
        if (current == null) return;

        current.SetSelected(false);
        current = null;
    }
}