using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Master Controller Layar Smartphone (Vertical Feed Scroll ala Reels/TikTok & 4 Minigame):
/// 1. Awal buka HP: Verifikasi Sidik Jari (Hold 2 detik, Sukses -> Masuk FYP, Gagal -> Ulangi).
/// 2. Vertical Feed Scroll Sungguhan:
///    - Saat mouse ditarik naik dan belum dilepas, konten sebelumnya ikut tergeser ke atas dan
///      konten baru langsung muncul naik dari bawah (terpotong rapi oleh RectMask2D).
///    - Jika melewati jarak tertentu saat ditarik ATAU dilepas melewati ambang batas, transisi
///      menyelesaikan snap ke konten baru sepenuhnya; jika kurang, kembali ke konten sebelumnya.
/// 3. FYP Reels (Tiap Reel adalah Minigame):
///    - Tap the Cat (Misi 20 tap)
///    - Kuis MTK (3 soal per sesi)
///    - Tangkap Bakso (Ada timer & tampilan Result)
/// 4. Pop-Up Iklan Menyebalkan: Muncul tiap 2 minigame dengan random trigger.
/// </summary>
[RequireComponent(typeof(Image))]
public class PhoneFeedScrollController : MonoBehaviour
{
    private enum ScrollState
    {
        Idle,
        Dragging,
        SnappingToNext,
        SnappingBack
    }

    [Header("Pengaturan Scroll Vertikal (Reels / TikTok)")]
    [Tooltip("Jarak tarikan awal minimal (unit Canvas HP) sebelum mode scroll aktif agar tap & drag Bakso tetap aman")]
    [SerializeField] private float dragStartDeadzone = 45f;

    [Tooltip("Minimal rasio arah vertikal vs horizontal agar tarikan miring/samping tidak membajak scroll")]
    [SerializeField] private float verticalAngleBias = 1.25f;

    [Tooltip("Minimal persentase tinggi layar saat mouse dilepas untuk lanjut ke konten baru (0.25 = 25%)")]
    [Range(0.10f, 0.75f)]
    [SerializeField] private float releaseCommitRatio = 0.25f;

    [Tooltip("Persentase tinggi layar agar scroll otomatis penuh ke konten baru walau mouse belum dilepas")]
    [Range(0.35f, 1.0f)]
    [SerializeField] private float autoCommitRatio = 0.65f;

    [Tooltip("Kecepatan jentikan cepat ke atas (unit Canvas/detik) saat dilepas agar tetap pindah konten")]
    [SerializeField] private float quickFlickVelocity = 1600f;

    [Tooltip("Kecepatan animasi snap menuju konten baru atau kembali ke konten sebelumnya")]
    [SerializeField] private float snapAnimationSpeed = 14f;

    [Header("Jeda Buffering Setiap Ganti Reel (3 - 5 Detik)")]
    [Tooltip("Apakah setiap kali ganti Reel baru ada jeda buffering/loading singkat agar pemain fokus jalan dulu")]
    [SerializeField] private bool enableReelBufferingCooldown = true;
    [SerializeField] private float minReelBufferDuration = 3.0f;
    [SerializeField] private float maxReelBufferDuration = 5.0f;
    [SerializeField] private GameObject reelBufferOverlayPanel;
    [SerializeField] private Text reelBufferOverlayText;

    [Header("Referensi Modul Layar HP (Otomatis Terhubung)")]
    [SerializeField] private MinigameFingerprint lockScreenFingerprint;
    [SerializeField] private MinigameTapCat minigameTapCat;
    [SerializeField] private MinigameMath minigameMath;
    [SerializeField] private MinigameBakso minigameBakso;
    [SerializeField] private MinigameOsu minigameOsu;
    [SerializeField] private PhoneAdPopupManager adPopupManager;
    [SerializeField] private PhoneWifeChatEvent wifeChatEvent;
    [SerializeField] private Text topStatusBarText;

    [Header("Overlay UI FYP (Muncul Setelah Konfirmasi Sidik Jari)")]
    [Tooltip("Otomatis terisi dari Assets/Sprite/Ingame/UI.png")]
    [SerializeField] private Image fypOverlayImage;
    [SerializeField] private Sprite fypOverlaySprite;
    [SerializeField] private Texture2D fypOverlayTexture;

    [Header("Status Permainan (Debug / Integrasi Kesadaran)")]
    [SerializeField] private bool isUnlocked = false;
    [SerializeField] private int currentMinigameIndex = -1;
    [SerializeField] private float totalDopamineEarned = 0f;

    private const int ReelCount = 4;

    private RectTransform selfRect;
    private Canvas parentCanvas;

    private readonly RectTransform[] reelRects = new RectTransform[ReelCount];
    private readonly Vector2[] baseReelAnchoredPositions = new Vector2[ReelCount];
    private readonly CanvasGroup[] reelCanvasGroups = new CanvasGroup[ReelCount];
    private bool baseLayoutCached = false;

    private ScrollState scrollState = ScrollState.Idle;
    private int candidateNextIndex = -1;
    private float activeSlideHeight = 1000f;
    private float currentDragOffsetY = 0f;

    private bool isTrackingPointer = false;
    private bool dragIntentLockedHorizontal = false;
    private Vector2 pointerDownLocalPos;
    private Vector2 lastDragLocalPos;
    private float dragScrollOriginLocalY;
    private float dragVelocityY = 0f;

    private bool isReelBuffering = false;
    private float reelBufferTimer = 0f;

    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

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

        if (minigameOsu == null)
        {
            Transform t = transform.Find("Reel_4_OsuRhythm");
            if (t != null) minigameOsu = t.GetComponent<MinigameOsu>();
            if (minigameOsu == null) minigameOsu = GetComponentInChildren<MinigameOsu>(true);
        }
        if (minigameOsu != null) minigameOsu.AutoBindExistingUI();

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

        if (wifeChatEvent == null)
        {
            Transform t = transform.Find("WifeChat_EventLayer");
            if (t != null) wifeChatEvent = t.GetComponent<PhoneWifeChatEvent>();
            if (wifeChatEvent == null) wifeChatEvent = GetComponentInChildren<PhoneWifeChatEvent>(true);
        }

        if (fypOverlayImage == null)
        {
            Transform t = transform.Find("FYP_UI_Overlay");
            if (t != null) fypOverlayImage = t.GetComponent<Image>();
        }

#if UNITY_EDITOR
        if (fypOverlaySprite == null)
        {
            fypOverlaySprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/Ingame/UI.png");
        }
        if (fypOverlayTexture == null)
        {
            fypOverlayTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprite/Ingame/UI.png");
        }
#endif
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

        // 4.2. Minigame 4: Rhythm Tap & Slider ala osu!
        if (minigameOsu == null)
        {
            RectTransform rt = PhoneUIHelper.CreatePanel(
                "Reel_4_OsuRhythm",
                transform,
                new Color(0.09f, 0.07f, 0.16f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0.92f)
            );
            // Samakan anchoredPosition awal dengan Reel_1 jika Reel_1 sudah diatur manual di scene
            if (minigameTapCat != null)
            {
                RectTransform refRt = minigameTapCat.GetComponent<RectTransform>();
                if (refRt != null)
                {
                    rt.anchorMin = refRt.anchorMin;
                    rt.anchorMax = refRt.anchorMax;
                    rt.anchoredPosition = refRt.anchoredPosition;
                    rt.sizeDelta = refRt.sizeDelta;
                }
            }
            minigameOsu = rt.gameObject.AddComponent<MinigameOsu>();
        }
        minigameOsu.BuildPlaceholderUI();

        // 4.5. Overlay UI FYP (Assets/Sprite/Ingame/UI.png - muncul setelah unlock sidik jari)
        EnsureFypOverlayBuilt();

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
        lockScreenFingerprint.transform.SetAsLastSibling();

        // 5.5. Layer Event Sekali: Balas Chat Istri (Notif bertumpuk + Ruang Chat QWERTY)
        if (wifeChatEvent == null)
        {
            RectTransform rt = PhoneUIHelper.CreatePanel(
                "WifeChat_EventLayer",
                transform,
                new Color(0f, 0f, 0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0.92f)
            );
            wifeChatEvent = rt.gameObject.AddComponent<PhoneWifeChatEvent>();
        }
        wifeChatEvent.BuildPlaceholderUI();
        wifeChatEvent.transform.SetAsLastSibling();

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

    private void EnsureFypOverlayBuilt()
    {
        if (fypOverlayImage == null)
        {
            Transform existing = transform.Find("FYP_UI_Overlay");
            if (existing != null)
            {
                fypOverlayImage = existing.GetComponent<Image>();
            }
        }

        if (fypOverlayImage == null)
        {
            RectTransform overlayRt = PhoneUIHelper.CreatePanel(
                "FYP_UI_Overlay",
                transform,
                Color.white,
                Vector2.zero,
                Vector2.one
            );
            fypOverlayImage = overlayRt.GetComponent<Image>();
        }

        Sprite resolvedSprite = ResolveFypOverlaySprite();
        if (resolvedSprite != null)
        {
            fypOverlayImage.sprite = resolvedSprite;
            fypOverlayImage.color = Color.white;
        }
        else
        {
            // Jika sprite belum dimuat, buat transparan agar tidak menutupi layar dengan kotak putih
            fypOverlayImage.color = new Color(1f, 1f, 1f, 0f);
        }

        fypOverlayImage.raycastTarget = false;
        fypOverlayImage.preserveAspect = false;
        fypOverlayImage.transform.SetAsLastSibling();
    }

    private Sprite ResolveFypOverlaySprite()
    {
        if (fypOverlaySprite != null)
            return fypOverlaySprite;

        if (fypOverlayTexture != null)
        {
            fypOverlaySprite = Sprite.Create(
                fypOverlayTexture,
                new Rect(0f, 0f, fypOverlayTexture.width, fypOverlayTexture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            return fypOverlaySprite;
        }

        return null;
    }

    private void OnValidate()
    {
        autoCommitRatio = Mathf.Max(releaseCommitRatio + 0.05f, autoCommitRatio);
        AutoBindReferences();
    }

    private void Awake()
    {
        selfRect = transform as RectTransform;
        parentCanvas = GetComponentInParent<Canvas>();

        EnsureMinigameHierarchyBuilt();
        CacheReelLayoutDefaults();
        BindMinigameCallbacks();
    }

    private void Start()
    {
        ShowLockScreen();
    }

    private void CacheReelLayoutDefaults()
    {
        if (baseLayoutCached) return;

        MonoBehaviour[] reelComponents = new MonoBehaviour[]
        {
            minigameTapCat,
            minigameMath,
            minigameBakso,
            minigameOsu
        };

        for (int i = 0; i < ReelCount; i++)
        {
            if (reelComponents[i] == null) continue;

            RectTransform rt = reelComponents[i].GetComponent<RectTransform>();
            reelRects[i] = rt;
            if (rt != null)
            {
                baseReelAnchoredPositions[i] = rt.anchoredPosition;

                CanvasGroup cg = rt.GetComponent<CanvasGroup>();
                if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = true;
                cg.interactable = true;
                reelCanvasGroups[i] = cg;
            }
        }

        baseLayoutCached = true;
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
                GameplayAudioManager.PlayPhoneTap();
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
                GameplayAudioManager.PlayPhoneTap();
                AddConsciousnessReward(amount);
            };
        }

        if (minigameOsu != null)
        {
            minigameOsu.OnConsciousnessReward = (amount) =>
            {
                AddConsciousnessReward(amount);
                if (adPopupManager != null) adPopupManager.NotifyPlayerInteractionTap();
            };
        }

        if (wifeChatEvent != null)
        {
            wifeChatEvent.OnChatCompletedReward = (amount) =>
            {
                AddConsciousnessReward(amount);
            };
            wifeChatEvent.OnChatWindowStateChanged = (isOpen) =>
            {
                if (currentMinigameIndex == 2 || currentMinigameIndex == 3)
                {
                    SetMinigamePaused(currentMinigameIndex, isOpen);
                }
            };
        }
    }

    private void Update()
    {
        UpdateReelBufferingCooldown();
        UpdateScrollInteraction();
        UpdateSnapAnimations();
    }

    private void UpdateReelBufferingCooldown()
    {
        if (!isReelBuffering) return;

        reelBufferTimer -= Time.deltaTime;
        if (reelBufferTimer > 0f)
        {
            if (reelBufferOverlayText != null)
            {
                reelBufferOverlayText.text = $"⏳ MEMUAT REEL...\n{reelBufferTimer:F1}s";
            }
        }
        else
        {
            EndReelBufferingCooldown();
        }
    }

    private void StartReelBufferingCooldown()
    {
        if (!enableReelBufferingCooldown) return;

        EnsureReelBufferOverlayBuilt();
        isReelBuffering = true;
        reelBufferTimer = Random.Range(
            Mathf.Min(minReelBufferDuration, maxReelBufferDuration),
            Mathf.Max(minReelBufferDuration, maxReelBufferDuration)
        );

        if (currentMinigameIndex >= 0)
        {
            SetMinigamePaused(currentMinigameIndex, true);
            SetReelRaycastsEnabled(currentMinigameIndex, false);
        }

        if (reelBufferOverlayPanel != null)
        {
            reelBufferOverlayPanel.SetActive(true);
            // Taruh di atas Reel tapi di bawah FYP_UI_Overlay, WifeChat, dan PopupIklan
            reelBufferOverlayPanel.transform.SetAsLastSibling();
            if (fypOverlayImage != null) fypOverlayImage.transform.SetAsLastSibling();
            if (wifeChatEvent != null) wifeChatEvent.transform.SetAsLastSibling();
            if (adPopupManager != null) adPopupManager.transform.SetAsLastSibling();
        }
    }

    private void EndReelBufferingCooldown()
    {
        isReelBuffering = false;
        reelBufferTimer = 0f;

        if (reelBufferOverlayPanel != null)
        {
            reelBufferOverlayPanel.SetActive(false);
        }

        if (currentMinigameIndex >= 0)
        {
            SetReelRaycastsEnabled(currentMinigameIndex, true);
            bool pauseForWifeChat = wifeChatEvent != null && wifeChatEvent.IsChatWindowOpen;
            SetMinigamePaused(currentMinigameIndex, pauseForWifeChat);
        }
    }

    private void EnsureReelBufferOverlayBuilt()
    {
        if (reelBufferOverlayPanel == null)
        {
            Transform existing = transform.Find("Reel_Buffering_Overlay");
            if (existing != null) reelBufferOverlayPanel = existing.gameObject;
        }

        if (reelBufferOverlayPanel == null)
        {
            RectTransform rt = PhoneUIHelper.CreatePanel(
                "Reel_Buffering_Overlay",
                transform,
                new Color(0.03f, 0.04f, 0.07f, 0.90f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0.92f)
            );
            rt.GetComponent<Image>().raycastTarget = true;
            reelBufferOverlayPanel = rt.gameObject;

            reelBufferOverlayText = PhoneUIHelper.CreateText(
                "Buffer_Text",
                rt,
                "⏳ MEMUAT REEL...\n3.5s",
                new Color(0.75f, 0.92f, 1f, 1f),
                new Vector2(0.08f, 0.30f),
                new Vector2(0.92f, 0.70f),
                TextAnchor.MiddleCenter,
                160
            );

            reelBufferOverlayPanel.SetActive(false);
        }
        else if (reelBufferOverlayText == null)
        {
            reelBufferOverlayText = reelBufferOverlayPanel.GetComponentInChildren<Text>(true);
        }
    }

    private void UpdateScrollInteraction()
    {
        if (!CanInteractWithFeed())
        {
            if (scrollState == ScrollState.Dragging)
            {
                BeginSnapBack();
            }
            isTrackingPointer = false;
            dragIntentLockedHorizontal = false;
            return;
        }

        if (Mouse.current == null) return;

        // 1. Awal klik di layar HP
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (scrollState == ScrollState.Idle &&
                IsPointerOverThisPhoneCanvas() &&
                TryGetCanvasLocalPoint(Mouse.current.position.ReadValue(), out Vector2 startLocal))
            {
                isTrackingPointer = true;
                dragIntentLockedHorizontal = false;
                pointerDownLocalPos = startLocal;
                lastDragLocalPos = startLocal;
                dragVelocityY = 0f;
            }
            else
            {
                isTrackingPointer = false;
            }
        }

        // 2. Selama mouse ditahan & ditarik
        if (isTrackingPointer && Mouse.current.leftButton.isPressed)
        {
            if (TryGetCanvasLocalPoint(Mouse.current.position.ReadValue(), out Vector2 currentLocal))
            {
                Vector2 totalDelta = currentLocal - pointerDownLocalPos;

                if (scrollState == ScrollState.Idle && !dragIntentLockedHorizontal)
                {
                    if (currentMinigameIndex == 3 && minigameOsu != null && minigameOsu.IsDraggingSlider)
                    {
                        // Pemain sedang aktif menarik bola Slider osu!, jangan bajak jadi scroll reel
                        dragIntentLockedHorizontal = true;
                    }
                    else if (Mathf.Abs(totalDelta.x) >= dragStartDeadzone &&
                        Mathf.Abs(totalDelta.x) > Mathf.Abs(totalDelta.y) * 1.15f)
                    {
                        // Gerakan awal dominan horizontal (misal geser mangkok Bakso), kunci agar tidak jadi scroll
                        dragIntentLockedHorizontal = true;
                    }
                    else if (totalDelta.y >= dragStartDeadzone &&
                             totalDelta.y > Mathf.Abs(totalDelta.x) * verticalAngleBias)
                    {
                        BeginInteractiveDrag(currentLocal);
                    }
                }
                else if (scrollState == ScrollState.Dragging)
                {
                    float dt = Mathf.Max(Time.deltaTime, 0.001f);
                    float instantVelY = (currentLocal.y - lastDragLocalPos.y) / dt;
                    dragVelocityY = Mathf.Lerp(dragVelocityY, instantVelY, 0.45f);
                    lastDragLocalPos = currentLocal;

                    float rawOffsetY = currentLocal.y - dragScrollOriginLocalY;
                    currentDragOffsetY = Mathf.Clamp(rawOffsetY, 0f, activeSlideHeight);
                    ApplyReelScrollOffsets(currentMinigameIndex, candidateNextIndex, currentDragOffsetY, activeSlideHeight);

                    if (currentDragOffsetY >= activeSlideHeight * autoCommitRatio)
                    {
                        // Sudah melewati jarak auto-commit: langsung penuhkan ke konten baru meski belum lepas
                        isTrackingPointer = false;
                        dragIntentLockedHorizontal = false;
                        BeginSnapToNext();
                    }
                    else
                    {
                        UpdateTopStatusBar("");
                    }
                }
            }
        }

        // 3. Saat mouse dilepas (atau hilang tekanannya saat sedang Dragging)
        if ((Mouse.current.leftButton.wasReleasedThisFrame || !Mouse.current.leftButton.isPressed) &&
            (isTrackingPointer || scrollState == ScrollState.Dragging))
        {
            if (scrollState == ScrollState.Dragging)
            {
                bool passedReleaseThreshold = currentDragOffsetY >= activeSlideHeight * releaseCommitRatio;
                bool isQuickFlick = dragVelocityY >= quickFlickVelocity && currentDragOffsetY > dragStartDeadzone * 0.5f;

                if (passedReleaseThreshold || isQuickFlick)
                {
                    BeginSnapToNext();
                }
                else
                {
                    BeginSnapBack();
                }
            }

            isTrackingPointer = false;
            dragIntentLockedHorizontal = false;
        }
    }

    private void BeginInteractiveDrag(Vector2 currentLocal)
    {
        if (currentMinigameIndex < 0) return;

        int nextIdx = PickNextRandomMinigameIndex();
        if (nextIdx < 0 || nextIdx == currentMinigameIndex) return;

        activeSlideHeight = ComputeSlideHeight(currentMinigameIndex);
        candidateNextIndex = nextIdx;
        scrollState = ScrollState.Dragging;
        dragScrollOriginLocalY = currentLocal.y;
        lastDragLocalPos = currentLocal;
        currentDragOffsetY = 0f;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // Nonaktifkan klik & jeda minigame sementara selama proses tarik scroll
        SetReelRaycastsEnabled(currentMinigameIndex, false);
        SetMinigamePaused(currentMinigameIndex, true);

        // Siapkan konten baru tepat di bawah layar dan reset kondisinya
        PrepareCandidateReel(candidateNextIndex, activeSlideHeight);
        ApplyReelScrollOffsets(currentMinigameIndex, candidateNextIndex, currentDragOffsetY, activeSlideHeight);
    }

    private void BeginSnapToNext()
    {
        if (candidateNextIndex < 0)
        {
            scrollState = ScrollState.Idle;
            return;
        }

        scrollState = ScrollState.SnappingToNext;
        GameplayAudioManager.PlayReelSwipe();
        SetReelRaycastsEnabled(currentMinigameIndex, false);
        SetReelRaycastsEnabled(candidateNextIndex, false);
        UpdateTopStatusBar("");
    }

    private void BeginSnapBack()
    {
        scrollState = ScrollState.SnappingBack;
        SetReelRaycastsEnabled(currentMinigameIndex, false);
        if (candidateNextIndex >= 0)
        {
            SetReelRaycastsEnabled(candidateNextIndex, false);
        }
        UpdateTopStatusBar("");
    }

    private void UpdateSnapAnimations()
    {
        if (scrollState == ScrollState.SnappingToNext)
        {
            float remaining = activeSlideHeight - currentDragOffsetY;
            float minSpeed = activeSlideHeight * 1.35f;
            float step = Mathf.Max(remaining * snapAnimationSpeed, minSpeed) * Time.deltaTime;
            currentDragOffsetY = Mathf.MoveTowards(currentDragOffsetY, activeSlideHeight, step);

            ApplyReelScrollOffsets(currentMinigameIndex, candidateNextIndex, currentDragOffsetY, activeSlideHeight);

            if (activeSlideHeight - currentDragOffsetY <= 0.5f)
            {
                CompleteTransitionToNext();
            }
        }
        else if (scrollState == ScrollState.SnappingBack)
        {
            float minSpeed = activeSlideHeight * 1.35f;
            float step = Mathf.Max(currentDragOffsetY * snapAnimationSpeed, minSpeed) * Time.deltaTime;
            currentDragOffsetY = Mathf.MoveTowards(currentDragOffsetY, 0f, step);

            ApplyReelScrollOffsets(currentMinigameIndex, candidateNextIndex, currentDragOffsetY, activeSlideHeight);

            if (currentDragOffsetY <= 0.5f)
            {
                CompleteSnapBack();
            }
        }
    }

    private void CompleteTransitionToNext()
    {
        int oldIndex = currentMinigameIndex;
        int newIndex = candidateNextIndex;

        if (oldIndex >= 0 && oldIndex != newIndex)
        {
            SetMinigamePaused(oldIndex, false);
            SetReelActive(oldIndex, false);
            ResetReelAnchoredPosition(oldIndex);
            SetReelRaycastsEnabled(oldIndex, true);
        }

        currentMinigameIndex = newIndex;
        candidateNextIndex = -1;
        currentDragOffsetY = 0f;
        scrollState = ScrollState.Idle;

        if (newIndex >= 0)
        {
            SetReelActive(newIndex, true);
            ResetReelAnchoredPosition(newIndex);
            SetReelRaycastsEnabled(newIndex, true);
            SetMinigamePaused(newIndex, false);
        }

        UpdateTopStatusBar("");

        // Aktifkan jeda buffering 3 - 5 detik setiap kali berpindah ke Reel baru
        StartReelBufferingCooldown();

        if (adPopupManager != null)
        {
            adPopupManager.NotifyReelChanged();
        }
    }

    private void CompleteSnapBack()
    {
        if (candidateNextIndex >= 0 && candidateNextIndex != currentMinigameIndex)
        {
            SetMinigamePaused(candidateNextIndex, false);
            SetReelActive(candidateNextIndex, false);
            ResetReelAnchoredPosition(candidateNextIndex);
            SetReelRaycastsEnabled(candidateNextIndex, true);
        }

        if (currentMinigameIndex >= 0)
        {
            SetReelActive(currentMinigameIndex, true);
            ResetReelAnchoredPosition(currentMinigameIndex);
            SetReelRaycastsEnabled(currentMinigameIndex, true);
            SetMinigamePaused(currentMinigameIndex, false);
        }

        candidateNextIndex = -1;
        currentDragOffsetY = 0f;
        scrollState = ScrollState.Idle;
        UpdateTopStatusBar("");
    }

    private void PrepareCandidateReel(int index, float slideHeight)
    {
        if (index < 0 || index >= ReelCount) return;

        RectTransform rt = reelRects[index];
        if (rt != null)
        {
            rt.anchoredPosition = baseReelAnchoredPositions[index] + new Vector2(0f, -slideHeight);
        }

        SetReelRaycastsEnabled(index, false);
        SetReelActive(index, true);
        ResetReelMinigame(index);
        SetMinigamePaused(index, true);
    }

    private void ApplyReelScrollOffsets(int outgoingIndex, int incomingIndex, float dragY, float slideHeight)
    {
        if (outgoingIndex >= 0 && outgoingIndex < ReelCount && reelRects[outgoingIndex] != null)
        {
            reelRects[outgoingIndex].anchoredPosition =
                baseReelAnchoredPositions[outgoingIndex] + new Vector2(0f, dragY);
        }

        if (incomingIndex >= 0 && incomingIndex < ReelCount && reelRects[incomingIndex] != null)
        {
            reelRects[incomingIndex].anchoredPosition =
                baseReelAnchoredPositions[incomingIndex] + new Vector2(0f, dragY - slideHeight);
        }
    }

    private float ComputeSlideHeight(int referenceReelIndex)
    {
        if (referenceReelIndex >= 0 && referenceReelIndex < ReelCount && reelRects[referenceReelIndex] != null)
        {
            float h = reelRects[referenceReelIndex].rect.height;
            if (h > 10f) return h;
        }

        if (selfRect != null && selfRect.rect.height > 10f)
        {
            return selfRect.rect.height * 0.92f;
        }

        return 1000f;
    }

    private int GetActiveLevelNumber()
    {
        if (ConsciousnessManager.Instance != null)
        {
            return ConsciousnessManager.Instance.CurrentLevel;
        }
        return 1;
    }

    private int GetAllowedReelCountForCurrentLevel()
    {
        // Level 1 hanya memunculkan 2 minigame pertama (0: Tap the Cat, 1: Kuis MTK)
        // Level 2 & Level 3 memunculkan seluruh 4 minigame (+ Tangkap Bakso & osu!)
        int level = GetActiveLevelNumber();
        return level <= 1 ? 2 : ReelCount;
    }

    public void ApplyLevelRulesToPhone(int levelNumber)
    {
        if (adPopupManager != null)
        {
            // Level 1 tanpa iklan; Level 2 & Level 3 iklan aktif
            adPopupManager.SetAdsEnabled(levelNumber >= 2);
        }

        if (wifeChatEvent != null && wifeChatEvent.isActiveAndEnabled)
        {
            wifeChatEvent.ResetEventForNewGame();
        }
    }

    public void TriggerWifeChatMilestoneIfEligible()
    {
        if (wifeChatEvent != null && wifeChatEvent.isActiveAndEnabled)
        {
            wifeChatEvent.TriggerWifeChatByDistanceMilestone();
        }
    }

    private int PickNextRandomMinigameIndex()
    {
        int allowedCount = Mathf.Clamp(GetAllowedReelCountForCurrentLevel(), 2, ReelCount);

        if (currentMinigameIndex < 0 || currentMinigameIndex >= allowedCount)
        {
            return Random.Range(0, allowedCount);
        }

        return (currentMinigameIndex + Random.Range(1, allowedCount)) % allowedCount;
    }

    private bool CanInteractWithFeed()
    {
        if (!isUnlocked) return false;
        if (isReelBuffering) return false;
        if (adPopupManager != null && adPopupManager.HasActiveAds) return false;
        if (wifeChatEvent != null && wifeChatEvent.IsChatWindowOpen) return false;
        if (ConsciousnessManager.Instance != null && ConsciousnessManager.Instance.IsGameEnded) return false;
        return true;
    }

    private Camera GetEventCamera()
    {
        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null && parentCanvas.worldCamera != null)
            return parentCanvas.worldCamera;
        return Camera.main;
    }

    private bool TryGetCanvasLocalPoint(Vector2 screenPos, out Vector2 localPoint)
    {
        if (selfRect == null) selfRect = transform as RectTransform;
        if (selfRect == null)
        {
            localPoint = Vector2.zero;
            return false;
        }

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            selfRect,
            screenPos,
            GetEventCamera(),
            out localPoint
        );
    }

    private bool IsPointerOverThisPhoneCanvas()
    {
        if (EventSystem.current == null || Mouse.current == null) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };

        raycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, raycastResults);

        if (raycastResults.Count == 0) return false;

        // Cek apakah elemen UI teratas di bawah kursor berada di dalam layar HP ini
        GameObject topHit = raycastResults[0].gameObject;
        return topHit != null && topHit.transform.IsChildOf(transform);
    }

    public void ShowLockScreen()
    {
        isUnlocked = false;
        isTrackingPointer = false;
        dragIntentLockedHorizontal = false;
        scrollState = ScrollState.Idle;
        candidateNextIndex = -1;
        currentDragOffsetY = 0f;
        currentMinigameIndex = -1;

        for (int i = 0; i < ReelCount; i++)
        {
            SetMinigamePaused(i, false);
            ResetReelAnchoredPosition(i);
            SetReelRaycastsEnabled(i, true);
            SetReelActive(i, false);
        }

        if (adPopupManager != null) adPopupManager.ClearAllAds();
        if (wifeChatEvent != null && wifeChatEvent.isActiveAndEnabled) wifeChatEvent.ResetEventForNewGame();

        isReelBuffering = false;
        reelBufferTimer = 0f;
        if (reelBufferOverlayPanel != null) reelBufferOverlayPanel.SetActive(false);

        // Sembunyikan UI.png selama masih di layar kunci Sidik Jari
        if (fypOverlayImage != null)
        {
            fypOverlayImage.gameObject.SetActive(false);
        }

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

        // Munculkan UI.png tepat setelah konfirmasi Sidik Jari berhasil
        if (fypOverlayImage != null)
        {
            EnsureFypOverlayBuilt();
            if (wifeChatEvent != null)
            {
                wifeChatEvent.transform.SetAsLastSibling();
            }
            if (adPopupManager != null)
            {
                adPopupManager.transform.SetAsLastSibling();
            }
            fypOverlayImage.gameObject.SetActive(true);
        }

        if (wifeChatEvent != null && wifeChatEvent.isActiveAndEnabled)
        {
            wifeChatEvent.NotifyPhoneUnlocked();
        }

        // Skala Max 20: Bonus kecil +1.5 saat berhasil membuka kunci Sidik Jari
        AddConsciousnessReward(1.5f);

        SwitchToNextRandomMinigame(firstOpen: true);
    }

    public void SwitchToNextRandomMinigame(bool firstOpen = false)
    {
        CacheReelLayoutDefaults();

        if (!firstOpen && currentMinigameIndex >= 0 && scrollState == ScrollState.Idle && isUnlocked)
        {
            int nextIndex = PickNextRandomMinigameIndex();
            activeSlideHeight = ComputeSlideHeight(currentMinigameIndex);
            candidateNextIndex = nextIndex;
            currentDragOffsetY = 0f;

            SetMinigamePaused(currentMinigameIndex, true);
            PrepareCandidateReel(candidateNextIndex, activeSlideHeight);
            BeginSnapToNext();
            return;
        }

        int chosenIndex = PickNextRandomMinigameIndex();
        currentMinigameIndex = chosenIndex;
        candidateNextIndex = -1;
        scrollState = ScrollState.Idle;
        currentDragOffsetY = 0f;

        for (int i = 0; i < ReelCount; i++)
        {
            bool active = i == currentMinigameIndex;
            SetMinigamePaused(i, false);
            ResetReelAnchoredPosition(i);
            SetReelRaycastsEnabled(i, true);
            SetReelActive(i, active);

            if (active)
            {
                ResetReelMinigame(i);
            }
        }

        UpdateTopStatusBar("");

        if (!firstOpen && adPopupManager != null)
        {
            adPopupManager.NotifyReelChanged();
        }
    }

    private void SetReelActive(int index, bool active)
    {
        MonoBehaviour target = GetMinigameComponent(index);
        if (target != null && target.gameObject.activeSelf != active)
        {
            target.gameObject.SetActive(active);
        }
    }

    private void ResetReelMinigame(int index)
    {
        switch (index)
        {
            case 0:
                if (minigameTapCat != null) minigameTapCat.ResetMinigame();
                break;
            case 1:
                if (minigameMath != null) minigameMath.ResetMinigame();
                break;
            case 2:
                if (minigameBakso != null) minigameBakso.ResetMinigame();
                break;
            case 3:
                if (minigameOsu != null) minigameOsu.ResetMinigame();
                break;
        }
    }

    private void SetMinigamePaused(int index, bool paused)
    {
        if (index == 2 && minigameBakso != null)
        {
            minigameBakso.SetGameplayPaused(paused);
        }
        else if (index == 3 && minigameOsu != null)
        {
            minigameOsu.SetGameplayPaused(paused);
        }
    }

    private void ResetReelAnchoredPosition(int index)
    {
        if (index < 0 || index >= ReelCount) return;
        if (reelRects[index] != null && baseLayoutCached)
        {
            reelRects[index].anchoredPosition = baseReelAnchoredPositions[index];
        }
    }

    private void SetReelRaycastsEnabled(int index, bool enabled)
    {
        if (index < 0 || index >= ReelCount) return;
        if (reelCanvasGroups[index] != null)
        {
            reelCanvasGroups[index].blocksRaycasts = enabled;
        }
    }

    private MonoBehaviour GetMinigameComponent(int index)
    {
        switch (index)
        {
            case 0: return minigameTapCat;
            case 1: return minigameMath;
            case 2: return minigameBakso;
            case 3: return minigameOsu;
            default: return null;
        }
    }

    private void AddConsciousnessReward(float amount)
    {
        if (adPopupManager != null && adPopupManager.HasActiveAds)
            return;

        if (amount > 0f)
        {
            totalDopamineEarned += amount;
            if (ConsciousnessManager.Instance != null)
            {
                ConsciousnessManager.Instance.AddConsciousness(amount);
            }
        }
        else if (amount < 0f)
        {
            if (ConsciousnessManager.Instance != null)
            {
                ConsciousnessManager.Instance.ReduceConsciousness(-amount);
            }
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
