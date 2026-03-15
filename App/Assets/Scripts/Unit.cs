using Unity.VisualScripting;
using UnityEngine;

public class Unit : MonoBehaviour
{
    [SerializeField] UnitIndicator indicatorPrefab;   // Tu prefab del paso 1
    [SerializeField] GameObject iconsCanvas;              // El IconsCanvas de la escena

    UnitIndicator indicatorInstance;

    void Start()
    {
        //iconsCanvas = FindObjectOfType<Canvas>();
        iconsCanvas = GameObject.Find("CanvasIcons");
        if (iconsCanvas == null) Debug.Log("Canvas no encontrado");
        else Debug.Log("Canvas encontrado: " + iconsCanvas.name);
        indicatorInstance = Instantiate(indicatorPrefab, iconsCanvas.transform);
        indicatorInstance.target = transform;
    }

    void OnDestroy()
    {
        if (indicatorInstance) Destroy(indicatorInstance.gameObject);
    }
}
