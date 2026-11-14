using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mengontrol pergerakan 3D tangan dan smartphone antara posisi Idle (santai di bawah)
/// dan posisi Focus (diangkat mendekat ke kamera) berdasarkan posisi kursor mouse di layar.
/// </summary>
public class Phone3DController : MonoBehaviour
{
    [Header("Titik Acuan (Target Transforms)")]
    [Tooltip("Titik acuan posisi & rotasi saat HP santai di bawah")]
    [SerializeField] private Transform idlePoint;

    [Tooltip("Titik acuan posisi & rotasi saat HP diangkat mendekat ke kamera")]
    [SerializeField] private Transform focusPoint;

    [Header("Deteksi Mouse di Layar")]
    [Tooltip("Batas horizontal layar untuk memicu fokus (0.5 = separuh kanan layar)")]
    [Range(0.2f, 0.8f)]
    [SerializeField] private float rightSideThreshold = 0.5f;

    [Tooltip("Jeda toleransi (buffer) agar tidak bergetar saat mouse tepat di garis tengah")]
    [Range(0.01f, 0.1f)]
    [SerializeField] private float thresholdBuffer = 0.03f;

    [Header("Kecepatan Transisi")]
    [Tooltip("Kecepatan perpindahan posisi")]
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("Kecepatan perpindahan rotasi")]
    [SerializeField] private float rotationSpeed = 8f;

    private bool isFocused;

    /// <summary>
    /// Apakah saat ini HP sedang berada dalam mode fokus (diangkat mendekat)?
    /// </summary>
    public bool IsFocused => isFocused;

    private void Start()
    {
        // Validasi referensi agar developer langsung tahu jika ada slot yang kosong di Inspector
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
    }

    private void Update()
    {
        CheckMousePosition();
        AnimatePhone();
    }

    /// <summary>
    /// Memeriksa posisi kursor mouse di layar secara horizontal (0.0 = kiri, 1.0 = kanan)
    /// </summary>
    private void CheckMousePosition()
    {
        if (Mouse.current == null) return;

        // Ambil koordinat X mouse dan ubah ke persentase layar (0.0 s/d 1.0)
        float mouseX = Mouse.current.position.ReadValue().x;
        float screenRatioX = Mathf.Clamp01(mouseX / Screen.width);

        // Gunakan buffer zone (histeresis) agar tidak flickering di perbatasan
        if (!isFocused && screenRatioX >= (rightSideThreshold + thresholdBuffer))
        {
            isFocused = true;
        }
        else if (isFocused && screenRatioX <= (rightSideThreshold - thresholdBuffer))
        {
            isFocused = false;
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
