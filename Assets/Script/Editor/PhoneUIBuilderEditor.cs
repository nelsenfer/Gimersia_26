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

        PhoneSwipeColorTest phoneScreen = Object.FindFirstObjectByType<PhoneSwipeColorTest>();
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
    }

    [MenuItem("Tools/Gimersia/Rebuild & Rapikan Posisi Semua UI Minigame")]
    public static void ManualRebuildPhoneUI()
    {
        PhoneSwipeColorTest phoneScreen = Object.FindFirstObjectByType<PhoneSwipeColorTest>();
        if (phoneScreen == null)
        {
            Debug.LogWarning("[Gimersia Phone Builder] Tidak ditemukan objek dengan komponen PhoneSwipeColorTest di scene aktif.");
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
