using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Minigame 4 (Lock Screen Awal): Verifikasi Sidik Jari.
/// Pemain harus menahan (Hold) area sensor selama 2 detik penuh.
/// - Jika berhasil 2 detik -> Tampil layar Sukses -> Masuk ke Reels Minigame.
/// - Jika dilepas sebelum 2 detik -> Langsung tampil layar Gagal -> Kembali ke Verifikasi Sidik Jari.
/// </summary>
public class MinigameFingerprint : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("Pengaturan Verifikasi")]
    [Tooltip("Durasi tahan (hold) dalam detik untuk berhasil verifikasi")]
    [SerializeField] private float requiredHoldDuration = 2.0f;

    [Tooltip("Durasi tampil pesan gagal sebelum kembali ke layar sidik jari")]
    [SerializeField] private float failScreenDuration = 0.85f;

    [Tooltip("Durasi tampil pesan sukses sebelum masuk ke FYP Reels")]
    [SerializeField] private float successScreenDuration = 0.65f;

    [Header("Referensi UI (Otomatis terhubung)")]
    [SerializeField] private Image backgroundPanel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text instructionText;
    [SerializeField] private Image sensorBoxImage;
    [SerializeField] private Text sensorIconText;
    [SerializeField] private RectTransform progressBarFill;
    [SerializeField] private GameObject resultOverlayPanel;
    [SerializeField] private Image resultOverlayImage;
    [SerializeField] private Text resultOverlayText;

    private enum VerifyState
    {
        WaitingForHold,
        Holding,
        ShowingFailed,
        ShowingSuccess
    }

    private VerifyState currentState = VerifyState.WaitingForHold;
    private float currentHoldTimer = 0f;
    private float stateTimer = 0f;
    private string cachedCustomInstructionText;
    private Color cachedCustomSensorColor = new Color(0.14f, 0.22f, 0.34f, 1f);
    private bool hasCachedCustomDefaults = false;

    public Action OnVerificationCompleted;

    public void AutoBindExistingUI()
    {
        if (backgroundPanel == null) backgroundPanel = GetComponent<Image>();

        if (titleText == null)
        {
            Transform t = transform.Find("Title_Text");
            if (t != null) titleText = t.GetComponent<Text>();
        }

        if (instructionText == null)
        {
            Transform t = transform.Find("Instruction_Text");
            if (t != null) instructionText = t.GetComponent<Text>();
        }

        if (sensorBoxImage == null)
        {
            Transform t = transform.Find("Fingerprint_Sensor");
            if (t != null) sensorBoxImage = t.GetComponent<Image>();
        }

        if (sensorBoxImage != null && sensorIconText == null)
        {
            sensorIconText = sensorBoxImage.GetComponentInChildren<Text>();
        }

        if (progressBarFill == null)
        {
            Transform bg = transform.Find("ProgressBar_BG");
            if (bg != null)
            {
                Transform fill = bg.Find("ProgressBar_Fill");
                if (fill != null) progressBarFill = fill.GetComponent<RectTransform>();
            }
        }

        if (resultOverlayPanel == null)
        {
            Transform t = transform.Find("Result_Overlay");
            if (t != null) resultOverlayPanel = t.gameObject;
        }

        if (resultOverlayPanel != null)
        {
            if (resultOverlayImage == null) resultOverlayImage = resultOverlayPanel.GetComponent<Image>();
            if (resultOverlayText == null) resultOverlayText = resultOverlayPanel.GetComponentInChildren<Text>();
        }
    }

    public void BuildPlaceholderUI()
    {
        AutoBindExistingUI();
        if (titleText != null && instructionText != null && sensorBoxImage != null && progressBarFill != null && resultOverlayPanel != null)
            return;

        if (backgroundPanel == null)
            backgroundPanel = gameObject.AddComponent<Image>();

        backgroundPanel.color = new Color(0.08f, 0.10f, 0.16f, 1f);
        backgroundPanel.raycastTarget = true;

        if (titleText == null)
        {
            titleText = PhoneUIHelper.CreateText(
                "Title_Text",
                transform,
                "🔒 KEAMANAN HP\nVERIFIKASI SIDIK JARI",
                new Color(0.4f, 0.85f, 1f),
                new Vector2(0.08f, 0.72f),
                new Vector2(0.92f, 0.90f),
                TextAnchor.MiddleCenter,
                110
            );
        }

        if (instructionText == null)
        {
            instructionText = PhoneUIHelper.CreateText(
                "Instruction_Text",
                transform,
                "KLIK & TAHAN (HOLD)\nSELAMA 2 DETIK",
                Color.white,
                new Vector2(0.1f, 0.58f),
                new Vector2(0.9f, 0.70f),
                TextAnchor.MiddleCenter,
                85
            );
        }

        if (sensorBoxImage == null)
        {
            RectTransform sensorRect = PhoneUIHelper.CreatePanel(
                "Fingerprint_Sensor",
                transform,
                new Color(0.14f, 0.22f, 0.34f, 1f),
                new Vector2(0.24f, 0.26f),
                new Vector2(0.76f, 0.54f)
            );
            sensorRect.GetComponent<Image>().raycastTarget = false;
            sensorBoxImage = sensorRect.GetComponent<Image>();

            sensorIconText = PhoneUIHelper.CreateText(
                "Sensor_Label",
                sensorRect,
                "(( ☝️ ))\nTEMPEL & TAHAN\nDI SINI",
                new Color(0.3f, 0.95f, 0.85f),
                new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.92f),
                TextAnchor.MiddleCenter,
                95
            );
        }

        if (progressBarFill == null)
        {
            RectTransform barBg = PhoneUIHelper.CreatePanel(
                "ProgressBar_BG",
                transform,
                new Color(0.2f, 0.24f, 0.3f, 1f),
                new Vector2(0.15f, 0.14f),
                new Vector2(0.85f, 0.20f)
            );
            barBg.GetComponent<Image>().raycastTarget = false;

            progressBarFill = PhoneUIHelper.CreatePanel(
                "ProgressBar_Fill",
                barBg,
                new Color(0.18f, 0.88f, 0.52f, 1f),
                new Vector2(0f, 0f),
                new Vector2(0f, 1f)
            );
            progressBarFill.GetComponent<Image>().raycastTarget = false;
        }

        if (resultOverlayPanel == null)
        {
            RectTransform overlayRect = PhoneUIHelper.CreatePanel(
                "Result_Overlay",
                transform,
                new Color(0.8f, 0.15f, 0.15f, 0.96f),
                Vector2.zero,
                Vector2.one
            );
            overlayRect.GetComponent<Image>().raycastTarget = false;
            resultOverlayPanel = overlayRect.gameObject;
            resultOverlayImage = overlayRect.GetComponent<Image>();

            resultOverlayText = PhoneUIHelper.CreateText(
                "Result_Text",
                overlayRect,
                "VERIFIKASI GAGAL!",
                Color.white,
                new Vector2(0.1f, 0.25f),
                new Vector2(0.9f, 0.75f),
                TextAnchor.MiddleCenter,
                130
            );

            resultOverlayPanel.SetActive(false);
        }
    }

    private void OnValidate()
    {
        AutoBindExistingUI();
    }

    private void Awake()
    {
        BuildPlaceholderUI();
        CacheHierarchyCustomDefaults();
    }

    private void CacheHierarchyCustomDefaults()
    {
        if (hasCachedCustomDefaults) return;

        if (instructionText != null && !string.IsNullOrEmpty(instructionText.text))
        {
            cachedCustomInstructionText = instructionText.text;
        }
        if (sensorBoxImage != null)
        {
            cachedCustomSensorColor = sensorBoxImage.color;
        }
        hasCachedCustomDefaults = true;
    }

    public void ResetToLockScreen()
    {
        BuildPlaceholderUI();
        CacheHierarchyCustomDefaults();

        currentState = VerifyState.WaitingForHold;
        currentHoldTimer = 0f;
        UpdateProgressVisual(0f);

        if (resultOverlayPanel != null)
            resultOverlayPanel.SetActive(false);

        if (sensorBoxImage != null)
            sensorBoxImage.color = cachedCustomSensorColor;

        if (instructionText != null)
        {
            instructionText.text = !string.IsNullOrEmpty(cachedCustomInstructionText)
                ? cachedCustomInstructionText
                : "KLIK & TAHAN (HOLD)\nSELAMA 2 DETIK";
        }
    }

    private void Update()
    {
        switch (currentState)
        {
            case VerifyState.Holding:
                currentHoldTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(currentHoldTimer / Mathf.Max(0.1f, requiredHoldDuration));
                UpdateProgressVisual(progress);

                if (instructionText != null)
                {
                    float remaining = Mathf.Max(0f, requiredHoldDuration - currentHoldTimer);
                    instructionText.text = $"MENSCAN SIDIK JARI...\n{remaining:F1} DETIK";
                }

                if (currentHoldTimer >= requiredHoldDuration)
                {
                    TriggerSuccessState();
                }
                break;

            case VerifyState.ShowingFailed:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    ResetToLockScreen();
                }
                break;

            case VerifyState.ShowingSuccess:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    if (resultOverlayPanel != null)
                        resultOverlayPanel.SetActive(false);

                    OnVerificationCompleted?.Invoke();
                }
                break;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (currentState != VerifyState.WaitingForHold) return;

        currentState = VerifyState.Holding;
        currentHoldTimer = 0f;

        if (sensorBoxImage != null)
            sensorBoxImage.color = new Color(0.16f, 0.45f, 0.55f, 1f);

        UpdateProgressVisual(0f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (currentState == VerifyState.Holding)
        {
            TriggerFailedState();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (currentState == VerifyState.Holding)
        {
            TriggerFailedState();
        }
    }

    private void TriggerFailedState()
    {
        currentState = VerifyState.ShowingFailed;
        stateTimer = failScreenDuration;
        currentHoldTimer = 0f;
        UpdateProgressVisual(0f);

        GameplayAudioManager.PlayPhoneUnlockFailed();

        if (resultOverlayPanel != null)
        {
            resultOverlayPanel.SetActive(true);
            resultOverlayImage.color = new Color(0.78f, 0.14f, 0.18f, 0.97f);
            resultOverlayText.text = "❌ VERIFIKASI GAGAL!\n\nJangan dilepas sebelum\n2 detik penuh!\n\n[Mengulang...]";
        }
    }

    private void TriggerSuccessState()
    {
        currentState = VerifyState.ShowingSuccess;
        stateTimer = successScreenDuration;
        UpdateProgressVisual(1f);

        GameplayAudioManager.PlayPhoneUnlockSuccess();

        if (resultOverlayPanel != null)
        {
            resultOverlayPanel.SetActive(true);
            resultOverlayImage.color = new Color(0.12f, 0.68f, 0.35f, 0.97f);
            resultOverlayText.text = "✅ VERIFIKASI SUKSES!\n\nAkses Diterima\nMembuka FYP...";
        }
    }

    private void UpdateProgressVisual(float normalizedValue)
    {
        if (progressBarFill != null)
        {
            PhoneUIHelper.ApplyAnchorBox(
                progressBarFill,
                new Vector2(0f, 0f),
                new Vector2(Mathf.Clamp01(normalizedValue), 1f)
            );
        }
    }
}
