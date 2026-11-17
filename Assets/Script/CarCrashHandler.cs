
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

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform != null)
        {
            originalCameraPosition =
                cameraTransform.localPosition;

            originalCameraRotation =
                cameraTransform.localRotation;
        }
    }

    private bool isCrashCooldown;

    private void OnTriggerEnter(Collider other)
    {
        TryHandleCrash(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryHandleCrash(collision.collider);
    }

    private void TryHandleCrash(Collider other)
    {
        if (other == null || hasCrashed || isCrashCooldown)
            return;

        Transform obstacleRoot = GetObstacleRoot(other);
        if (obstacleRoot == null)
            return;

        // Start camera impact effect
        if (cameraTransform != null)
        {
            StartCoroutine(CameraShake());
        }

        // Cek sistem 3 Nyawa & Adrenalin dari ConsciousnessManager
        if (ConsciousnessManager.Instance != null)
        {
            bool isGameOver = ConsciousnessManager.Instance.HandleCarCrash();
            if (!isGameOver)
            {
                // Rem mendadak berhasil! Hapus obstacle yang tertabrak & lanjut jalan
                Destroy(obstacleRoot.gameObject);
                StartCoroutine(CrashCooldownRoutine());
                return;
            }
        }

        hasCrashed = true;

        Debug.LogWarning("CAR CRASH! GAME STOPPED!");

        // Stop all moving environment objects
        EnvironmentMover.StopEnvironment();

        // Stop spawning new trees & NPCs
        TreeSpawner.StopSpawning();
        NPCSpawner.StopSpawning();

        // Stop road line movement
        RoadLineSpawner.StopLines();

        // Disable car controls
        if (carController != null)
        {
            carController.enabled = false;
        }

        // Stop car physics
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        Debug.Log("Crash sequence completed.");
    }

    private IEnumerator CrashCooldownRoutine()
    {
        isCrashCooldown = true;
        yield return new WaitForSeconds(0.6f);
        isCrashCooldown = false;
    }

    private Transform GetObstacleRoot(Collider other)
    {
        Transform current = other.transform;

        while (current != null)
        {
            if (current.CompareTag("Obstacle"))
                return current;

            current = current.parent;
        }

        return null;
    }

    private bool isShaking;

    private IEnumerator CameraShake()
    {
        if (isShaking) yield break;
        isShaking = true;

        // Ambil posisi kamera saat ini
        if (cameraTransform.parent != null && cameraTransform.localPosition.sqrMagnitude < 0.001f)
        {
            originalCameraPosition = Vector3.zero;
            originalCameraRotation = Quaternion.identity;
        }
        else
        {
            originalCameraPosition = cameraTransform.localPosition;
            originalCameraRotation = cameraTransform.localRotation;
        }

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

        // Restore original camera position and rotation
        cameraTransform.localPosition =
            originalCameraPosition;

        cameraTransform.localRotation =
            originalCameraRotation;

        isShaking = false;
    }
}
