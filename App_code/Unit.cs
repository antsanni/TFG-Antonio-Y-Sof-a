using UnityEngine;

public class Unit : MonoBehaviour
{
    [SerializeField] UnitIndicator indicatorPrefab;   // Tu prefab del paso 1
    [SerializeField] Canvas iconsCanvas;              // El IconsCanvas de la escena

    UnitIndicator indicatorInstance;

    void Start()
    {
        indicatorInstance = Instantiate(indicatorPrefab, iconsCanvas.transform);
        indicatorInstance.target = transform;
    }

    void OnDestroy()
    {
        if (indicatorInstance) Destroy(indicatorInstance.gameObject);
    }
}
