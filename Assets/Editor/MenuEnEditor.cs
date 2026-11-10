using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Hace que la aplicación SIEMPRE arranque por el menú:
///   - En el Editor: el botón Play empieza en la escena Menu aunque tengas abierta TestAR
///     (EditorSceneManager.playModeStartScene). Se puede apagar en Tools > Circuito > Play empieza en el menú,
///     por ejemplo para probar el simulador directamente sin pasar por el menú.
///   - En el APK: deja Menu como primera escena de Build Settings (y TestAR después).
/// Se aplica sola cada vez que Unity abre el proyecto o recompila los scripts.
/// </summary>
[InitializeOnLoad]
public static class MenuEnEditor
{
    private const string RutaMenu = "Assets/Scenes/Menu.unity";
    private const string PrefPlayDesdeMenu = "TTI_PlayDesdeMenu";
    private const string ItemMenu = "Tools/Circuito/Play empieza en el menú";

    static MenuEnEditor()
    {
        EditorApplication.delayCall += Aplicar;
    }

    private static void Aplicar()
    {
        var menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaMenu);
        if (menu == null)
        {
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        AsegurarBuildSettings();
        EditorSceneManager.playModeStartScene = EditorPrefs.GetBool(PrefPlayDesdeMenu, true) ? menu : null;
    }

    /// <summary>Menu como escena 0; el resto de escenas (TestAR) se conservan detrás.</summary>
    private static void AsegurarBuildSettings()
    {
        var actuales = EditorBuildSettings.scenes;
        if (actuales.Length > 0 && actuales[0].path == RutaMenu && actuales[0].enabled) return;

        var lista = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(RutaMenu, true) };
        foreach (var s in actuales)
            if (s.path != RutaMenu) lista.Add(s);
        EditorBuildSettings.scenes = lista.ToArray();
        Debug.Log("[MenuEnEditor] Build Settings: Menu quedó como primera escena.");
    }

    [MenuItem(ItemMenu)]
    private static void Alternar()
    {
        EditorPrefs.SetBool(PrefPlayDesdeMenu, !EditorPrefs.GetBool(PrefPlayDesdeMenu, true));
        Aplicar();
    }

    [MenuItem(ItemMenu, true)]
    private static bool AlternarValidar()
    {
        Menu.SetChecked(ItemMenu, EditorPrefs.GetBool(PrefPlayDesdeMenu, true));
        return true;
    }
}
