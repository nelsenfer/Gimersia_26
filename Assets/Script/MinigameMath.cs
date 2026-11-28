using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minigame 2: Kuis Matematika (Quick Math).
/// - Terdiri dari 3 soal berturut-turut dalam 1 sesi (Soal 1/3, 2/3, 3/3).
/// - Full text dengan background warna dasar, 1 soal & 4 pilihan jawaban (1 benar, 3 random).
/// - Setelah 3 soal selesai, menampilkan hasil akhir.
/// </summary>
public class MinigameMath : MonoBehaviour
{
    [Header("Pengaturan Kuis")]
    [Tooltip("Jumlah soal dalam 1 sesi Kuis MTK")]
    [SerializeField] private int totalQuestionsPerSession = 3;

    [Header("Warna Dasar")]
    [SerializeField] private Color baseBackgroundColor = new Color(0.12f, 0.27f, 0.42f, 1f);
    [SerializeField] private Color normalButtonColor = new Color(0.95f, 0.96f, 0.98f, 1f);
    [SerializeField] private Color correctButtonColor = new Color(0.18f, 0.80f, 0.44f, 1f);
    [SerializeField] private Color wrongButtonColor = new Color(0.90f, 0.25f, 0.25f, 1f);

    [Header("Asset Background Kuis MTK (Otomatis Terisi KONTEN 2 BG.png)")]
    [SerializeField] private Sprite mathBackgroundSprite;

    [Header("Referensi UI")]
    [SerializeField] private Image backgroundPanel;
    [SerializeField] private Text headerText;
    [SerializeField] private Text questionText;
    [SerializeField] private Text feedbackText;
    [SerializeField] private Button[] answerButtons = new Button[4];
    [SerializeField] private Text[] answerLabels = new Text[4];

    private int currentQuestionNumber = 0; // 1..3
    private int correctCountInSession = 0;
    private int correctAnswer;
    private int correctButtonIndex;
    private bool isWaitingNextStep = false;
    private bool isQuizFinished = false;
    private float nextStepTimer = 0f;

    public Action<float> OnConsciousnessReward;

    public void AutoBindExistingUI()
    {
        if (backgroundPanel == null) backgroundPanel = GetComponent<Image>();

        if (headerText == null)
        {
            Transform t = transform.Find("Math_Header");
            if (t != null) headerText = t.GetComponent<Text>();
        }

        if (questionText == null)
        {
            Transform qBox = transform.Find("Question_Box");
            if (qBox != null)
            {
                Transform qt = qBox.Find("Question_Text");
                if (qt != null) questionText = qt.GetComponent<Text>();
            }
        }

        if (feedbackText == null)
        {
            Transform t = transform.Find("Feedback_Text");
            if (t != null) feedbackText = t.GetComponent<Text>();
        }

        if (answerButtons == null || answerButtons.Length != 4)
            answerButtons = new Button[4];

        if (answerLabels == null || answerLabels.Length != 4)
            answerLabels = new Text[4];

        for (int i = 0; i < 4; i++)
        {
            if (answerButtons[i] == null)
            {
                Transform btnT = transform.Find($"Answer_Btn_{i + 1}");
                if (btnT != null) answerButtons[i] = btnT.GetComponent<Button>();
            }

            if (answerButtons[i] != null && answerLabels[i] == null)
            {
                answerLabels[i] = answerButtons[i].GetComponentInChildren<Text>();
            }
        }
    }

    public void BuildPlaceholderUI()
    {
        AutoBindExistingUI();
        if (headerText != null && questionText != null && answerButtons[0] != null)
            return;

        if (backgroundPanel == null)
            backgroundPanel = gameObject.AddComponent<Image>();

        if (mathBackgroundSprite != null)
        {
            backgroundPanel.sprite = mathBackgroundSprite;
            backgroundPanel.color = Color.white;
        }
        else
        {
            backgroundPanel.color = baseBackgroundColor;
        }
        backgroundPanel.raycastTarget = true;

        if (headerText == null)
        {
            headerText = PhoneUIHelper.CreateText(
                "Math_Header",
                transform,
                "🧮 KUIS MTK (1 / 3)",
                new Color(1f, 0.88f, 0.35f),
                new Vector2(0.08f, 0.82f),
                new Vector2(0.92f, 0.91f),
                TextAnchor.MiddleCenter,
                95
            );
        }

        if (questionText == null)
        {
            RectTransform questionBox = PhoneUIHelper.CreatePanel(
                "Question_Box",
                transform,
                new Color(0.07f, 0.16f, 0.26f, 1f),
                new Vector2(0.08f, 0.56f),
                new Vector2(0.92f, 0.79f)
            );
            questionBox.GetComponent<Image>().raycastTarget = false;

            questionText = PhoneUIHelper.CreateText(
                "Question_Text",
                questionBox,
                "12 + 7 = ?",
                Color.white,
                new Vector2(0.05f, 0.1f),
                new Vector2(0.95f, 0.9f),
                TextAnchor.MiddleCenter,
                150
            );
        }

        if (feedbackText == null)
        {
            feedbackText = PhoneUIHelper.CreateText(
                "Feedback_Text",
                transform,
                "Pilih 1 jawaban yang benar!",
                new Color(0.85f, 0.92f, 1f),
                new Vector2(0.08f, 0.46f),
                new Vector2(0.92f, 0.54f),
                TextAnchor.MiddleCenter,
                80
            );
        }

        Vector2[] mins = new Vector2[4]
        {
            new Vector2(0.08f, 0.28f),
            new Vector2(0.52f, 0.28f),
            new Vector2(0.08f, 0.11f),
            new Vector2(0.52f, 0.11f)
        };

        Vector2[] maxs = new Vector2[4]
        {
            new Vector2(0.48f, 0.43f),
            new Vector2(0.92f, 0.43f),
            new Vector2(0.48f, 0.26f),
            new Vector2(0.92f, 0.26f)
        };

        for (int i = 0; i < 4; i++)
        {
            if (answerButtons[i] == null)
            {
                answerButtons[i] = PhoneUIHelper.CreateButton(
                    $"Answer_Btn_{i + 1}",
                    transform,
                    "0",
                    normalButtonColor,
                    new Color(0.1f, 0.14f, 0.2f),
                    mins[i],
                    maxs[i],
                    out answerLabels[i],
                    120
                );
            }
        }
    }

    private void AutoBindMathSpritesInEditor()
    {
#if UNITY_EDITOR
        if (mathBackgroundSprite == null)
        {
            mathBackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprite/Ingame/KONTEN 2 BG.png");
        }
#endif
    }

    private void OnValidate()
    {
        AutoBindMathSpritesInEditor();
        AutoBindExistingUI();
    }

    private void Awake()
    {
        AutoBindMathSpritesInEditor();
        BuildPlaceholderUI();
        if (backgroundPanel != null && mathBackgroundSprite != null)
        {
            backgroundPanel.sprite = mathBackgroundSprite;
            backgroundPanel.color = Color.white;
        }
        BindButtons();
    }

    private void BindButtons()
    {
        if (answerButtons == null) return;

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int index = i;
            if (answerButtons[i] != null)
            {
                answerButtons[i].onClick.RemoveAllListeners();
                answerButtons[i].onClick.AddListener(() => OnAnswerSelected(index));
            }
        }
    }

    public void ResetMinigame()
    {
        BuildPlaceholderUI();
        BindButtons();

        currentQuestionNumber = 0;
        correctCountInSession = 0;
        isQuizFinished = false;
        isWaitingNextStep = false;

        SetButtonsInteractable(true);
        AdvanceToNextQuestionOrFinish();
    }

    private void Update()
    {
        if (isWaitingNextStep)
        {
            nextStepTimer -= Time.deltaTime;
            if (nextStepTimer <= 0f)
            {
                isWaitingNextStep = false;
                AdvanceToNextQuestionOrFinish();
            }
        }
    }

    private void AdvanceToNextQuestionOrFinish()
    {
        if (currentQuestionNumber >= totalQuestionsPerSession)
        {
            ShowQuizFinishedScreen();
            return;
        }

        currentQuestionNumber++;
        GenerateNewQuestion();
    }

    public void GenerateNewQuestion()
    {
        isWaitingNextStep = false;
        SetButtonsInteractable(true);

        if (headerText != null)
        {
            headerText.text = $"🧮 KUIS MTK (SOAL {currentQuestionNumber} / {totalQuestionsPerSession})";
        }

        int opType = UnityEngine.Random.Range(0, 3);
        int a, b;
        string opSymbol;

        if (opType == 0)
        {
            a = UnityEngine.Random.Range(4, 30);
            b = UnityEngine.Random.Range(3, 25);
            correctAnswer = a + b;
            opSymbol = "+";
        }
        else if (opType == 1)
        {
            a = UnityEngine.Random.Range(12, 40);
            b = UnityEngine.Random.Range(3, a - 1);
            correctAnswer = a - b;
            opSymbol = "-";
        }
        else
        {
            a = UnityEngine.Random.Range(2, 10);
            b = UnityEngine.Random.Range(2, 10);
            correctAnswer = a * b;
            opSymbol = "×";
        }

        if (questionText != null)
            questionText.text = $"{a} {opSymbol} {b} = ?";

        if (feedbackText != null)
        {
            feedbackText.text = $"Pertanyaan ke-{currentQuestionNumber} dari {totalQuestionsPerSession}";
            feedbackText.color = new Color(0.85f, 0.92f, 1f);
        }

        correctButtonIndex = UnityEngine.Random.Range(0, 4);
        HashSet<int> usedAnswers = new HashSet<int> { correctAnswer };

        for (int i = 0; i < 4; i++)
        {
            int val;
            if (i == correctButtonIndex)
            {
                val = correctAnswer;
            }
            else
            {
                int attempts = 0;
                do
                {
                    int offset = UnityEngine.Random.Range(-10, 11);
                    if (offset == 0) offset = 3;
                    val = Mathf.Max(0, correctAnswer + offset);
                    attempts++;
                }
                while (usedAnswers.Contains(val) && attempts < 30);

                while (usedAnswers.Contains(val))
                {
                    val++;
                }
                usedAnswers.Add(val);
            }

            if (answerLabels[i] != null)
                answerLabels[i].text = val.ToString();

            if (answerButtons[i] != null)
            {
                Image btnImg = answerButtons[i].GetComponent<Image>();
                if (btnImg != null) btnImg.color = normalButtonColor;
            }
        }
    }

    private void OnAnswerSelected(int index)
    {
        if (isWaitingNextStep || isQuizFinished) return;

        isWaitingNextStep = true;
        nextStepTimer = 0.65f;
        SetButtonsInteractable(false);

        if (index == correctButtonIndex)
        {
            correctCountInSession++;
            GameplayAudioManager.PlayPhoneUnlockSuccess();

            if (answerButtons[index] != null)
                answerButtons[index].GetComponent<Image>().color = correctButtonColor;

            if (feedbackText != null)
            {
                feedbackText.text = "✅ JAWABAN BENAR!";
                feedbackText.color = new Color(0.3f, 1f, 0.5f);
            }

            // Skala Max 100 (Ngantuk di 60): +10 per jawaban benar
            OnConsciousnessReward?.Invoke(10.0f);
        }
        else
        {
            GameplayAudioManager.PlayPhoneUnlockFailed();

            if (answerButtons[index] != null)
                answerButtons[index].GetComponent<Image>().color = wrongButtonColor;

            if (answerButtons[correctButtonIndex] != null)
                answerButtons[correctButtonIndex].GetComponent<Image>().color = correctButtonColor;

            if (feedbackText != null)
            {
                feedbackText.text = $"❌ SALAH! Jawaban: {correctAnswer}";
                feedbackText.color = new Color(1f, 0.45f, 0.45f);
            }

            // Penalti ringan salah jawab (-3 Kesadaran)
            OnConsciousnessReward?.Invoke(-3.0f);
        }
    }

    private void ShowQuizFinishedScreen()
    {
        isQuizFinished = true;
        SetButtonsInteractable(false);

        // Bonus selesai 3 soal jika berhasil menjawab benar minimal 2 soal (+14) -> Total benar 3/3 = +44 Poin
        if (correctCountInSession >= 2)
        {
            OnConsciousnessReward?.Invoke(14.0f);
        }

        if (headerText != null)
        {
            headerText.text = "🧮 KUIS MTK SELESAI!";
        }

        if (questionText != null)
        {
            questionText.text = $"HASIL AKHIR:\n{correctCountInSession} / {totalQuestionsPerSession} BENAR";
        }

        if (feedbackText != null)
        {
            feedbackText.text = "";
            feedbackText.color = new Color(1f, 0.88f, 0.35f);
        }

        for (int i = 0; i < 4; i++)
        {
            if (answerLabels[i] != null)
                answerLabels[i].text = "-";
        }
    }

    private void SetButtonsInteractable(bool state)
    {
        if (answerButtons == null) return;
        for (int i = 0; i < answerButtons.Length; i++)
        {
            if (answerButtons[i] != null)
                answerButtons[i].interactable = state;
        }
    }
}
