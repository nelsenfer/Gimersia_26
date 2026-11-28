
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnvironmentMover : MonoBehaviour
{
    private Rigidbody rb;

    private Vector3 moveDirection;
    private float moveSpeed;
    private float maxDistance;
    private float hideVisualsDistance;
    private float travelledDistance;

    private bool initialized;
    private bool visualsHidden;

    // Fitur Belok / Pindah Jalur (Khusus NPC Troll)
    private bool isChangingLane = false;
    private float laneChangeStartX;
    private float laneChangeTargetX;
    private float laneChangeElapsed = 0f;
    private float laneChangeDuration = 0.48f;
    private float laneChangeMaxYawAngle = 13f;
    private Quaternion baseSpawnRotation;

    public bool IsChangingLane => isChangingLane;

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
        float despawnDistance,
        float hideMeshAndColliderAtDistance = -1f)
    {
        moveDirection = direction.normalized;
        moveSpeed = speed;
        maxDistance = despawnDistance;
        hideVisualsDistance = hideMeshAndColliderAtDistance > 0f
            ? hideMeshAndColliderAtDistance
            : despawnDistance;

        travelledDistance = 0f;
        visualsHidden = false;
        isChangingLane = false;
        baseSpawnRotation = transform.rotation;
        initialized = true;
    }

    /// <summary>
    /// Memicu manuver banting setir pindah jalur secara mulus (dengan efek miring/drift komedi lalu kembali lurus).
    /// </summary>
    public void StartTrollLaneChange(float targetX, float duration = 0.48f, float maxYawDegrees = 13f)
    {
        if (!initialized || isChangingLane) return;

        baseSpawnRotation = transform.rotation;
        laneChangeStartX = rb != null ? rb.position.x : transform.position.x;
        laneChangeTargetX = targetX;
        laneChangeDuration = Mathf.Max(0.15f, duration);
        laneChangeMaxYawAngle = maxYawDegrees;
        laneChangeElapsed = 0f;
        isChangingLane = true;
    }

    private void FixedUpdate()
    {
        if (!initialized || !environmentRunning)
            return;

        float movement = moveSpeed * Time.fixedDeltaTime;
        Vector3 nextPos = rb.position + moveDirection * movement;

        if (isChangingLane)
        {
            laneChangeElapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(laneChangeElapsed / laneChangeDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            nextPos.x = Mathf.Lerp(laneChangeStartX, laneChangeTargetX, smoothT);

            // Efek miring/drift bodi saat banting setir lalu kembali lurus ketika sampai di jalur tujuan
            float dirSign = Mathf.Sign(laneChangeTargetX - laneChangeStartX);
            float yawTilt = Mathf.Sin(t * Mathf.PI) * laneChangeMaxYawAngle * dirSign;
            rb.MoveRotation(Quaternion.Euler(0f, yawTilt, 0f) * baseSpawnRotation);

            if (t >= 1f)
            {
                isChangingLane = false;
                rb.MoveRotation(baseSpawnRotation);
            }
        }

        rb.MovePosition(nextPos);

        travelledDistance += movement;

        // Jika objek sudah jauh di belakang kamera namun masih memutar fade-out Audio 3D (seperti NPC Troll Kampanye / Kapal Bajak Laut),
        // matikan MeshRenderer & Collider lebih dulu agar tidak membebani GPU/Fisika sementara AudioSource 3D terus menjauh secara natural!
        if (!visualsHidden && travelledDistance >= hideVisualsDistance && hideVisualsDistance < maxDistance)
        {
            visualsHidden = true;
            DisableVisualsAndCollidersKeepAudio();
        }

        if (travelledDistance >= maxDistance)
        {
            Destroy(gameObject);
        }
    }

    private void DisableVisualsAndCollidersKeepAudio()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = false;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }

    public static void StopEnvironment()
    {
        environmentRunning = false;
    }

    public static void ResetEnvironment()
    {
        environmentRunning = true;
    }
}
