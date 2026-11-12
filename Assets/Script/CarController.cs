
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Mouse Drag Settings")]
    [SerializeField] private float dragSensitivity = 0.01f;
    [SerializeField] private float horizontalLimit = 4.5f;

    [Header("Movement Settings")]
    [SerializeField] private float maxHorizontalSpeed = 8f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float followStrength = 12f;

    private Rigidbody rb;

    private bool isDragging;

    private float startMouseX;
    private float startCarX;

    private float targetX;
    private float minX;
    private float maxX;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        float initialX = rb.position.x;

        minX = initialX - horizontalLimit;
        maxX = initialX + horizontalLimit;

        targetX = initialX;

        rb.interpolation = RigidbodyInterpolation.Interpolate;

        rb.constraints |= RigidbodyConstraints.FreezeRotation;
    }

    private void Update()
    {
        if (Mouse.current == null)
            return;

        // Start dragging
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            isDragging = true;

            startMouseX = Mouse.current.position.ReadValue().x;

            startCarX = rb.position.x;
        }

        // While dragging
        if (isDragging && Mouse.current.leftButton.isPressed)
        {
            float currentMouseX =
                Mouse.current.position.ReadValue().x;

            float mouseDeltaX = currentMouseX - startMouseX;

            // Convert mouse movement into car movement
            targetX = startCarX + mouseDeltaX * dragSensitivity;

            // Limit horizontal movement
            targetX = Mathf.Clamp(targetX, minX, maxX);
        }

        // Stop dragging
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }
    }

    private void FixedUpdate()
    {
        // Calculate desired horizontal velocity
        float error = targetX - rb.position.x;

        float desiredVelocityX = Mathf.Clamp(
            error * followStrength,
            -maxHorizontalSpeed,
            maxHorizontalSpeed
        );

        // Smooth horizontal movement
        Vector3 velocity = rb.linearVelocity;

        velocity.x = Mathf.MoveTowards(
            velocity.x,
            desiredVelocityX,
            acceleration * Time.fixedDeltaTime
        );

        rb.linearVelocity = velocity;
    }
}
