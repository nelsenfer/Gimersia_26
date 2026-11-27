using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Minigame 3: Tangkap Bakso.
/// - Mangkok di bawah bisa digeser ke kiri dan kanan.
/// - Memiliki Timer permainan.
/// - Saat timer habis, langsung menampilkan Result dapat berapa bakso
///   (baik di teks atas maupun di tengah area bermain, tanpa menampilkan kesadaran).
/// </summary>
public class MinigameBakso : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Header("Pengaturan Gameplay Bakso")]
    [Tooltip("Durasi waktu permainan bakso dalam detik")]
    [SerializeField] private float gameDuration = 10.0f;

    [Tooltip("Interval waktu (detik) munculnya bakso baru")]
    [SerializeField] private float spawnInterval = 0.75f;

    [Tooltip("Kecepatan jatuh bakso (dalam proporsi tinggi area bermain per detik)")]
    [SerializeField] private float fallSpeedNormalized = 0.52f;

    [Tooltip("Tambahan kesadaran di balik layar setiap menangkap 1 bakso (Skala Max 20)")]
    [SerializeField] private float consciousnessPerBakso = 1.25f;
    [Tooltip("Bonus kesadaran saat waktu habis jika berhasil menangkap minimal 5 bakso")]
    [SerializeField] private float sessionCompleteBonus = 4.0f;

    [Header("Referensi UI")]
    [SerializeField] private Image backgroundPanel;
    [SerializeField] private Text instructionText;
    [SerializeField] private Text scoreAndTimerText;
    [SerializeField] private RectTransform playAreaRect;
    [SerializeField] private RectTransform bowlRect;
    [SerializeField] private GameObject resultOverlayPanel;
    [SerializeField] private Text resultOverlayText;

    private class FallingBakso
    {
        public RectTransform rect;
        public Vector2 normalizedPos; // X: 0..1, Y: 0..1 di dalam playAreaRect
    }

    private readonly List<FallingBakso> activeBaksos = new List<FallingBakso>();
    private float spawnTimer = 0f;
    private float remainingTime = 0f;
    private bool isSessionFinished = false;
    private bool isInitializedForPlay = false;
    private bool isGameplayPaused = false;
    private int caughtCount = 0;
    private float bowlNormalizedX = 0.5f;

    public Action<float> OnConsciousnessReward;

    /// <summary>
    /// Menjeda sementara timer & jatuhnya bakso saat reel sedang di-preview/scroll oleh user.
    /// </summary>
    public void SetGameplayPaused(bool paused)
    {
        isGameplayPaused = paused;
    }

    public void AutoBindExistingUI()
    {
        if (backgroundPanel == null) backgroundPanel = GetComponent<Image>();

        if (instructionText == null)
        {
            Transform t = transform.Find("Bakso_Title");
            if (t != null) instructionText = t.GetComponent<Text>();
        }

        if (scoreAndTimerText == null)
        {
            Transform t = transform.Find("Bakso_Score");
            if (t != null) scoreAndTimerText = t.GetComponent<Text>();
        }

        if (playAreaRect == null)
        {
            Transform t = transform.Find("Bakso_PlayArea");
            if (t != null) playAreaRect = t.GetComponent<RectTransform>();
        }

        if (playAreaRect != null && bowlRect == null)
        {
            Transform t = playAreaRect.Find("Mangkok_Player");
            if (t != null) bowlRect = t.GetComponent<RectTransform>();
        }

        // Cari Bakso_ResultOverlay baik di dalam playAreaRect maupun di transform
        if (resultOverlayPanel == null)
        {
            if (playAreaRect != null)
            {
                Transform t = playAreaRect.Find("Bakso_ResultOverlay");
                if (t != null) resultOverlayPanel = t.gameObject;
            }
            if (resultOverlayPanel == null)
            {
                Transform t = transform.Find("Bakso_ResultOverlay");
                if (t != null) resultOverlayPanel = t.gameObject;
            }
        }

        if (resultOverlayPanel != null && resultOverlayText == null)
        {
            resultOverlayText = resultOverlayPanel.GetComponentInChildren<Text>(true);
        }
    }

    public void BuildPlaceholderUI()
    {
        AutoBindExistingUI();

        if (backgroundPanel == null)
        {
            backgroundPanel = gameObject.AddComponent<Image>();
            backgroundPanel.color = new Color(0.24f, 0.14f, 0.10f, 1f);
            backgroundPanel.raycastTarget = true;
        }

        if (instructionText == null)
        {
            instructionText = PhoneUIHelper.CreateText(
                "Bakso_Title",
                transform,
                "🍜 AMBIL BAKSO\nSEBANYAK-BANYAKNYA!",
                new Color(1f, 0.88f, 0.4f),
                new Vector2(0.06f, 0.81f),
                new Vector2(0.94f, 0.92f),
                TextAnchor.MiddleCenter,
                90
            );
        }

        if (scoreAndTimerText == null)
        {
            scoreAndTimerText = PhoneUIHelper.CreateText(
                "Bakso_Score",
                transform,
                "⏱ 10.0s  |  Bakso: 0",
                Color.white,
                new Vector2(0.06f, 0.72f),
                new Vector2(0.94f, 0.80f),
                TextAnchor.MiddleCenter,
                85
            );
        }

        if (playAreaRect == null)
        {
            playAreaRect = PhoneUIHelper.CreatePanel(
                "Bakso_PlayArea",
                transform,
                new Color(0.16f, 0.09f, 0.06f, 1f),
                new Vector2(0.05f, 0.08f),
                new Vector2(0.95f, 0.70f)
            );
            playAreaRect.GetComponent<Image>().raycastTarget = false;
        }

        if (bowlRect == null && playAreaRect != null)
        {
            bowlRect = PhoneUIHelper.CreatePanel(
                "Mangkok_Player",
                playAreaRect,
                new Color(0.95f, 0.95f, 0.98f, 1f),
                new Vector2(0.36f, 0.08f),
                new Vector2(0.64f, 0.16f)
            );
            bowlRect.GetComponent<Image>().raycastTarget = false;

            PhoneUIHelper.CreateText(
                "Mangkok_Label",
                bowlRect,
                "🥣 MANGKOK",
                new Color(0.15f, 0.15f, 0.2f),
                new Vector2(0.04f, 0.05f),
                new Vector2(0.96f, 0.95f),
                TextAnchor.MiddleCenter,
                65
            );
        }

        EnsureResultOverlayInsidePlayArea();
    }

    private void EnsureResultOverlayInsidePlayArea()
    {
        Transform targetParent = playAreaRect != null ? playAreaRect : transform;

        if (resultOverlayPanel == null)
        {
            RectTransform overlayRt = PhoneUIHelper.CreatePanel(
                "Bakso_ResultOverlay",
                targetParent,
                new Color(0.08f, 0.05f, 0.03f, 0.95f),
                Vector2.zero,
                Vector2.one
            );
            overlayRt.GetComponent<Image>().raycastTarget = false;
            resultOverlayPanel = overlayRt.gameObject;

            resultOverlayText = PhoneUIHelper.CreateText(
                "Result_Text",
                overlayRt,
                "WAKTU HABIS!\n\nDapat: 0 Bakso",
                new Color(1f, 0.9f, 0.35f),
                new Vector2(0.06f, 0.12f),
                new Vector2(0.94f, 0.88f),
                TextAnchor.MiddleCenter,
                110
            );

            resultOverlayPanel.SetActive(false);
        }
        else
        {
            if (resultOverlayText == null)
            {
                resultOverlayText = resultOverlayPanel.GetComponentInChildren<Text>(true);
            }
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

    private void OnEnable()
    {
        // Pastikan setiap kali panel Bakso aktif (baik dari Swipe maupun dinyalakan saat tes), game di-reset dengan benar
        if (Application.isPlaying)
        {
            ResetMinigame();
        }
    }

    public void ResetMinigame()
    {
        BuildPlaceholderUI();
        ClearAllBaksos();

        caughtCount = 0;
        remainingTime = Mathf.Max(1f, gameDuration);
        isSessionFinished = false;
        isInitializedForPlay = true;
        isGameplayPaused = false;
        spawnTimer = 0.25f;
        bowlNormalizedX = 0.5f;

        if (instructionText != null)
        {
            instructionText.text = "🍜 AMBIL BAKSO\nSEBANYAK-BANYAKNYA!";
            instructionText.color = new Color(1f, 0.88f, 0.4f);
        }

        if (bowlRect != null)
        {
            bowlRect.gameObject.SetActive(true);
        }

        if (resultOverlayPanel != null)
        {
            resultOverlayPanel.SetActive(false);
        }

        UpdateBowlPositionInPlayArea();
        UpdateScoreAndTimerUI();
    }

    private void OnDisable()
    {
        ClearAllBaksos();
    }

    private void Update()
    {
        if (!isInitializedForPlay)
        {
            ResetMinigame();
        }

        if (isGameplayPaused || isSessionFinished) return;
        if (playAreaRect == null || bowlRect == null) return;

        // Hitung mundur timer
        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            FinishBaksoSession();
            return;
        }

        UpdateScoreAndTimerUI();

        // Spawn bakso berkala
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            spawnTimer = spawnInterval;
            SpawnBakso();
        }

        UpdateFallingBaksos();
    }

    private void FinishBaksoSession()
    {
        isSessionFinished = true;
        ClearAllBaksos();

        if (caughtCount >= 5)
        {
            OnConsciousnessReward?.Invoke(sessionCompleteBonus);
        }

        // 1. Tampilkan hasil di teks atas (yang sudah pasti terlihat di layar)
        if (instructionText != null)
        {
            instructionText.text = "⏱ WAKTU HABIS!";
            instructionText.color = new Color(1f, 0.45f, 0.4f);
        }

        if (scoreAndTimerText != null)
        {
            scoreAndTimerText.text = $"🎉 DAPAT {caughtCount} BAKSO!";
            scoreAndTimerText.color = new Color(0.35f, 1f, 0.55f);
        }

        // 2. Sembunyikan mangkok dan tampilkan panel Result tepat di tengah area bermain
        if (bowlRect != null)
        {
            bowlRect.gameObject.SetActive(false);
        }

        EnsureResultOverlayInsidePlayArea();
        if (resultOverlayPanel != null)
        {
            resultOverlayPanel.SetActive(true);
            resultOverlayPanel.transform.SetAsLastSibling();

            if (resultOverlayText != null)
            {
                resultOverlayText.text = $"⏱ WAKTU HABIS!\n\nKAMU DAPAT:\n{caughtCount} BAKSO";
            }
        }

        Debug.Log($"<color=lime>[Minigame Bakso]</color> Waktu Habis! Result: Dapat {caughtCount} Bakso.");
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isGameplayPaused || isSessionFinished) return;
        MoveBowlToPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isGameplayPaused || isSessionFinished) return;
        MoveBowlToPointer(eventData);
    }

    private void MoveBowlToPointer(PointerEventData eventData)
    {
        if (playAreaRect == null) return;

        Camera cam = eventData.pressEventCamera != null ? eventData.pressEventCamera : Camera.main;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playAreaRect,
            eventData.position,
            cam,
            out Vector2 localPoint))
        {
            Rect r = playAreaRect.rect;
            if (r.width > 1f)
            {
                float normX = (localPoint.x - r.xMin) / r.width;
                bowlNormalizedX = Mathf.Clamp(normX, 0.14f, 0.86f);
                UpdateBowlPositionInPlayArea();
            }
        }
    }

    private void UpdateBowlPositionInPlayArea()
    {
        if (bowlRect == null || playAreaRect == null) return;

        Rect area = playAreaRect.rect;
        if (area.width > 1f && area.height > 1f)
        {
            // Gerakkan posisi X mangkok di dalam koordinat lokal playAreaRect tanpa merusak ukuran mangkok yang diatur user
            float targetLocalX = Mathf.Lerp(area.xMin, area.xMax, bowlNormalizedX);
            float targetLocalY = Mathf.Lerp(area.yMin, area.yMax, 0.14f);

            Vector3 pos = bowlRect.localPosition;
            pos.x = targetLocalX;
            pos.y = targetLocalY;
            pos.z = 0f;
            bowlRect.localPosition = pos;
        }
    }

    private void SpawnBakso()
    {
        if (playAreaRect == null) return;

        float randomX = UnityEngine.Random.Range(0.12f, 0.88f);
        float startY = 0.92f;

        float halfSize = 0.055f;
        RectTransform baksoRect = PhoneUIHelper.CreatePanel(
            "Bakso_Item",
            playAreaRect,
            new Color(0.68f, 0.45f, 0.32f, 1f),
            new Vector2(randomX - halfSize, startY - halfSize),
            new Vector2(randomX + halfSize, startY + halfSize)
        );
        baksoRect.GetComponent<Image>().raycastTarget = false;

        PhoneUIHelper.CreateText(
            "Label",
            baksoRect,
            "🧆",
            Color.white,
            new Vector2(0.05f, 0.05f),
            new Vector2(0.95f, 0.95f),
            TextAnchor.MiddleCenter,
            55
        );

        PositionItemInPlayArea(baksoRect, new Vector2(randomX, startY));

        activeBaksos.Add(new FallingBakso
        {
            rect = baksoRect,
            normalizedPos = new Vector2(randomX, startY)
        });
    }

    private void PositionItemInPlayArea(RectTransform itemRect, Vector2 normPos)
    {
        if (itemRect == null || playAreaRect == null) return;

        Rect area = playAreaRect.rect;
        if (area.width > 1f && area.height > 1f)
        {
            float lx = Mathf.Lerp(area.xMin, area.xMax, normPos.x);
            float ly = Mathf.Lerp(area.yMin, area.yMax, normPos.y);
            itemRect.localPosition = new Vector3(lx, ly, 0f);
        }
    }

    private void UpdateFallingBaksos()
    {
        for (int i = activeBaksos.Count - 1; i >= 0; i--)
        {
            FallingBakso item = activeBaksos[i];
            if (item.rect == null)
            {
                activeBaksos.RemoveAt(i);
                continue;
            }

            item.normalizedPos.y -= fallSpeedNormalized * Time.deltaTime;
            PositionItemInPlayArea(item.rect, item.normalizedPos);

            // Deteksi tangkapan mangkok
            bool overlapX = Mathf.Abs(item.normalizedPos.x - bowlNormalizedX) <= 0.18f;
            bool overlapY = Mathf.Abs(item.normalizedPos.y - 0.14f) <= 0.10f;

            if (overlapX && overlapY)
            {
                caughtCount++;
                OnConsciousnessReward?.Invoke(consciousnessPerBakso);
                UpdateScoreAndTimerUI();

                Destroy(item.rect.gameObject);
                activeBaksos.RemoveAt(i);
                continue;
            }

            if (item.normalizedPos.y < -0.05f)
            {
                Destroy(item.rect.gameObject);
                activeBaksos.RemoveAt(i);
            }
        }
    }

    private void UpdateScoreAndTimerUI()
    {
        if (scoreAndTimerText != null)
        {
            scoreAndTimerText.color = Color.white;
            scoreAndTimerText.text = $"⏱ {remainingTime:F1}s   |   Bakso: {caughtCount}";
        }
    }

    private void ClearAllBaksos()
    {
        for (int i = 0; i < activeBaksos.Count; i++)
        {
            if (activeBaksos[i].rect != null)
            {
                if (Application.isPlaying)
                    Destroy(activeBaksos[i].rect.gameObject);
                else
                    DestroyImmediate(activeBaksos[i].rect.gameObject);
            }
        }
        activeBaksos.Clear();
    }
}
