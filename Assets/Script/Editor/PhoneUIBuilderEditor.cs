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

        // Pastikan GameplayHUD_Canvas & ConsciousnessManager juga ada di Scene
        EnsureConsciousnessHUDExists();

        // Pastikan CameraPivot pas di titik pusat mata Main Camera (Scale 1,1,1)
        EnsureCameraPivotAligned();
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
