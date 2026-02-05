using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook : MonoBehaviour
{
    public float MouseSens = 200f;
    public Transform cam;

    private float xRotation = 0f;
    private Vector2 lookInput;


    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
    }

    void Update()
    {
        HandleMouseLook();
    }

    public void OnLook(InputValue value){
        lookInput = value.Get<Vector2>();
    }

    void HandleMouseLook(){
        float mousex = lookInput.x * MouseSens * Time.deltaTime;
        float mousey = lookInput.y * MouseSens * Time.deltaTime;

        xRotation -= mousey;
        xRotation = Mathf.Clamp(xRotation, -90,90);

        cam.localRotation = Quaternion.Euler(xRotation,0f,0f);
        transform.Rotate(Vector3.up * mousex);

    }





}
