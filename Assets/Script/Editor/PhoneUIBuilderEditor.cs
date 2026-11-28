#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor utility untuk menghubungkan referensi komponen UI secara otomatis
/// tanpa merusak susunan tata letak (layout) yang sudah diatur manual oleh user.
/// </summary>
[InitializeOnLoad]
public static class PhoneUIBuilderEditor
{
    static PhoneUIBuilderEditor()
    {
        EditorApplication.delayCall += AutoBindOrBuildIfEmpty;
    }

    private static void AutoBindOrBuildIfEmpty()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        PhoneFeedScrollController phoneScreen = Object.FindFirstObjectByType<PhoneFeedScrollController>();
        if (phoneScreen == null) return;

        // Jika benar-benar masih kosong belum ada child sama sekali, baru buatkan pertama kali
        if (phoneScreen.transform.childCount == 0)
        {
            Undo.RegisterFullObjectHierarchyUndo(phoneScreen.gameObject, "Build Phone Minigames UI");
            phoneScreen.EnsureMinigameHierarchyBuilt();
            EditorUtility.SetDirty(phoneScreen);
            EditorSceneManager.MarkSceneDirty(phoneScreen.gameObject.scene);
            Debug.Log("<color=lime>[Gimersia Phone Builder]</color> UI Placeholder 4 Minigame berhasil dibuat di dalam ScreenBackground!");
        }
        else
        {
            // Jika sudah ada (atau sudah diatur user), cukup hubungkan slot referensi yang belum terisi tanpa mengubah posisi!
            phoneScreen.AutoBindReferences();
            phoneScreen.EnsureMinigameHierarchyBuilt();
            EditorUtility.SetDirty(phoneScreen);
        }

        // Pastikan semua PNG di folder Assets/Sprite/ dan Assets/Asset fix/Button dan UI/ berformat Sprite (2D and UI)
        EnsureAllUISpriteAssetsConfigured();

        // Pastikan GameplayHUD_Canvas & ConsciousnessManager juga ada di Scene & memakai sprite UI terbaru
        EnsureConsciousnessHUDExists();

        // Jika ScreenBackground sudah ada, pastikan sprite Minigame (Kucing, Kuis MTK BG, Mangkok Bakso) terpasang otomatis
        if (phoneScreen != null)
        {
            phoneScreen.AutoBindReferences();
            EditorUtility.SetDirty(phoneScreen);
        }

        // Pastikan GameplayAudioManager juga ada di Scene agar user bisa langsung drag & drop AudioClip di Inspector
        EnsureAudioManagerExists();

        // Pastikan CameraPivot pas di titik pusat mata Main Camera (Scale 1,1,1)
        EnsureCameraPivotAligned();

        // Terapkan font Dimbo Regular ke seluruh komponen Text & TMP_Text yang ada di Scene aktif
        ApplyProvidedFontToAllTextsInActiveScene(logResult: false);
    }

    [MenuItem("Tools/Gimersia/Pastikan Semua Gambar UI Berformat Sprite (2D and UI)")]
    public static void EnsureAllUISpriteAssetsConfigured()
    {
        string[] uiSpriteFolders = new string[]
        {
            "Assets/Sprite"
        };

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", uiSpriteFolders);
        int converted = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path)) continue;

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode == SpriteImportMode.None))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                converted++;
            }
        }

        if (converted > 0)
        {
            Debug.Log($"<color=lime>[Gimersia UI Sprites]</color> Mengubah {converted} file PNG di folder Sprite menjadi Sprite (2D and UI)!");
        }
    }

    [MenuItem("Tools/Gimersia/Terapkan Font Dimbo ke Semua Text di Scene")]
    public static void ApplyProvidedFontMenuCommand()
    {
        ApplyProvidedFontToAllTextsInActiveScene(logResult: true);
    }

    private static void ApplyProvidedFontToAllTextsInActiveScene(bool logResult)
    {
        int changed = PhoneUIHelper.ApplyProvidedFontToAllSceneTexts();
        if (changed > 0)
        {
            EditorSceneManager.MarkAllScenesDirty();
            if (logResult)
            {
                Debug.Log($"<color=lime>[Gimersia Font]</color> Berhasil menerapkan font Dimbo Regular ke {changed} objek Text/TMP_Text di Scene!");
            }
        }
        else if (logResult)
        {
            Debug.Log("<color=cyan>[Gimersia Font]</color> Seluruh objek Text/TMP_Text di Scene sudah menggunakan font Dimbo Regular!");
        }
    }

    [MenuItem("Tools/Gimersia/Buat atau Pilih GameplayAudioManager (Slot Audio & SFX)")]
    public static void EnsureAudioManagerExists()
    {
        GameplayAudioManager audioMgr = Object.FindFirstObjectByType<GameplayAudioManager>();
        if (audioMgr == null)
        {
            GameObject audioObj = new GameObject("GameplayAudioManager");
            Undo.RegisterCreatedObjectUndo(audioObj, "Create GameplayAudioManager");
            audioMgr = audioObj.AddComponent<GameplayAudioManager>();
            audioMgr.AutoBindAudioClipsInEditor();
            EditorUtility.SetDirty(audioObj);
            EditorSceneManager.MarkSceneDirty(audioObj.scene);
            Debug.Log("<color=lime>[Gimersia Audio]</color> GameplayAudioManager berhasil dibuat & ke-14 file SFX di Assets/Audio/SFX otomatis terpasang!");
        }
        else
        {
            audioMgr.AutoBindAudioClipsInEditor();
            EditorUtility.SetDirty(audioMgr);
        }
    }

    [MenuItem("Tools/Gimersia/Align Camera Pivot to Eye (Pusatkan Pivot Kamera)")]
    public static void EnsureCameraPivotAligned()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Transform camTf = cam.transform;
        Transform pivotTf = camTf.parent;

        if (pivotTf != null && pivotTf.name.Contains("Pivot"))
        {
            if (camTf.localPosition.sqrMagnitude > 0.001f || (pivotTf.localScale - Vector3.one).sqrMagnitude > 0.001f)
            {
                Vector3 worldPos = camTf.position;
                Quaternion worldRot = camTf.rotation;

                Undo.RecordObject(pivotTf, "Align Camera Pivot to Eye");
                Undo.RecordObject(camTf, "Align Camera to Pivot Center");

                camTf.SetParent(null, true);

                pivotTf.position = worldPos;
                pivotTf.rotation = worldRot;
                pivotTf.localScale = Vector3.one;

                camTf.SetParent(pivotTf, true);
                camTf.localPosition = Vector3.zero;
                camTf.localRotation = Quaternion.identity;
                camTf.localScale = Vector3.one;

                EditorUtility.SetDirty(pivotTf);
                EditorUtility.SetDirty(camTf);
                EditorSceneManager.MarkSceneDirty(pivotTf.gameObject.scene);

                Debug.Log("<color=lime>[Gimersia Camera]</color> CameraPivot berhasil di-align tepat ke mata Main Camera (Scale 1,1,1). Memutar pivot sekarang akan berputar di tempat tanpa mengorbit!");
            }
        }
    }

    public static void EnsureConsciousnessHUDExists()
    {
        ConsciousnessManager manager = Object.FindFirstObjectByType<ConsciousnessManager>();
        if (manager == null)
        {
            GameObject hudObj = new GameObject("GameplayHUD_Canvas");
            Undo.RegisterCreatedObjectUndo(hudObj, "Create GameplayHUD_Canvas");
            manager = hudObj.AddComponent<ConsciousnessManager>();
            manager.EnsureHUDCreated();
            EditorUtility.SetDirty(hudObj);
            EditorSceneManager.MarkSceneDirty(hudObj.scene);
            Debug.Log("<color=lime>[Gimersia HUD Builder]</color> GameplayHUD_Canvas (Bar Kesadaran, Nyawa, Jarak, Kelopak Mata) berhasil dibuat di Hierarchy!");
        }
        else
        {
            manager.EnsureHUDCreated();
            EditorUtility.SetDirty(manager);
        }
    }

    [MenuItem("Tools/Gimersia/Build atau Rebuild Gameplay HUD (Bar Kesadaran)")]
    public static void ManualRebuildHUD()
    {
        ConsciousnessManager existing = Object.FindFirstObjectByType<ConsciousnessManager>();
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        EnsureConsciousnessHUDExists();
        ConsciousnessManager created = Object.FindFirstObjectByType<ConsciousnessManager>();
        if (created != null)
        {
            Selection.activeGameObject = created.gameObject;
        }
    }

    [MenuItem("Tools/Gimersia/Rebuild & Rapikan Posisi Semua UI Minigame")]
    public static void ManualRebuildPhoneUI()
    {
        PhoneFeedScrollController phoneScreen = Object.FindFirstObjectByType<PhoneFeedScrollController>();
        if (phoneScreen == null)
        {
            Debug.LogWarning("[Gimersia Phone Builder] Tidak ditemukan objek dengan komponen PhoneFeedScrollController di scene aktif.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(phoneScreen.gameObject, "Rebuild Phone Minigames UI");

        for (int i = phoneScreen.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(phoneScreen.transform.GetChild(i).gameObject);
        }

        phoneScreen.EnsureMinigameHierarchyBuilt();
        EditorUtility.SetDirty(phoneScreen);
        EditorSceneManager.MarkSceneDirty(phoneScreen.gameObject.scene);

        Selection.activeGameObject = phoneScreen.gameObject;
        Debug.Log("<color=cyan>[Gimersia Phone Builder]</color> UI Layar HP & 4 Minigame berhasil disusun ulang ke posisi masing-masing!");
    }
}
#endif
