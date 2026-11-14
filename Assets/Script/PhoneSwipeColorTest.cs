using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Script uji interaksi layar HP:
/// Saat pemain melakukan drag / swipe ke atas di layar HP, warna layar akan berubah secara dinamis.
/// Ini membuktikan interaksi World Space Canvas dan EventSystem berjalan dengan baik.
/// </summary>
[RequireComponent(typeof(Image))]
public class PhoneSwipeColorTest : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Pengaturan Swipe")]
    [Tooltip("Jarak minimal tarikan mouse ke atas (dalam pixel) untuk mengganti warna")]
    [SerializeField] private float swipeDistanceThreshold = 80f;

    [Tooltip("Daftar palet warna pop-art yang akan berganti saat di-swipe")]
    [SerializeField] private Color[] screenColors = new Color[]
    {
        new Color(0.20f, 0.60f, 0.86f), // Biru Tosca
        new Color(0.91f, 0.30f, 0.24f), // Merah Coral
        new Color(0.18f, 0.80f, 0.44f), // Hijau Neon
        new Color(0.95f, 0.77f, 0.06f), // Kuning Cerah
        new Color(0.61f, 0.35f, 0.71f), // Ungu Pop
        new Color(0.90f, 0.49f, 0.13f), // Oranye
        new Color(0.17f, 0.24f, 0.31f)  // Dark Blue / Midnight
    };

    [Header("Efek Transisi")]
    [Tooltip("Kecepatan transisi perpindahan warna")]
    [SerializeField] private float colorChangeSpeed = 10f;

    private Image screenImage;
    private int currentColorIndex = 0;
    private Color targetColor;

    private Vector2 startDragPosition;
    private bool hasTriggeredThisSwipe = false;

    private void Awake()
    {
        screenImage = GetComponent<Image>();
        
        // Pastikan Raycast Target aktif agar bisa menerima klik & drag mouse
        screenImage.raycastTarget = true;

        if (screenColors != null && screenColors.Length > 0)
        {
            targetColor = screenColors[0];
            screenImage.color = targetColor;
        }
        else
        {
            targetColor = Color.cyan;
            screenImage.color = targetColor;
        }
    }

    private void Update()
    {
        // Transisi warna yang halus (Lerp)
        if (screenImage != null)
        {
            screenImage.color = Color.Lerp(
                screenImage.color,
                targetColor,
                Time.deltaTime * colorChangeSpeed
            );
        }
    }

    /// <summary>
    /// Dipanggil saat mouse pertama kali menekan layar HP
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        startDragPosition = eventData.position;
        hasTriggeredThisSwipe = false;
    }

    /// <summary>
    /// Dipanggil saat mouse digeser (drag) di atas layar HP
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (hasTriggeredThisSwipe) return;

        // Hitung jarak geser ke atas (Delta Y positif berarti geser ke atas)
        float deltaY = eventData.position.y - startDragPosition.y;

        // Jika digeser ke atas melebihi batas threshold
        if (deltaY >= swipeDistanceThreshold)
        {
            NextColor();
            hasTriggeredThisSwipe = true;
            Debug.Log("<color=cyan>[Layar HP]</color> Swipe ke atas terdeteksi! Ganti warna ke index: " + currentColorIndex);
        }
    }

    /// <summary>
    /// Dipanggil saat mouse dilepas dari layar HP
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        hasTriggeredThisSwipe = false;
    }

    /// <summary>
    /// Mengganti ke warna berikutnya dalam daftar
    /// </summary>
    private void NextColor()
    {
        if (screenColors == null || screenColors.Length == 0) return;

        currentColorIndex = (currentColorIndex + 1) % screenColors.Length;
        targetColor = screenColors[currentColorIndex];
    }
}
