
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnvironmentMover : MonoBehaviour
{
    private Rigidbody rb;

    private Vector3 moveDirection;
    private float moveSpeed;
    private float maxDistance;
    private float travelledDistance;

    private bool initialized;

    private static bool environmentRunning = true;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        environmentRunning = true;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void Initialize(
        Vector3 direction,
        float speed,
        float despawnDistance)
    {
        moveDirection = direction.normalized;
        moveSpeed = speed;
        maxDistance = despawnDistance;

        travelledDistance = 0f;
        initialized = true;
    }

    private void FixedUpdate()
    {
        if (!initialized || !environmentRunning)
            return;

        float movement = moveSpeed * Time.fixedDeltaTime;

        rb.MovePosition(
            rb.position + moveDirection * movement
        );

        travelledDistance += movement;

        if (travelledDistance >= maxDistance)
        {
            Destroy(gameObject);
        }
    }

    public static void StopEnvironment()
    {
        environmentRunning = false;
    }
}
