using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mengatur sistem Virus Pop-Up Iklan Menyebalkan:
/// - Dipicu setiap 2 minigame dengan undian random number (ibarat tidak sengaja kepencet).
/// - Memiliki tombol (X) kecil untuk menutup iklan.
/// - Jika pemain meleset (klik badan iklan), iklan akan beranak-pinak!
/// </summary>
public class PhoneAdPopupManager : MonoBehaviour
{
    [Header("Pengaturan Trigger Iklan")]
    [Tooltip("Muncul setiap berapa kali ganti minigame")]
    [SerializeField] private int reelsInterval = 2;

    [Tooltip("Peluang muncul iklan (0 - 100%) saat syarat 2 minigame terpenuhi")]
    [Range(10, 100)]
    [SerializeField] private int randomTriggerChancePercent = 75;

    [Tooltip("Batas maksimal pop-up iklan beranak-pinak di layar")]
    [SerializeField] private int maxSimultaneousAds = 6;

    [Header("Referensi Layer")]
    [SerializeField] private Image blockerBackground;

    private readonly List<GameObject> activeAdPopups = new List<GameObject>();
    private int reelsPlayedCounter = 0;
    private bool pendingRandomTapAd = false;

    private static readonly string[] AdSpamTexts = new string[]
    {
        "⚠️ PERINGATAN!\nHP ANDA KENA VIRUS!\n[JANGAN KLIK BODY!]",
        "💰 PINJOL KILAT!\nCAIR DALAM 3 DETIK!\nBUNGA 99%!",
        "🔥 OBAT ANTI NGANTUK!\nDIJAMIN MELEK 7 HARI!\nKLIK SEKARANG!",
        "🎁 SELAMAT!!\nANDA MENANG UNDIAN\nMOTOR MATIC!",
        "📢 IKLAN MENYEBALKAN!\nCARI TOMBOL [X] KECIL\nUNTUK MENUTUP!"
    };

    private static readonly Color[] AdColors = new Color[]
    {
        new Color(0.95f, 0.22f, 0.22f, 1f),
        new Color(0.95f, 0.65f, 0.05f, 1f),
        new Color(0.65f, 0.15f, 0.85f, 1f),
        new Color(0.10f, 0.65f, 0.45f, 1f)
    };

    public bool HasActiveAds => activeAdPopups.Count > 0;

    public void BuildPlaceholderUI()
    {
        if (blockerBackground != null) return;

        blockerBackground = GetComponent<Image>();
        if (blockerBackground == null)
            blockerBackground = gameObject.AddComponent<Image>();

        // Gelap transparan saat ada iklan aktif agar memblokir klik ke minigame di bawahnya
        blockerBackground.color = new Color(0f, 0f, 0f, 0.55f);
        blockerBackground.raycastTarget = true;
        blockerBackground.enabled = false;
    }

    private void Awake()
    {
        BuildPlaceholderUI();
        UpdateBlockerState();
    }

    /// <summary>
    /// Dipanggil setiap kali pemain berpindah ke reel minigame baru.
    /// Tiap 2 minigame, mengundi random number untuk memicu pop-up iklan.
    /// </summary>
    public void NotifyReelChanged()
    {
        reelsPlayedCounter++;

        if (reelsPlayedCounter >= reelsInterval)
        {
            reelsPlayedCounter = 0;

            int roll = Random.Range(0, 100);
            Debug.Log($"<color=yellow>[Cek Iklan 2 Minigame]</color> Random Roll: {roll} (Target < {randomTriggerChancePercent})");

            if (roll < randomTriggerChancePercent)
            {
                // Langsung munculkan iklan atau aktifkan jebakan klik berikutnya
                SpawnAdPopup();
            }
            else
            {
                // Jika tidak langsung muncul saat swipe, pasang jebakan "gak sengaja kepencet" saat klik di minigame
                pendingRandomTapAd = true;
            }
        }
    }

    /// <summary>
    /// Dipanggil saat pemain mengeklik layar di dalam minigame (simulasi gak sengaja mencet iklan)
    /// </summary>
    public void NotifyPlayerInteractionTap()
    {
        if (!pendingRandomTapAd) return;

        int roll = Random.Range(0, 100);
        if (roll < 45)
        {
            pendingRandomTapAd = false;
            Debug.Log("<color=magenta>[Iklan Kepencet!]</color> Pemain tidak sengaja memicu pop-up iklan!");
            SpawnAdPopup();
        }
    }

    public void SpawnAdPopup()
    {
        BuildPlaceholderUI();

        activeAdPopups.RemoveAll(ad => ad == null);
        if (activeAdPopups.Count >= maxSimultaneousAds)
            return;

        GameplayAudioManager.PlayAdPopup();

        // Posisi acak di dalam layar HP
        float width = Random.Range(0.72f, 0.84f);
        float height = Random.Range(0.32f, 0.40f);
        float minX = Random.Range(0.05f, 0.95f - width);
        float minY = Random.Range(0.18f, 0.86f - height);

        Vector2 anchorMin = new Vector2(minX, minY);
        Vector2 anchorMax = new Vector2(minX + width, minY + height);

        Color borderColor = Color.white;
        RectTransform adFrame = PhoneUIHelper.CreatePanel(
            $"AdPopup_{activeAdPopups.Count + 1}",
            transform,
            borderColor,
            anchorMin,
            anchorMax
        );

        // Badan Iklan (Jika diklik = Meleset -> Iklan Beranak-Pinak!)
        Color bodyColor = AdColors[Random.Range(0, AdColors.Length)];
        string spamMsg = AdSpamTexts[Random.Range(0, AdSpamTexts.Length)];

        Button bodyMisclickButton = PhoneUIHelper.CreateButton(
            "Ad_Body_MisclickZone",
            adFrame,
            spamMsg,
            bodyColor,
            Color.white,
            new Vector2(0.03f, 0.04f),
            new Vector2(0.97f, 0.96f),
            out Text bodyLabel,
            75
        );

        bodyMisclickButton.onClick.AddListener(() =>
        {
            Debug.Log("<color=red>[IKLAN MELESET!]</color> Salah klik badan iklan! Iklan beranak-pinak!");
            SpawnAdPopup();
        });

        // Tombol Silang (X) Kecil di Pojok Kanan Atas
        Button closeBtn = PhoneUIHelper.CreateButton(
            "Close_X_Button",
            adFrame,
            "✕",
            new Color(0.12f, 0.12f, 0.12f, 1f),
            new Color(1f, 0.3f, 0.3f, 1f),
            new Vector2(0.82f, 0.78f),
            new Vector2(0.98f, 0.98f),
            out Text closeLabel,
            70
        );

        GameObject popupObj = adFrame.gameObject;
        closeBtn.onClick.AddListener(() =>
        {
            CloseAdPopup(popupObj);
        });

        activeAdPopups.Add(popupObj);
        UpdateBlockerState();
    }

    private void CloseAdPopup(GameObject popupObj)
    {
        if (popupObj != null)
        {
            activeAdPopups.Remove(popupObj);
            Destroy(popupObj);
        }

        activeAdPopups.RemoveAll(ad => ad == null);
        UpdateBlockerState();
        Debug.Log($"<color=cyan>[Iklan Ditutup]</color> Sisa iklan di layar: {activeAdPopups.Count}");
    }

    public void ClearAllAds()
    {
        for (int i = 0; i < activeAdPopups.Count; i++)
        {
            if (activeAdPopups[i] != null)
            {
                if (Application.isPlaying)
                    Destroy(activeAdPopups[i]);
                else
                    DestroyImmediate(activeAdPopups[i]);
            }
        }
        activeAdPopups.Clear();
        UpdateBlockerState();
    }

    private void UpdateBlockerState()
    {
        if (blockerBackground != null)
        {
            blockerBackground.enabled = activeAdPopups.Count > 0;
        }
    }
}
