using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public float sensitivity = 2f;
    public float minY = -40f;
    public float maxY = 40f;

    private float rotationX = 0f;
    private float rotationY = 0f;

    void OnEnable()
    {
        // Properly initialize rotation from current transform
        Vector3 currentRotation = transform.localEulerAngles;
        
        // Convert to -180 to 180 range for proper clamping
        rotationX = currentRotation.x;
        if (rotationX > 180f) rotationX -= 360f;
        
        rotationY = currentRotation.y;
        if (rotationY > 180f) rotationY -= 360f;
        
        // Clamp to limits
        rotationX = Mathf.Clamp(rotationX, minY, maxY);
    }

    void Update()
    {
        float mouseY = Input.GetAxis("Mouse Y");
        float mouseX = Input.GetAxis("Mouse X");

        // Update rotations
        rotationX -= mouseY * sensitivity;
        rotationX = Mathf.Clamp(rotationX, minY, maxY);
        
        rotationY += mouseX * sensitivity;

        // Apply rotation
        transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0f);
    }
}



