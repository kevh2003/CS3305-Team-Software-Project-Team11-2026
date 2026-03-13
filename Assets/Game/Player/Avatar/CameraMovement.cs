using UnityEngine;
using UnityEngine.InputSystem;

// Applies local look rotation from mouse/controller input with saved sensitivity.
public class CameraMovement : MonoBehaviour
{
    private const string PrefSensitivity = "settings_sensitivity";

    [Header("Settings")]
    public float sensitivity = 2f;
    public float minY = -40f;
    public float maxY = 40f;

    private float rotationX = 0f;
    private float rotationY = 0f;

    public void SetSensitivity(float value)
    {
        sensitivity = Mathf.Clamp(value, 0.02f, 2f);
    }

    void OnEnable()
    {
        sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(PrefSensitivity, sensitivity), 0.02f, 2f);

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

        Vector2 mouseDelta = mouse.delta.ReadValue();

        // Apply sensitivity
        float adjustedSensitivity = sensitivity * 1.0f;
        
        rotationX -= mouseDelta.y * adjustedSensitivity;
        rotationX = Mathf.Clamp(rotationX, minY, maxY);
        
        rotationY += mouseDelta.x * adjustedSensitivity;

        // Apply rotation
        transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0f);
    }
}