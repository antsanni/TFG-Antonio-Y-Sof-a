using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class IconsLayer : MonoBehaviour
{
    public static IconsLayer Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
}