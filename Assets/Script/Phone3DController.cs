using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mengontrol pergerakan 3D tangan dan smartphone antara posisi Idle (santai di bawah)
/// dan posisi Focus (diangkat mendekat ke kamera).
/// Sekarang diubah menggunakan KLIK KANAN MOUSE (Toggle) agar tidak mengganggu kontrol kemudi.
/// </summary>
public class Phone3DController : MonoBehaviour
{
    [Header("Titik Acuan (Target Transforms)")]
    [Tooltip("Titik acuan posisi & rotasi saat HP santai di bawah")]
    [SerializeField] private Transform idlePoint;

    [Tooltip("Titik acuan posisi & rotasi saat HP diangkat mendekat ke kamera")]
    [SerializeField] private Transform focusPoint;

    [Header("Mode Input")]
    [Tooltip("True: Toggle menggunakan Klik Kanan Mouse (Disarankan). False: Otomatis berdasarkan mouse di area kanan.")]
    [SerializeField] private bool useRightClickToggle = true;

    [Tooltip("Batas horizontal layar jika useRightClickToggle = false (0.5 = separuh kanan layar)")]
    [Range(0.2f, 0.8f)]
    [SerializeField] private float rightSideThreshold = 0.5f;

    [Tooltip("Jeda toleransi jika menggunakan posisi mouse")]
    [Range(0.01f, 0.1f)]
    [SerializeField] private float thresholdBuffer = 0.03f;

    [Header("Kecepatan Transisi")]
    [Tooltip("Kecepatan perpindahan posisi")]
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("Kecepatan perpindahan rotasi")]
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Cahaya Layar HP (Mengarah ke POV User)")]
    [SerializeField] private bool enableScreenLight = true;
    [SerializeField] private Light screenLight;
    [SerializeField] private Color screenLightColor = new Color(0.68f, 0.85f, 1.0f, 1.0f);
    [SerializeField] private float idleLightIntensity = 0.9f;
    [SerializeField] private float focusLightIntensity = 1.6f;
    [SerializeField] private float screenLightRange = 0.85f;
    [Range(25f, 95f)]
    [SerializeField] private float screenSpotAngle = 52f;
    [Range(10f, 80f)]
    [SerializeField] private float screenInnerSpotAngle = 22f;

    private bool isFocused;
    private Transform cachedScreenTransform;

    /// <summary>
    /// Apakah saat ini HP sedang berada dalam mode fokus (diangkat mendekat)?
    /// </summary>
    public bool IsFocused => isFocused;

    private void Start()
    {
        if (idlePoint == null || focusPoint == null)
        {
            Debug.LogWarning(
                "[Phone3DController] 'idlePoint' atau 'focusPoint' belum dipasang di Inspector GameObject: " + gameObject.name,
                this
            );
        }

        // Posisi awal diset ke idlePoint jika sudah ada
        if (idlePoint != null)
        {
            transform.localPosition = idlePoint.localPosition;
            transform.localRotation = idlePoint.localRotation;
        }

        EnsureScreenLightInitialized();
    }

    private void Update()
    {
        CheckFocusInput();
        AnimatePhone();
        UpdateScreenLight();
    }

    private void EnsureScreenLightInitialized()
    {
        if (!enableScreenLight)
        {
            if (screenLight != null) screenLight.enabled = false;
            return;
        }

        Canvas phoneCanvas = GetComponentInChildren<Canvas>(true);
        if (phoneCanvas != null)
        {
            cachedScreenTransform = phoneCanvas.transform;
        }

        if (screenLight == null)
        {
            Transform existing = transform.Find("PhoneScreenLight");
            if (existing != null)
            {
                screenLight = existing.GetComponent<Light>();
            }
        }

        if (screenLight == null)
        {
            GameObject lightObj = new GameObject("PhoneScreenLight");
            lightObj.transform.SetParent(transform, false);
            screenLight = lightObj.AddComponent<Light>();
        }

        screenLight.type = LightType.Spot;
        screenLight.color = screenLightColor;
        screenLight.range = screenLightRange;
        screenLight.spotAngle = screenSpotAngle;
        screenLight.innerSpotAngle = Mathf.Min(screenInnerSpotAngle, screenSpotAngle);
        screenLight.intensity = idleLightIntensity;
        screenLight.shadows = LightShadows.Soft;
        screenLight.enabled = true;

        AimScreenLightToUserPOV();
    }

    private void AimScreenLightToUserPOV()
    {
        if (screenLight == null) return;

        Vector3 screenPos = cachedScreenTransform != null
            ? cachedScreenTransform.position
            : transform.TransformPoint(new Vector3(-0.032f, 0.342f, 0.714f));

        Vector3 toPov = Vector3.back;
        if (Camera.main != null)
        {
            Vector3 diff = Camera.main.transform.position - screenPos;
            if (diff.sqrMagnitude > 0.0001f)
            {
                toPov = diff.normalized;
            }
        }

        Vector3 outwardNormal = toPov;
        if (cachedScreenTransform != null)
        {
            outwardNormal = cachedScreenTransform.forward;
            if (Vector3.Dot(outwardNormal, toPov) < 0f)
            {
                outwardNormal = -outwardNormal;
            }
        }

        // Taruh tepat 1.5 cm di depan kaca layar dan arahkan sorotan kerucut (Spot) langsung ke arah POV user
        Vector3 aimDirection = Vector3.Slerp(outwardNormal, toPov, 0.7f).normalized;
        screenLight.transform.position = screenPos + outwardNormal * 0.015f;
        screenLight.transform.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);
    }

    private void UpdateScreenLight()
    {
        if (screenLight == null) return;

        if (!enableScreenLight)
        {
            if (screenLight.enabled) screenLight.enabled = false;
            return;
        }

        if (!screenLight.enabled) screenLight.enabled = true;

        AimScreenLightToUserPOV();

        float targetIntensity = isFocused ? focusLightIntensity : idleLightIntensity;
        screenLight.type = LightType.Spot;
        screenLight.color = screenLightColor;
        screenLight.range = screenLightRange;
        screenLight.spotAngle = screenSpotAngle;
        screenLight.innerSpotAngle = Mathf.Min(screenInnerSpotAngle, screenSpotAngle);
        screenLight.intensity = Mathf.Lerp(screenLight.intensity, targetIntensity, Time.deltaTime * moveSpeed);
    }

    /// <summary>
    /// Memeriksa input untuk toggle fokus HP
    /// </summary>
    private void CheckFocusInput()
    {
        if (Mouse.current == null) return;

        if (useRightClickToggle)
        {
            // TOGGLE FOKUS DENGAN KLIK KANAN
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                isFocused = !isFocused;
                Debug.Log("<color=yellow>[HP Mode]</color> Status Fokus: " + (isFocused ? "ANGKAT HP" : "TURUNKAN HP"));
            }
        }
        else
        {
            // Opsi Lama: Berdasarkan posisi mouse horizontal
            float mouseX = Mouse.current.position.ReadValue().x;
            float screenRatioX = Mathf.Clamp01(mouseX / Screen.width);

            if (!isFocused && screenRatioX >= (rightSideThreshold + thresholdBuffer))
            {
                isFocused = true;
            }
            else if (isFocused && screenRatioX <= (rightSideThreshold - thresholdBuffer))
            {
                isFocused = false;
            }
        }
    }

    /// <summary>
    /// Menggerakkan dan merotasikan GameObject ini secara halus ke target posisi
    /// </summary>
    private void AnimatePhone()
    {
        if (idlePoint == null || focusPoint == null) return;

        // Tentukan titik tujuan berdasarkan status fokus
        Vector3 targetPosition = isFocused ? focusPoint.localPosition : idlePoint.localPosition;
        Quaternion targetRotation = isFocused ? focusPoint.localRotation : idlePoint.localRotation;

        // Interpolasi posisi (Lerp halus)
        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPosition,
            Time.deltaTime * moveSpeed
        );

        // Interpolasi rotasi (Slerp halus)
        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            targetRotation,
            Time.deltaTime * rotationSpeed
        );
    }

    // Gambar visual bantuan (Gizmos) di Scene view saat objek dipilih
    private void OnDrawGizmosSelected()
    {
        if (idlePoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(idlePoint.position, 0.04f);
        }

        if (focusPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(focusPoint.position, 0.04f);
        }
    }
}
