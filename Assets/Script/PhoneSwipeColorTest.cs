using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Master Controller Layar Smartphone (Reels FYP Interaktif & 4 Minigame sesuai GDD):
/// 1. Awal buka HP: Verifikasi Sidik Jari (Hold 2 detik, Sukses -> Masuk FYP, Gagal -> Ulangi).
/// 2. FYP Reels (Tiap Reel adalah Minigame):
///    - Tap the Cat (Misi 20 tap, lewat 20 tidak dihitung/tambah kesadaran tapi layar tetap bereaksi)
///    - Kuis MTK (3 soal per sesi, background warna dasar, 1 soal & 4 pilihan jawaban)
///    - Tangkap Bakso (Ada timer & tampilan Result dapat berapa bakso tanpa menampilkan kesadaran)
/// 3. Pop-Up Iklan Menyebalkan: Muncul tiap 2 minigame dengan random trigger & beranak-pinak jika salah klik.
/// </summary>
[RequireComponent(typeof(Image))]
public class PhoneSwipeColorTest : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Pengaturan Swipe Ganti Reels")]
    [Tooltip("Jarak minimal tarikan mouse ke atas (pixel layar) untuk ganti ke minigame berikutnya")]
    [SerializeField] private float swipeDistanceThreshold = 75f;

    [Header("Referensi Modul Layar HP (Otomatis Terhubung)")]
    [SerializeField] private MinigameFingerprint lockScreenFingerprint;
    [SerializeField] private MinigameTapCat minigameTapCat;
    [SerializeField] private MinigameMath minigameMath;
    [SerializeField] private MinigameBakso minigameBakso;
    [SerializeField] private PhoneAdPopupManager adPopupManager;
    [SerializeField] private Text topStatusBarText;

    [Header("Status Permainan (Debug / Integrasi Kesadaran)")]
    [SerializeField] private bool isUnlocked = false;
    [SerializeField] private int currentMinigameIndex = -1;
    [SerializeField] private float totalDopamineEarned = 0f;

    private Vector2 pointerDownScreenPos;
    private bool isTrackingSwipe = false;
    private bool hasSwipedThisDrag = false;

    public float TotalDopamineEarned => totalDopamineEarned;
    public bool IsUnlocked => isUnlocked;

    public void AutoBindReferences()
    {
        Transform existingTopBar = transform.Find("Top_StatusBar");
        if (existingTopBar != null && topStatusBarText == null)
        {
            topStatusBarText = existingTopBar.GetComponentInChildren<Text>();
        }

        if (minigameTapCat == null)
        {
            Transform t = transform.Find("Reel_1_TapTheCat");
            if (t != null) minigameTapCat = t.GetComponent<MinigameTapCat>();
            if (minigameTapCat == null) minigameTapCat = GetComponentInChildren<MinigameTapCat>(true);
        }
        if (minigameTapCat != null) minigameTapCat.AutoBindExistingUI();

        if (minigameMath == null)
        {
            Transform t = transform.Find("Reel_2_KuisMTK");
            if (t != null) minigameMath = t.GetComponent<MinigameMath>();
            if (minigameMath == null) minigameMath = GetComponentInChildren<MinigameMath>(true);
        }
        if (minigameMath != null) minigameMath.AutoBindExistingUI();

        if (minigameBakso == null)
        {
            Transform t = transform.Find("Reel_3_TangkapBakso");
            if (t != null) minigameBakso = t.GetComponent<MinigameBakso>();
            if (minigameBakso == null) minigameBakso = GetComponentInChildren<MinigameBakso>(true);
        }
        if (minigameBakso != null) minigameBakso.AutoBindExistingUI();

        if (lockScreenFingerprint == null)
        {
            Transform t = transform.Find("LockScreen_SidikJari");
            if (t != null) lockScreenFingerprint = t.GetComponent<MinigameFingerprint>();
            if (lockScreenFingerprint == null) lockScreenFingerprint = GetComponentInChildren<MinigameFingerprint>(true);
        }
        if (lockScreenFingerprint != null) lockScreenFingerprint.AutoBindExistingUI();

        if (adPopupManager == null)
        {
            Transform t = transform.Find("PopupIklan_Layer");
            if (t != null) adPopupManager = t.GetComponent<PhoneAdPopupManager>();
            if (adPopupManager == null) adPopupManager = GetComponentInChildren<PhoneAdPopupManager>(true);
        }
    }

    [ContextMenu("Build / Rebuild Phone Minigames UI")]
    public void EnsureMinigameHierarchyBuilt()
    {
        AutoBindReferences();

        Image rootImg = GetComponent<Image>();
        if (rootImg != null)
        {
            rootImg.color = new Color(0.06f, 0.07f, 0.10f, 1f);
            rootImg.raycastTarget = true;
        }

        if (GetComponent<RectMask2D>() == null)
        {
            gameObject.AddComponent<RectMask2D>();
        }

        // 1. Top Status Bar
        Transform existingTopBar = transform.Find("Top_StatusBar");
        if (existingTopBar == null)
        {
            RectTransform topBar = PhoneUIHelper.CreatePanel(
                "Top_StatusBar",
                transform,
                new Color(0.04f, 0.04f, 0.06f, 0.92f),
                new Vector2(0f, 0.92f),
                new Vector2(1f, 1f)
            );
            topBar.GetComponent<Image>().raycastTarget = false;

            topStatusBarText = PhoneUIHelper.CreateText(
                "Status_Text",
                topBar,
                "🔒 TERKUNCI | Tahan Sidik Jari",
                new Color(0.4f, 0.95f, 0.65f),
                new Vector2(0.04f, 0.1f),
                new Vector2(0.96f, 0.9f),
                TextAnchor.MiddleCenter,
                65
            );
        }
        else if (topStatusBarText == null)
        {
            topStatusBarText = existingTopBar.GetComponentInChildren<Text>();
        }

        // 2. Minigame 1: Tap the Cat
        if (minigameTapCat == null)
        {
            RectTransform rt = PhoneUIHelper.CreatePanel(
                "Reel_1_TapTheCat",
                transform,
                new Color(0.16f, 0.12f, 0.24f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0.92f)
            );
            minigameTapCat = rt.gameObject.AddComponent<MinigameTapCat>();
        }
        minigameTapCat.BuildPlaceholderUI();

        // 3. Minigame 2: Kuis MTK
        if (minigameMath == null)
        {
            RectTransform rt = PhoneUIHelper.CreatePanel(
                "Reel_2_KuisMTK",
                transform,
                new Color(0.12f, 0.27f, 0.42f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0.92f)
            );
            minigameMath = rt.gameObject.AddComponent<MinigameMath>();
        }
        minigameMath.BuildPlaceholderUI();

        // 4. Minigame 3: Tangkap Bakso
        if (minigameBakso == null)
        {
            RectTransform rt = PhoneUIHelper.CreatePanel(
                "Reel_3_TangkapBakso",
                transform,
                new Color(0.24f, 0.14f, 0.10f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0.92f)
            );
            minigameBakso = rt.gameObject.AddComponent<MinigameBakso>();
        }
        minigameBakso.BuildPlaceholderUI();

        // 5. Lock Screen Awal: Verifikasi Sidik Jari
        if (lockScreenFingerprint == null)
        {
            RectTransform rt = PhoneUIHelper.CreatePanel(
                "LockScreen_SidikJari",
                transform,
                new Color(0.08f, 0.10f, 0.16f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0.92f)
            );
            lockScreenFingerprint = rt.gameObject.AddComponent<MinigameFingerprint>();
        }
        lockScreenFingerprint.BuildPlaceholderUI();

        // 6. Layer Pop-Up Iklan
        if (adPopupManager == null)
        {
            RectTransform rt = PhoneUIHelper.CreatePanel(
                "PopupIklan_Layer",
                transform,
                new Color(0f, 0f, 0f, 0.55f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0.92f)
            );
            adPopupManager = rt.gameObject.AddComponent<PhoneAdPopupManager>();
        }
        adPopupManager.BuildPlaceholderUI();
        adPopupManager.transform.SetAsLastSibling();
    }

    private void OnValidate()
    {
        AutoBindReferences();
    }

    private void Awake()
    {
        EnsureMinigameHierarchyBuilt();
        BindMinigameCallbacks();
    }

    private void Start()
    {
        ShowLockScreen();
    }

    private void BindMinigameCallbacks()
    {
        if (lockScreenFingerprint != null)
        {
            lockScreenFingerprint.OnVerificationCompleted = HandleFingerprintUnlocked;
        }

        if (minigameTapCat != null)
        {
            minigameTapCat.OnConsciousnessReward = (amount) =>
            {
                AddConsciousnessReward(amount);
                if (adPopupManager != null) adPopupManager.NotifyPlayerInteractionTap();
            };
        }

        if (minigameMath != null)
        {
            minigameMath.OnConsciousnessReward = (amount) =>
            {
                AddConsciousnessReward(amount);
                if (adPopupManager != null) adPopupManager.NotifyPlayerInteractionTap();
            };
        }

        if (minigameBakso != null)
        {
            minigameBakso.OnConsciousnessReward = (amount) =>
            {
                AddConsciousnessReward(amount);
            };
        }
    }

    private void Update()
    {
        DetectGlobalReelSwipeUp();
    }

    private void DetectGlobalReelSwipeUp()
    {
        if (!isUnlocked) return;
        if (adPopupManager != null && adPopupManager.HasActiveAds) return;
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (IsPointerOverThisPhoneCanvas())
            {
                isTrackingSwipe = true;
                hasSwipedThisDrag = false;
                pointerDownScreenPos = Mouse.current.position.ReadValue();
            }
            else
            {
                isTrackingSwipe = false;
            }
        }

        if (isTrackingSwipe && Mouse.current.leftButton.isPressed && !hasSwipedThisDrag)
        {
            Vector2 currentPos = Mouse.current.position.ReadValue();
            float deltaY = currentPos.y - pointerDownScreenPos.y;
            float deltaX = currentPos.x - pointerDownScreenPos.x;

            if (deltaY >= swipeDistanceThreshold && deltaY > Mathf.Abs(deltaX) * 1.6f)
            {
                hasSwipedThisDrag = true;
                SwitchToNextRandomMinigame();
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isTrackingSwipe = false;
            hasSwipedThisDrag = false;
        }
    }

    private bool IsPointerOverThisPhoneCanvas()
    {
        if (EventSystem.current == null || Mouse.current == null) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject != null && results[i].gameObject.transform.IsChildOf(transform))
            {
                return true;
            }
        }
        return false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDownScreenPos = eventData.position;
        hasSwipedThisDrag = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isUnlocked || hasSwipedThisDrag) return;
        if (adPopupManager != null && adPopupManager.HasActiveAds) return;

        float deltaY = eventData.position.y - pointerDownScreenPos.y;
        float deltaX = eventData.position.x - pointerDownScreenPos.x;

        if (deltaY >= swipeDistanceThreshold && deltaY > Mathf.Abs(deltaX) * 1.6f)
        {
            hasSwipedThisDrag = true;
            SwitchToNextRandomMinigame();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        hasSwipedThisDrag = false;
    }

    public void ShowLockScreen()
    {
        isUnlocked = false;
        currentMinigameIndex = -1;

        if (minigameTapCat != null) minigameTapCat.gameObject.SetActive(false);
        if (minigameMath != null) minigameMath.gameObject.SetActive(false);
        if (minigameBakso != null) minigameBakso.gameObject.SetActive(false);
        if (adPopupManager != null) adPopupManager.ClearAllAds();

        if (lockScreenFingerprint != null)
        {
            lockScreenFingerprint.gameObject.SetActive(true);
            lockScreenFingerprint.ResetToLockScreen();
        }

        UpdateTopStatusBar("🔒 HP TERKUNCI | Tahan 2 Detik");
    }

    private void HandleFingerprintUnlocked()
    {
        isUnlocked = true;
        if (lockScreenFingerprint != null)
            lockScreenFingerprint.gameObject.SetActive(false);

        AddConsciousnessReward(5f);

        SwitchToNextRandomMinigame(firstOpen: true);
    }

    public void SwitchToNextRandomMinigame(bool firstOpen = false)
    {
        int nextIndex;
        if (currentMinigameIndex < 0)
        {
            nextIndex = Random.Range(0, 3);
        }
        else
        {
            nextIndex = (currentMinigameIndex + Random.Range(1, 3)) % 3;
        }

        currentMinigameIndex = nextIndex;

        if (minigameTapCat != null) minigameTapCat.gameObject.SetActive(currentMinigameIndex == 0);
        if (minigameMath != null) minigameMath.gameObject.SetActive(currentMinigameIndex == 1);
        if (minigameBakso != null) minigameBakso.gameObject.SetActive(currentMinigameIndex == 2);

        if (currentMinigameIndex == 0 && minigameTapCat != null)
        {
            minigameTapCat.ResetMinigame();
            UpdateTopStatusBar("▲ Swipe Naik Ganti Reel ▲");
        }
        else if (currentMinigameIndex == 1 && minigameMath != null)
        {
            minigameMath.ResetMinigame();
            UpdateTopStatusBar("▲ Swipe Naik Ganti Reel ▲");
        }
        else if (currentMinigameIndex == 2 && minigameBakso != null)
        {
            minigameBakso.ResetMinigame();
            UpdateTopStatusBar("▲ Swipe Naik Ganti Reel ▲");
        }

        if (!firstOpen && adPopupManager != null)
        {
            adPopupManager.NotifyReelChanged();
        }
    }

    private void AddConsciousnessReward(float amount)
    {
        if (adPopupManager != null && adPopupManager.HasActiveAds)
            return;

        totalDopamineEarned += amount;

        if (ConsciousnessManager.Instance != null)
        {
            ConsciousnessManager.Instance.AddConsciousness(amount);
        }
    }

    private void UpdateTopStatusBar(string message)
    {
        if (topStatusBarText != null)
        {
            topStatusBarText.text = message;
        }
    }
}
