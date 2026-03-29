using UnityEngine;

public class Billboard : MonoBehaviour {
    Transform camTransform;

    void Start() { 
        if (Camera.main != null) {
            camTransform = Camera.main.transform;
        }
    }

    void LateUpdate() {
        if (camTransform != null) {
            transform.rotation = camTransform.rotation;
        }
    }
}