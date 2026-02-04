using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMovement : MonoBehaviour
{
    [Header("Settings")]
    public float sensitivity = 2f;
    public float minY = -40f;
    public float maxY = 40f;

    private float rotationX = 0f;
    private float rotationY = 0f;

    void OnEnable()
    {
        // Initialize rotation from current transform
        Vector3 currentRotation = transform.localEulerAngles;
        
        // Convert to -180 to 180 range
        rotationX = currentRotation.x;
        if (rotationX > 180f) rotationX -= 360f;
        
        rotationY = currentRotation.y;
        if (rotationY > 180f) rotationY -= 360f;
        
        // Clamp to limits
        rotationX = Mathf.Clamp(rotationX, minY, maxY);
    }

    void Update()
    {
        // Get mouse input directly
        var mouse = Mouse.current;
        if (mouse == null) return; // No mouse connected

        // Read mouse delta (how much the mouse moved this frame)
        Vector2 mouseDelta = mouse.delta.ReadValue();

        // Apply sensitivity - adjusted to feel similar to old Input.GetAxis
        float adjustedSensitivity = sensitivity * 0.1f;
        
        rotationX -= mouseDelta.y * adjustedSensitivity;
        rotationX = Mathf.Clamp(rotationX, minY, maxY);
        
        rotationY += mouseDelta.x * adjustedSensitivity;

        // Apply rotation
        transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0f);
    }
}



