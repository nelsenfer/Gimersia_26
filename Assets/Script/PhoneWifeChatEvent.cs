using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Event Sekali & Random: "Balas Chat Istri"
/// - Mempertahankan (preserve) perubahan struktur/teks/posisi UI yang diedit user di Hierarchy
///   melalui AutoBindExistingUI() (tidak menghapus child saat Play!).
/// - Ukuran font 2x lebih besar (resizeTextMaxSize = 300, lineSpacing rapat 0.88).
/// </summary>
public class PhoneWifeChatEvent : MonoBehaviour
{
    [Header("Pengaturan Trigger Level 3 (Aktif Setelah Menempuh 3 KM)")]
    [Tooltip("Jika true, Chat Istri menunggu dipicu oleh ConsciousnessManager setelah menempuh 3 KM di Level 3")]
    [SerializeField] private bool triggerByDistanceInLevel3 = true;
    [Tooltip("Waktu minimal (detik) setelah HP dibuka jika tidak memakai trigger jarak Level 3")]
    [SerializeField] private float minTriggerDelay = 12f;
    [Tooltip("Waktu maksimal (detik) setelah HP dibuka jika tidak memakai trigger jarak Level 3")]
    [SerializeField] private float maxTriggerDelay = 22f;

    [Header("Pengaturan Eskalasi Spam Notifikasi")]
    [Tooltip("Jeda detik sebelum notif susulan ke-2 & ke-3 menumpuk jika dicuekin")]
    [SerializeField] private float spamEscalationInterval = 5.0f;
    [Tooltip("Interval bunyi pengingat saat tumpukan notif sudah maksimal (P! P! P!)")]
    [SerializeField] private float maxSpamRepeatSoundInterval = 3.8f;

    [Header("Pengaturan Kalimat Balasan & Reward")]
    [SerializeField] private string targetReplySentence = "INI PULANG";
    [SerializeField] private float consciousnessRewardOnSent = 30f;

    [Header("Referensi UI (Otomatis Terhubung & Tidak Di-reset Saat Play)")]
    [SerializeField] private GameObject notifStackRoot;
    [SerializeField] private Button[] notifBanners = new Button[3];
    [SerializeField] private GameObject chatWindowRoot;
    [SerializeField] private GameObject wifeSpamBubble2;
    [SerializeField] private GameObject wifeSpamBubble3;
    [SerializeField] private GameObject playerSentBubble;
    [SerializeField] private Text playerSentBubbleText;
    [SerializeField] private Image inputBoxBackground;
    [SerializeField] private Text inputPreviewText;
    [SerializeField] private Text typingHintText;
    [SerializeField] private Button sendButton;
    [SerializeField] private Image sendButtonImage;
    [SerializeField] private Text sendButtonLabel;

    private enum EventPhase
    {
        WaitingToTrigger,
        NotificationStacking,
        ChatWindowOpen,
        ShowingSentConfirmation,
        Completed
    }

    private EventPhase currentPhase = EventPhase.WaitingToTrigger;
    private bool isPhoneUnlocked = false;
    private bool distanceMilestoneReached = false;
    private float triggerCountdown = 15f;
    private float escalationTimer = 0f;
    private float repeatAlertTimer = 0f;
    private float postSendCloseTimer = 0f;
    private float wrongKeyFlashTimer = 0f;

    private int activeBannerCount = 0;
    private int typedCharIndex = 0;

    private readonly Dictionary<char, Image> keyButtonImages = new Dictionary<char, Image>();

    private static readonly Color KeyNormalColor = new Color(0.20f, 0.24f, 0.31f, 1f);
    private static readonly Color KeyTargetHighlightColor = new Color(1.0f, 0.86f, 0.22f, 1f);
    private static readonly Color SendDisabledColor = new Color(0.24f, 0.27f, 0.33f, 1f);
    private static readonly Color SendReadyColor = new Color(0.12f, 0.85f, 0.38f, 1f);
    private static readonly Color InputNormalColor = new Color(0.11f, 0.15f, 0.20f, 1f);
    private static readonly Color InputWrongFlashColor = new Color(0.60f, 0.12f, 0.14f, 1f);

    public Action<float> OnChatCompletedReward;
    public Action<bool> OnChatWindowStateChanged;

    public bool IsChatWindowOpen =>
        currentPhase == EventPhase.ChatWindowOpen ||
        currentPhase == EventPhase.ShowingSentConfirmation;

    public bool IsEventActive =>
        currentPhase == EventPhase.NotificationStacking ||
        IsChatWindowOpen;

    /// <summary>
    /// Menghubungkan referensi UI yang sudah ada di Hierarchy TANPA menghapus atau menimpa
    /// teks/posisi yang sudah diedit manual oleh user di Unity Editor.
    /// </summary>
    public void AutoBindExistingUI()
    {
        if (notifStackRoot == null)
        {
            Transform t = transform.Find("NotifStack_Container");
            if (t != null) notifStackRoot = t.gameObject;
        }

        if (notifStackRoot != null)
        {
            if (notifBanners == null || notifBanners.Length != 3)
                notifBanners = new Button[3];

            for (int i = 0; i < 3; i++)
            {
                if (notifBanners[i] == null)
                {
                    Transform b = notifStackRoot.transform.Find($"WifeNotif_Banner_{i + 1}");
                    if (b != null)
                    {
                        notifBanners[i] = b.GetComponent<Button>();
                        if (notifBanners[i] == null)
                            notifBanners[i] = b.GetComponentInChildren<Button>(true);
                    }
                }
            }
        }

        if (chatWindowRoot == null)
        {
            Transform t = transform.Find("ChatApp_Window");
            if (t != null) chatWindowRoot = t.gameObject;
        }

        if (chatWindowRoot != null)
        {
            Transform history = chatWindowRoot.transform.Find("Chat_HistoryArea");
            if (history != null)
            {
                if (wifeSpamBubble2 == null)
                {
                    Transform b2 = history.Find("Wife_Bubble_2");
                    if (b2 != null) wifeSpamBubble2 = b2.gameObject;
                }
                if (wifeSpamBubble3 == null)
                {
                    Transform b3 = history.Find("Wife_Bubble_3");
                    if (b3 != null) wifeSpamBubble3 = b3.gameObject;
                }
                if (playerSentBubble == null)
                {
                    Transform sent = history.Find("Player_SentBubble");
                    if (sent != null) playerSentBubble = sent.gameObject;
                }
                if (playerSentBubble != null && playerSentBubbleText == null)
                {
                    playerSentBubbleText = playerSentBubble.GetComponentInChildren<Text>(true);
                }
            }

            if (typingHintText == null)
            {
                Transform hint = chatWindowRoot.transform.Find("Typing_Hint");
                if (hint != null) typingHintText = hint.GetComponent<Text>();
            }

            Transform inputBox = chatWindowRoot.transform.Find("Input_PreviewBox");
            if (inputBox != null)
            {
                if (inputBoxBackground == null) inputBoxBackground = inputBox.GetComponent<Image>();
                if (inputPreviewText == null) inputPreviewText = inputBox.GetComponentInChildren<Text>(true);
            }

            Transform sendTf = chatWindowRoot.transform.Find("Send_Button");
            if (sendTf != null)
            {
                if (sendButton == null) sendButton = sendTf.GetComponent<Button>();
                if (sendButtonImage == null) sendButtonImage = sendTf.GetComponent<Image>();
                if (sendButtonLabel == null) sendButtonLabel = sendTf.GetComponentInChildren<Text>(true);
            }

            Transform kb = chatWindowRoot.transform.Find("Virtual_Keyboard");
            if (kb != null)
            {
                keyButtonImages.Clear();
                Button[] kbButtons = kb.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < kbButtons.Length; i++)
                {
                    Button btn = kbButtons[i];
                    if (btn == null) continue;

                    string objName = btn.gameObject.name;
                    char keyChar = '\0';
                    if (objName == "Key_SPACE")
                    {
                        keyChar = ' ';
                    }
                    else if (objName.StartsWith("Key_") && objName.Length == 5)
                    {
                        keyChar = char.ToUpperInvariant(objName[4]);
                    }

                    if (keyChar != '\0')
                    {
                        char captured = keyChar;
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => HandleVirtualKeyClicked(captured));
                        Image img = btn.GetComponent<Image>();
                        if (img != null) keyButtonImages[keyChar] = img;
                    }
                }
            }
        }

        BindExistingButtonEventsAndUpgradeFontLimits();
    }

    private void BindExistingButtonEventsAndUpgradeFontLimits()
    {
        if (notifBanners != null)
        {
            for (int i = 0; i < notifBanners.Length; i++)
            {
                if (notifBanners[i] != null)
                {
                    notifBanners[i].onClick.RemoveAllListeners();
                    notifBanners[i].onClick.AddListener(OpenChatWindow);
                }
            }
        }

        if (sendButton != null)
        {
            sendButton.onClick.RemoveAllListeners();
            sendButton.onClick.AddListener(HandleSendButtonClicked);
        }

        // Perbesar batas ukuran font pada semua komponen Text yang sudah ada (2x lipat sampai 300)
        // TANPA mengubah isi teks (.text) yang sudah diedit user!
        Text[] allTexts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < allTexts.Length; i++)
        {
            Text t = allTexts[i];
            if (t == null) continue;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 48;
            t.resizeTextMaxSize = 300;
            t.lineSpacing = 0.86f;
        }
    }

    public void BuildPlaceholderUI()
    {
        Image rootImg = GetComponent<Image>();
        if (rootImg != null)
        {
            rootImg.color = new Color(0f, 0f, 0f, 0f);
            rootImg.raycastTarget = false;
        }

        AutoBindExistingUI();

        // PENTING: Jika UI sudah ada di Hierarchy (dan mungkin sudah diedit manual oleh user),
        // JANGAN dihapus/ditimpa!
        if (notifStackRoot != null && chatWindowRoot != null && keyButtonImages.Count > 0)
        {
            return;
        }

        if (notifStackRoot == null)
        {
            BuildNotificationStackUI();
        }

        if (chatWindowRoot == null)
        {
            BuildChatWindowUI();
        }

        AutoBindExistingUI();

        if (notifStackRoot != null) notifStackRoot.SetActive(false);
        if (chatWindowRoot != null) chatWindowRoot.SetActive(false);
    }

    [ContextMenu("Force Rebuild Wife Chat UI (Reset ke Default)")]
    public void ForceRebuildDefaultUI()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        notifStackRoot = null;
        chatWindowRoot = null;
        keyButtonImages.Clear();
        BuildPlaceholderUI();
    }

    private static Text CreateLargeFittedText(
        string name,
        Transform parent,
        string content,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        TextAnchor alignment,
        int maxFontSize = 300)
    {
        Text txt = PhoneUIHelper.CreateText(
            name,
            parent,
            content,
            color,
            anchorMin,
            anchorMax,
            alignment,
            maxFontSize,
            FontStyle.Bold
        );
        txt.verticalOverflow = VerticalWrapMode.Truncate;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 48;
        txt.resizeTextMaxSize = Mathf.Clamp(maxFontSize, 100, 300);
        txt.lineSpacing = 0.86f;
        return txt;
    }

    private static Button CreateLargeTextButton(
        string name,
        Transform parent,
        string labelText,
        Color bgColor,
        Color textColor,
        Vector2 anchorMin,
        Vector2 anchorMax,
        out Text labelComponent,
        int maxFontSize = 300)
    {
        Button btn = PhoneUIHelper.CreateButton(
            name,
            parent,
            labelText,
            bgColor,
            textColor,
            anchorMin,
            anchorMax,
            out labelComponent,
            maxFontSize
        );

        if (labelComponent != null)
        {
            PhoneUIHelper.ApplyAnchorBox(
                labelComponent.GetComponent<RectTransform>(),
                new Vector2(0.02f, 0.02f),
                new Vector2(0.98f, 0.98f)
            );
            labelComponent.verticalOverflow = VerticalWrapMode.Truncate;
            labelComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            labelComponent.resizeTextForBestFit = true;
            labelComponent.resizeTextMinSize = 48;
            labelComponent.resizeTextMaxSize = Mathf.Clamp(maxFontSize, 100, 300);
            labelComponent.lineSpacing = 0.86f;
        }

        return btn;
    }

    private void BuildNotificationStackUI()
    {
        RectTransform stackRt = PhoneUIHelper.CreatePanel(
            "NotifStack_Container",
            transform,
            new Color(0f, 0f, 0f, 0f),
            Vector2.zero,
            Vector2.one
        );
        stackRt.GetComponent<Image>().raycastTarget = false;
        notifStackRoot = stackRt.gameObject;

        // Struktur chat sesuai yang disukai user: My ISTRI \n "Mas di mana? Kok belum pulang?"
        string[] bannerMessages = new string[]
        {
            "My ISTRI\n\"Mas di mana? Kok belum pulang?\"",
            "My ISTRI (2)\n\"Kok dibaca doang?! Bales mas!\"",
            "My ISTRI (SPAM!)\n\"P! P! P! BALES SEKARANG!!\""
        };

        Color[] bannerColors = new Color[]
        {
            new Color(0.07f, 0.42f, 0.32f, 0.98f),
            new Color(0.68f, 0.36f, 0.06f, 0.98f),
            new Color(0.78f, 0.12f, 0.14f, 0.99f)
        };

        Vector2[] mins = new Vector2[]
        {
            new Vector2(0.02f, 0.68f),
            new Vector2(0.02f, 0.40f),
            new Vector2(0.02f, 0.08f)
        };

        Vector2[] maxs = new Vector2[]
        {
            new Vector2(0.98f, 0.94f),
            new Vector2(0.98f, 0.66f),
            new Vector2(0.98f, 0.38f)
        };

        for (int i = 0; i < 3; i++)
        {
            Button btn = CreateLargeTextButton(
                $"WifeNotif_Banner_{i + 1}",
                stackRt,
                bannerMessages[i],
                bannerColors[i],
                Color.white,
                mins[i],
                maxs[i],
                out Text lbl,
                300
            );

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OpenChatWindow);
            notifBanners[i] = btn;
            btn.gameObject.SetActive(false);
        }
    }

    private void BuildChatWindowUI()
    {
        RectTransform chatRt = PhoneUIHelper.CreatePanel(
            "ChatApp_Window",
            transform,
            new Color(0.06f, 0.09f, 0.12f, 1f),
            Vector2.zero,
            Vector2.one
        );
        chatWindowRoot = chatRt.gameObject;

        // 1. Header Bar WA
        RectTransform headerRt = PhoneUIHelper.CreatePanel(
            "Chat_Header",
            chatRt,
            new Color(0.04f, 0.38f, 0.32f, 1f),
            new Vector2(0f, 0.90f),
            new Vector2(1f, 1f)
        );
        CreateLargeFittedText(
            "Header_Title",
            headerRt,
            "My ISTRI ❤️",
            Color.white,
            new Vector2(0.03f, 0.04f),
            new Vector2(0.97f, 0.96f),
            TextAnchor.MiddleCenter,
            300
        );

        // 2. Area Riwayat Bubble Chat (0.54 - 0.90)
        RectTransform historyRt = PhoneUIHelper.CreatePanel(
            "Chat_HistoryArea",
            chatRt,
            new Color(0.05f, 0.08f, 0.11f, 1f),
            new Vector2(0f, 0.54f),
            new Vector2(1f, 0.90f)
        );

        RectTransform b1 = PhoneUIHelper.CreatePanel(
            "Wife_Bubble_1",
            historyRt,
            new Color(0.16f, 0.23f, 0.29f, 1f),
            new Vector2(0.02f, 0.68f),
            new Vector2(0.96f, 0.97f)
        );
        CreateLargeFittedText(
            "Text",
            b1,
            "Mas di mana? Kok belum pulang?",
            Color.white,
            new Vector2(0.02f, 0.04f),
            new Vector2(0.98f, 0.96f),
            TextAnchor.MiddleLeft,
            280
        );

        RectTransform b2 = PhoneUIHelper.CreatePanel(
            "Wife_Bubble_2",
            historyRt,
            new Color(0.22f, 0.24f, 0.16f, 1f),
            new Vector2(0.02f, 0.42f),
            new Vector2(0.96f, 0.66f)
        );
        CreateLargeFittedText(
            "Text",
            b2,
            "Kok dibaca doang?! Bales!",
            new Color(1f, 0.92f, 0.55f),
            new Vector2(0.02f, 0.04f),
            new Vector2(0.98f, 0.96f),
            TextAnchor.MiddleLeft,
            280
        );
        wifeSpamBubble2 = b2.gameObject;

        RectTransform b3 = PhoneUIHelper.CreatePanel(
            "Wife_Bubble_3",
            historyRt,
            new Color(0.45f, 0.12f, 0.14f, 1f),
            new Vector2(0.02f, 0.21f),
            new Vector2(0.96f, 0.40f)
        );
        CreateLargeFittedText(
            "Text",
            b3,
            "P! P! P! BALES SEKARANG!!",
            new Color(1f, 0.85f, 0.85f),
            new Vector2(0.02f, 0.04f),
            new Vector2(0.98f, 0.96f),
            TextAnchor.MiddleLeft,
            280
        );
        wifeSpamBubble3 = b3.gameObject;

        RectTransform sentRt = PhoneUIHelper.CreatePanel(
            "Player_SentBubble",
            historyRt,
            new Color(0.04f, 0.48f, 0.36f, 1f),
            new Vector2(0.10f, 0.01f),
            new Vector2(0.98f, 0.19f)
        );
        playerSentBubbleText = CreateLargeFittedText(
            "Text",
            sentRt,
            "INI PULANG ✓✓",
            Color.white,
            new Vector2(0.02f, 0.04f),
            new Vector2(0.98f, 0.96f),
            TextAnchor.MiddleRight,
            280
        );
        playerSentBubble = sentRt.gameObject;
        playerSentBubble.SetActive(false);

        // 3. Petunjuk & Kotak Ketik Besar + Tombol [KIRIM] (0.37 - 0.54)
        typingHintText = CreateLargeFittedText(
            "Typing_Hint",
            chatRt,
            "KLIK HURUF KUNING:",
            new Color(1f, 0.92f, 0.45f),
            new Vector2(0.02f, 0.48f),
            new Vector2(0.98f, 0.54f),
            TextAnchor.MiddleCenter,
            240
        );

        RectTransform inputBoxRt = PhoneUIHelper.CreatePanel(
            "Input_PreviewBox",
            chatRt,
            InputNormalColor,
            new Vector2(0.02f, 0.37f),
            new Vector2(0.68f, 0.475f)
        );
        inputBoxBackground = inputBoxRt.GetComponent<Image>();

        inputPreviewText = CreateLargeFittedText(
            "Input_Text",
            inputBoxRt,
            "INI PULANG",
            Color.white,
            new Vector2(0.02f, 0.02f),
            new Vector2(0.98f, 0.98f),
            TextAnchor.MiddleCenter,
            300
        );
        inputPreviewText.supportRichText = true;

        sendButton = CreateLargeTextButton(
            "Send_Button",
            chatRt,
            "KIRIM",
            SendDisabledColor,
            Color.white,
            new Vector2(0.70f, 0.37f),
            new Vector2(0.98f, 0.475f),
            out sendButtonLabel,
            280
        );
        sendButtonImage = sendButton.GetComponent<Image>();
        sendButton.onClick.RemoveAllListeners();
        sendButton.onClick.AddListener(HandleSendButtonClicked);

        // 4. Keyboard Virtual QWERTY (0.01 - 0.365)
        RectTransform kbBg = PhoneUIHelper.CreatePanel(
            "Virtual_Keyboard",
            chatRt,
            new Color(0.08f, 0.10f, 0.14f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0.365f)
        );

        BuildKeyboardRow(kbBg, "QWERTYUIOP", 0.74f, 0.98f, 0.008f);
        BuildKeyboardRow(kbBg, "ASDFGHJKL", 0.50f, 0.72f, 0.04f);
        BuildKeyboardRow(kbBg, "ZXCVBNM", 0.26f, 0.48f, 0.11f);

        Button spaceBtn = CreateLargeTextButton(
            "Key_SPACE",
            kbBg,
            "SPASI",
            KeyNormalColor,
            Color.white,
            new Vector2(0.15f, 0.02f),
            new Vector2(0.85f, 0.24f),
            out Text spaceLabel,
            280
        );
        spaceBtn.onClick.RemoveAllListeners();
        spaceBtn.onClick.AddListener(() => HandleVirtualKeyClicked(' '));
        keyButtonImages[' '] = spaceBtn.GetComponent<Image>();
    }

    private void BuildKeyboardRow(RectTransform parent, string keys, float minY, float maxY, float sidePadding)
    {
        int count = keys.Length;
        float usableWidth = 1f - (sidePadding * 2f);
        float step = usableWidth / count;
        float gap = 0.002f;

        for (int i = 0; i < count; i++)
        {
            char c = keys[i];
            float minX = sidePadding + i * step + gap;
            float maxX = sidePadding + (i + 1) * step - gap;

            Button keyBtn = CreateLargeTextButton(
                $"Key_{c}",
                parent,
                c.ToString(),
                KeyNormalColor,
                Color.white,
                new Vector2(minX, minY),
                new Vector2(maxX, maxY),
                out Text keyLabel,
                300
            );

            char capturedChar = c;
            keyBtn.onClick.RemoveAllListeners();
            keyBtn.onClick.AddListener(() => HandleVirtualKeyClicked(capturedChar));
            keyButtonImages[c] = keyBtn.GetComponent<Image>();
        }
    }

    private void OnValidate()
    {
        AutoBindExistingUI();
    }

    private void Awake()
    {
        BuildPlaceholderUI();
    }

    public void ResetEventForNewGame()
    {
        BuildPlaceholderUI();

        currentPhase = EventPhase.WaitingToTrigger;
        isPhoneUnlocked = false;
        distanceMilestoneReached = false;
        triggerCountdown = UnityEngine.Random.Range(minTriggerDelay, maxTriggerDelay);
        escalationTimer = 0f;
        repeatAlertTimer = 0f;
        activeBannerCount = 0;
        typedCharIndex = 0;

        if (notifStackRoot != null) notifStackRoot.SetActive(false);
        if (chatWindowRoot != null) chatWindowRoot.SetActive(false);

        if (notifBanners != null)
        {
            for (int i = 0; i < notifBanners.Length; i++)
            {
                if (notifBanners[i] != null)
                {
                    GameObject bannerObj = GetBannerRootObject(notifBanners[i]);
                    if (bannerObj != null) bannerObj.SetActive(false);
                }
            }
        }

        OnChatWindowStateChanged?.Invoke(false);
    }

    private GameObject GetBannerRootObject(Button bannerBtn)
    {
        if (bannerBtn == null) return null;
        if (notifStackRoot != null &&
            bannerBtn.transform.parent != null &&
            bannerBtn.transform.parent != notifStackRoot.transform &&
            bannerBtn.transform.parent.parent == notifStackRoot.transform)
        {
            return bannerBtn.transform.parent.gameObject;
        }
        return bannerBtn.gameObject;
    }

    public void NotifyPhoneUnlocked()
    {
        isPhoneUnlocked = true;
        if (triggerByDistanceInLevel3 && distanceMilestoneReached && currentPhase == EventPhase.WaitingToTrigger)
        {
            StartNotificationStacking();
        }
    }

    /// <summary>
    /// Dipanggil oleh ConsciousnessManager di Level 3 tepat saat pemain sudah menempuh 3 KM perjalanan.
    /// </summary>
    public void TriggerWifeChatByDistanceMilestone()
    {
        if (!gameObject.activeInHierarchy) return;

        distanceMilestoneReached = true;
        if (isPhoneUnlocked && currentPhase == EventPhase.WaitingToTrigger)
        {
            StartNotificationStacking();
        }
    }

    private void Update()
    {
        if (!isPhoneUnlocked) return;
        if (ConsciousnessManager.Instance != null && ConsciousnessManager.Instance.IsGameEnded) return;

        if (wrongKeyFlashTimer > 0f)
        {
            wrongKeyFlashTimer -= Time.deltaTime;
            if (wrongKeyFlashTimer <= 0f && inputBoxBackground != null)
            {
                inputBoxBackground.color = InputNormalColor;
            }
        }

        switch (currentPhase)
        {
            case EventPhase.WaitingToTrigger:
                if (triggerByDistanceInLevel3)
                {
                    if (distanceMilestoneReached)
                    {
                        StartNotificationStacking();
                    }
                }
                else
                {
                    triggerCountdown -= Time.deltaTime;
                    if (triggerCountdown <= 0f)
                    {
                        StartNotificationStacking();
                    }
                }
                break;

            case EventPhase.NotificationStacking:
                escalationTimer += Time.deltaTime;

                if (activeBannerCount == 1 && escalationTimer >= spamEscalationInterval)
                {
                    ShowNextSpamBanner(2);
                }
                else if (activeBannerCount == 2 && escalationTimer >= spamEscalationInterval * 2f)
                {
                    ShowNextSpamBanner(3);
                    repeatAlertTimer = maxSpamRepeatSoundInterval;
                }
                else if (activeBannerCount >= 3)
                {
                    repeatAlertTimer -= Time.deltaTime;
                    if (repeatAlertTimer <= 0f)
                    {
                        repeatAlertTimer = maxSpamRepeatSoundInterval;
                        GameplayAudioManager.PlayAdPopup();
                    }
                }
                break;

            case EventPhase.ShowingSentConfirmation:
                postSendCloseTimer -= Time.deltaTime;
                if (postSendCloseTimer <= 0f)
                {
                    CompleteAndCloseChat();
                }
                break;
        }
    }

    private void StartNotificationStacking()
    {
        currentPhase = EventPhase.NotificationStacking;
        escalationTimer = 0f;
        activeBannerCount = 0;

        if (notifStackRoot != null)
        {
            notifStackRoot.SetActive(true);
            transform.SetAsLastSibling();
        }

        ShowNextSpamBanner(1);
    }

    private void ShowNextSpamBanner(int count)
    {
        activeBannerCount = Mathf.Clamp(count, 1, 3);
        if (notifBanners != null)
        {
            for (int i = 0; i < notifBanners.Length; i++)
            {
                if (notifBanners[i] != null)
                {
                    GameObject bannerObj = GetBannerRootObject(notifBanners[i]);
                    if (bannerObj != null) bannerObj.SetActive(i < activeBannerCount);
                }
            }
        }

        GameplayAudioManager.PlayAdPopup();
    }

    public void OpenChatWindow()
    {
        if (currentPhase != EventPhase.NotificationStacking) return;

        currentPhase = EventPhase.ChatWindowOpen;
        typedCharIndex = 0;

        if (notifStackRoot != null) notifStackRoot.SetActive(false);
        if (chatWindowRoot != null) chatWindowRoot.SetActive(true);

        if (wifeSpamBubble2 != null) wifeSpamBubble2.SetActive(activeBannerCount >= 2);
        if (wifeSpamBubble3 != null) wifeSpamBubble3.SetActive(activeBannerCount >= 3);
        if (playerSentBubble != null) playerSentBubble.SetActive(false);

        GameplayAudioManager.PlayPhoneTap();
        OnChatWindowStateChanged?.Invoke(true);
        RefreshTypingVisuals();
    }

    private void HandleVirtualKeyClicked(char clickedChar)
    {
        if (currentPhase != EventPhase.ChatWindowOpen) return;

        string target = string.IsNullOrEmpty(targetReplySentence) ? "INI PULANG" : targetReplySentence.ToUpperInvariant();
        if (typedCharIndex >= target.Length)
        {
            if (typingHintText != null)
            {
                typingHintText.text = "KLIK TOMBOL [KIRIM] HIJAU!";
                typingHintText.color = new Color(0.35f, 1f, 0.55f);
            }
            GameplayAudioManager.PlayPhoneTap();
            return;
        }

        char expectedChar = target[typedCharIndex];
        if (char.ToUpperInvariant(clickedChar) == expectedChar)
        {
            typedCharIndex++;
            GameplayAudioManager.PlayPhoneTap();
            RefreshTypingVisuals();
        }
        else
        {
            GameplayAudioManager.PlayPhoneUnlockFailed();
            wrongKeyFlashTimer = 0.25f;
            if (inputBoxBackground != null)
            {
                inputBoxBackground.color = InputWrongFlashColor;
            }
            if (typingHintText != null)
            {
                string expectedLabel = expectedChar == ' ' ? "[SPASI]" : $"HURUF [{expectedChar}]";
                typingHintText.text = $"SALAH! KLIK {expectedLabel}!";
                typingHintText.color = new Color(1f, 0.45f, 0.45f);
            }
        }
    }

    private void HandleSendButtonClicked()
    {
        if (currentPhase != EventPhase.ChatWindowOpen) return;

        string target = string.IsNullOrEmpty(targetReplySentence) ? "INI PULANG" : targetReplySentence.ToUpperInvariant();
        if (typedCharIndex < target.Length)
        {
            GameplayAudioManager.PlayPhoneUnlockFailed();
            if (typingHintText != null)
            {
                typingHintText.text = "KETIK 'INI PULANG' DULU!";
                typingHintText.color = new Color(1f, 0.75f, 0.35f);
            }
            return;
        }

        currentPhase = EventPhase.ShowingSentConfirmation;
        postSendCloseTimer = 1.15f;

        GameplayAudioManager.PlayPhoneUnlockSuccess();

        if (playerSentBubble != null)
        {
            playerSentBubble.SetActive(true);
            if (playerSentBubbleText != null)
            {
                playerSentBubbleText.text = $"{target} ✓✓";
            }
        }

        if (typingHintText != null)
        {
            typingHintText.text = "TERKIRIM KE ISTRI! (+KESADARAN)";
            typingHintText.color = new Color(0.35f, 1f, 0.55f);
        }

        if (inputPreviewText != null)
        {
            inputPreviewText.text = "<color=#4EFF8B>TERKIRIM ✓✓</color>";
        }

        OnChatCompletedReward?.Invoke(consciousnessRewardOnSent);
    }

    private void CompleteAndCloseChat()
    {
        currentPhase = EventPhase.Completed;
        if (chatWindowRoot != null) chatWindowRoot.SetActive(false);
        if (notifStackRoot != null) notifStackRoot.SetActive(false);

        OnChatWindowStateChanged?.Invoke(false);
    }

    private void RefreshTypingVisuals()
    {
        string target = string.IsNullOrEmpty(targetReplySentence) ? "INI PULANG" : targetReplySentence.ToUpperInvariant();
        bool allTyped = typedCharIndex >= target.Length;

        if (inputPreviewText != null)
        {
            if (allTyped)
            {
                inputPreviewText.text = $"<color=#4EFF8B>{target}</color>";
            }
            else
            {
                string typedPart = target.Substring(0, typedCharIndex);
                char nextChar = target[typedCharIndex];
                string nextDisplay = nextChar == ' ' ? "_" : nextChar.ToString();
                string remainingPart = typedCharIndex + 1 < target.Length
                    ? target.Substring(typedCharIndex + 1)
                    : "";

                inputPreviewText.text =
                    $"<color=#4EFF8B>{typedPart}</color><color=#FFE548>{nextDisplay}</color><color=#687488>{remainingPart}</color>";
            }
        }

        if (typingHintText != null)
        {
            if (allTyped)
            {
                typingHintText.text = "SELESAI! KLIK [KIRIM] HIJAU!";
                typingHintText.color = new Color(0.35f, 1f, 0.55f);
            }
            else
            {
                char nextChar = target[typedCharIndex];
                string nextLabel = nextChar == ' ' ? "TOMBOL [SPASI]" : $"HURUF [{nextChar}]";
                typingHintText.text = $"KLIK {nextLabel} DI BAWAH:";
                typingHintText.color = new Color(1f, 0.92f, 0.45f);
            }
        }

        char highlightChar = allTyped ? '\0' : target[typedCharIndex];
        foreach (KeyValuePair<char, Image> kvp in keyButtonImages)
        {
            if (kvp.Value == null) continue;
            bool isTargetKey = !allTyped && kvp.Key == highlightChar;
            kvp.Value.color = isTargetKey ? KeyTargetHighlightColor : KeyNormalColor;

            Text keyLbl = kvp.Value.GetComponentInChildren<Text>();
            if (keyLbl != null)
            {
                keyLbl.color = isTargetKey ? new Color(0.08f, 0.10f, 0.14f) : Color.white;
            }
        }

        if (sendButtonImage != null)
        {
            sendButtonImage.color = allTyped ? SendReadyColor : SendDisabledColor;
        }
    }
}
