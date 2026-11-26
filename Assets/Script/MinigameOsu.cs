using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Minigame 4: Rhythm Tap & Drag Slider ala osu! (Reel_4_OsuRhythm)
/// - Kombinasi 2 jenis beat dalam 1 sesi (default 6 beat):
///   1) TAP CIRCLE: Klik lingkaran bernomor saat cincin Approach Ring mengecil pas ke tepi lingkaran.
///   2) DRAG SLIDER ("Tarik"): Klik & tarik (drag) bola slider mengikuti jalur track menuju titik ★ TUJUAN.
/// - Mendukung AutoBindExistingUI() agar tidak merusak perubahan posisi/layout manual di Hierarchy.
/// </summary>
public class MinigameOsu : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Pengaturan Gameplay osu!")]
    [Tooltip("Jumlah total beat (Tap + Slider) dalam 1 sesi Reel")]
    [SerializeField] private int totalBeatsPerSession = 6;
    [Tooltip("Durasi cincin mengecil pada Tap Circle (detik)")]
    [SerializeField] private float tapApproachDuration = 1.35f;
    [Tooltip("Durasi waktu untuk menarik Slider sampai ujung (detik)")]
    [SerializeField] private float sliderDuration = 2.10f;
    [Tooltip("Reward kesadaran saat Tap Circle berhasil (Skala Max 20)")]
    [SerializeField] private float tapRewardConsciousness = 0.8f;
    [Tooltip("Reward kesadaran saat Drag Slider berhasil ditarik sampai ujung")]
    [SerializeField] private float sliderRewardConsciousness = 1.1f;
    [Tooltip("Bonus kesadaran saat Stage Clear (minimal 4 beat berhasil)")]
    [SerializeField] private float stageClearBonusConsciousness = 2.5f;
    [Tooltip("Penalti pengurangan kesadaran saat Miss beat")]
    [SerializeField] private float missPenaltyConsciousness = 1.5f;

    [Header("Referensi UI (Otomatis Terhubung)")]
    [SerializeField] private Image backgroundPanel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text statusScoreText;
    [SerializeField] private RectTransform playAreaRect;
    [SerializeField] private Text judgementFeedbackText;

    // Tap Note UI
    [SerializeField] private RectTransform tapNoteRoot;
    [SerializeField] private RectTransform tapApproachRing;
    [SerializeField] private Button tapHitButton;
    [SerializeField] private Image tapHitCircleImage;
    [SerializeField] private Text tapNumberText;

    // Slider Note UI
    [SerializeField] private RectTransform sliderNoteRoot;
    [SerializeField] private RectTransform sliderTrackBar;
    [SerializeField] private Image sliderTrackFillImage;
    [SerializeField] private RectTransform sliderEndTarget;
    [SerializeField] private RectTransform sliderDragBall;
    [SerializeField] private RectTransform sliderApproachRing;
    [SerializeField] private Text sliderBallText;

    // Result Overlay
    [SerializeField] private GameObject resultOverlayPanel;
    [SerializeField] private Text resultOverlayText;

    private enum BeatType
    {
        TapCircle,
        DragSlider
    }

    private BeatType currentBeatType = BeatType.TapCircle;
    private int currentBeatIndex = 0; // 1..totalBeatsPerSession
    private int hitSuccessCount = 0;
    private float beatTimer = 0f;
    private float activeBeatDuration = 1.35f;
    private float nextBeatDelayTimer = 0f;
    private float judgementHideTimer = 0f;

    private bool isWaitingNextBeat = false;
    private bool isSessionFinished = false;
    private bool isGameplayPaused = false;
    private bool isInitializedForPlay = false;

    // Slider Drag State
    private bool isDraggingSlider = false;
    private Vector2 sliderStartLocalPos;
    private Vector2 sliderEndLocalPos;
    private float sliderCurrentProgress = 0f;

    // Procedural Circular & Hollow Ring Sprites (agar objek osu! bulat murni & cincin tengahnya bolong)
    private static Sprite cachedSolidCircleWithBorderSprite;
    private static Sprite cachedHollowRingSprite;
    private static Sprite cachedRoundedCapsuleSprite;

    public Action<float> OnConsciousnessReward;

    /// <summary>
    /// True saat pemain sedang aktif menahan & menarik bola Slider osu!
    /// (Mengunci scroll vertikal FYP sementara agar tarikan slider tidak membajak scroll layar HP).
    /// </summary>
    public bool IsDraggingSlider => isDraggingSlider && !isSessionFinished && !isGameplayPaused;

    public void SetGameplayPaused(bool paused)
    {
        isGameplayPaused = paused;
        if (paused)
        {
            isDraggingSlider = false;
        }
    }

    public void AutoBindExistingUI()
    {
        if (backgroundPanel == null) backgroundPanel = GetComponent<Image>();

        if (titleText == null)
        {
            Transform t = transform.Find("Osu_Title");
            if (t != null) titleText = t.GetComponent<Text>();
        }

        if (statusScoreText == null)
        {
            Transform t = transform.Find("Osu_StatusScore");
            if (t != null) statusScoreText = t.GetComponent<Text>();
        }

        if (playAreaRect == null)
        {
            Transform t = transform.Find("Osu_PlayArea");
            if (t != null) playAreaRect = t.GetComponent<RectTransform>();
        }

        if (playAreaRect != null)
        {
            if (judgementFeedbackText == null)
            {
                Transform t = playAreaRect.Find("Osu_JudgementText");
                if (t != null) judgementFeedbackText = t.GetComponent<Text>();
            }

            if (tapNoteRoot == null)
            {
                Transform t = playAreaRect.Find("TapNote_Root");
                if (t != null) tapNoteRoot = t.GetComponent<RectTransform>();
            }

            if (tapNoteRoot != null)
            {
                if (tapApproachRing == null)
                {
                    Transform r = tapNoteRoot.Find("Approach_Ring");
                    if (r != null) tapApproachRing = r.GetComponent<RectTransform>();
                }
                if (tapHitButton == null)
                {
                    Transform b = tapNoteRoot.Find("Hit_Circle_Button");
                    if (b != null) tapHitButton = b.GetComponent<Button>();
                }
                if (tapHitButton != null)
                {
                    if (tapHitCircleImage == null) tapHitCircleImage = tapHitButton.GetComponent<Image>();
                    if (tapNumberText == null) tapNumberText = tapHitButton.GetComponentInChildren<Text>(true);
                }
            }

            if (sliderNoteRoot == null)
            {
                Transform t = playAreaRect.Find("SliderNote_Root");
                if (t != null) sliderNoteRoot = t.GetComponent<RectTransform>();
            }

            if (sliderNoteRoot != null)
            {
                if (sliderTrackBar == null)
                {
                    Transform tr = sliderNoteRoot.Find("Slider_TrackBar");
                    if (tr != null) sliderTrackBar = tr.GetComponent<RectTransform>();
                }
                if (sliderTrackBar != null && sliderTrackFillImage == null)
                {
                    sliderTrackFillImage = sliderTrackBar.GetComponent<Image>();
                }
                if (sliderEndTarget == null)
                {
                    Transform et = sliderNoteRoot.Find("Slider_EndTarget");
                    if (et != null) sliderEndTarget = et.GetComponent<RectTransform>();
                }
                if (sliderDragBall == null)
                {
                    Transform db = sliderNoteRoot.Find("Slider_DragBall");
                    if (db != null) sliderDragBall = db.GetComponent<RectTransform>();
                }
                if (sliderDragBall != null)
                {
                    if (sliderApproachRing == null)
                    {
                        Transform ar = sliderDragBall.Find("Slider_ApproachRing");
                        if (ar != null) sliderApproachRing = ar.GetComponent<RectTransform>();
                    }
                    if (sliderBallText == null)
                    {
                        sliderBallText = sliderDragBall.GetComponentInChildren<Text>(true);
                    }
                }
            }

            if (resultOverlayPanel == null)
            {
                Transform ro = playAreaRect.Find("Osu_ResultOverlay");
                if (ro != null) resultOverlayPanel = ro.gameObject;
            }
            if (resultOverlayPanel != null && resultOverlayText == null)
            {
                resultOverlayText = resultOverlayPanel.GetComponentInChildren<Text>(true);
            }
        }
    }

    public void BuildPlaceholderUI()
    {
        AutoBindExistingUI();

        if (backgroundPanel == null)
        {
            backgroundPanel = gameObject.AddComponent<Image>();
        }
        backgroundPanel.color = new Color(0.09f, 0.07f, 0.16f, 1f);
        backgroundPanel.raycastTarget = true;

        if (titleText == null)
        {
            titleText = PhoneUIHelper.CreateText(
                "Osu_Title",
                transform,
                "🎵 OSU! TAP & TARIK SLIDER",
                new Color(1f, 0.45f, 0.85f),
                new Vector2(0.04f, 0.81f),
                new Vector2(0.96f, 0.91f),
                TextAnchor.MiddleCenter,
                160
            );
        }

        if (statusScoreText == null)
        {
            statusScoreText = PhoneUIHelper.CreateText(
                "Osu_StatusScore",
                transform,
                "BEAT: 1 / 6  |  SKOR: 0",
                Color.white,
                new Vector2(0.04f, 0.72f),
                new Vector2(0.96f, 0.80f),
                TextAnchor.MiddleCenter,
                140
            );
        }

        if (playAreaRect == null)
        {
            playAreaRect = PhoneUIHelper.CreatePanel(
                "Osu_PlayArea",
                transform,
                new Color(0.05f, 0.04f, 0.10f, 1f),
                new Vector2(0.04f, 0.08f),
                new Vector2(0.96f, 0.70f)
            );
            playAreaRect.GetComponent<Image>().raycastTarget = true;
        }

        BuildTapNoteUI();
        BuildSliderNoteUI();
        BuildJudgementAndResultUI();
        ApplyOsuCircularSprites();
        BindEvents();
    }

    /// <summary>
    /// Memasang sprite lingkaran bulat penuh (dengan ring putih di tepinya) pada Hit Circle / Slider Ball,
    /// serta sprite Cincin Bolong (Hollow Ring transparan di tengah) pada Approach Ring.
    /// Dipanggil baik saat UI baru dibuat maupun saat UI sudah ada di Hierarchy.
    /// </summary>
    private void ApplyOsuCircularSprites()
    {
        Sprite circleSprite = GetOrCreateSolidCircleWithBorderSprite();
        Sprite hollowRingSprite = GetOrCreateHollowRingSprite();
        Sprite capsuleSprite = GetOrCreateRoundedCapsuleSprite();

        // 1. Tap Note: Lingkaran Hit Bulat + Approach Ring Cincin Bolong di Depan
        if (tapHitCircleImage != null)
        {
            tapHitCircleImage.sprite = circleSprite;
            tapHitCircleImage.type = Image.Type.Simple;
            tapHitCircleImage.color = new Color(0.96f, 0.22f, 0.62f, 1f);
        }

        if (tapApproachRing != null)
        {
            Image ringImg = tapApproachRing.GetComponent<Image>();
            if (ringImg != null)
            {
                ringImg.sprite = hollowRingSprite;
                ringImg.type = Image.Type.Simple;
                ringImg.color = new Color(0.30f, 0.98f, 1.0f, 0.96f);
                ringImg.raycastTarget = false;
            }
            // Taruh di urutan paling atas dalam TapNote_Root agar cincin bolong tetap terlihat saat menyentuh tepi lingkaran
            tapApproachRing.SetAsLastSibling();
        }

        // 2. Slider Note: Bola Tarik Bulat, Target Ujung Bulat, Cincin Bolong, & Jalur Track Kapsul
        if (sliderTrackFillImage != null)
        {
            sliderTrackFillImage.sprite = capsuleSprite;
            sliderTrackFillImage.type = Image.Type.Sliced;
        }

        if (sliderEndTarget != null)
        {
            Image endImg = sliderEndTarget.GetComponent<Image>();
            if (endImg != null)
            {
                endImg.sprite = circleSprite;
                endImg.type = Image.Type.Simple;
                endImg.color = new Color(0.15f, 0.85f, 0.55f, 0.95f);
            }
        }

        if (sliderDragBall != null)
        {
            Image ballImg = sliderDragBall.GetComponent<Image>();
            if (ballImg != null)
            {
                ballImg.sprite = circleSprite;
                ballImg.type = Image.Type.Simple;
                ballImg.color = new Color(1.0f, 0.55f, 0.12f, 1f);
            }
        }

        if (sliderApproachRing != null)
        {
            Image sRingImg = sliderApproachRing.GetComponent<Image>();
            if (sRingImg != null)
            {
                sRingImg.sprite = hollowRingSprite;
                sRingImg.type = Image.Type.Simple;
                sRingImg.color = new Color(1.0f, 0.95f, 0.35f, 0.96f);
                sRingImg.raycastTarget = false;
            }
            sliderApproachRing.SetAsLastSibling();
        }
    }

    private static Sprite GetOrCreateSolidCircleWithBorderSprite()
    {
        if (cachedSolidCircleWithBorderSprite != null) return cachedSolidCircleWithBorderSprite;

        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "Osu_HitCircle_Procedural";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float maxRadius = size * 0.48f;
        float borderInnerRadius = maxRadius * 0.84f; // Tepi luar berupa list putih ala osu! hit circle

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > maxRadius + 1.5f)
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                }
                else
                {
                    float outerAlpha = Mathf.Clamp01((maxRadius + 1.0f - dist) / 2.0f);

                    // Di bagian dalam (dist < borderInnerRadius), warna putih akan dikalikan dengan Image.color.
                    // Di bagian pinggir (borderInnerRadius..maxRadius), kita buat sedikit lebih terang/tegas dengan ring gelap tipis di perbatasannya agar kontras.
                    float rimHighlight = Mathf.SmoothStep(borderInnerRadius - 2f, borderInnerRadius + 1.5f, dist);
                    float innerShadow = (dist >= borderInnerRadius - 5f && dist < borderInnerRadius)
                        ? 0.72f
                        : 1.0f;

                    byte c = (byte)Mathf.RoundToInt(Mathf.Lerp(235f * innerShadow, 255f, rimHighlight));
                    byte a = (byte)Mathf.RoundToInt(outerAlpha * 255f);
                    pixels[y * size + x] = new Color32(c, c, c, a);
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);

        cachedSolidCircleWithBorderSprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );
        return cachedSolidCircleWithBorderSprite;
    }

    private static Sprite GetOrCreateHollowRingSprite()
    {
        if (cachedHollowRingSprite != null) return cachedHollowRingSprite;

        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "Osu_HollowApproachRing_Procedural";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        // Ketebalan cincin dibuat jelas & bagian tengahnya 100% bolong transparan (alpha = 0)
        float outerRadius = size * 0.475f;
        float innerRadius = size * 0.405f;
        float midRadius = (outerRadius + innerRadius) * 0.5f;
        float halfThickness = (outerRadius - innerRadius) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float distFromRingCenter = Mathf.Abs(dist - midRadius);
                if (distFromRingCenter > halfThickness + 1.8f)
                {
                    // Tengah cincin & luar cincin 100% bolong (transparan)
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                }
                else
                {
                    float alpha = Mathf.Clamp01((halfThickness + 1.2f - distFromRingCenter) / 2.0f);
                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);

        cachedHollowRingSprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );
        return cachedHollowRingSprite;
    }

    private static Sprite GetOrCreateRoundedCapsuleSprite()
    {
        if (cachedRoundedCapsuleSprite != null) return cachedRoundedCapsuleSprite;

        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "Osu_SliderCapsule_Procedural";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float maxRadius = size * 0.48f;
        float innerRadius = maxRadius * 0.78f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > maxRadius + 1.5f)
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                }
                else
                {
                    float outerAlpha = Mathf.Clamp01((maxRadius + 1.0f - dist) / 2.0f);
                    // Buat tepi track slider lebih terang seperti border track osu!
                    float isBorder = dist >= innerRadius ? 1.0f : 0.78f;
                    byte c = (byte)Mathf.RoundToInt(255f * isBorder);
                    byte a = (byte)Mathf.RoundToInt(outerAlpha * 255f);
                    pixels[y * size + x] = new Color32(c, c, c, a);
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);

        // 9-slice border di tengah supaya ujung kiri-kanan track selalu melengkung setengah lingkaran (kapsul)
        float slice = size * 0.48f;
        cachedRoundedCapsuleSprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(slice, slice, slice, slice)
        );
        return cachedRoundedCapsuleSprite;
    }

    private void BuildTapNoteUI()
    {
        if (playAreaRect == null || tapNoteRoot != null) return;

        tapNoteRoot = PhoneUIHelper.CreatePanel(
            "TapNote_Root",
            playAreaRect,
            new Color(0f, 0f, 0f, 0f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f)
        );
        tapNoteRoot.sizeDelta = new Vector2(360f, 360f);
        tapNoteRoot.GetComponent<Image>().raycastTarget = false;

        // Approach Ring (Cincin Luar Mengecil)
        tapApproachRing = PhoneUIHelper.CreatePanel(
            "Approach_Ring",
            tapNoteRoot,
            new Color(0.25f, 0.95f, 1.0f, 0.42f),
            Vector2.zero,
            Vector2.one
        );
        tapApproachRing.GetComponent<Image>().raycastTarget = false;

        // Lingkaran Hit Utama di Tengah
        tapHitButton = PhoneUIHelper.CreateButton(
            "Hit_Circle_Button",
            tapNoteRoot,
            "1\nTAP!",
            new Color(0.96f, 0.22f, 0.62f, 1f),
            Color.white,
            new Vector2(0.10f, 0.10f),
            new Vector2(0.90f, 0.90f),
            out tapNumberText,
            240
        );
        tapHitCircleImage = tapHitButton.GetComponent<Image>();
    }

    private void BuildSliderNoteUI()
    {
        if (playAreaRect == null || sliderNoteRoot != null) return;

        sliderNoteRoot = PhoneUIHelper.CreatePanel(
            "SliderNote_Root",
            playAreaRect,
            new Color(0f, 0f, 0f, 0f),
            Vector2.zero,
            Vector2.one
        );
        sliderNoteRoot.GetComponent<Image>().raycastTarget = false;

        // 1. Jalur Track Penghubung
        sliderTrackBar = PhoneUIHelper.CreatePanel(
            "Slider_TrackBar",
            sliderNoteRoot,
            new Color(0.25f, 0.55f, 0.95f, 0.65f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f)
        );
        sliderTrackBar.pivot = new Vector2(0f, 0.5f);
        sliderTrackBar.sizeDelta = new Vector2(500f, 125f);
        sliderTrackFillImage = sliderTrackBar.GetComponent<Image>();
        sliderTrackFillImage.raycastTarget = false;

        // 2. Titik Ujung Target Slider
        sliderEndTarget = PhoneUIHelper.CreatePanel(
            "Slider_EndTarget",
            sliderNoteRoot,
            new Color(0.15f, 0.85f, 0.55f, 0.95f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f)
        );
        sliderEndTarget.sizeDelta = new Vector2(310f, 310f);
        sliderEndTarget.GetComponent<Image>().raycastTarget = false;

        PhoneUIHelper.CreateText(
            "End_Label",
            sliderEndTarget,
            "★\nTUJUAN",
            Color.white,
            new Vector2(0.05f, 0.05f),
            new Vector2(0.95f, 0.95f),
            TextAnchor.MiddleCenter,
            180
        );

        // 3. Bola Slider yang Ditarik
        sliderDragBall = PhoneUIHelper.CreatePanel(
            "Slider_DragBall",
            sliderNoteRoot,
            new Color(1.0f, 0.55f, 0.12f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f)
        );
        sliderDragBall.sizeDelta = new Vector2(340f, 340f);
        sliderDragBall.GetComponent<Image>().raycastTarget = true;

        sliderApproachRing = PhoneUIHelper.CreatePanel(
            "Slider_ApproachRing",
            sliderDragBall,
            new Color(1f, 0.92f, 0.3f, 0.38f),
            Vector2.zero,
            Vector2.one
        );
        sliderApproachRing.GetComponent<Image>().raycastTarget = false;

        sliderBallText = PhoneUIHelper.CreateText(
            "Ball_Label",
            sliderDragBall,
            "1\nTARIK ➔",
            Color.white,
            new Vector2(0.05f, 0.05f),
            new Vector2(0.95f, 0.95f),
            TextAnchor.MiddleCenter,
            210
        );
    }

    private void BuildJudgementAndResultUI()
    {
        if (playAreaRect == null) return;

        if (judgementFeedbackText == null)
        {
            judgementFeedbackText = PhoneUIHelper.CreateText(
                "Osu_JudgementText",
                playAreaRect,
                "PERFECT!",
                new Color(0.35f, 1f, 0.55f),
                new Vector2(0.10f, 0.82f),
                new Vector2(0.90f, 0.96f),
                TextAnchor.MiddleCenter,
                190
            );
            judgementFeedbackText.gameObject.SetActive(false);
        }

        if (resultOverlayPanel == null)
        {
            RectTransform overlayRt = PhoneUIHelper.CreatePanel(
                "Osu_ResultOverlay",
                playAreaRect,
                new Color(0.06f, 0.04f, 0.12f, 0.96f),
                Vector2.zero,
                Vector2.one
            );
            overlayRt.GetComponent<Image>().raycastTarget = false;
            resultOverlayPanel = overlayRt.gameObject;

            resultOverlayText = PhoneUIHelper.CreateText(
                "Result_Text",
                overlayRt,
                "STAGE CLEAR\n\nSKOR: 6 / 6",
                new Color(0.45f, 0.95f, 1f),
                new Vector2(0.06f, 0.12f),
                new Vector2(0.94f, 0.88f),
                TextAnchor.MiddleCenter,
                180
            );

            resultOverlayPanel.SetActive(false);
        }
    }

    private void BindEvents()
    {
        if (tapHitButton != null)
        {
            tapHitButton.onClick.RemoveAllListeners();
            tapHitButton.onClick.AddListener(HandleTapCircleClicked);
        }
    }

    private void OnValidate()
    {
        AutoBindExistingUI();
        ApplyOsuCircularSprites();
    }

    private void Awake()
    {
        BuildPlaceholderUI();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            ResetMinigame();
        }
    }

    public void ResetMinigame()
    {
        BuildPlaceholderUI();
        BindEvents();

        currentBeatIndex = 0;
        hitSuccessCount = 0;
        isSessionFinished = false;
        isGameplayPaused = false;
        isWaitingNextBeat = false;
        isDraggingSlider = false;
        isInitializedForPlay = true;
        judgementHideTimer = 0f;

        if (resultOverlayPanel != null) resultOverlayPanel.SetActive(false);
        if (judgementFeedbackText != null) judgementFeedbackText.gameObject.SetActive(false);

        SpawnNextBeat();
    }

    private void Update()
    {
        if (!isInitializedForPlay)
        {
            ResetMinigame();
        }

        if (isGameplayPaused || isSessionFinished) return;

        if (judgementHideTimer > 0f)
        {
            judgementHideTimer -= Time.deltaTime;
            if (judgementHideTimer <= 0f && judgementFeedbackText != null)
            {
                judgementFeedbackText.gameObject.SetActive(false);
            }
        }

        if (isWaitingNextBeat)
        {
            nextBeatDelayTimer -= Time.deltaTime;
            if (nextBeatDelayTimer <= 0f)
            {
                isWaitingNextBeat = false;
                SpawnNextBeat();
            }
            return;
        }

        beatTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(beatTimer / Mathf.Max(0.2f, activeBeatDuration));

        if (currentBeatType == BeatType.TapCircle)
        {
            // Animasi Approach Ring (cincin bolong) mengecil dari 2.15x menuju 0.80x (tepat menyatu dengan tepi Hit_Circle_Button)
            if (tapApproachRing != null)
            {
                float ringScale = Mathf.Lerp(2.15f, 0.80f, progress);
                tapApproachRing.localScale = new Vector3(ringScale, ringScale, 1f);
            }
        }
        else if (currentBeatType == BeatType.DragSlider)
        {
            if (sliderApproachRing != null)
            {
                float ringScale = isDraggingSlider
                    ? 1.15f + Mathf.Sin(Time.time * 14f) * 0.08f
                    : Mathf.Lerp(1.95f, 1.0f, progress);
                sliderApproachRing.localScale = new Vector3(ringScale, ringScale, 1f);
            }
        }

        // Waktu habis sebelum selesai -> MISS!
        if (beatTimer >= activeBeatDuration)
        {
            HandleBeatMissed();
        }
    }

    private void SpawnNextBeat()
    {
        isDraggingSlider = false;
        sliderCurrentProgress = 0f;

        if (currentBeatIndex >= totalBeatsPerSession)
        {
            FinishOsuSession();
            return;
        }

        currentBeatIndex++;
        UpdateHeaderScoreUI();

        // Pastikan ada beberapa Slider yang ditarik (misal di beat genap 2, 4, atau acak)
        bool spawnSlider = (currentBeatIndex % 2 == 0) || (UnityEngine.Random.value < 0.35f);
        currentBeatType = spawnSlider ? BeatType.DragSlider : BeatType.TapCircle;

        Rect area = GetPlayAreaLocalRect();

        if (currentBeatType == BeatType.TapCircle)
        {
            activeBeatDuration = tapApproachDuration;
            beatTimer = 0f;

            if (tapNoteRoot != null) tapNoteRoot.gameObject.SetActive(true);
            if (sliderNoteRoot != null) sliderNoteRoot.gameObject.SetActive(false);

            if (tapNoteRoot != null)
            {
                float randX = Mathf.Lerp(area.xMin + 220f, area.xMax - 220f, UnityEngine.Random.Range(0.1f, 0.9f));
                float randY = Mathf.Lerp(area.yMin + 220f, area.yMax - 260f, UnityEngine.Random.Range(0.1f, 0.85f));
                tapNoteRoot.localPosition = new Vector3(randX, randY, 0f);
            }

            if (tapApproachRing != null)
            {
                tapApproachRing.localScale = new Vector3(2.15f, 2.15f, 1f);
            }

            if (tapNumberText != null)
            {
                tapNumberText.text = $"{currentBeatIndex}\nTAP!";
            }
        }
        else
        {
            activeBeatDuration = sliderDuration;
            beatTimer = 0f;

            if (tapNoteRoot != null) tapNoteRoot.gameObject.SetActive(false);
            if (sliderNoteRoot != null) sliderNoteRoot.gameObject.SetActive(true);

            // Buat jalur Slider dengan arah horizontal / diagonal agar mudah ditarik
            bool leftToRight = UnityEngine.Random.value > 0.5f;
            float startNormX = leftToRight ? UnityEngine.Random.Range(0.18f, 0.32f) : UnityEngine.Random.Range(0.68f, 0.82f);
            float endNormX = leftToRight ? UnityEngine.Random.Range(0.68f, 0.82f) : UnityEngine.Random.Range(0.18f, 0.32f);
            float startNormY = UnityEngine.Random.Range(0.24f, 0.72f);
            float endNormY = Mathf.Clamp(startNormY + UnityEngine.Random.Range(-0.22f, 0.22f), 0.22f, 0.74f);

            sliderStartLocalPos = new Vector2(
                Mathf.Lerp(area.xMin, area.xMax, startNormX),
                Mathf.Lerp(area.yMin, area.yMax, startNormY)
            );
            sliderEndLocalPos = new Vector2(
                Mathf.Lerp(area.xMin, area.xMax, endNormX),
                Mathf.Lerp(area.yMin, area.yMax, endNormY)
            );

            LayoutSliderVisuals(0f);

            if (sliderBallText != null)
            {
                string arrow = leftToRight ? "➔" : "⬅";
                sliderBallText.text = $"{currentBeatIndex}\nTARIK {arrow}";
            }
        }
    }

    private void LayoutSliderVisuals(float progress)
    {
        Vector2 delta = sliderEndLocalPos - sliderStartLocalPos;
        float dist = delta.magnitude;
        float angleDeg = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        if (sliderTrackBar != null)
        {
            sliderTrackBar.localPosition = new Vector3(sliderStartLocalPos.x, sliderStartLocalPos.y, 0f);
            sliderTrackBar.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
            sliderTrackBar.sizeDelta = new Vector2(Mathf.Max(80f, dist), 120f);
        }

        if (sliderTrackFillImage != null)
        {
            sliderTrackFillImage.color = Color.Lerp(
                new Color(0.25f, 0.55f, 0.95f, 0.65f),
                new Color(0.20f, 0.95f, 0.55f, 0.90f),
                progress
            );
        }

        if (sliderEndTarget != null)
        {
            sliderEndTarget.localPosition = new Vector3(sliderEndLocalPos.x, sliderEndLocalPos.y, 0f);
        }

        if (sliderDragBall != null)
        {
            Vector2 ballPos = Vector2.Lerp(sliderStartLocalPos, sliderEndLocalPos, progress);
            sliderDragBall.localPosition = new Vector3(ballPos.x, ballPos.y, 0f);
        }
    }

    private void HandleTapCircleClicked()
    {
        if (isGameplayPaused || isSessionFinished || isWaitingNextBeat) return;
        if (currentBeatType != BeatType.TapCircle) return;

        float progress = Mathf.Clamp01(beatTimer / Mathf.Max(0.2f, activeBeatDuration));
        bool isPerfect = progress >= 0.42f;

        hitSuccessCount++;
        UpdateHeaderScoreUI();

        if (isPerfect)
        {
            GameplayAudioManager.PlayPhoneUnlockSuccess();
            ShowJudgement("PERFECT! ★", new Color(0.30f, 1.0f, 0.55f));
            OnConsciousnessReward?.Invoke(tapRewardConsciousness);
        }
        else
        {
            GameplayAudioManager.PlayPhoneTap();
            ShowJudgement("GOOD!", new Color(1.0f, 0.88f, 0.35f));
            OnConsciousnessReward?.Invoke(tapRewardConsciousness * 0.65f);
        }

        QueueNextBeat(0.22f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isGameplayPaused || isSessionFinished || isWaitingNextBeat) return;
        if (currentBeatType != BeatType.DragSlider) return;

        if (TryGetPlayAreaLocalPoint(eventData, out Vector2 localPt))
        {
            Vector2 currentBallPos = Vector2.Lerp(sliderStartLocalPos, sliderEndLocalPos, sliderCurrentProgress);
            float distToBall = Vector2.Distance(localPt, currentBallPos);

            // Jika klik mengenai bola slider (radius toleransi luas agar nyaman di layar HP 3D)
            if (distToBall <= 310f)
            {
                isDraggingSlider = true;
                GameplayAudioManager.PlayPhoneTap();
                UpdateSliderDragPosition(localPt);
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDraggingSlider || isGameplayPaused || isSessionFinished || isWaitingNextBeat) return;
        if (currentBeatType != BeatType.DragSlider) return;

        if (TryGetPlayAreaLocalPoint(eventData, out Vector2 localPt))
        {
            UpdateSliderDragPosition(localPt);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDraggingSlider = false;
    }

    private void UpdateSliderDragPosition(Vector2 pointerLocalPt)
    {
        Vector2 trackVec = sliderEndLocalPos - sliderStartLocalPos;
        float trackLenSq = trackVec.sqrMagnitude;
        if (trackLenSq < 1f) return;

        float projectedT = Vector2.Dot(pointerLocalPt - sliderStartLocalPos, trackVec) / trackLenSq;
        sliderCurrentProgress = Mathf.Clamp01(Mathf.Max(sliderCurrentProgress, projectedT));
        LayoutSliderVisuals(sliderCurrentProgress);

        // Jika bola sudah ditarik sampai menyentuh lingkaran ★ TUJUAN
        if (sliderCurrentProgress >= 0.86f)
        {
            isDraggingSlider = false;
            hitSuccessCount++;
            UpdateHeaderScoreUI();

            GameplayAudioManager.PlayPhoneUnlockSuccess();
            ShowJudgement("SLIDER PERFECT! ★", new Color(0.25f, 1.0f, 0.65f));
            OnConsciousnessReward?.Invoke(sliderRewardConsciousness);

            QueueNextBeat(0.26f);
        }
    }

    private void HandleBeatMissed()
    {
        isDraggingSlider = false;
        GameplayAudioManager.PlayPhoneUnlockFailed();
        ShowJudgement("MISS! ✖", new Color(1.0f, 0.38f, 0.38f));
        OnConsciousnessReward?.Invoke(-missPenaltyConsciousness);
        QueueNextBeat(0.28f);
    }

    private void QueueNextBeat(float delay)
    {
        isWaitingNextBeat = true;
        nextBeatDelayTimer = delay;

        if (tapNoteRoot != null) tapNoteRoot.gameObject.SetActive(false);
        if (sliderNoteRoot != null) sliderNoteRoot.gameObject.SetActive(false);
    }

    private void FinishOsuSession()
    {
        isSessionFinished = true;
        isDraggingSlider = false;

        if (hitSuccessCount >= 4)
        {
            OnConsciousnessReward?.Invoke(stageClearBonusConsciousness);
        }

        if (tapNoteRoot != null) tapNoteRoot.gameObject.SetActive(false);
        if (sliderNoteRoot != null) sliderNoteRoot.gameObject.SetActive(false);

        if (statusScoreText != null)
        {
            statusScoreText.text = $"🎉 SELESAI! SKOR: {hitSuccessCount} / {totalBeatsPerSession}";
            statusScoreText.color = new Color(0.35f, 1f, 0.55f);
        }

        if (resultOverlayPanel != null)
        {
            resultOverlayPanel.SetActive(true);
            resultOverlayPanel.transform.SetAsLastSibling();
            if (resultOverlayText != null)
            {
                resultOverlayText.text = $"STAGE CLEAR\n\nSKOR: {hitSuccessCount} / {totalBeatsPerSession}";
            }
        }
    }

    private void ShowJudgement(string text, Color color)
    {
        if (judgementFeedbackText == null) return;
        judgementFeedbackText.gameObject.SetActive(true);
        judgementFeedbackText.text = text;
        judgementFeedbackText.color = color;
        judgementFeedbackText.transform.SetAsLastSibling();
        judgementHideTimer = 0.55f;
    }

    private void UpdateHeaderScoreUI()
    {
        if (statusScoreText != null)
        {
            statusScoreText.text = $"BEAT: {currentBeatIndex} / {totalBeatsPerSession}  |  HIT: {hitSuccessCount}";
            statusScoreText.color = Color.white;
        }
    }

    private Rect GetPlayAreaLocalRect()
    {
        if (playAreaRect != null && playAreaRect.rect.width > 50f && playAreaRect.rect.height > 50f)
        {
            return playAreaRect.rect;
        }
        return new Rect(-750f, -950f, 1500f, 1900f);
    }

    private bool TryGetPlayAreaLocalPoint(PointerEventData eventData, out Vector2 localPoint)
    {
        if (playAreaRect == null)
        {
            localPoint = Vector2.zero;
            return false;
        }

        Camera cam = eventData.pressEventCamera != null ? eventData.pressEventCamera : Camera.main;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playAreaRect,
            eventData.position,
            cam,
            out localPoint
        );
    }
}
