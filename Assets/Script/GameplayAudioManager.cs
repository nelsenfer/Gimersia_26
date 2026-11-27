using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Mengelola seluruh Audio & SFX Permainan dengan auto-bind ke 14 file di Assets/Audio/SFX/:
/// - motor-loop.mp3        -> Engine mobil player (Loop)
/// - NPCEngine.mp3         -> Engine mobil NPC (3D Spatial Loop - makin dekat makin terdengar)
/// - Pindahjalur.mp3       -> Saat mobil belok pindah jalur
/// - Crash.mp3             -> Saat tabrakan biasa (masih ada sisa nyawa / rem mendadak)
/// - CrashFinal.mp3        -> Saat kecelakaan fatal (nyawa habis), diikuti GameOver.mp3 saat panel muncul
/// - GameOver.mp3          -> Saat panel Game Over muncul
/// - double-car-honk.mp3   -> Klakson 2x saat sampai rumah (0 KM), diikuti level-complete.mp3
/// - level-complete.mp3    -> Musik/chime panel Menang (Level Complete)
/// - heartbeat.mp3         -> Detak jantung saat ngantuk kritis
/// - UnlockBerhasil.mp3    -> Sidik jari berhasil (2 detik penuh)
/// - UnlockGagal.mp3       -> Sidik jari gagal (dilepas sebelum 2 detik)
/// - Scroll.mp3            -> Swipe/scroll ganti reel HP
/// - TapPhonemp3.mp3       -> Tap layar HP / interaksi minigame
/// - Iklanpop.mp3          -> Pop-up iklan muncul
/// </summary>
public class GameplayAudioManager : MonoBehaviour
{
    public static GameplayAudioManager Instance { get; private set; }

    [Header("Mode Placeholder (Fallback Jika Ada Slot Kosong)")]
    [SerializeField] private bool useProceduralPlaceholdersIfEmpty = true;

    [Header("1. Audio Mobil Player")]
    [Tooltip("motor-loop.mp3 - Suara mesin mobil player (Loop)")]
    [SerializeField] private AudioClip playerEngineLoopClip;
    [Range(0f, 1f)]
    [SerializeField] private float playerEngineVolume = 0.40f;
    [Range(0.5f, 2f)]
    [SerializeField] private float playerEnginePitch = 1.0f;

    [Tooltip("Pindahjalur.mp3 - Suara saat mobil belok pindah jalur")]
    [SerializeField] private AudioClip laneChangeSwerveClip;
    [Range(0f, 1f)]
    [SerializeField] private float laneChangeVolume = 0.75f;

    [Tooltip("Crash.mp3 - Suara saat tabrakan biasa (masih ada sisa nyawa)")]
    [SerializeField] private AudioClip normalCrashClip;
    [Range(0f, 1f)]
    [SerializeField] private float normalCrashVolume = 0.90f;

    [Tooltip("CrashFinal.mp3 - Suara kecelakaan fatal saat nyawa habis")]
    [SerializeField] private AudioClip finalCrashClip;
    [Range(0f, 1f)]
    [SerializeField] private float finalCrashVolume = 1.0f;

    [Header("2. Audio Mobil NPC (3D Spatial - Makin Dekat Makin Terdengar)")]
    [Tooltip("NPCEngine.mp3 - Suara mesin mobil NPC standar (Loop 3D)")]
    [SerializeField] private AudioClip npcEngineLoopClip;
    [Tooltip("Audio Khusus / Troll untuk 1 Mobil NPC Spesial (Otomatis terisi tereret_Copy.mp3 / bisa diganti di Inspector)")]
    [SerializeField] private AudioClip specialTrollNpcAudioClip;
    [Tooltip("Nama kata kunci prefab mobil NPC yang memakai audio khusus troll (misal: 'Angkot' atau 'lemon'), atau peluang random jika semua mobil bisa kena")]
    [SerializeField] private string specialTrollNpcNameKeyword = "Angkot";
    [Tooltip("Peluang (0 - 100%) mobil NPC lain ikut memakai suara troll (0 = hanya mobil yang cocok keyword di atas)")]
    [Range(0, 100)]
    [SerializeField] private int randomTrollNpcChancePercent = 20;
    [Range(0f, 1f)]
    [SerializeField] private float npcEngineMaxVolume = 0.80f;
    [Range(0f, 1f)]
    [SerializeField] private float specialTrollNpcMaxVolume = 0.95f;
    [Tooltip("Jarak di mana suara mobil NPC terdengar paling keras (meter)")]
    [SerializeField] private float npcAudioMinDistance = 4.5f;
    [Tooltip("Jarak mulai terdengarnya mobil NPC dari kejauhan (meter)")]
    [SerializeField] private float npcAudioMaxDistance = 48f;
    [Range(0f, 2f)]
    [SerializeField] private float npcDopplerLevel = 0.5f;

    [Header("3. Audio Ngantuk Kritis, Game Over & Menang")]
    [Tooltip("heartbeat.mp3 - Detak jantung saat kesadaran kritis (< 28%)")]
    [SerializeField] private AudioClip drowsyHeartbeatLoopClip;
    [Range(0f, 1f)]
    [SerializeField] private float heartbeatMaxVolume = 0.85f;

    [Tooltip("GameOver.mp3 - Suara saat panel Game Over muncul")]
    [SerializeField] private AudioClip gameOverPanelClip;
    [Tooltip("Jeda (detik) setelah CrashFinal sebelum GameOver.mp3 berbunyi")]
    [SerializeField] private float gameOverAfterFinalCrashDelay = 0.65f;

    [Tooltip("double-car-honk.mp3 - Klakson 2x saat sampai rumah")]
    [SerializeField] private AudioClip winCarHonkClip;
    [Tooltip("level-complete.mp3 - Suara panel Level Complete muncul")]
    [SerializeField] private AudioClip levelCompletePanelClip;
    [Tooltip("Jeda (detik) setelah klakson 2x sebelum level-complete.mp3 berbunyi")]
    [SerializeField] private float levelCompleteAfterHonkDelay = 0.45f;

    [Header("4. Audio Smartphone & Minigame")]
    [Tooltip("UnlockBerhasil.mp3 - Sidik jari berhasil")]
    [SerializeField] private AudioClip phoneUnlockSuccessClip;
    [Tooltip("UnlockGagal.mp3 - Sidik jari gagal")]
    [SerializeField] private AudioClip phoneUnlockFailClip;
    [Tooltip("Scroll.mp3 - Swipe/scroll ganti reel")]
    [SerializeField] private AudioClip reelSwipeClip;
    [Tooltip("TapPhonemp3.mp3 - Tap layar HP / minigame")]
    [SerializeField] private AudioClip phoneTapClip;
    [Tooltip("Iklanpop.mp3 - Pop-up iklan muncul")]
    [SerializeField] private AudioClip adPopupClip;
    [Range(0f, 1f)]
    [SerializeField] private float phoneSfxVolume = 0.75f;

    private AudioSource playerEngineSource;
    private AudioSource heartbeatSource;
    private AudioSource sfxOneShotSource;

    private AudioClip activePlayerEngineClip;
    private AudioClip activeLaneChangeClip;
    private AudioClip activeNormalCrashClip;
    private AudioClip activeFinalCrashClip;
    private AudioClip activeNpcEngineClip;
    private AudioClip activeSpecialTrollNpcClip;
    private AudioClip activeHeartbeatClip;
    private AudioClip activeGameOverPanelClip;
    private AudioClip activeWinHonkClip;
    private AudioClip activeLevelCompleteClip;
    private AudioClip activeUnlockSuccessClip;
    private AudioClip activeUnlockFailClip;
    private AudioClip activeReelSwipeClip;
    private AudioClip activePhoneTapClip;
    private AudioClip activeAdPopupClip;

    private readonly List<AudioSource> activeNpcSources = new List<AudioSource>();
    private bool isGameplayStopped = false;

    public static GameplayAudioManager EnsureExists()
    {
        if (Instance != null) return Instance;

        Instance = FindFirstObjectByType<GameplayAudioManager>();
        if (Instance != null) return Instance;

        GameObject go = new GameObject("GameplayAudioManager");
        Instance = go.AddComponent<GameplayAudioManager>();
        return Instance;
    }

#if UNITY_EDITOR
    [ContextMenu("Auto-Bind Semua SFX dari Assets/Audio/SFX")]
    public void AutoBindAudioClipsInEditor()
    {
        if (playerEngineLoopClip == null)
            playerEngineLoopClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/motor-loop.mp3");
        if (npcEngineLoopClip == null)
            npcEngineLoopClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/NPCEngine.mp3");
        if (specialTrollNpcAudioClip == null)
        {
            specialTrollNpcAudioClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/tereret_Copy.mp3");
            if (specialTrollNpcAudioClip == null)
                specialTrollNpcAudioClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/NPCEngine2.mp3");
        }
        if (laneChangeSwerveClip == null)
            laneChangeSwerveClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Pindahjalur.mp3");
        if (normalCrashClip == null)
            normalCrashClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Crash.mp3");
        if (finalCrashClip == null)
            finalCrashClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/CrashFinal.mp3");
        if (drowsyHeartbeatLoopClip == null)
            drowsyHeartbeatLoopClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/heartbeat.mp3");
        if (gameOverPanelClip == null)
            gameOverPanelClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/GameOver.mp3");
        if (winCarHonkClip == null)
            winCarHonkClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/double-car-honk.mp3");
        if (levelCompletePanelClip == null)
            levelCompletePanelClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/level-complete.mp3");
        if (phoneUnlockSuccessClip == null)
            phoneUnlockSuccessClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/UnlockBerhasil.mp3");
        if (phoneUnlockFailClip == null)
            phoneUnlockFailClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/UnlockGagal.mp3");
        if (reelSwipeClip == null)
            reelSwipeClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Scroll.mp3");
        if (phoneTapClip == null)
            phoneTapClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/TapPhonemp3.mp3");
        if (adPopupClip == null)
            adPopupClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Iklanpop.mp3");
    }

    private void Reset()
    {
        AutoBindAudioClipsInEditor();
    }

    private void OnValidate()
    {
        AutoBindAudioClipsInEditor();
    }
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

#if UNITY_EDITOR
        AutoBindAudioClipsInEditor();
#endif

        PrepareAudioClips();
        SetupAudioSources();
    }

    private void Start()
    {
        StartPlayerEngineLoop();
    }

    private void Update()
    {
        if (isGameplayStopped) return;

        if (playerEngineSource != null)
        {
            playerEngineSource.volume = playerEngineVolume;
            playerEngineSource.pitch = playerEnginePitch;
        }

        UpdateHeartbeatByConsciousness();
    }

    private void PrepareAudioClips()
    {
        activePlayerEngineClip = playerEngineLoopClip != null
            ? playerEngineLoopClip
            : (useProceduralPlaceholdersIfEmpty ? CreateEngineLoopClip("Placeholder_PlayerEngine", 58f, 1.4f) : null);

        activeNpcEngineClip = npcEngineLoopClip != null
            ? npcEngineLoopClip
            : (useProceduralPlaceholdersIfEmpty ? CreateEngineLoopClip("Placeholder_NPCEngine", 82f, 1.2f) : null);

        activeSpecialTrollNpcClip = specialTrollNpcAudioClip != null
            ? specialTrollNpcAudioClip
            : activeNpcEngineClip;

        activeLaneChangeClip = laneChangeSwerveClip != null
            ? laneChangeSwerveClip
            : (useProceduralPlaceholdersIfEmpty ? CreateSwerveWhooshClip() : null);

        activeNormalCrashClip = normalCrashClip != null
            ? normalCrashClip
            : (useProceduralPlaceholdersIfEmpty ? CreateCrashImpactClip() : null);

        activeFinalCrashClip = finalCrashClip != null
            ? finalCrashClip
            : activeNormalCrashClip;

        activeHeartbeatClip = drowsyHeartbeatLoopClip != null
            ? drowsyHeartbeatLoopClip
            : (useProceduralPlaceholdersIfEmpty ? CreateHeartbeatLoopClip() : null);

        activeGameOverPanelClip = gameOverPanelClip != null
            ? gameOverPanelClip
            : (useProceduralPlaceholdersIfEmpty ? CreateToneSequenceClip("Placeholder_GameOver", new float[] { 320f, 240f, 175f }, 0.22f) : null);

        activeWinHonkClip = winCarHonkClip != null
            ? winCarHonkClip
            : (useProceduralPlaceholdersIfEmpty ? CreateToneSequenceClip("Placeholder_Honk", new float[] { 420f, 420f }, 0.12f) : null);

        activeLevelCompleteClip = levelCompletePanelClip != null
            ? levelCompletePanelClip
            : (useProceduralPlaceholdersIfEmpty ? CreateToneSequenceClip("Placeholder_GameWin", new float[] { 440f, 554f, 659f, 880f }, 0.16f) : null);

        activeUnlockSuccessClip = phoneUnlockSuccessClip != null
            ? phoneUnlockSuccessClip
            : (useProceduralPlaceholdersIfEmpty ? CreateToneSequenceClip("Placeholder_UnlockSuccess", new float[] { 587f, 880f }, 0.08f) : null);

        activeUnlockFailClip = phoneUnlockFailClip != null
            ? phoneUnlockFailClip
            : (useProceduralPlaceholdersIfEmpty ? CreateToneSequenceClip("Placeholder_UnlockFail", new float[] { 300f, 210f }, 0.10f) : null);

        activeReelSwipeClip = reelSwipeClip != null
            ? reelSwipeClip
            : (useProceduralPlaceholdersIfEmpty ? CreateSwipeWhooshClip() : null);

        activePhoneTapClip = phoneTapClip != null
            ? phoneTapClip
            : (useProceduralPlaceholdersIfEmpty ? CreateToneSequenceClip("Placeholder_Tap", new float[] { 720f }, 0.045f) : null);

        activeAdPopupClip = adPopupClip != null
            ? adPopupClip
            : (useProceduralPlaceholdersIfEmpty ? CreateToneSequenceClip("Placeholder_AdPopup", new float[] { 880f, 620f, 880f }, 0.07f) : null);
    }

    private void SetupAudioSources()
    {
        playerEngineSource = CreateChildAudioSource("PlayerEngine_Source", true);
        playerEngineSource.clip = activePlayerEngineClip;
        playerEngineSource.volume = playerEngineVolume;
        playerEngineSource.pitch = playerEnginePitch;

        heartbeatSource = CreateChildAudioSource("Heartbeat_Source", true);
        heartbeatSource.clip = activeHeartbeatClip;
        heartbeatSource.volume = 0f;

        sfxOneShotSource = CreateChildAudioSource("SFX_OneShot_Source", false);
    }

    private AudioSource CreateChildAudioSource(string sourceName, bool loop)
    {
        Transform existing = transform.Find(sourceName);
        GameObject child = existing != null ? existing.gameObject : new GameObject(sourceName);
        if (existing == null)
        {
            child.transform.SetParent(transform, false);
        }

        AudioSource src = child.GetComponent<AudioSource>();
        if (src == null) src = child.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = 0f;
        return src;
    }

    public void StartPlayerEngineLoop()
    {
        isGameplayStopped = false;
        if (playerEngineSource != null && activePlayerEngineClip != null && !playerEngineSource.isPlaying)
        {
            playerEngineSource.clip = activePlayerEngineClip;
            playerEngineSource.volume = playerEngineVolume;
            playerEngineSource.pitch = playerEnginePitch;
            playerEngineSource.Play();
        }

        if (heartbeatSource != null && activeHeartbeatClip != null && !heartbeatSource.isPlaying)
        {
            heartbeatSource.clip = activeHeartbeatClip;
            heartbeatSource.volume = 0f;
            heartbeatSource.Play();
        }
    }

    private void UpdateHeartbeatByConsciousness()
    {
        if (heartbeatSource == null || ConsciousnessManager.Instance == null) return;

        float c = ConsciousnessManager.Instance.CurrentConsciousness;
        // Detak jantung baru mulai terdengar saat masuk fase microsleep/kritis (di bawah 6 poin pada skala Max 20, atau <= 30% dari Max)
        float heartbeatThreshold = Mathf.Min(
            ConsciousnessManager.Instance.DrowsyThreshold * 0.65f,
            ConsciousnessManager.Instance.MaxConsciousness * 0.30f
        );
        heartbeatThreshold = Mathf.Max(2f, heartbeatThreshold);

        float targetVol = 0f;
        if (c < heartbeatThreshold && !ConsciousnessManager.Instance.IsGameEnded)
        {
            float factor = 1f - Mathf.Clamp01(c / heartbeatThreshold);
            targetVol = factor * heartbeatMaxVolume;
            heartbeatSource.pitch = Mathf.Lerp(0.95f, 1.30f, factor);
        }

        heartbeatSource.volume = Mathf.MoveTowards(heartbeatSource.volume, targetVol, Time.deltaTime * 1.5f);
    }

    public static void PlayLaneChangeSwerve()
    {
        EnsureExists().PlayOneShotInternal(Instance.activeLaneChangeClip, Instance.laneChangeVolume, Random.Range(0.96f, 1.04f));
    }

    /// <summary>
    /// Tabrakan biasa (masih ada sisa nyawa / rem mendadak) -> Crash.mp3
    /// </summary>
    public static void PlayNormalCrash()
    {
        EnsureExists().PlayOneShotInternal(Instance.activeNormalCrashClip, Instance.normalCrashVolume);
    }

    /// <summary>
    /// Tabrakan fatal saat nyawa habis -> CrashFinal.mp3 lalu disusul GameOver.mp3 saat panel tampil
    /// </summary>
    public static void PlayFinalCrashAndGameOver()
    {
        EnsureExists();
        StopAllLoopingSounds();
        Instance.PlayOneShotInternal(Instance.activeFinalCrashClip, Instance.finalCrashVolume);
        Instance.StartCoroutine(Instance.DelayedOneShotRoutine(Instance.activeGameOverPanelClip, 0.90f, Instance.gameOverAfterFinalCrashDelay));
    }

    /// <summary>
    /// Game Over karena tertidur di jalan (tanpa tabrakan) -> langsung GameOver.mp3 saat panel muncul
    /// </summary>
    public static void PlayGameOverPanelOnly()
    {
        EnsureExists();
        StopAllLoopingSounds();
        Instance.PlayOneShotInternal(Instance.activeGameOverPanelClip, 0.90f);
    }

    /// <summary>
    /// Menang sampai rumah -> double-car-honk.mp3 (2x klakson) lalu level-complete.mp3 saat panel muncul
    /// </summary>
    public static void PlayWinSequence()
    {
        EnsureExists();
        StopAllLoopingSounds();
        Instance.PlayOneShotInternal(Instance.activeWinHonkClip, 0.95f);
        Instance.StartCoroutine(Instance.DelayedOneShotRoutine(Instance.activeLevelCompleteClip, 0.90f, Instance.levelCompleteAfterHonkDelay));
    }

    public static void PlayPhoneUnlockSuccess()
    {
        EnsureExists().PlayOneShotInternal(Instance.activeUnlockSuccessClip, Instance.phoneSfxVolume);
    }

    public static void PlayPhoneUnlockFailed()
    {
        EnsureExists().PlayOneShotInternal(Instance.activeUnlockFailClip, Instance.phoneSfxVolume);
    }

    public static void PlayReelSwipe()
    {
        EnsureExists().PlayOneShotInternal(Instance.activeReelSwipeClip, Instance.phoneSfxVolume);
    }

    public static void PlayPhoneTap()
    {
        EnsureExists().PlayOneShotInternal(Instance.activePhoneTapClip, Instance.phoneSfxVolume * 0.85f, Random.Range(0.96f, 1.05f));
    }

    public static void PlayAdPopup()
    {
        EnsureExists().PlayOneShotInternal(Instance.activeAdPopupClip, Instance.phoneSfxVolume);
    }

    private IEnumerator DelayedOneShotRoutine(AudioClip clip, float volume, float delaySeconds)
    {
        if (delaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
        }
        PlayOneShotInternal(clip, volume);
    }

    public static void StopAllLoopingSounds()
    {
        if (Instance == null) return;
        Instance.isGameplayStopped = true;

        if (Instance.playerEngineSource != null) Instance.playerEngineSource.Stop();
        if (Instance.heartbeatSource != null) Instance.heartbeatSource.Stop();

        for (int i = Instance.activeNpcSources.Count - 1; i >= 0; i--)
        {
            if (Instance.activeNpcSources[i] != null)
            {
                Instance.activeNpcSources[i].Stop();
            }
        }
        Instance.activeNpcSources.Clear();
    }

    /// <summary>
    /// Memasang AudioSource 3D Spatial (NPCEngine.mp3 atau Special Troll Audio seperti tereret_Copy.mp3)
    /// pada mobil NPC yang di-spawn sehingga semakin dekat ke mobil player, suaranya semakin terdengar jelas dari arah jalurnya.
    /// </summary>
    public static void AttachNPCEngineAudio(GameObject npcObject, AudioClip customOverrideClip = null)
    {
        if (npcObject == null) return;
        EnsureExists();

        bool useTrollAudio = false;
        if (customOverrideClip == null && Instance.activeSpecialTrollNpcClip != null)
        {
            if (!string.IsNullOrWhiteSpace(Instance.specialTrollNpcNameKeyword) &&
                npcObject.name.IndexOf(Instance.specialTrollNpcNameKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                useTrollAudio = true;
            }
            else if (Instance.randomTrollNpcChancePercent > 0 && Random.Range(0, 100) < Instance.randomTrollNpcChancePercent)
            {
                useTrollAudio = true;
            }
        }

        AudioClip chosenClip = customOverrideClip != null
            ? customOverrideClip
            : (useTrollAudio ? Instance.activeSpecialTrollNpcClip : Instance.activeNpcEngineClip);

        if (chosenClip == null) return;

        AudioSource npcSrc = npcObject.GetComponent<AudioSource>();
        if (npcSrc == null)
        {
            npcSrc = npcObject.AddComponent<AudioSource>();
        }

        npcSrc.clip = chosenClip;
        npcSrc.loop = true;
        npcSrc.playOnAwake = false;
        npcSrc.spatialBlend = 1.0f; // 100% 3D Spatial Audio
        npcSrc.rolloffMode = AudioRolloffMode.Custom;
        npcSrc.minDistance = Mathf.Max(1f, Instance.npcAudioMinDistance);
        npcSrc.maxDistance = Mathf.Max(npcSrc.minDistance + 5f, Instance.npcAudioMaxDistance);

        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.25f, 0.55f),
            new Keyframe(0.6f, 0.16f),
            new Keyframe(1f, 0f)
        );
        npcSrc.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);

        bool isSpecialClip = customOverrideClip != null || useTrollAudio;
        npcSrc.volume = isSpecialClip ? Instance.specialTrollNpcMaxVolume : Instance.npcEngineMaxVolume;
        npcSrc.pitch = isSpecialClip ? 1.0f : Random.Range(0.92f, 1.08f);
        npcSrc.dopplerLevel = isSpecialClip ? 0.15f : Instance.npcDopplerLevel;
        npcSrc.Play();

        for (int i = Instance.activeNpcSources.Count - 1; i >= 0; i--)
        {
            if (Instance.activeNpcSources[i] == null)
                Instance.activeNpcSources.RemoveAt(i);
        }
        Instance.activeNpcSources.Add(npcSrc);
    }

    private void PlayOneShotInternal(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null || sfxOneShotSource == null) return;
        sfxOneShotSource.pitch = pitch;
        sfxOneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    // =========================================================================
    // PROCEDURAL FALLBACK SYNTHESIZER (Hanya dipakai jika ada slot yang kosong)
    // =========================================================================

    private static AudioClip CreateEngineLoopClip(string name, float baseFreq, float durationSec)
    {
        int sampleRate = 22050;
        int samplesPerPeriod = Mathf.Max(1, Mathf.RoundToInt(sampleRate / baseFreq));
        int totalPeriods = Mathf.Max(10, Mathf.RoundToInt((sampleRate * durationSec) / samplesPerPeriod));
        int sampleCount = samplesPerPeriod * totalPeriods;

        float[] data = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float fundamental = Mathf.Sin(2f * Mathf.PI * baseFreq * t);
            float harmonic2 = Mathf.Sin(2f * Mathf.PI * (baseFreq * 2f) * t) * 0.45f;
            float harmonic3 = Mathf.Sin(2f * Mathf.PI * (baseFreq * 3f) * t) * 0.20f;
            float rumbleMod = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * (baseFreq * 0.25f) * t);

            float wave = (fundamental + harmonic2 + harmonic3) * 0.35f * rumbleMod;
            data[i] = Mathf.Clamp(wave, -0.8f, 0.8f);
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip CreateSwerveWhooshClip()
    {
        int sampleRate = 22050;
        float duration = 0.24f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[sampleCount];

        System.Random rng = new System.Random(1337);
        float lp = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float norm = (float)i / sampleCount;
            float env = Mathf.Sin(norm * Mathf.PI);
            float freq = Mathf.Lerp(190f, 115f, norm);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * ((float)i / sampleRate)) * 0.35f;

            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp = Mathf.Lerp(lp, white, 0.18f);

            data[i] = (tone + lp * 0.55f) * env * 0.7f;
        }

        AudioClip clip = AudioClip.Create("Placeholder_LaneSwerve", sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip CreateCrashImpactClip()
    {
        int sampleRate = 22050;
        float duration = 0.55f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[sampleCount];

        System.Random rng = new System.Random(4242);
        float lp = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float norm = (float)i / sampleCount;

            float impactEnv = Mathf.Exp(-norm * 7.5f);
            float screechEnv = Mathf.Clamp01(1f - norm * 1.4f);

            float thud = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(140f, 38f, norm) * t) * impactEnv * 0.75f;
            float screech = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(1450f, 980f, norm) * t) * screechEnv * 0.18f;

            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp = Mathf.Lerp(lp, white, 0.35f);
            float crunch = lp * impactEnv * 0.65f;

            data[i] = Mathf.Clamp(thud + screech + crunch, -0.95f, 0.95f);
        }

        AudioClip clip = AudioClip.Create("Placeholder_CarCrash", sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip CreateHeartbeatLoopClip()
    {
        int sampleRate = 22050;
        float duration = 0.95f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;

            if (t >= 0.05f && t <= 0.19f)
            {
                float local = (t - 0.05f) / 0.14f;
                float env = Mathf.Sin(local * Mathf.PI);
                sample += Mathf.Sin(2f * Mathf.PI * 58f * t) * env * 0.8f;
            }
            else if (t >= 0.24f && t <= 0.37f)
            {
                float local = (t - 0.24f) / 0.13f;
                float env = Mathf.Sin(local * Mathf.PI);
                sample += Mathf.Sin(2f * Mathf.PI * 48f * t) * env * 0.6f;
            }

            data[i] = sample;
        }

        AudioClip clip = AudioClip.Create("Placeholder_Heartbeat", sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip CreateSwipeWhooshClip()
    {
        int sampleRate = 22050;
        float duration = 0.14f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float norm = (float)i / sampleCount;
            float t = (float)i / sampleRate;
            float env = Mathf.Sin(norm * Mathf.PI);
            float freq = Mathf.Lerp(280f, 620f, norm);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.45f;
        }

        AudioClip clip = AudioClip.Create("Placeholder_ReelSwipe", sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip CreateToneSequenceClip(string name, float[] frequencies, float noteDuration)
    {
        int sampleRate = 22050;
        int samplesPerNote = Mathf.Max(1, Mathf.RoundToInt(sampleRate * noteDuration));
        int totalSamples = samplesPerNote * frequencies.Length;
        float[] data = new float[totalSamples];

        for (int n = 0; n < frequencies.Length; n++)
        {
            float freq = frequencies[n];
            int start = n * samplesPerNote;
            for (int i = 0; i < samplesPerNote; i++)
            {
                float norm = (float)i / samplesPerNote;
                float env = Mathf.Sin(norm * Mathf.PI);
                float t = (float)i / sampleRate;
                data[start + i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.5f;
            }
        }

        AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
