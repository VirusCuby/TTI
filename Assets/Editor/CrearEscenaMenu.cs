using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Herramienta de Editor: crea la escena del menú principal (Assets/Scenes/Menu.unity) y la deja como
/// PRIMERA escena de Build Settings, con el simulador (TestAR) como segunda.
///
/// La escena solo lleva una cámara, un EventSystem y el componente MenuController: toda la interfaz
/// se construye por código al arrancar. No toca la escena del simulador.
/// </summary>
public static class CrearEscenaMenu
{
    private const string RutaMenu = "Assets/Scenes/Menu.unity";

    [MenuItem("Tools/Circuito/Crear escena de menú y configurar Build")]
    private static void Crear()
    {
        if (File.Exists(RutaMenu) &&
            !EditorUtility.DisplayDialog("Escena de menú", "Ya existe " + RutaMenu + ". ¿Quieres volver a crearla?", "Sí, recrear", "Cancelar"))
            return;

        // Guardar lo que haya abierto (por si TestAR tiene cambios sin guardar)
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string escenaAnterior = EditorSceneManager.GetActiveScene().path;

        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Cámara con fondo liso (la interfaz es un Canvas en pantalla completa)
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.04f, 0.10f);
        cam.cullingMask = 0;
        camGo.AddComponent<AudioListener>();

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();

        new GameObject("Menu").AddComponent<MenuController>();

        EditorSceneManager.SaveScene(escena, RutaMenu);

        string detalle = ConfigurarBuild();

        // Volver a la escena en la que se estaba trabajando
        if (!string.IsNullOrEmpty(escenaAnterior) && escenaAnterior != RutaMenu)
            EditorSceneManager.OpenScene(escenaAnterior);

        Debug.Log("[CrearEscenaMenu]\n" + detalle);
        EditorUtility.DisplayDialog("Escena de menú creada",
            "Se creó " + RutaMenu + ".\n\n" + detalle +
            "\n\nPara probarlo: abre Assets/Scenes/Menu.unity y pulsa Play.", "OK");
    }

    /// <summary>Menu primero, después el resto de escenas que ya había (TestAR).</summary>
    private static string ConfigurarBuild()
    {
        var lista = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(RutaMenu, true) };
        foreach (var s in EditorBuildSettings.scenes)
            if (s.path != RutaMenu) lista.Add(s);
        EditorBuildSettings.scenes = lista.ToArray();

        var sb = new System.Text.StringBuilder("Build Settings (en este orden):\n");
        for (int i = 0; i < lista.Count; i++)
            sb.AppendLine("  " + i + ". " + lista[i].path + (lista[i].enabled ? "" : "  (desactivada)"));
        return sb.ToString();
    }
}
