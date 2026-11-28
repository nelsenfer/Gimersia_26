using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minigame 1: Tap the Cat.
/// - Misi: Tap Kucing 20 kali.
/// - Memiliki tampilan Idle, dan tiap tap berubah ke tampilan Tap selama sekian detik.
/// - Jika waktu habis kembali ke Idle; jika di-tap berkali-kali durasi di-reset dari tap terakhir.
/// - Jika sudah mencapai 20 tap: counter berhenti di 20 dan tidak menambah kesadaran lagi,
///   namun visual kucing tetap bereaksi saat di-tap.
/// </summary>
public class MinigameTapCat : MonoBehaviour
{
    [Header("Pengaturan Misi & Pose")]
    [Tooltip("Target jumlah tap maksimal")]
    [SerializeField] private int targetTapCount = 20;

    [Tooltip("Durasi (detik) gambar kucing berubah saat di-tap sebelum kembali ke Idle")]
    [SerializeField] private float tapPoseDuration = 0.45f;

    [Header("Asset Gambar Kucing (Opsional - Bisa Diisi Nanti)")]
    [Tooltip("Sprite kucing saat diam (Idle)")]
    [SerializeField] private Sprite idleCatSprite;

    [Tooltip("Sprite kucing saat di-tap (Meong/Kaget)")]
    [SerializeField] private Sprite tappedCatSprite;

    [Header("Warna Placeholder (Jika Sprite Belum Ada)")]
    [SerializeField] private Color idlePlaceholderColor = new Color(0.95f, 0.76f, 0.35f, 1f);
    [SerializeField] private Color tappedPlaceholderColor = new Color(1.0f, 0.42f, 0.55f, 1f);

    [Header("Referensi UI")]
    [SerializeField] private Image backgroundPanel;
    [SerializeField] private Text missionTitleText;
    [SerializeField] private Text counterText;
    [SerializeField] private Button catTargetButton;
    [SerializeField] private Image catImage;
    [SerializeField] private Text catPlaceholderText;
    [SerializeField] private Text statusHintText;

    private int currentTaps = 0;
    private float tapPoseTimer = 0f;
    private bool isShowingTapPose = false;
    private bool missionCompleted = false;
    private string cachedCustomMissionTitle;
    private string cachedCustomHintText;
    private string cachedCustomIdleButtonText;
    private bool hasCachedCustomTexts = false;

    public Action<float> OnConsciousnessReward;

    public void AutoBindExistingUI()
    {
        if (backgroundPanel == null) backgroundPanel = GetComponent<Image>();

        if (missionTitleText == null)
        {
            Transform t = transform.Find("Mission_Title");
            if (t != null) missionTitleText = t.GetComponent<Text>();
        }

        if (counterText == null)
        {
            Transform t = transform.Find("Tap_Counter");
            if (t != null) counterText = t.GetComponent<Text>();
        }

        if (catTargetButton == null)
        {
            Transform t = transform.Find("Cat_Interactive_Box");
            if (t != null) catTargetButton = t.GetComponent<Button>();
        }

        if (catTargetButton != null)
        {
            if (catImage == null) catImage = catTargetButton.GetComponent<Image>();
            if (catPlaceholderText == null) catPlaceholderText = catTargetButton.GetComponentInChildren<Text>();
        }

        if (statusHintText == null)
        {
            Transform t = transform.Find("Hint_Text");
            if (t != null) statusHintText = t.GetComponent<Text>();
        }
    }

    public void BuildPlaceholderUI()
    {
        AutoBindExistingUI();
        if (missionTitleText != null && counterText != null && catTargetButton != null)
            return;

        if (backgroundPanel == null)
            backgroundPanel = gameObject.AddComponent<Image>();

        backgroundPanel.color = new Color(0.16f, 0.12f, 0.24f, 1f);
        backgroundPanel.raycastTarget = true;

        if (missionTitleText == null)
        {
            missionTitleText = PhoneUIHelper.CreateText(
                "Mission_Title",
                transform,
                "🐱 MISI: TAP KUCING 20 KALI!",
                new Color(1f, 0.88f, 0.35f),
                new Vector2(0.06f, 0.80f),
                new Vector2(0.94f, 0.91f),
                TextAnchor.MiddleCenter,
                95
            );
        }

        if (counterText == null)
        {
            counterText = PhoneUIHelper.CreateText(
                "Tap_Counter",
                transform,
                "0 / 20 TAP",
                Color.white,
                new Vector2(0.1f, 0.69f),
                new Vector2(0.9f, 0.79f),
                TextAnchor.MiddleCenter,
                115
            );
        }

        if (catTargetButton == null)
        {
            catTargetButton = PhoneUIHelper.CreateButton(
                "Cat_Interactive_Box",
                transform,
                "(=^･ω･^=)\n\n[ KUCING IDLE ]\nTAP AKU!",
                idlePlaceholderColor,
                new Color(0.15f, 0.1f, 0.1f),
                new Vector2(0.16f, 0.24f),
                new Vector2(0.84f, 0.65f),
                out catPlaceholderText,
                95
            );
            catImage = catTargetButton.GetComponent<Image>();
        }

        if (statusHintText == null)
        {
            statusHintText = PhoneUIHelper.CreateText(
                "Hint_Text",
                transform,
                "Ketuk kucing cepat-cepat!",
                new Color(0.8f, 0.8f, 0.9f),
                new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.20f),
                TextAnchor.MiddleCenter,
                70,
                FontStyle.Normal
            );
        }
    }

    private void OnValidate()
    {
        AutoBindExistingUI();
    }

    private void Awake()
    {
        BuildPlaceholderUI();
        CacheHierarchyCustomTexts();
        BindButtonEvents();
    }

    private void CacheHierarchyCustomTexts()
    {
        if (hasCachedCustomTexts) return;

        if (missionTitleText != null && !string.IsNullOrEmpty(missionTitleText.text))
            cachedCustomMissionTitle = missionTitleText.text;
        if (statusHintText != null && !string.IsNullOrEmpty(statusHintText.text))
            cachedCustomHintText = statusHintText.text;
        if (catPlaceholderText != null)
            cachedCustomIdleButtonText = catPlaceholderText.text;

        hasCachedCustomTexts = true;
    }

    private void BindButtonEvents()
    {
        if (catTargetButton != null)
        {
            catTargetButton.onClick.RemoveAllListeners();
            catTargetButton.onClick.AddListener(HandleCatTapped);
        }
    }

    public void ResetMinigame()
    {
        BuildPlaceholderUI();
        CacheHierarchyCustomTexts();
        BindButtonEvents();

        currentTaps = 0;
        tapPoseTimer = 0f;
        isShowingTapPose = false;
        missionCompleted = false;

        SetCatVisualState(false);
        UpdateMissionText();
    }

    private void Update()
    {
        if (isShowingTapPose)
        {
            tapPoseTimer -= Time.deltaTime;
            if (tapPoseTimer <= 0f)
            {
                // Waktu habis sejak tap terakhir -> Kembali ke gambar Idle
                isShowingTapPose = false;
                SetCatVisualState(false);
            }
        }
    }

    public void HandleCatTapped()
    {
        // Layar / Kucing TETAP bereaksi setiap kali di-tap meskipun sudah mencapai 20!
        tapPoseTimer = tapPoseDuration;
        isShowingTapPose = true;
        SetCatVisualState(true);

        // Jika sudah mencapai 20, jangan dihitung lagi dan jangan tambah kesadaran lagi
        if (currentTaps >= targetTapCount)
        {
            return;
        }

        currentTaps++;
        // Skala Max 100 (Ngantuk di 60): +0.8 per tap (20 tap = +16) + Bonus Selesai +25 (Total 1 Reel = +41 Poin)
        OnConsciousnessReward?.Invoke(0.8f);

        if (!missionCompleted && currentTaps >= targetTapCount)
        {
            missionCompleted = true;
            OnConsciousnessReward?.Invoke(25.0f);
            Debug.Log("<color=lime>[Tap the Cat]</color> Misi 20 Tap Selesai! Bonus dopamin +25.");
        }

        UpdateMissionText();
    }

    private void SetCatVisualState(bool tappedPose)
    {
        if (catImage == null) return;

        if (tappedPose)
        {
            if (tappedCatSprite != null)
            {
                catImage.sprite = tappedCatSprite;
                catImage.color = Color.white;
            }
            else
            {
                catImage.color = tappedPlaceholderColor;
            }

            if (catPlaceholderText != null)
            {
                catPlaceholderText.text = tappedCatSprite != null
                    ? ""
                    : "(=✧ω✧=)\n\nMEONG!! [TAP!]\n💥✨";
            }

            catImage.rectTransform.localScale = new Vector3(1.06f, 1.06f, 1f);
        }
        else
        {
            if (idleCatSprite != null)
            {
                catImage.sprite = idleCatSprite;
                catImage.color = Color.white;
            }
            else
            {
                catImage.color = idlePlaceholderColor;
            }

            if (catPlaceholderText != null)
            {
                catPlaceholderText.text = idleCatSprite != null
                    ? ""
                    : (cachedCustomIdleButtonText != null
                        ? cachedCustomIdleButtonText
                        : "(=^･ω･^=)\n\n[ KUCING IDLE ]\nTAP AKU!");
            }

            catImage.rectTransform.localScale = Vector3.one;
        }
    }

    private void UpdateMissionText()
    {
        if (missionTitleText != null)
        {
            missionTitleText.text = !string.IsNullOrEmpty(cachedCustomMissionTitle)
                ? cachedCustomMissionTitle
                : $"🐱 MISI: TAP KUCING {targetTapCount} KALI!";
        }

        if (statusHintText != null)
        {
            statusHintText.text = !string.IsNullOrEmpty(cachedCustomHintText)
                ? cachedCustomHintText
                : "Ketuk kucing cepat-cepat!";
        }

        if (counterText != null)
        {
            if (missionCompleted)
            {
                counterText.text = $"✅ SELESAI! ({targetTapCount} / {targetTapCount})";
                counterText.color = new Color(0.3f, 1f, 0.5f);
            }
            else
            {
                counterText.text = $"{currentTaps} / {targetTapCount} TAP";
                counterText.color = Color.white;
            }
        }
    }
}
