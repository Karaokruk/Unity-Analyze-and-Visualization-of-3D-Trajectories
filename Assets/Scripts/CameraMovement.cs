using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{

    [SerializeField]
    [Range(0.0f, 1.0f)]
    private float sensitivity = 1.0f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float xAxisValue = Input.GetAxis("Horizontal");
        float zAxisValue = Input.GetAxis("Vertical");
        if (Camera.current != null) Camera.current.transform.Translate(new Vector3(xAxisValue, 0.0f, zAxisValue) * sensitivity);
    }
}
