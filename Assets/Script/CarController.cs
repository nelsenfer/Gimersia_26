
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

    [Header("Steering Wheel Settings")]
    [SerializeField] private Transform steeringWheelPivot;
    [SerializeField] private float maxSteeringAngle = 120f;
    [SerializeField] private float steeringSmoothness = 10f;
    [SerializeField] private Vector3 steeringAxis = Vector3.forward;
    [SerializeField] private bool invertSteering = false;

    private Rigidbody rb;

    private bool isDragging;

    private float startMouseX;
    private float startCarX;

    private float targetX;
    private float minX;
    private float maxX;

    private float steeringInput;
    private Quaternion initialWheelRotation;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        float initialX = rb.position.x;

        minX = initialX - horizontalLimit;
        maxX = initialX + horizontalLimit;

        targetX = initialX;

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints |= RigidbodyConstraints.FreezeRotation;

        if (steeringWheelPivot != null)
        {
            initialWheelRotation =
                steeringWheelPivot.localRotation;
        }
    }

    private void Update()
    {
        if (Mouse.current != null)
        {
            // Start dragging
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                isDragging = true;

                startMouseX =
                    Mouse.current.position.ReadValue().x;

                startCarX = rb.position.x;
                targetX = startCarX;
                steeringInput = 0f;
            }

            // While dragging
            if (isDragging && Mouse.current.leftButton.isPressed)
            {
                float currentMouseX =
                    Mouse.current.position.ReadValue().x;

                float mouseDeltaX =
                    currentMouseX - startMouseX;

                // Move car horizontally
                targetX = startCarX +
                    mouseDeltaX * dragSensitivity;

                targetX = Mathf.Clamp(
                    targetX,
                    minX,
                    maxX
                );

                // Calculate steering direction
                steeringInput = Mathf.Clamp(
                    (targetX - startCarX) /
                    Mathf.Max(horizontalLimit, 0.001f),
                    -1f,
                    1f
                );
            }

            // Release mouse
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                isDragging = false;
                steeringInput = 0f;
            }
        }
        else
        {
            steeringInput = 0f;
            isDragging = false;
        }

        UpdateSteeringWheel();
    }

    private void UpdateSteeringWheel()
    {
        if (steeringWheelPivot == null)
            return;

        float direction = invertSteering ? -1f : 1f;

        float targetAngle =
            steeringInput * maxSteeringAngle * direction;

        Vector3 axis = steeringAxis.sqrMagnitude > 0.001f
            ? steeringAxis.normalized
            : Vector3.forward;

        Quaternion targetRotation =
            initialWheelRotation *
            Quaternion.AngleAxis(targetAngle, axis);

        float smooth = 1f - Mathf.Exp(
            -steeringSmoothness * Time.deltaTime
        );

        steeringWheelPivot.localRotation =
            Quaternion.Slerp(
                steeringWheelPivot.localRotation,
                targetRotation,
                smooth
            );
    }

    private void FixedUpdate()
    {
        float error = targetX - rb.position.x;

        float desiredVelocityX = Mathf.Clamp(
            error * followStrength,
            -maxHorizontalSpeed,
            maxHorizontalSpeed
        );

        Vector3 velocity = rb.linearVelocity;

        velocity.x = Mathf.MoveTowards(
            velocity.x,
            desiredVelocityX,
            acceleration * Time.fixedDeltaTime
        );

        rb.linearVelocity = velocity;
    }
}
