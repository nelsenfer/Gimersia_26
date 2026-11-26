using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mengatur sistem Virus Pop-Up Iklan Hama Menyebalkan:
/// - Dipicu setiap beberapa minigame dengan undian random number (atau tidak sengaja kepencet).
/// - Iklan Utama memiliki 2 jenis jebakan:
///   1) Opsi B (2 Tombol X: 1 Tombol [X] Asli & 1 Tombol [X] Palsu).
///   2) Delay "Skip Ad in 3.. 2.. 1.." di mana tombol [X] asli belum muncul sampai countdown selesai.
/// - Jika pemain meleset klik badan iklan ATAU tertipu mengeklik tombol [X] Palsu,
///   iklan langsung meledak memunculkan 5 Pop-Up Iklan yang posisinya menyebar (mencar) di layar HP!
/// </summary>
public class PhoneAdPopupManager : MonoBehaviour
{
    [Header("Pengaturan Trigger Iklan")]
    [Tooltip("Apakah fitur pop-up iklan diaktifkan pada level ini")]
    [SerializeField] private bool adsEnabled = true;

    [Tooltip("Muncul setiap berapa kali ganti minigame")]
    [SerializeField] private int reelsInterval = 2;

    [Tooltip("Peluang muncul iklan (0 - 100%) saat syarat 2 minigame terpenuhi")]
    [Range(10, 100)]
    [SerializeField] private int randomTriggerChancePercent = 75;

    [Tooltip("Jumlah iklan mencar yang langsung muncul saat salah klik badan iklan / kena Fake X")]
    [SerializeField] private int scatterBurstCount = 5;

    [Tooltip("Batas maksimal pop-up iklan beranak-pinak di layar")]
    [SerializeField] private int maxSimultaneousAds = 8;

    [Header("Pengaturan Jebakan Iklan Hama (Fake X & Skip Ad)")]
    [Tooltip("Peluang (0 - 100%) iklan utama memiliki 2 tombol X (1 Asli & 1 Palsu)")]
    [Range(0, 100)]
    [SerializeField] private int fakeXTrapChancePercent = 75;

    [Tooltip("Peluang (0 - 100%) iklan utama harus menunggu countdown 'Skip Ad' sebelum tombol X muncul")]
    [Range(0, 100)]
    [SerializeField] private int skipAdCountdownChancePercent = 55;

    [Tooltip("Durasi tunggu (detik) untuk iklan 'Skip Ad' sebelum tombol [X] bisa diklik")]
    [SerializeField] private float skipAdDurationSeconds = 3.0f;

    [Header("Referensi Layer")]
    [SerializeField] private Image blockerBackground;

    private class ActiveAdEntry
    {
        public GameObject rootObj;
        public bool isCountdownActive;
        public float remainingSkipTime;
        public Button realCloseBtn;
        public Text realCloseLabel;
    }

    private readonly List<ActiveAdEntry> activeAdPopups = new List<ActiveAdEntry>();
    private int reelsPlayedCounter = 0;
    private bool pendingRandomTapAd = false;

    // 5 Zona Mencar (Kiri Atas, Kanan Atas, Tengah, Kiri Bawah, Kanan Bawah) agar 5 iklan menyebar merata
    private static readonly Vector2[] ScatterSlotMins = new Vector2[]
    {
        new Vector2(0.03f, 0.60f),
        new Vector2(0.45f, 0.56f),
        new Vector2(0.16f, 0.36f),
        new Vector2(0.04f, 0.14f),
        new Vector2(0.44f, 0.16f)
    };

    private static readonly Vector2[] ScatterSlotMaxs = new Vector2[]
    {
        new Vector2(0.55f, 0.87f),
        new Vector2(0.97f, 0.84f),
        new Vector2(0.84f, 0.64f),
        new Vector2(0.56f, 0.42f),
        new Vector2(0.96f, 0.44f)
    };

    private static readonly string[] AdSpamTexts = new string[]
    {
        "⚠️ PERINGATAN!\nHP ANDA KENA VIRUS!\n[AWAS TOMBOL X PALSU!]",
        "💰 PINJOL KILAT!\nCAIR DALAM 3 DETIK!\nBUNGA 99%!",
        "🔥 OBAT ANTI NGANTUK!\nDIJAMIN MELEK 7 HARI!\nKLIK SEKARANG!",
        "🎁 SELAMAT!!\nANDA MENANG UNDIAN\nMOTOR MATIC!",
        "📢 IKLAN HAMA!\nHATI-HATI SALAH KLIK\nBISA BERANAK 5!"
    };

    private static readonly Color[] AdColors = new Color[]
    {
        new Color(0.92f, 0.20f, 0.20f, 1f),
        new Color(0.92f, 0.60f, 0.05f, 1f),
        new Color(0.60f, 0.15f, 0.82f, 1f),
        new Color(0.10f, 0.62f, 0.44f, 1f),
        new Color(0.18f, 0.38f, 0.85f, 1f)
    };

    public bool HasActiveAds => activeAdPopups.Count > 0;

    public void SetAdsEnabled(bool enabledState)
    {
        adsEnabled = enabledState;
        if (!adsEnabled)
        {
            ClearAllAds();
        }
    }

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

    private void Update()
    {
        if (activeAdPopups.Count == 0) return;

        for (int i = activeAdPopups.Count - 1; i >= 0; i--)
        {
            ActiveAdEntry entry = activeAdPopups[i];
            if (entry == null || entry.rootObj == null)
            {
                activeAdPopups.RemoveAt(i);
                continue;
            }

            if (entry.isCountdownActive)
            {
                entry.remainingSkipTime -= Time.deltaTime;
                if (entry.remainingSkipTime > 0f)
                {
                    int sec = Mathf.CeilToInt(entry.remainingSkipTime);
                    if (entry.realCloseLabel != null)
                    {
                        entry.realCloseLabel.text = $"Skip Ad {sec}";
                    }
                }
                else
                {
                    // Countdown selesai! Ubah "Skip Ad" menjadi tombol [✕] asli yang bisa diklik
                    entry.isCountdownActive = false;
                    if (entry.realCloseBtn != null)
                    {
                        entry.realCloseBtn.interactable = true;
                        RectTransform rt = entry.realCloseBtn.GetComponent<RectTransform>();
                        if (rt != null)
                        {
                            // Kembalikan ukuran dari kotak teks Skip Ad menjadi tombol X kecil
                            bool isLeft = rt.anchorMin.x < 0.5f;
                            rt.anchorMin = isLeft ? new Vector2(0.03f, 0.78f) : new Vector2(0.83f, 0.78f);
                            rt.anchorMax = isLeft ? new Vector2(0.18f, 0.96f) : new Vector2(0.97f, 0.96f);
                        }
                    }
                    if (entry.realCloseLabel != null)
                    {
                        entry.realCloseLabel.text = "✕";
                        entry.realCloseLabel.color = new Color(0.92f, 0.95f, 0.98f, 1f);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Dipanggil setiap kali pemain berpindah ke reel minigame baru.
    /// Tiap 2 minigame, mengundi random number untuk memicu pop-up iklan.
    /// </summary>
    public void NotifyReelChanged()
    {
        if (!adsEnabled) return;

        reelsPlayedCounter++;

        if (reelsPlayedCounter >= reelsInterval)
        {
            reelsPlayedCounter = 0;

            int roll = Random.Range(0, 100);
            Debug.Log($"<color=yellow>[Cek Iklan 2 Minigame]</color> Random Roll: {roll} (Target < {randomTriggerChancePercent})");

            if (roll < randomTriggerChancePercent)
            {
                SpawnAdPopup(isScatterChild: false);
            }
            else
            {
                pendingRandomTapAd = true;
            }
        }
    }

    /// <summary>
    /// Dipanggil saat pemain mengeklik layar di dalam minigame (simulasi gak sengaja mencet iklan)
    /// </summary>
    public void NotifyPlayerInteractionTap()
    {
        if (!adsEnabled || !pendingRandomTapAd) return;

        int roll = Random.Range(0, 100);
        if (roll < 45)
        {
            pendingRandomTapAd = false;
            Debug.Log("<color=magenta>[Iklan Kepencet!]</color> Pemain tidak sengaja memicu pop-up iklan!");
            SpawnAdPopup(isScatterChild: false);
        }
    }

    /// <summary>
    /// Memunculkan 1 pop-up iklan utama.
    /// </summary>
    public void SpawnAdPopup()
    {
        SpawnAdPopup(isScatterChild: false);
    }

    private void SpawnAdPopup(bool isScatterChild, int scatterSlotIndex = -1)
    {
        if (!adsEnabled) return;

        BuildPlaceholderUI();

        activeAdPopups.RemoveAll(ad => ad == null || ad.rootObj == null);
        if (activeAdPopups.Count >= maxSimultaneousAds)
            return;

        GameplayAudioManager.PlayAdPopup();

        Vector2 anchorMin;
        Vector2 anchorMax;

        if (isScatterChild && scatterSlotIndex >= 0 && scatterSlotIndex < ScatterSlotMins.Length)
        {
            // Posisi menyebar (mencar) di 5 titik layar HP dengan sedikit variasi jitter
            Vector2 jitter = new Vector2(Random.Range(-0.02f, 0.02f), Random.Range(-0.02f, 0.02f));
            anchorMin = ScatterSlotMins[scatterSlotIndex] + jitter;
            anchorMax = ScatterSlotMaxs[scatterSlotIndex] + jitter;
            anchorMin.x = Mathf.Clamp(anchorMin.x, 0.02f, 0.55f);
            anchorMin.y = Mathf.Clamp(anchorMin.y, 0.08f, 0.65f);
            anchorMax.x = Mathf.Clamp(anchorMax.x, anchorMin.x + 0.38f, 0.98f);
            anchorMax.y = Mathf.Clamp(anchorMax.y, anchorMin.y + 0.24f, 0.90f);
        }
        else
        {
            // Iklan Utama berukuran cukup besar di tengah layar HP
            float width = Random.Range(0.74f, 0.86f);
            float height = Random.Range(0.34f, 0.42f);
            float minX = Random.Range(0.05f, 0.95f - width);
            float minY = Random.Range(0.20f, 0.84f - height);

            anchorMin = new Vector2(minX, minY);
            anchorMax = new Vector2(minX + width, minY + height);
        }

        Color borderColor = Color.white;
        RectTransform adFrame = PhoneUIHelper.CreatePanel(
            $"AdPopup_{activeAdPopups.Count + 1}",
            transform,
            borderColor,
            anchorMin,
            anchorMax
        );

        Color bodyColor = AdColors[Random.Range(0, AdColors.Length)];
        string spamMsg = AdSpamTexts[Random.Range(0, AdSpamTexts.Length)];

        // Badan Iklan (Jika diklik = Meleset -> Meledak Muncul 5 Iklan Mencar!)
        Button bodyMisclickButton = PhoneUIHelper.CreateButton(
            "Ad_Body_MisclickZone",
            adFrame,
            spamMsg,
            bodyColor,
            Color.white,
            new Vector2(0.03f, 0.04f),
            new Vector2(0.97f, 0.96f),
            out Text bodyLabel,
            72
        );

        GameObject popupObj = adFrame.gameObject;

        bodyMisclickButton.onClick.AddListener(() =>
        {
            Debug.Log("<color=red>[IKLAN MELESET!]</color> Salah klik badan iklan! Meledak 5 iklan mencar!");
            TriggerScatterAdExplosion(popupObj);
        });

        // Tentukan apakah iklan ini punya Tombol [X] Palsu (Opsi B) & apakah pakai "Skip Ad" countdown
        bool hasFakeX = !isScatterChild
            ? (Random.Range(0, 100) < fakeXTrapChancePercent)
            : (Random.Range(0, 100) < 35); // Anak iklan pun kadang masih punya Fake X

        bool hasSkipAdCountdown = !isScatterChild && (Random.Range(0, 100) < skipAdCountdownChancePercent);

        // Acak sisi pojok atas: apakah tombol [X] Asli di Kiri Atas dan [X] Palsu di Kanan Atas, atau sebaliknya!
        bool realXOnLeft = hasFakeX && (Random.value > 0.4f);

        // 1. Jika ada Fake X (Opsi B): Buat tombol [X] Palsu yang mencolok (warna merah terang)
        if (hasFakeX)
        {
            Vector2 fakeMin = realXOnLeft ? new Vector2(0.82f, 0.77f) : new Vector2(0.03f, 0.77f);
            Vector2 fakeMax = realXOnLeft ? new Vector2(0.97f, 0.96f) : new Vector2(0.18f, 0.96f);

            Button fakeCloseBtn = PhoneUIHelper.CreateButton(
                "Fake_X_Button",
                adFrame,
                "✖",
                new Color(0.95f, 0.15f, 0.15f, 1f),
                Color.yellow,
                fakeMin,
                fakeMax,
                out Text fakeLabel,
                68
            );

            fakeCloseBtn.onClick.AddListener(() =>
            {
                Debug.Log("<color=red>[JEBAKAN FAKE X!]</color> Pemain tertipu tombol X palsu! Meledak 5 iklan mencar!");
                TriggerScatterAdExplosion(popupObj);
            });
        }

        // 2. Buat Tombol [X] Asli (atau awalnya bertuliskan "Skip Ad 3" jika terkena countdown)
        Vector2 realMin;
        Vector2 realMax;

        if (hasSkipAdCountdown)
        {
            // Kotak sedikit lebih lebar untuk tulisan "Skip Ad 3"
            realMin = realXOnLeft ? new Vector2(0.03f, 0.77f) : new Vector2(0.62f, 0.77f);
            realMax = realXOnLeft ? new Vector2(0.38f, 0.96f) : new Vector2(0.97f, 0.96f);
        }
        else
        {
            realMin = realXOnLeft ? new Vector2(0.03f, 0.78f) : new Vector2(0.83f, 0.78f);
            realMax = realXOnLeft ? new Vector2(0.18f, 0.96f) : new Vector2(0.97f, 0.96f);
        }

        string initialRealBtnText = hasSkipAdCountdown
            ? $"Skip Ad {Mathf.CeilToInt(skipAdDurationSeconds)}"
            : "✕";

        Button realCloseBtn = PhoneUIHelper.CreateButton(
            "Real_Close_X_Button",
            adFrame,
            initialRealBtnText,
            new Color(0.12f, 0.14f, 0.18f, 0.92f),
            hasSkipAdCountdown ? new Color(0.85f, 0.85f, 0.85f, 1f) : new Color(0.92f, 0.95f, 0.98f, 1f),
            realMin,
            realMax,
            out Text realCloseLabel,
            64
        );

        if (hasSkipAdCountdown)
        {
            realCloseBtn.interactable = false;
        }

        realCloseBtn.onClick.AddListener(() =>
        {
            GameplayAudioManager.PlayPhoneTap();
            CloseAdPopup(popupObj);
        });

        ActiveAdEntry newEntry = new ActiveAdEntry
        {
            rootObj = popupObj,
            isCountdownActive = hasSkipAdCountdown,
            remainingSkipTime = skipAdDurationSeconds,
            realCloseBtn = realCloseBtn,
            realCloseLabel = realCloseLabel
        };

        activeAdPopups.Add(newEntry);
        UpdateBlockerState();
    }

    /// <summary>
    /// Saat salah klik badan iklan atau terkena Fake X:
    /// Tutup iklan pemicunya lalu langsung ledakkan 5 pop-up iklan yang mencar di seluruh layar HP!
    /// </summary>
    private void TriggerScatterAdExplosion(GameObject triggeredPopup)
    {
        if (triggeredPopup != null)
        {
            activeAdPopups.RemoveAll(e => e == null || e.rootObj == null || e.rootObj == triggeredPopup);
            Destroy(triggeredPopup);
        }

        int countToSpawn = Mathf.Max(1, scatterBurstCount);
        for (int i = 0; i < countToSpawn; i++)
        {
            if (activeAdPopups.Count >= maxSimultaneousAds)
                break;

            int slotIdx = i % ScatterSlotMins.Length;
            SpawnAdPopup(isScatterChild: true, scatterSlotIndex: slotIdx);
        }

        UpdateBlockerState();
    }

    private void CloseAdPopup(GameObject popupObj)
    {
        if (popupObj != null)
        {
            activeAdPopups.RemoveAll(e => e == null || e.rootObj == null || e.rootObj == popupObj);
            Destroy(popupObj);
        }

        activeAdPopups.RemoveAll(ad => ad == null || ad.rootObj == null);
        UpdateBlockerState();
        Debug.Log($"<color=cyan>[Iklan Ditutup]</color> Sisa iklan di layar: {activeAdPopups.Count}");
    }

    public void ClearAllAds()
    {
        for (int i = 0; i < activeAdPopups.Count; i++)
        {
            if (activeAdPopups[i] != null && activeAdPopups[i].rootObj != null)
            {
                if (Application.isPlaying)
                    Destroy(activeAdPopups[i].rootObj);
                else
                    DestroyImmediate(activeAdPopups[i].rootObj);
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
