using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Mengelola Core Loop Permainan sesuai GDD:
/// - Meteran Kesadaran (0 - 100), turun pasif -1 poin/detik.
/// - Efek Ngantuk Menyiksa:
///   1) Kelopak mata berat bertahap (< 50%) + Microsleep acak menutup rapat lalu tersentak (< 30%).
///   2) Head Nodding pada kamera: kepala perlahan terkulai ke bawah/miring lalu tersentak kaget naik kembali.
/// - Tertidur (0 selama 5 detik berturut-turut) -> Game Over.
/// - 3 Nyawa & Rem Mendadak: Tabrakan mengurangi 1 nyawa (atau 2 jika Kritis < 20) + bonus +40 Kesadaran.
/// - Jarak Tempuh: Menghitung mundur KM menuju 0 KM (Kondisi Menang).
/// </summary>
public class ConsciousnessManager : MonoBehaviour
{
    public static ConsciousnessManager Instance { get; private set; }

    [Header("Pengaturan Kesadaran 2 Zona (Skala 0 - 100, Ngantuk di 60)")]
    [SerializeField] private float maxConsciousness = 100f;
    [SerializeField] private float startingConsciousness = 100f;
    [Tooltip("Penurunan kesadaran saat masih di Zona Segar (> 60): Turun CEPAT (-6/detik) agar pemain cepat merasakan ngantuk")]
    [SerializeField] private float freshZoneDrainPerSecond = 6.0f;
    [Tooltip("Penurunan kesadaran saat sudah di Zona Ngantuk (<= 60): Turun LAMBAT (-2/detik) agar pemain sering merasakan ngantuk tapi tidak gampang mati ketiduran")]
    [SerializeField] private float drowsyZoneDrainPerSecond = 2.0f;
    [Tooltip("Batas kesadaran kritis (penalti tabrakan jadi 2 nyawa & bunyi detak jantung)")]
    [SerializeField] private float criticalThreshold = 25f;
    [Tooltip("Durasi kesadaran 0 sebelum tertidur (Game Over)")]
    [SerializeField] private float sleepGameOverDelay = 5.0f;

    [Header("Efek Ngantuk 1: Kelopak Mata & Microsleep")]
    [Tooltip("Kesadaran di bawah nilai ini kelopak mata mulai terasa berat (Default: 60)")]
    [SerializeField] private float drowsyStartThreshold = 60f;
    [Tooltip("Kesadaran di bawah nilai ini mulai terjadi Microsleep (mata menutup rapat sesaat)")]
    [SerializeField] private float microsleepThreshold = 32f;
    [SerializeField] private float minMicrosleepDuration = 0.35f;
    [SerializeField] private float maxMicrosleepDuration = 0.75f;
    [SerializeField] private float minMicrosleepCooldown = 1.8f;
    [SerializeField] private float maxMicrosleepCooldown = 4.2f;

    [Header("Efek Ngantuk 2: Kepala Terkulai (Head Nodding)")]
    [Tooltip("Transform Pivot Kepala (otomatis dibuat tepat di posisi mata Main Camera)")]
    [SerializeField] private Transform headPivotTransform;
    [Tooltip("Kesadaran di bawah nilai ini kepala mulai terkulai (Default: 60)")]
    [SerializeField] private float headNodStartThreshold = 60f;
    [Tooltip("Sudut maksimal kepala menunduk ke bawah (derajat)")]
    [SerializeField] private float maxNodPitchAngle = 7f;
    [Tooltip("Sudut maksimal kepala miring ke samping (derajat)")]
    [SerializeField] private float maxNodRollAngle = 3.5f;

    [Header("Efek Ngantuk 3: Blur & Vignette (URP Global Volume)")]
    [SerializeField] private float blurStartThreshold = 60f;
    [SerializeField] private float maxBlurRadius = 1.25f;
    [SerializeField] private float maxVignetteIntensity = 0.68f;

    [Header("Pengaturan Nyawa & Tabrakan")]
    [SerializeField] private int maxLives = 3;
    [Tooltip("Lonjakan kesadaran saat kaget/rem mendadak menabrak")]
    [SerializeField] private float crashAdrenalineBonus = 40f;

    [Header("Pengaturan Level & Progresi (Level 1 - 3)")]
    [Tooltip("Level saat ini (1 = 1.5 KM & 2 Minigame, 2 = 3.0 KM & 4 Minigame + Iklan, 3 = 5.0 KM + Iklan + Chat Istri setelah 3 KM)")]
    [Range(1, 3)]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private float level1DistanceKm = 1.5f;
    [SerializeField] private float level2DistanceKm = 3.0f;
    [SerializeField] private float level3DistanceKm = 5.0f;
    [Tooltip("Jarak tempuh (KM) di Level 3 sebelum Event Chat Istri muncul (default: setelah menempuh 3.0 KM)")]
    [SerializeField] private float level3WifeChatTriggerTraveledKm = 3.0f;

    [Header("Pengaturan Jarak Tempuh (Win Condition)")]
    [SerializeField] private float startingDistanceKm = 1.5f;
    [Tooltip("Kecepatan pengurangan jarak tempuh (KM per detik)")]
    [SerializeField] private float distanceSpeedKmPerSec = 0.025f;

    [Header("Referensi HUD (Otomatis dibuat jika kosong)")]
    [SerializeField] private RectTransform consciousnessFillRect;
    [SerializeField] private Image consciousnessFillImage;
    [SerializeField] private Text consciousnessLabelText;
    [SerializeField] private Text livesText;
    [SerializeField] private Text distanceText;
    [SerializeField] private Image screenDimImage;
    [SerializeField] private RectTransform topEyelidRect;
    [SerializeField] private RectTransform bottomEyelidRect;
    [SerializeField] private Text sleepWarningText;
    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private Text endGameTitleText;
    [SerializeField] private Text endGameDetailText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Text restartButtonLabel;
    [SerializeField] private Button homeButton;
    [SerializeField] private Text homeButtonLabel;
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Text nextLevelButtonLabel;

    [Header("Font Utama Game (Otomatis Terisi Dimbo Regular)")]
    [SerializeField] private Font customGameFont;
    [SerializeField] private TMPro.TMP_FontAsset customGameTmpFont;

    [Header("Asset Gambar UI HUD & End Screen (Otomatis Terisi dari Assets/Sprite/ui)")]
    [SerializeField] private Sprite energyBarFrameSprite;   // CANVAS ENERGY BAR.png
    [SerializeField] private Sprite energyBarFillSprite;    // ENERGY BAR.png
    [SerializeField] private Sprite heartIconSprite;        // HEART.png
    [SerializeField] private Sprite kmBarFrameSprite;       // CANVAS BAR KM.png
    [SerializeField] private Sprite kmBarFillSprite;        // BAR KM.png
    [SerializeField] private Sprite pauseIdleSprite;        // PAUSE.png
    [SerializeField] private Sprite pauseHoverOrPlaySprite; // PAUSE 2.png
    [SerializeField] private Sprite levelCompleteTitleSprite; // LEVEL COMPLETE.png
    [SerializeField] private Sprite youLoseTitleSprite;       // YOU LOSE.png
    [SerializeField] private Sprite btnMainMenuNormalSprite;  // BUTTON MAIN MENU.png
    [SerializeField] private Sprite btnMainMenuHoverSprite;   // BUTTON TEKAN MAIN MENU.png
    [SerializeField] private Sprite btnNextLevelNormalSprite; // BUTTON NEXT LEVEL.png
    [SerializeField] private Sprite btnNextLevelHoverSprite;  // BUTTON TEKAN NEXT LEVEL.png
    [SerializeField] private Sprite btnTryAgainNormalSprite;  // BUTTON TRY AGAIN.png
    [SerializeField] private Sprite btnTryAgainHoverSprite;   // BUTTON TEKAN TRY AGAIN.png

    [SerializeField] private Image[] heartIconImages = new Image[3];
    [SerializeField] private Image endGameTitleBannerImage;
    [SerializeField] private Button pauseHudButton;
    [SerializeField] private Image pauseHudButtonImage;
    [SerializeField] private RectTransform distanceBarFillRect;
    [SerializeField] private Image distanceBarFillImage;
    private bool isGamePausedByHud = false;

    // Menyimpan level yang dipilih saat transisi "Level Selanjutnya" / "Home"
    private static int pendingLevelOverride = -1;

    private float currentConsciousness;
    private int currentLives;
    private float remainingDistanceKm;
    private float zeroConsciousnessTimer;
    private bool isGameEnded = false;
    private bool wifeChatMilestoneTriggered = false;
    private PhoneFeedScrollController phoneController;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticLevelOverride()
    {
        pendingLevelOverride = -1;
    }

    // State Microsleep
    private bool isMicrosleeping = false;
    private float microsleepTimer = 0f;
    private float nextMicrosleepCountdown = 2.5f;
    private float currentEyelidCoverage = 0f;

    // State Head Nodding
    private Quaternion initialHeadRotation;
    private bool hasHeadPivot = false;
    private float currentNodPitch = 0f;
    private float currentNodRoll = 0f;
    private float targetNodRollSign = 1f;
    private bool isSnappingHeadUp = false;
    private float headSnapCooldown = 1.5f;

    // State Blur & Vignette URP
    private DepthOfField dofEffect;
    private Vignette vignetteEffect;
    private float baseVignetteIntensity = 0.44f;

    public float CurrentConsciousness => currentConsciousness;
    public float MaxConsciousness => Mathf.Max(1f, maxConsciousness);
    public float DrowsyThreshold => drowsyStartThreshold;
    public float CriticalThreshold => criticalThreshold;
    public int CurrentLives => currentLives;
    public bool IsGameEnded => isGameEnded;
    public int CurrentLevel => Mathf.Clamp(currentLevel, 1, 3);

    private void AutoBindUISpritesInEditor()
    {
#if UNITY_EDITOR
        if (customGameFont == null)
            customGameFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Asset fix/font/Dimbo Regular.ttf");
        if (customGameTmpFont == null)
            customGameTmpFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Asset fix/font/Dimbo Regular SDF.asset");

        if (energyBarFrameSprite == null)
            energyBarFrameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/ui/ui ingame/CANVAS ENERGY BAR.png");
        if (energyBarFillSprite == null)
            energyBarFillSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/ui/ui ingame/ENERGY BAR.png");
        if (heartIconSprite == null)
            heartIconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/ui/ui ingame/HEART.png");
        if (kmBarFrameSprite == null)
            kmBarFrameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/ui/ui ingame/CANVAS BAR KM.png");
        if (kmBarFillSprite == null)
            kmBarFillSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/ui/ui ingame/BAR KM.png");
        if (pauseIdleSprite == null)
            pauseIdleSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/ui/ui ingame/PAUSE.png");
        if (pauseHoverOrPlaySprite == null)
            pauseHoverOrPlaySprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/ui/ui ingame/PAUSE 2.png");

        if (levelCompleteTitleSprite == null)
            levelCompleteTitleSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/ui/New folder/LEVEL COMPLETE.png");
        if (youLoseTitleSprite == null)
            youLoseTitleSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/ui/New folder/YOU LOSE.png");
        if (btnMainMenuNormalSprite == null)
            btnMainMenuNormalSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/ui/New folder/BUTTON MAIN MENU.png");
        if (btnMainMenuHoverSprite == null)
            btnMainMenuHoverSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/ui/New folder/BUTTON TEKAN MAIN MENU.png");
        if (btnNextLevelNormalSprite == null)
            btnNextLevelNormalSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/ui/New folder/BUTTON NEXT LEVEL.png");
        if (btnNextLevelHoverSprite == null)
            btnNextLevelHoverSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/ui/New folder/BUTTON TEKAN NEXT LEVEL.png");
        if (btnTryAgainNormalSprite == null)
            btnTryAgainNormalSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/ui/New folder/BUTTON TRY AGAIN.png");
        if (btnTryAgainHoverSprite == null)
            btnTryAgainHoverSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/ui/New folder/BUTTON TEKAN TRY AGAIN.png");
#endif
        PhoneUIHelper.SetCustomDefaultFont(customGameFont, customGameTmpFont);
    }

    private void OnValidate()
    {
        UpgradeLegacyScale20ToScale100IfNeeded();
        AutoBindUISpritesInEditor();
        AutoBindUISpritesInEditor();
    }

    private void UpgradeLegacyScale20ToScale100IfNeeded()
    {
        // Jika di Inspector masih tersimpan angka skala 20 lama (Max <= 25), otomatis konversi ke skala 100 (Ngantuk di 60)
        if (maxConsciousness <= 25f)
        {
            maxConsciousness = 100f;
            startingConsciousness = 100f;
            freshZoneDrainPerSecond = 6.0f;
            drowsyZoneDrainPerSecond = 2.0f;
            criticalThreshold = 25f;
            drowsyStartThreshold = 60f;
            microsleepThreshold = 32f;
            headNodStartThreshold = 60f;
            blurStartThreshold = 60f;
            crashAdrenalineBonus = 40f;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        UpgradeLegacyScale20ToScale100IfNeeded();
        AutoBindUISpritesInEditor();

        if (pendingLevelOverride >= 1 && pendingLevelOverride <= 3)
        {
            currentLevel = pendingLevelOverride;
            pendingLevelOverride = -1;
        }
        else
        {
            currentLevel = Mathf.Clamp(currentLevel, 1, 3);
        }

        ApplyCurrentLevelSettings();
        ResetAllGameplaySystems();
        EnsureHUDCreated();
        InitHeadPivot();
        InitPostProcessingBlur();
    }

    private void ApplyCurrentLevelSettings()
    {
        switch (CurrentLevel)
        {
            case 1:
                startingDistanceKm = Mathf.Max(0.1f, level1DistanceKm);
                break;
            case 2:
                startingDistanceKm = Mathf.Max(0.1f, level2DistanceKm);
                break;
            case 3:
                startingDistanceKm = Mathf.Max(0.1f, level3DistanceKm);
                break;
        }
    }

    private void InitHeadPivot()
    {
        if (headPivotTransform == null)
        {
            if (Camera.main != null && Camera.main.transform.parent != null && Camera.main.transform.parent.name.Contains("Pivot"))
            {
                headPivotTransform = Camera.main.transform.parent;
            }
            else if (Camera.main != null)
            {
                headPivotTransform = Camera.main.transform;
            }
        }

        // PENTING: Jika CameraPivot di scene masih memakai offset lama / scale non-uniform (1.8, 1, 3.5),
        // auto-align tepat ke posisi mata kamera agar pivot point-nya pas di lensa (tidak mengorbit lingkaran).
        if (headPivotTransform != null && Camera.main != null && Camera.main.transform.parent == headPivotTransform)
        {
            Transform camTf = Camera.main.transform;
            if (camTf.localPosition.sqrMagnitude > 0.001f || (headPivotTransform.localScale - Vector3.one).sqrMagnitude > 0.001f)
            {
                Vector3 currentCamWorldPos = camTf.position;
                Quaternion currentCamWorldRot = camTf.rotation;

                camTf.SetParent(null, true);

                headPivotTransform.position = currentCamWorldPos;
                headPivotTransform.rotation = currentCamWorldRot;
                headPivotTransform.localScale = Vector3.one;

                camTf.SetParent(headPivotTransform, true);
                camTf.localPosition = Vector3.zero;
                camTf.localRotation = Quaternion.identity;
                camTf.localScale = Vector3.one;

                Debug.Log("<color=lime>[ConsciousnessManager]</color> Auto-aligned CameraPivot to camera eye center (Scale 1,1,1).");
            }
        }

        if (headPivotTransform != null)
        {
            initialHeadRotation = headPivotTransform.localRotation;
            hasHeadPivot = true;
        }
    }

    private void InitPostProcessingBlur()
    {
        Volume vol = FindFirstObjectByType<Volume>();
        if (vol == null || vol.profile == null) return;

        if (!vol.profile.TryGet(out dofEffect))
        {
            dofEffect = vol.profile.Add<DepthOfField>(true);
        }

        if (dofEffect != null)
        {
            dofEffect.active = true;
            dofEffect.mode.Override(DepthOfFieldMode.Gaussian);
            dofEffect.gaussianStart.Override(0f);
            dofEffect.gaussianEnd.Override(12f);
            dofEffect.gaussianMaxRadius.Override(0f);
        }

        if (vol.profile.TryGet(out vignetteEffect))
        {
            baseVignetteIntensity = vignetteEffect.intensity.value;
        }
    }

    private void Start()
    {
        ApplyCurrentLevelSettings();
        currentConsciousness = Mathf.Clamp(startingConsciousness, 0f, maxConsciousness);
        currentLives = maxLives;
        remainingDistanceKm = startingDistanceKm;
        zeroConsciousnessTimer = 0f;
        isGameEnded = false;
        wifeChatMilestoneTriggered = false;

        phoneController = FindFirstObjectByType<PhoneFeedScrollController>();
        if (phoneController != null)
        {
            phoneController.ApplyLevelRulesToPhone(CurrentLevel);
        }

        isMicrosleeping = false;
        nextMicrosleepCountdown = Random.Range(minMicrosleepCooldown, maxMicrosleepCooldown);
        currentEyelidCoverage = 0f;
        currentNodPitch = 0f;
        currentNodRoll = 0f;
        targetNodRollSign = Random.value > 0.5f ? 1f : -1f;

        if (endGamePanel != null)
            endGamePanel.SetActive(false);

        BindEndScreenButtons();
        UpdateAllHUD();
    }

    private void BindEndScreenButtons()
    {
        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(RestartLevel);
        }

        if (homeButton != null)
        {
            homeButton.onClick.RemoveAllListeners();
            homeButton.onClick.AddListener(GoToHomeOrLevel1);
        }

        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.RemoveAllListeners();
            nextLevelButton.onClick.AddListener(LoadNextLevel);
        }
    }

    private void Update()
    {
        if (isGameEnded) return;

        // 1. Sistem Kesadaran 2 Zona yang Simpel & Konsisten:
        //    - Di Zona Segar (> drowsyStartThreshold = 60): Turun CEPAT (-6/detik) agar pemain cepat masuk fase ngantuk.
        //    - Di Zona Ngantuk (<= drowsyStartThreshold = 60): Turun LAMBAT (-2/detik) agar pemain sering merasakan efek ngantuk tapi punya waktu panjang (30 detik) sebelum tertidur!
        float activeDrainRate = currentConsciousness > drowsyStartThreshold
            ? freshZoneDrainPerSecond
            : drowsyZoneDrainPerSecond;

        currentConsciousness = Mathf.Max(0f, currentConsciousness - activeDrainRate * Time.deltaTime);

        // 2. Cek kondisi tertidur (0 selama 5 detik berturut-turut)
        if (currentConsciousness <= 0.01f)
        {
            currentConsciousness = 0f;
            zeroConsciousnessTimer += Time.deltaTime;

            if (zeroConsciousnessTimer >= sleepGameOverDelay)
            {
                GameplayAudioManager.PlayGameOverPanelOnly();
                TriggerGameOver("TERTIDUR DI JALAN!", "Kesadaranmu habis selama 5 detik dan kamu tertidur saat menyetir.");
                return;
            }
        }
        else
        {
            zeroConsciousnessTimer = 0f;
        }

        // 3. Hitung mundur jarak tempuh ke rumah
        remainingDistanceKm = Mathf.Max(0f, remainingDistanceKm - distanceSpeedKmPerSec * Time.deltaTime);

        // Khusus Level 3: Picu Event Chat Istri setelah menempuh 3.0 KM perjalanan (saat sisa jarak <= 2.0 KM dari 5.0 KM)
        if (!wifeChatMilestoneTriggered && CurrentLevel == 3)
        {
            float distanceTraveledKm = startingDistanceKm - remainingDistanceKm;
            if (distanceTraveledKm >= level3WifeChatTriggerTraveledKm)
            {
                wifeChatMilestoneTriggered = true;
                if (phoneController == null)
                {
                    phoneController = FindFirstObjectByType<PhoneFeedScrollController>();
                }
                if (phoneController != null)
                {
                    phoneController.TriggerWifeChatMilestoneIfEligible();
                }
            }
        }

        if (remainingDistanceKm <= 0f)
        {
            TriggerWin();
            return;
        }

        UpdateAllHUD();
        UpdateEyelidAndMicrosleep();
        UpdateHeadNodding();
        UpdateDrowsyBlurAndVignette();
    }

    public void AddConsciousness(float amount)
    {
        if (isGameEnded || amount <= 0f) return;

        currentConsciousness = Mathf.Clamp(currentConsciousness + amount, 0f, maxConsciousness);
        if (currentConsciousness > 0f)
        {
            zeroConsciousnessTimer = 0f;
        }

        // Saat dapat dopamin besar / kaget, hentikan microsleep & sentak kepala balik sadar
        if (amount >= 8f)
        {
            isMicrosleeping = false;
            isSnappingHeadUp = true;
        }

        UpdateAllHUD();
    }

    public void ReduceConsciousness(float amount)
    {
        if (isGameEnded || amount <= 0f) return;

        currentConsciousness = Mathf.Max(0f, currentConsciousness - amount);
        UpdateAllHUD();
    }

    public bool HandleCarCrash()
    {
        if (isGameEnded) return true;

        bool isCritical = currentConsciousness < criticalThreshold;
        int lifePenalty = isCritical ? 2 : 1;

        currentLives = Mathf.Max(0, currentLives - lifePenalty);

        if (currentLives <= 0)
        {
            UpdateAllHUD();
            TriggerGameOver(
                isCritical ? "TABRAKAN FATAL SAAT NGANTUK!" : "KECELAKAAN!",
                "Kesempatan rem mendadak (nyawa) kamu sudah habis."
            );
            return true;
        }

        // Efek kaget/adrenalin (+40 Kesadaran) langsung membangunkan mata & kepala
        AddConsciousness(crashAdrenalineBonus);
        UpdateAllHUD();

        Debug.Log($"<color=orange>[Rem Mendadak]</color> Nyawa berkurang {lifePenalty} (Sisa: {currentLives}/{maxLives}). Adrenalin +{crashAdrenalineBonus} Kesadaran!");
        return false;
    }

    private void TriggerGameOver(string title, string detail)
    {
        isGameEnded = true;
        StopAllGameplaySystems();

        if (currentConsciousness <= 0.01f)
        {
            SetEyelidCoverage(0.55f, 0.85f);
        }

        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);
            if (endGameTitleBannerImage != null && youLoseTitleSprite != null)
            {
                endGameTitleBannerImage.gameObject.SetActive(true);
                endGameTitleBannerImage.sprite = youLoseTitleSprite;
                endGameTitleBannerImage.color = Color.white;
                endGameTitleBannerImage.preserveAspect = true;
                if (endGameTitleText != null) endGameTitleText.text = "";
            }
            else if (endGameTitleText != null)
            {
                endGameTitleText.text = title;
                endGameTitleText.color = new Color(1f, 0.35f, 0.35f);
            }
            if (endGameDetailText != null)
            {
                endGameDetailText.text = $"Level {CurrentLevel}  |  {detail}\n\nSisa Jarak: {remainingDistanceKm:F2} KM";
            }

            ConfigureEndScreenButtons(isWin: false);
        }

        Debug.LogWarning($"[GAME OVER - LEVEL {CurrentLevel}] {title} - {detail}");
    }

    private void TriggerWin()
    {
        isGameEnded = true;
        StopAllGameplaySystems();
        GameplayAudioManager.PlayWinSequence();
        SetEyelidCoverage(0f, 0f);
        ResetHeadPivot();

        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);
            if (endGameTitleBannerImage != null && levelCompleteTitleSprite != null)
            {
                endGameTitleBannerImage.gameObject.SetActive(true);
                endGameTitleBannerImage.sprite = levelCompleteTitleSprite;
                endGameTitleBannerImage.color = Color.white;
                endGameTitleBannerImage.preserveAspect = true;
                if (endGameTitleText != null) endGameTitleText.text = "";
            }
            else if (endGameTitleText != null)
            {
                endGameTitleText.text = CurrentLevel < 3
                    ? $"🏠 LEVEL {CurrentLevel} SELESAI!"
                    : "🏠 SELAMAT SAMPAI RUMAH!";
                endGameTitleText.color = new Color(0.35f, 1f, 0.55f);
            }
            if (endGameDetailText != null)
            {
                endGameDetailText.text = $"Level {CurrentLevel} ({startingDistanceKm:F1} KM) berhasil dilewati!\nSisa Nyawa: {currentLives}/{maxLives}";
            }

            ConfigureEndScreenButtons(isWin: true);
        }

        Debug.Log($"<color=lime>[MENANG LEVEL {CurrentLevel}]</color> Berhasil menyelesaikan {startingDistanceKm:F1} KM!");
    }

    private void ConfigureEndScreenButtons(bool isWin)
    {
        EnsureEndScreenButtonsBuilt();
        BindEndScreenButtons();

        if (homeButton != null)
        {
            homeButton.gameObject.SetActive(true);
            ApplySpriteSwapToButton(homeButton, homeButtonLabel, btnMainMenuNormalSprite, btnMainMenuHoverSprite, "MAIN MENU");
        }

        if (isWin)
        {
            if (CurrentLevel < 3)
            {
                // Menang Level 1 atau 2 -> Tampilkan 2 tombol: [ MAIN MENU ] dan [ NEXT LEVEL ]
                if (restartButton != null) restartButton.gameObject.SetActive(false);
                if (nextLevelButton != null)
                {
                    nextLevelButton.gameObject.SetActive(true);
                    ApplySpriteSwapToButton(nextLevelButton, nextLevelButtonLabel, btnNextLevelNormalSprite, btnNextLevelHoverSprite, $"LEVEL {CurrentLevel + 1} ➔");
                }
            }
            else
            {
                // Tamat Level 3 -> Tampilkan [ MAIN MENU ] dan [ TRY AGAIN ]
                if (nextLevelButton != null) nextLevelButton.gameObject.SetActive(false);
                if (restartButton != null)
                {
                    restartButton.gameObject.SetActive(true);
                    ApplySpriteSwapToButton(restartButton, restartButtonLabel, btnTryAgainNormalSprite, btnTryAgainHoverSprite, "TRY AGAIN");
                }
            }
        }
        else
        {
            // Kalah (Game Over) -> Tampilkan [ MAIN MENU ] dan [ TRY AGAIN ]
            if (nextLevelButton != null) nextLevelButton.gameObject.SetActive(false);
            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(true);
                ApplySpriteSwapToButton(restartButton, restartButtonLabel, btnTryAgainNormalSprite, btnTryAgainHoverSprite, "TRY AGAIN");
            }
        }
    }

    private static void ApplySpriteSwapToButton(Button btn, Text fallbackLabel, Sprite normalSprite, Sprite hoverAndPressedSprite, string fallbackText)
    {
        if (btn == null) return;

        Image btnImg = btn.GetComponent<Image>();
        if (normalSprite != null && btnImg != null)
        {
            btnImg.sprite = normalSprite;
            btnImg.color = Color.white;
            btnImg.type = Image.Type.Simple;
            btnImg.preserveAspect = true;

            btn.transition = Selectable.Transition.SpriteSwap;
            SpriteState st = btn.spriteState;
            st.highlightedSprite = hoverAndPressedSprite != null ? hoverAndPressedSprite : normalSprite;
            st.pressedSprite = hoverAndPressedSprite != null ? hoverAndPressedSprite : normalSprite;
            st.selectedSprite = hoverAndPressedSprite != null ? hoverAndPressedSprite : normalSprite;
            btn.spriteState = st;

            if (fallbackLabel != null)
            {
                fallbackLabel.text = "";
                fallbackLabel.gameObject.SetActive(false);
            }
        }
        else if (fallbackLabel != null)
        {
            fallbackLabel.gameObject.SetActive(true);
            fallbackLabel.text = fallbackText;
        }
    }

    private void StopAllGameplaySystems()
    {
        EnvironmentMover.StopEnvironment();
        TreeSpawner.StopSpawning();
        NPCSpawner.StopSpawning();
        RoadLineSpawner.StopLines();

        CarController car = FindFirstObjectByType<CarController>();
        if (car != null) car.enabled = false;
    }

    private void ResetAllGameplaySystems()
    {
        Time.timeScale = 1f;
        EnvironmentMover.ResetEnvironment();
        TreeSpawner.ResetSpawning();
        NPCSpawner.ResetSpawning();
        RoadLineSpawner.ResetLines();
    }

    public void RestartLevel()
    {
        pendingLevelOverride = CurrentLevel;
        ResetAllGameplaySystems();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadNextLevel()
    {
        int nextLvl = Mathf.Clamp(CurrentLevel + 1, 1, 3);
        pendingLevelOverride = nextLvl;
        ResetAllGameplaySystems();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToHomeOrLevel1()
    {
        ResetAllGameplaySystems();
        // Jika ada scene MainMenu di Build Settings (index 0 dan bukan scene aktif saat ini), muat scene tersebut;
        // jika hanya ada 1 scene di project, kembali ke Level 1.
        int currentBuildIdx = SceneManager.GetActiveScene().buildIndex;
        if (SceneManager.sceneCountInBuildSettings > 1 && currentBuildIdx > 0)
        {
            pendingLevelOverride = 1;
            SceneManager.LoadScene(0);
        }
        else
        {
            pendingLevelOverride = 1;
            SceneManager.LoadScene(currentBuildIdx);
        }
    }

    private void UpdateAllHUD()
    {
        float ratio = Mathf.Clamp01(currentConsciousness / Mathf.Max(1f, maxConsciousness));

        if (consciousnessFillRect != null)
        {
            if (energyBarFillSprite != null && consciousnessFillImage != null && consciousnessFillImage.type == Image.Type.Filled)
            {
                consciousnessFillImage.fillAmount = ratio;
            }
            else if (energyBarFrameSprite == null)
            {
                consciousnessFillRect.anchorMax = new Vector2(ratio, consciousnessFillRect.anchorMax.y);
            }
        }

        if (consciousnessFillImage != null)
        {
            if (energyBarFillSprite != null)
            {
                // Jika menggunakan sprite ENERGY BAR.png biru strip, beri sedikit tint merah saat kritis agar tetap terbaca jelas
                if (currentConsciousness < criticalThreshold)
                    consciousnessFillImage.color = new Color(1.0f, 0.45f, 0.45f, 1f);
                else if (currentConsciousness < drowsyStartThreshold)
                    consciousnessFillImage.color = new Color(1.0f, 0.90f, 0.65f, 1f);
                else
                    consciousnessFillImage.color = Color.white;
            }
            else
            {
                if (currentConsciousness < criticalThreshold)
                    consciousnessFillImage.color = new Color(0.95f, 0.22f, 0.22f, 1f);
                else if (currentConsciousness < drowsyStartThreshold)
                    consciousnessFillImage.color = new Color(0.96f, 0.72f, 0.15f, 1f);
                else
                    consciousnessFillImage.color = new Color(0.20f, 0.85f, 0.45f, 1f);
            }
        }

        if (consciousnessLabelText != null)
        {
            // Hanya berupa visual bar tanpa persentase
            consciousnessLabelText.text = "";
        }

        if (heartIconImages != null && heartIconImages.Length > 0 && heartIconSprite != null)
        {
            for (int i = 0; i < heartIconImages.Length; i++)
            {
                if (heartIconImages[i] == null) continue;
                bool alive = i < currentLives;
                heartIconImages[i].color = alive
                    ? Color.white
                    : new Color(0.22f, 0.22f, 0.25f, 0.50f);
            }
            if (livesText != null)
            {
                livesText.text = "";
            }
        }
        else if (livesText != null)
        {
            string hearts = "";
            for (int i = 0; i < maxLives; i++)
            {
                hearts += (i < currentLives) ? "❤️ " : "🖤 ";
            }
            livesText.text = $"NYAWA: {hearts.Trim()}";
        }

        float distProgress = 1f - Mathf.Clamp01(remainingDistanceKm / Mathf.Max(0.01f, startingDistanceKm));
        if (distanceBarFillImage != null && kmBarFillSprite != null)
        {
            distanceBarFillImage.fillAmount = distProgress;
        }
        else if (distanceBarFillRect != null)
        {
            distanceBarFillRect.anchorMax = new Vector2(0.84f, Mathf.Lerp(0.06f, 0.94f, distProgress));
        }

        if (distanceText != null)
        {
            distanceText.text = $"LV.{CurrentLevel} | {remainingDistanceKm:F2} KM";
        }

        if (sleepWarningText != null)
        {
            if (currentConsciousness <= 0.01f && !isGameEnded)
            {
                sleepWarningText.gameObject.SetActive(true);
                float timeLeft = Mathf.Max(0f, sleepGameOverDelay - zeroConsciousnessTimer);
                sleepWarningText.text = $"⚠️ MATA TERTUTUP! MAIN HP SEKARANG! ({timeLeft:F1}s)";
            }
            else if (currentConsciousness < criticalThreshold && !isGameEnded)
            {
                sleepWarningText.gameObject.SetActive(true);
                sleepWarningText.text = "⚠️ NGANTUK BERAT! CARI DOPAMIN DI HP!";
            }
            else
            {
                sleepWarningText.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Efek Ngantuk 1: Kelopak mata makin berat (< 50%) + Microsleep acak menutup rapat (< 30%).
    /// </summary>
    private void UpdateEyelidAndMicrosleep()
    {
        if (currentConsciousness >= drowsyStartThreshold)
        {
            isMicrosleeping = false;
            currentEyelidCoverage = Mathf.Lerp(currentEyelidCoverage, 0f, Time.deltaTime * 8f);
            SetEyelidCoverage(currentEyelidCoverage, 0f);
            return;
        }

        // Jika Kesadaran 0: menutup rapat bertahap selama 5 detik menuju blackout
        if (currentConsciousness <= 0.01f)
        {
            float sleepProgress = Mathf.Clamp01(zeroConsciousnessTimer / Mathf.Max(0.1f, sleepGameOverDelay));
            float struggleWave = Mathf.Sin(Time.time * 12f) * 0.015f; // Sedikit bergetar melawan kantuk
            float targetCov = Mathf.Lerp(0.36f, 0.54f, sleepProgress) + struggleWave;
            currentEyelidCoverage = Mathf.Lerp(currentEyelidCoverage, targetCov, Time.deltaTime * 8f);
            SetEyelidCoverage(currentEyelidCoverage, Mathf.Lerp(0.35f, 0.85f, sleepProgress));
            return;
        }

        // Hitung tingkat keparahan kantuk (0 di 50%, 1 di 0%)
        float drowsyFactor = 1f - (currentConsciousness / Mathf.Max(1f, drowsyStartThreshold));

        // Logika Microsleep (< 30%)
        if (currentConsciousness < microsleepThreshold)
        {
            if (isMicrosleeping)
            {
                microsleepTimer -= Time.deltaTime;
                if (microsleepTimer <= 0f)
                {
                    // Tersentak bangun dari microsleep!
                    isMicrosleeping = false;
                    isSnappingHeadUp = true;
                    float severity = 1f - (currentConsciousness / Mathf.Max(1f, microsleepThreshold));
                    nextMicrosleepCountdown = Mathf.Lerp(maxMicrosleepCooldown, minMicrosleepCooldown, severity) * Random.Range(0.75f, 1.15f);
                }
            }
            else
            {
                nextMicrosleepCountdown -= Time.deltaTime;
                if (nextMicrosleepCountdown <= 0f)
                {
                    // Tiba-tiba mata menutup rapat sesaat!
                    isMicrosleeping = true;
                    float severity = 1f - (currentConsciousness / Mathf.Max(1f, microsleepThreshold));
                    microsleepTimer = Mathf.Lerp(minMicrosleepDuration, maxMicrosleepDuration, severity);
                }
            }
        }
        else
        {
            isMicrosleeping = false;
        }

        float targetCoverage;
        float targetDim;

        if (isMicrosleeping)
        {
            // Mata menutup rapat (gelap hampir total 95-100%)
            targetCoverage = 0.51f;
            targetDim = 0.65f;
            currentEyelidCoverage = Mathf.Lerp(currentEyelidCoverage, targetCoverage, Time.deltaTime * 12f);
        }
        else
        {
            // Kelopak mata terasa berat + kedipan lambat tidak teratur
            float baseWeight = Mathf.Lerp(0.04f, 0.24f, drowsyFactor);
            float heavyBlink = Mathf.Pow((Mathf.Sin(Time.time * 2.4f) + 1f) * 0.5f, 2.5f) * Mathf.Lerp(0.05f, 0.14f, drowsyFactor);
            targetCoverage = baseWeight + heavyBlink;
            targetDim = drowsyFactor * 0.28f;

            // Tersentak terbuka lebih cepat saat baru bangun
            currentEyelidCoverage = Mathf.Lerp(currentEyelidCoverage, targetCoverage, Time.deltaTime * 10f);
        }

        SetEyelidCoverage(currentEyelidCoverage, targetDim);
    }

    /// <summary>
    /// Efek Ngantuk 2: Kepala perlahan terkulai ke bawah/miring, lalu tersentak kaget ke atas.
    /// </summary>
    private void UpdateHeadNodding()
    {
        if (!hasHeadPivot || headPivotTransform == null) return;

        if (currentConsciousness >= headNodStartThreshold)
        {
            currentNodPitch = Mathf.Lerp(currentNodPitch, 0f, Time.deltaTime * 8f);
            currentNodRoll = Mathf.Lerp(currentNodRoll, 0f, Time.deltaTime * 8f);
            headPivotTransform.localRotation = initialHeadRotation * Quaternion.Euler(currentNodPitch, 0f, currentNodRoll);
            return;
        }

        float nodSeverity = 1f - (currentConsciousness / Mathf.Max(1f, headNodStartThreshold));

        if (isSnappingHeadUp)
        {
            // Kepala tersentak kaget naik dengan cepat
            currentNodPitch = Mathf.Lerp(currentNodPitch, 0f, Time.deltaTime * 18f);
            currentNodRoll = Mathf.Lerp(currentNodRoll, 0f, Time.deltaTime * 18f);

            if (Mathf.Abs(currentNodPitch) < 0.6f)
            {
                isSnappingHeadUp = false;
                targetNodRollSign = Random.value > 0.5f ? 1f : -1f;
                headSnapCooldown = Mathf.Lerp(2.2f, 0.8f, nodSeverity) * Random.Range(0.8f, 1.3f);
            }
        }
        else
        {
            headSnapCooldown -= Time.deltaTime;
            if (headSnapCooldown <= 0f)
            {
                // Kepala perlahan terkulai jatuh ke bawah & miring
                float fallSpeed = Mathf.Lerp(2.5f, 7.5f, nodSeverity);
                float maxPitchThisState = maxNodPitchAngle * Mathf.Lerp(0.45f, 1.0f, nodSeverity);
                float maxRollThisState = maxNodRollAngle * Mathf.Lerp(0.4f, 1.0f, nodSeverity) * targetNodRollSign;

                currentNodPitch = Mathf.MoveTowards(currentNodPitch, maxPitchThisState, fallSpeed * Time.deltaTime);
                currentNodRoll = Mathf.MoveTowards(currentNodRoll, maxRollThisState, fallSpeed * 0.45f * Time.deltaTime);

                // Jika kepala sudah jatuh terlalu dalam -> tersentak kaget naik kembali!
                if (currentNodPitch >= maxPitchThisState - 0.3f)
                {
                    isSnappingHeadUp = true;
                }
            }
        }

        headPivotTransform.localRotation = initialHeadRotation * Quaternion.Euler(currentNodPitch, 0f, currentNodRoll);
    }

    private void ResetHeadPivot()
    {
        if (hasHeadPivot && headPivotTransform != null)
        {
            headPivotTransform.localRotation = initialHeadRotation;
        }
    }

    /// <summary>
    /// Efek Ngantuk 3: Blur (Depth of Field Gaussian) & Vignette menyempit pada URP Global Volume.
    /// </summary>
    private void UpdateDrowsyBlurAndVignette()
    {
        float drowsyFactor = currentConsciousness < blurStartThreshold
            ? 1f - (currentConsciousness / Mathf.Max(1f, blurStartThreshold))
            : 0f;

        if (dofEffect != null)
        {
            float targetBlur = drowsyFactor * maxBlurRadius;
            if (isMicrosleeping) targetBlur = Mathf.Max(targetBlur, maxBlurRadius * 1.15f);

            dofEffect.gaussianMaxRadius.value = Mathf.Lerp(
                dofEffect.gaussianMaxRadius.value,
                targetBlur,
                Time.deltaTime * 6f
            );
        }

        if (vignetteEffect != null)
        {
            float targetVignette = Mathf.Lerp(baseVignetteIntensity, maxVignetteIntensity, drowsyFactor);
            if (isMicrosleeping) targetVignette = Mathf.Max(targetVignette, 0.78f);

            vignetteEffect.intensity.value = Mathf.Lerp(
                vignetteEffect.intensity.value,
                targetVignette,
                Time.deltaTime * 6f
            );
        }
    }

    private void SetEyelidCoverage(float ratioPerSide, float dimAlpha)
    {
        ratioPerSide = Mathf.Clamp(ratioPerSide, 0f, 0.55f);

        if (topEyelidRect != null)
        {
            topEyelidRect.anchorMin = new Vector2(0f, 1f - ratioPerSide);
            topEyelidRect.anchorMax = new Vector2(1f, 1f);
        }

        if (bottomEyelidRect != null)
        {
            bottomEyelidRect.anchorMin = new Vector2(0f, 0f);
            bottomEyelidRect.anchorMax = new Vector2(1f, ratioPerSide);
        }

        if (screenDimImage != null)
        {
            screenDimImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(dimAlpha));
        }
    }

    [ContextMenu("Build / Rebuild Gameplay HUD")]
    public void EnsureHUDCreated()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
        }

        if (GetComponent<CanvasScaler>() == null)
        {
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        // 0. Screen Dim Overlay (Untuk efek gelap saat microsleep)
        if (screenDimImage == null)
        {
            Transform t = transform.Find("Screen_DimOverlay");
            if (t == null)
            {
                RectTransform dimRt = PhoneUIHelper.CreatePanel("Screen_DimOverlay", transform, new Color(0f, 0f, 0f, 0f), Vector2.zero, Vector2.one);
                screenDimImage = dimRt.GetComponent<Image>();
                screenDimImage.raycastTarget = false;
                dimRt.SetAsFirstSibling();
            }
            else screenDimImage = t.GetComponent<Image>();
        }

        // 1. Kelopak Mata Hitam (Atas & Bawah)
        if (topEyelidRect == null)
        {
            Transform t = transform.Find("Eyelid_Top");
            if (t == null)
            {
                topEyelidRect = PhoneUIHelper.CreatePanel("Eyelid_Top", transform, new Color(0f, 0f, 0f, 0.98f), new Vector2(0f, 1f), new Vector2(1f, 1f));
                topEyelidRect.GetComponent<Image>().raycastTarget = false;
            }
            else topEyelidRect = t.GetComponent<RectTransform>();
        }

        if (bottomEyelidRect == null)
        {
            Transform t = transform.Find("Eyelid_Bottom");
            if (t == null)
            {
                bottomEyelidRect = PhoneUIHelper.CreatePanel("Eyelid_Bottom", transform, new Color(0f, 0f, 0f, 0.98f), new Vector2(0f, 0f), new Vector2(1f, 0f));
                bottomEyelidRect.GetComponent<Image>().raycastTarget = false;
            }
            else bottomEyelidRect = t.GetComponent<RectTransform>();
        }

        // 2. Bar Kesadaran (Pojok Kiri Atas) + Sprite CANVAS ENERGY BAR.png & ENERGY BAR.png
        Transform barRoot = transform.Find("HUD_ConsciousnessBar");
        bool isNewBarRoot = false;
        if (barRoot == null)
        {
            RectTransform bg = PhoneUIHelper.CreatePanel("HUD_ConsciousnessBar", transform, Color.white, new Vector2(0.025f, 0.885f), new Vector2(0.31f, 0.965f));
            bg.GetComponent<Image>().raycastTarget = false;
            barRoot = bg;
            isNewBarRoot = true;

            consciousnessFillRect = PhoneUIHelper.CreatePanel("Fill", bg, Color.white, new Vector2(0.148f, 0.25f), new Vector2(0.982f, 0.75f));
            consciousnessFillImage = consciousnessFillRect.GetComponent<Image>();
            consciousnessFillImage.raycastTarget = false;

            consciousnessLabelText = PhoneUIHelper.CreateText("Label", bg, "", Color.white, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f), TextAnchor.MiddleCenter, 28);
        }
        else
        {
            Transform fill = barRoot.Find("Fill");
            if (fill != null)
            {
                consciousnessFillRect = fill.GetComponent<RectTransform>();
                consciousnessFillImage = fill.GetComponent<Image>();
            }
            Transform lbl = barRoot.Find("Label");
            if (lbl != null) consciousnessLabelText = lbl.GetComponent<Text>();
        }

        if (barRoot != null && energyBarFrameSprite != null)
        {
            Image frameImg = barRoot.GetComponent<Image>();
            if (frameImg != null && frameImg.sprite == null)
            {
                frameImg.sprite = energyBarFrameSprite;
                frameImg.color = Color.white;
                frameImg.type = Image.Type.Simple;
                frameImg.preserveAspect = false;
            }
        }

        if (consciousnessFillRect != null && consciousnessFillImage != null && energyBarFillSprite != null)
        {
            if (consciousnessFillImage.sprite == null)
            {
                if (isNewBarRoot)
                {
                    PhoneUIHelper.ApplyAnchorBox(consciousnessFillRect, new Vector2(0.148f, 0.25f), new Vector2(0.982f, 0.75f));
                }
                consciousnessFillImage.sprite = energyBarFillSprite;
                consciousnessFillImage.color = Color.white;
                consciousnessFillImage.type = Image.Type.Filled;
                consciousnessFillImage.fillMethod = Image.FillMethod.Horizontal;
                consciousnessFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                consciousnessFillImage.fillAmount = 1f;
            }
        }

        // 3. Nyawa (3 Ikon HEART.png di Kiri Atas tepat di bawah Energy Bar)
        Transform livesTf = transform.Find("HUD_LivesText");
        if (livesTf == null)
        {
            livesText = PhoneUIHelper.CreateText("HUD_LivesText", transform, "", Color.white, new Vector2(0.03f, 0.805f), new Vector2(0.21f, 0.875f), TextAnchor.MiddleLeft, 28);
            livesTf = livesText.transform;
        }
        else
        {
            livesText = livesTf.GetComponent<Text>();
        }

        if (livesTf != null && heartIconSprite != null)
        {
            if (livesText != null) livesText.text = "";

            if (heartIconImages == null || heartIconImages.Length != 3)
                heartIconImages = new Image[3];

            for (int i = 0; i < 3; i++)
            {
                Transform hTf = livesTf.Find($"Heart_Icon_{i + 1}");
                if (hTf == null)
                {
                    float minX = i * 0.34f;
                    float maxX = minX + 0.30f;
                    RectTransform hRt = PhoneUIHelper.CreatePanel($"Heart_Icon_{i + 1}", livesTf, Color.white, new Vector2(minX, 0.05f), new Vector2(maxX, 0.95f));
                    hTf = hRt;
                }
                Image hImg = hTf.GetComponent<Image>();
                if (hImg != null)
                {
                    if (hImg.sprite == null)
                    {
                        hImg.sprite = heartIconSprite;
                        hImg.color = Color.white;
                        hImg.preserveAspect = true;
                    }
                    hImg.raycastTarget = false;
                    heartIconImages[i] = hImg;
                }
            }
        }

        // 4. SISI KANAN: Tombol PAUSE di Pojok Kanan Atas + Bar Jarak KM Vertikal tepat di bawahnya!
        // 4a. Tombol Pause di Pojok Kanan Atas (PAUSE.png & Hover/Toggle PAUSE 2.png)
        if (pauseIdleSprite != null)
        {
            Transform pauseTf = transform.Find("HUD_PauseButton");
            if (pauseTf == null)
            {
                pauseHudButton = PhoneUIHelper.CreateButton(
                    "HUD_PauseButton",
                    transform,
                    "",
                    Color.white,
                    Color.white,
                    new Vector2(0.93f, 0.885f),
                    new Vector2(0.98f, 0.965f),
                    out Text pauseLbl,
                    24
                );
                if (pauseLbl != null) pauseLbl.gameObject.SetActive(false);
                pauseTf = pauseHudButton.transform;
            }
            else
            {
                pauseHudButton = pauseTf.GetComponent<Button>();
            }

            if (pauseHudButton != null)
            {
                pauseHudButtonImage = pauseHudButton.GetComponent<Image>();
                if (pauseHudButtonImage != null && pauseHudButtonImage.sprite == null)
                {
                    pauseHudButtonImage.sprite = pauseIdleSprite;
                    pauseHudButtonImage.color = Color.white;
                    pauseHudButtonImage.preserveAspect = true;
                }

                pauseHudButton.transition = Selectable.Transition.SpriteSwap;
                SpriteState pst = pauseHudButton.spriteState;
                pst.highlightedSprite = pauseHoverOrPlaySprite != null ? pauseHoverOrPlaySprite : pauseIdleSprite;
                pst.pressedSprite = pauseHoverOrPlaySprite != null ? pauseHoverOrPlaySprite : pauseIdleSprite;
                pauseHudButton.spriteState = pst;

                pauseHudButton.onClick.RemoveAllListeners();
                pauseHudButton.onClick.AddListener(ToggleHudPause);
            }
        }

        // 4b. Bar KM Vertikal di SISI KANAN tepat di bawah Tombol Pause (CANVAS BAR KM.png & BAR KM.png)
        if (kmBarFrameSprite != null && kmBarFillSprite != null)
        {
            Transform kmBarTf = transform.Find("HUD_VerticalKmBar");
            if (kmBarTf == null)
            {
                RectTransform kmFrameRt = PhoneUIHelper.CreatePanel("HUD_VerticalKmBar", transform, Color.white, new Vector2(0.935f, 0.36f), new Vector2(0.975f, 0.86f));
                kmFrameRt.GetComponent<Image>().raycastTarget = false;
                kmBarTf = kmFrameRt;
            }

            Image kmFrameImg = kmBarTf.GetComponent<Image>();
            if (kmFrameImg != null)
            {
                if (kmFrameImg.sprite == null)
                {
                    kmFrameImg.sprite = kmBarFrameSprite;
                    kmFrameImg.color = Color.white;
                    kmFrameImg.preserveAspect = true;
                }
                kmFrameImg.raycastTarget = false;
            }

            Transform kmFillTf = kmBarTf.Find("KM_Fill");
            if (kmFillTf == null)
            {
                RectTransform fillRt = PhoneUIHelper.CreatePanel("KM_Fill", kmBarTf, Color.white, new Vector2(0.18f, 0.05f), new Vector2(0.82f, 0.95f));
                kmFillTf = fillRt;
            }

            distanceBarFillRect = kmFillTf.GetComponent<RectTransform>();
            distanceBarFillImage = kmFillTf.GetComponent<Image>();
            if (distanceBarFillImage != null)
            {
                if (distanceBarFillImage.sprite == null)
                {
                    distanceBarFillImage.sprite = kmBarFillSprite;
                    distanceBarFillImage.color = Color.white;
                    distanceBarFillImage.type = Image.Type.Filled;
                    distanceBarFillImage.fillMethod = Image.FillMethod.Vertical;
                    distanceBarFillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
                    distanceBarFillImage.fillAmount = 0f;
                }
                distanceBarFillImage.raycastTarget = false;
            }
        }

        // 4c. Label Teks Jarak Tempuh (Opsional/Pendamping di dekat Bar KM Kanan)
        Transform distRoot = transform.Find("HUD_DistanceText");
        if (distRoot == null)
        {
            RectTransform distBg = PhoneUIHelper.CreatePanel("HUD_DistanceText", transform, new Color(0.08f, 0.10f, 0.14f, 0.82f), new Vector2(0.75f, 0.895f), new Vector2(0.915f, 0.955f));
            distBg.GetComponent<Image>().raycastTarget = false;
            distanceText = PhoneUIHelper.CreateText("Text", distBg, "LV.1 | 1.50 KM", new Color(1f, 0.92f, 0.40f), new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f), TextAnchor.MiddleCenter, 28);
            distRoot = distBg;
        }
        else
        {
            distanceText = distRoot.GetComponentInChildren<Text>();
        }

        // 5. Peringatan Ngantuk
        if (sleepWarningText == null)
        {
            Transform t = transform.Find("HUD_SleepWarning");
            if (t == null)
            {
                sleepWarningText = PhoneUIHelper.CreateText("HUD_SleepWarning", transform, "⚠️ NGANTUK BERAT!", new Color(1f, 0.35f, 0.35f), new Vector2(0.25f, 0.78f), new Vector2(0.75f, 0.86f), TextAnchor.MiddleCenter, 34);
                sleepWarningText.gameObject.SetActive(false);
            }
            else sleepWarningText = t.GetComponent<Text>();
        }

        // 6. Panel Win / Game Over
        if (endGamePanel == null)
        {
            Transform t = transform.Find("EndGame_Panel");
            if (t == null)
            {
                RectTransform panelRt = PhoneUIHelper.CreatePanel("EndGame_Panel", transform, new Color(0.05f, 0.06f, 0.09f, 0.92f), Vector2.zero, Vector2.one);
                endGamePanel = panelRt.gameObject;

                endGameTitleText = PhoneUIHelper.CreateText("Title", panelRt, "GAME OVER", Color.red, new Vector2(0.15f, 0.58f), new Vector2(0.85f, 0.74f), TextAnchor.MiddleCenter, 64);
                endGameDetailText = PhoneUIHelper.CreateText("Detail", panelRt, "Detail", Color.white, new Vector2(0.20f, 0.42f), new Vector2(0.80f, 0.56f), TextAnchor.MiddleCenter, 32, FontStyle.Normal);

                endGamePanel.SetActive(false);
            }
            else
            {
                endGamePanel = t.gameObject;
                Transform titleT = t.Find("Title");
                if (titleT != null) endGameTitleText = titleT.GetComponent<Text>();
                Transform detailT = t.Find("Detail");
                if (detailT != null) endGameDetailText = detailT.GetComponent<Text>();
            }
        }

        if (endGamePanel != null)
        {
            Transform bannerT = endGamePanel.transform.Find("Title_BannerImage");
            if (bannerT == null)
            {
                RectTransform bannerRt = PhoneUIHelper.CreatePanel(
                    "Title_BannerImage",
                    endGamePanel.transform,
                    Color.white,
                    new Vector2(0.18f, 0.56f),
                    new Vector2(0.82f, 0.80f)
                );
                bannerRt.GetComponent<Image>().raycastTarget = false;
                bannerT = bannerRt;
            }
            endGameTitleBannerImage = bannerT.GetComponent<Image>();
            if (endGameTitleBannerImage != null)
            {
                endGameTitleBannerImage.preserveAspect = true;
                if (youLoseTitleSprite != null)
                {
                    endGameTitleBannerImage.sprite = youLoseTitleSprite;
                    endGameTitleBannerImage.color = Color.white;
                }
            }
        }

        EnsureEndScreenButtonsBuilt();
        ConfigureEndScreenButtons(isWin: false);
        PhoneUIHelper.ApplyProvidedFontToAllSceneTexts(customGameFont, customGameTmpFont);
    }

    public void ToggleHudPause()
    {
        if (isGameEnded) return;

        isGamePausedByHud = !isGamePausedByHud;
        Time.timeScale = isGamePausedByHud ? 0f : 1f;

        if (pauseHudButtonImage != null)
        {
            pauseHudButtonImage.sprite = isGamePausedByHud && pauseHoverOrPlaySprite != null
                ? pauseHoverOrPlaySprite
                : pauseIdleSprite;
        }
    }

    private void EnsureEndScreenButtonsBuilt()
    {
        if (endGamePanel == null) return;
        Transform panelTf = endGamePanel.transform;

        // 1. Tombol Kiri: [ HOME ]
        if (homeButton == null)
        {
            Transform homeT = panelTf.Find("Home_Button");
            if (homeT != null) homeButton = homeT.GetComponent<Button>();
        }
        if (homeButton == null)
        {
            homeButton = PhoneUIHelper.CreateButton(
                "Home_Button",
                panelTf,
                "HOME",
                new Color(0.24f, 0.28f, 0.36f, 1f),
                Color.white,
                new Vector2(0.22f, 0.24f),
                new Vector2(0.48f, 0.35f),
                out homeButtonLabel,
                34
            );
        }
        else if (homeButtonLabel == null)
        {
            homeButtonLabel = homeButton.GetComponentInChildren<Text>(true);
        }

        // 2. Tombol Kanan (Saat Kalah / Tamat Level 3): [ COBA LAGI / MAIN LAGI ]
        if (restartButton == null)
        {
            Transform btnT = panelTf.Find("Restart_Button");
            if (btnT != null) restartButton = btnT.GetComponent<Button>();
        }
        if (restartButton == null)
        {
            restartButton = PhoneUIHelper.CreateButton(
                "Restart_Button",
                panelTf,
                "COBA LAGI",
                new Color(0.20f, 0.75f, 0.42f, 1f),
                Color.white,
                new Vector2(0.52f, 0.24f),
                new Vector2(0.78f, 0.35f),
                out restartButtonLabel,
                34
            );
        }
        else
        {
            if (restartButtonLabel == null)
            {
                restartButtonLabel = restartButton.GetComponentInChildren<Text>(true);
            }
        }

        // 3. Tombol Kanan (Saat Menang Level 1 atau 2): [ LEVEL SELANJUTNYA ]
        if (nextLevelButton == null)
        {
            Transform nextT = panelTf.Find("NextLevel_Button");
            if (nextT != null) nextLevelButton = nextT.GetComponent<Button>();
        }
        if (nextLevelButton == null)
        {
            nextLevelButton = PhoneUIHelper.CreateButton(
                "NextLevel_Button",
                panelTf,
                "LEVEL 2 ➔",
                new Color(0.15f, 0.65f, 0.95f, 1f),
                Color.white,
                new Vector2(0.52f, 0.24f),
                new Vector2(0.78f, 0.35f),
                out nextLevelButtonLabel,
                34
            );
            nextLevelButton.gameObject.SetActive(false);
        }
        else if (nextLevelButtonLabel == null)
        {
            nextLevelButtonLabel = nextLevelButton.GetComponentInChildren<Text>(true);
        }
    }
}
