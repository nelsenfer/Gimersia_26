
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    private enum StartingLane
    {
        Left,
        Right
    }

    [Header("Two Lane Settings")]
    [SerializeField] private float roadCenterX = -0.14f;
    [SerializeField] private float laneOffset = 3f;
    [SerializeField] private StartingLane startingLane = StartingLane.Right;
    [SerializeField] private float laneChangeSpeed = 8f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float followStrength = 12f;

    [Header("Mouse Drag Settings")]
    [SerializeField] private float swipeThresholdPixels = 60f;

    [Header("Steering Wheel Settings")]
    [SerializeField] private Transform steeringWheelPivot;
    [SerializeField] private float maxSteeringAngle = 120f;
    [SerializeField] private float steeringHoldDuration = 0.08f;
    [SerializeField] private float steeringTurnSmoothTime = 0.05f;
    [SerializeField] private float steeringReturnSmoothTime = 0.12f;
    [SerializeField] private Vector3 steeringAxis = Vector3.forward;
    [SerializeField] private bool invertSteering = false;

    private Rigidbody rb;

    private bool isDragging;
    private float startMouseX;

    // 0 = Left, 1 = Right
    private int currentLane;

    private float targetX;

    private Quaternion initialWheelRotation;

    private float currentSteeringAngle;
    private float steeringTargetAngle;
    private float steeringAngularVelocity;
    private float steeringTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints |= RigidbodyConstraints.FreezeRotation;

        // Set starting lane
        currentLane = startingLane == StartingLane.Left ? 0 : 1;

        targetX = GetLanePosition(currentLane);

        // Place car directly in selected lane
        Vector3 position = rb.position;
        position.x = targetX;
        rb.position = position;

        // Save original steering wheel rotation
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
            // Start mouse drag
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                isDragging = true;

                startMouseX =
                    Mouse.current.position.ReadValue().x;
            }

            // Detect drag release
            if (isDragging &&
                Mouse.current.leftButton.wasReleasedThisFrame)
            {
                float currentMouseX =
                    Mouse.current.position.ReadValue().x;

                float dragDistance =
                    currentMouseX - startMouseX;

                if (Mathf.Abs(dragDistance) >= swipeThresholdPixels)
                {
                    if (dragDistance < 0)
                    {
                        ChangeLane(0);
                    }
                    else
                    {
                        ChangeLane(1);
                    }
                }

                isDragging = false;
            }
        }

        UpdateSteeringWheel();
    }

    private float GetLanePosition(int lane)
    {
        if (lane == 0)
            return roadCenterX - laneOffset;

        return roadCenterX + laneOffset;
    }

    private void ChangeLane(int targetLane)
    {
        targetLane = Mathf.Clamp(targetLane, 0, 1);

        // Ignore input if already in the selected lane
        if (currentLane == targetLane)
            return;

        // Determine steering direction
        int steeringDirection =
            targetLane > currentLane ? 1 : -1;

        currentLane = targetLane;

        // Set target lane position
        targetX = GetLanePosition(currentLane);

        // Turn steering wheel immediately
        float direction = invertSteering ? -1f : 1f;

        steeringTargetAngle =
            maxSteeringAngle *
            steeringDirection *
            direction;

        // Start automatic steering return timer
        steeringTimer = steeringHoldDuration;
    }

    private void UpdateSteeringWheel()
    {
        if (steeringWheelPivot == null)
            return;

        // Automatically return steering wheel after turning
        if (steeringTimer > 0f)
        {
            steeringTimer -= Time.deltaTime;
        }
        else
        {
            steeringTargetAngle = 0f;
        }

        // Use different smoothing for turning and returning
        float smoothTime = Mathf.Approximately(
            steeringTargetAngle, 0f
        )
            ? steeringReturnSmoothTime
            : steeringTurnSmoothTime;

        // Smooth steering animation
        currentSteeringAngle = Mathf.SmoothDampAngle(
            currentSteeringAngle,
            steeringTargetAngle,
            ref steeringAngularVelocity,
            smoothTime,
            Mathf.Infinity,
            Time.deltaTime
        );

        Vector3 axis = steeringAxis.sqrMagnitude > 0.001f
            ? steeringAxis.normalized
            : Vector3.forward;

        // Apply rotation without changing scale
        steeringWheelPivot.localRotation =
            initialWheelRotation *
            Quaternion.AngleAxis(
                currentSteeringAngle,
                axis
            );
    }

    private void FixedUpdate()
    {
        float error = targetX - rb.position.x;

        float desiredVelocityX = Mathf.Clamp(
            error * followStrength,
            -laneChangeSpeed,
            laneChangeSpeed
        );

        Vector3 velocity = rb.linearVelocity;

        velocity.x = Mathf.MoveTowards(
            velocity.x,
            desiredVelocityX,
            acceleration * Time.fixedDeltaTime
        );

        // Stop horizontal movement when reaching target lane
        if (Mathf.Abs(error) < 0.02f &&
            Mathf.Abs(velocity.x) < 0.15f)
        {
            velocity.x = 0f;
        }

        rb.linearVelocity = velocity;
    }
}
