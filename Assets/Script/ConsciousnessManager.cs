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

    [Header("Pengaturan Kesadaran (0 - 100)")]
    [SerializeField] private float maxConsciousness = 100f;
    [SerializeField] private float startingConsciousness = 100f;
    [Tooltip("Pengurangan kesadaran otomatis per detik")]
    [SerializeField] private float passiveDrainPerSecond = 1.0f;
    [Tooltip("Batas kesadaran kritis (penalti tabrakan jadi 2 nyawa)")]
    [SerializeField] private float criticalThreshold = 20f;
    [Tooltip("Durasi kesadaran 0 sebelum tertidur (Game Over)")]
    [SerializeField] private float sleepGameOverDelay = 5.0f;

    [Header("Efek Ngantuk 1: Kelopak Mata & Microsleep")]
    [Tooltip("Kesadaran di bawah nilai ini kelopak mata mulai terasa berat")]
    [SerializeField] private float drowsyStartThreshold = 50f;
    [Tooltip("Kesadaran di bawah nilai ini mulai terjadi Microsleep (mata menutup rapat sesaat)")]
    [SerializeField] private float microsleepThreshold = 30f;
    [SerializeField] private float minMicrosleepDuration = 0.35f;
    [SerializeField] private float maxMicrosleepDuration = 0.75f;
    [SerializeField] private float minMicrosleepCooldown = 1.8f;
    [SerializeField] private float maxMicrosleepCooldown = 4.2f;

    [Header("Efek Ngantuk 2: Kepala Terkulai (Head Nodding)")]
    [Tooltip("Transform Pivot Kepala (otomatis dibuat tepat di posisi mata Main Camera)")]
    [SerializeField] private Transform headPivotTransform;
    [Tooltip("Kesadaran di bawah nilai ini kepala mulai terkulai")]
    [SerializeField] private float headNodStartThreshold = 45f;
    [Tooltip("Sudut maksimal kepala menunduk ke bawah (derajat)")]
    [SerializeField] private float maxNodPitchAngle = 7f;
    [Tooltip("Sudut maksimal kepala miring ke samping (derajat)")]
    [SerializeField] private float maxNodRollAngle = 3.5f;

    [Header("Efek Ngantuk 3: Blur & Vignette (URP Global Volume)")]
    [SerializeField] private float blurStartThreshold = 50f;
    [SerializeField] private float maxBlurRadius = 1.25f;
    [SerializeField] private float maxVignetteIntensity = 0.68f;

    [Header("Pengaturan Nyawa & Tabrakan")]
    [SerializeField] private int maxLives = 3;
    [Tooltip("Lonjakan kesadaran saat kaget/rem mendadak menabrak")]
    [SerializeField] private float crashAdrenalineBonus = 40f;

    [Header("Pengaturan Jarak Tempuh (Win Condition)")]
    [SerializeField] private float startingDistanceKm = 2.0f;
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

    private float currentConsciousness;
    private int currentLives;
    private float remainingDistanceKm;
    private float zeroConsciousnessTimer;
    private bool isGameEnded = false;

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
    public int CurrentLives => currentLives;
    public bool IsGameEnded => isGameEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        ResetAllGameplaySystems();
        EnsureHUDCreated();
        InitHeadPivot();
        InitPostProcessingBlur();
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
        currentConsciousness = Mathf.Clamp(startingConsciousness, 0f, maxConsciousness);
        currentLives = maxLives;
        remainingDistanceKm = startingDistanceKm;
        zeroConsciousnessTimer = 0f;
        isGameEnded = false;

        isMicrosleeping = false;
        nextMicrosleepCountdown = Random.Range(minMicrosleepCooldown, maxMicrosleepCooldown);
        currentEyelidCoverage = 0f;
        currentNodPitch = 0f;
        currentNodRoll = 0f;
        targetNodRollSign = Random.value > 0.5f ? 1f : -1f;

        if (endGamePanel != null)
            endGamePanel.SetActive(false);

        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(RestartLevel);
        }

        UpdateAllHUD();
    }

    private void Update()
    {
        if (isGameEnded) return;

        // 1. Penurunan kesadaran pasif (-1/detik)
        currentConsciousness = Mathf.Max(0f, currentConsciousness - passiveDrainPerSecond * Time.deltaTime);

        // 2. Cek kondisi tertidur (0 selama 5 detik berturut-turut)
        if (currentConsciousness <= 0.01f)
        {
            currentConsciousness = 0f;
            zeroConsciousnessTimer += Time.deltaTime;

            if (zeroConsciousnessTimer >= sleepGameOverDelay)
            {
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
            if (endGameTitleText != null)
            {
                endGameTitleText.text = title;
                endGameTitleText.color = new Color(1f, 0.35f, 0.35f);
            }
            if (endGameDetailText != null)
            {
                endGameDetailText.text = $"{detail}\n\nSisa Jarak: {remainingDistanceKm:F2} KM";
            }
        }

        Debug.LogWarning($"[GAME OVER] {title} - {detail}");
    }

    private void TriggerWin()
    {
        isGameEnded = true;
        StopAllGameplaySystems();
        SetEyelidCoverage(0f, 0f);
        ResetHeadPivot();

        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);
            if (endGameTitleText != null)
            {
                endGameTitleText.text = "🏠 SELAMAT SAMPAI RUMAH!";
                endGameTitleText.color = new Color(0.35f, 1f, 0.55f);
            }
            if (endGameDetailText != null)
            {
                endGameDetailText.text = $"Kamu berhasil bertahan mengemudi hingga 0.00 KM!\nSisa Nyawa: {currentLives}/{maxLives}";
            }
        }

        Debug.Log("<color=lime>[MENANG]</color> Berhasil sampai rumah (0.00 KM)!");
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
        ResetAllGameplaySystems();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void UpdateAllHUD()
    {
        float ratio = Mathf.Clamp01(currentConsciousness / Mathf.Max(1f, maxConsciousness));

        if (consciousnessFillRect != null)
        {
            consciousnessFillRect.anchorMax = new Vector2(ratio, 1f);
        }

        if (consciousnessFillImage != null)
        {
            if (currentConsciousness < criticalThreshold)
                consciousnessFillImage.color = new Color(0.95f, 0.22f, 0.22f, 1f);
            else if (currentConsciousness < drowsyStartThreshold)
                consciousnessFillImage.color = new Color(0.96f, 0.72f, 0.15f, 1f);
            else
                consciousnessFillImage.color = new Color(0.20f, 0.85f, 0.45f, 1f);
        }

        if (consciousnessLabelText != null)
        {
            consciousnessLabelText.text = $"KESADARAN: {Mathf.CeilToInt(currentConsciousness)}%";
        }

        if (livesText != null)
        {
            string hearts = "";
            for (int i = 0; i < maxLives; i++)
            {
                hearts += (i < currentLives) ? "❤️ " : "🖤 ";
            }
            livesText.text = $"NYAWA: {hearts.Trim()}";
        }

        if (distanceText != null)
        {
            distanceText.text = $"🏠 JARAK: {remainingDistanceKm:F2} KM";
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

        // 2. Bar Kesadaran (Pojok Kiri Atas)
        if (consciousnessFillRect == null)
        {
            Transform barRoot = transform.Find("HUD_ConsciousnessBar");
            if (barRoot == null)
            {
                RectTransform bg = PhoneUIHelper.CreatePanel("HUD_ConsciousnessBar", transform, new Color(0.08f, 0.10f, 0.14f, 0.85f), new Vector2(0.03f, 0.90f), new Vector2(0.32f, 0.95f));
                bg.GetComponent<Image>().raycastTarget = false;

                consciousnessFillRect = PhoneUIHelper.CreatePanel("Fill", bg, new Color(0.20f, 0.85f, 0.45f, 1f), new Vector2(0f, 0f), new Vector2(1f, 1f));
                consciousnessFillImage = consciousnessFillRect.GetComponent<Image>();
                consciousnessFillImage.raycastTarget = false;

                consciousnessLabelText = PhoneUIHelper.CreateText("Label", bg, "KESADARAN: 100%", Color.white, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f), TextAnchor.MiddleCenter, 28);
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
        }

        // 3. Teks Nyawa
        if (livesText == null)
        {
            Transform t = transform.Find("HUD_LivesText");
            if (t == null)
            {
                livesText = PhoneUIHelper.CreateText("HUD_LivesText", transform, "NYAWA: ❤️ ❤️ ❤️", Color.white, new Vector2(0.03f, 0.84f), new Vector2(0.32f, 0.89f), TextAnchor.MiddleLeft, 28);
            }
            else livesText = t.GetComponent<Text>();
        }

        // 4. Teks Jarak Tempuh
        if (distanceText == null)
        {
            Transform t = transform.Find("HUD_DistanceText");
            if (t == null)
            {
                RectTransform distBg = PhoneUIHelper.CreatePanel("HUD_DistanceText", transform, new Color(0.08f, 0.10f, 0.14f, 0.85f), new Vector2(0.76f, 0.90f), new Vector2(0.97f, 0.95f));
                distBg.GetComponent<Image>().raycastTarget = false;
                distanceText = PhoneUIHelper.CreateText("Text", distBg, "🏠 JARAK: 2.00 KM", new Color(0.4f, 0.9f, 1f), new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f), TextAnchor.MiddleCenter, 28);
            }
            else distanceText = t.GetComponentInChildren<Text>();
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
                restartButton = PhoneUIHelper.CreateButton("Restart_Button", panelRt, "MAIN LAGI (RESTART)", new Color(0.20f, 0.75f, 0.42f), Color.white, new Vector2(0.36f, 0.25f), new Vector2(0.64f, 0.36f), out Text btnLabel, 34);

                endGamePanel.SetActive(false);
            }
            else
            {
                endGamePanel = t.gameObject;
                Transform titleT = t.Find("Title");
                if (titleT != null) endGameTitleText = titleT.GetComponent<Text>();
                Transform detailT = t.Find("Detail");
                if (detailT != null) endGameDetailText = detailT.GetComponent<Text>();
                Transform btnT = t.Find("Restart_Button");
                if (btnT != null) restartButton = btnT.GetComponent<Button>();
            }
        }
    }
}
