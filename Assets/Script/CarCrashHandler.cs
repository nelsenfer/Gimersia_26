
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarCrashHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CarController carController;
    [SerializeField] private Transform cameraTransform;

    [Header("Camera Shake Settings")]
    [SerializeField] private float shakeDuration = 0.65f;
    [SerializeField] private float positionMagnitude = 0.18f;
    [SerializeField] private float rotationMagnitude = 7f;

    private bool hasCrashed;

    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (carController == null)
            carController = GetComponent<CarController>();

        if (cameraTransform != null)
        {
            originalCameraPosition =
                cameraTransform.localPosition;

            originalCameraRotation =
                cameraTransform.localRotation;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasCrashed)
            return;

        if (!IsObstacle(other))
            return;

        hasCrashed = true;

        Debug.LogWarning("CAR CRASH!");

        // Stop environment movement
        EnvironmentMover.StopEnvironment();

        // Disable car input
        if (carController != null)
            carController.enabled = false;

        // Stop car physics
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        // Start camera impact
        if (cameraTransform != null)
            StartCoroutine(CameraShake());
    }

    private bool IsObstacle(Collider other)
    {
        Transform current = other.transform;

        while (current != null)
        {
            if (current.CompareTag("Obstacle"))
                return true;

            current = current.parent;
        }

        return false;
    }

    private IEnumerator CameraShake()
    {
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float strength = 1f - (elapsed / shakeDuration);

            // Position shake
            Vector3 offset =
                Random.insideUnitSphere *
                positionMagnitude *
                strength;

            cameraTransform.localPosition =
                originalCameraPosition + offset;

            // Rotation shake
            Vector3 rotationOffset = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ) * rotationMagnitude * strength;

            cameraTransform.localRotation =
                originalCameraRotation *
                Quaternion.Euler(rotationOffset);

            elapsed += Time.unscaledDeltaTime;

            yield return null;
        }

        // Restore camera
        cameraTransform.localPosition =
            originalCameraPosition;

        cameraTransform.localRotation =
            originalCameraRotation;
    }
}
