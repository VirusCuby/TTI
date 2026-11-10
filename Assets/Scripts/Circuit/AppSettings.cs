using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ajustes y estado compartido entre el menú y el simulador.
///   - Sonido y etiquetas flotantes: se guardan en el dispositivo (PlayerPrefs).
///   - Modo libre y reto elegido: solo viven durante la sesión; el menú los fija antes de
///     cargar la escena del simulador y el simulador los consume al arrancar.
/// Si se abre el simulador directamente (sin pasar por el menú) los valores por defecto
/// reproducen el comportamiento de siempre: sonido y etiquetas activos, retos visibles.
/// </summary>
public static class AppSettings
{
    /// <summary>Nombre de la escena del menú principal.</summary>
    public const string MenuScene = "Menu";

    /// <summary>true si la escena del menú está en Build Settings (si no, no hay a dónde "salir").</summary>
    public static bool MenuAvailable => Application.CanStreamedLevelBeLoaded(MenuScene);

    public static void GoToMenu()
    {
        if (MenuAvailable) SceneManager.LoadScene(MenuScene);
    }

    private const string SoundKey = "ajuste_sonido";
    private const string LabelsKey = "ajuste_etiquetas";

    private static int sound = -1;   // -1 = todavía no leído
    private static int labels = -1;

    public static bool SoundEnabled
    {
        get
        {
            if (sound < 0) sound = PlayerPrefs.GetInt(SoundKey, 1);
            return sound == 1;
        }
        set
        {
            sound = value ? 1 : 0;
            PlayerPrefs.SetInt(SoundKey, sound);
            PlayerPrefs.Save();
        }
    }

    public static bool LabelsEnabled
    {
        get
        {
            if (labels < 0) labels = PlayerPrefs.GetInt(LabelsKey, 1);
            return labels == 1;
        }
        set
        {
            labels = value ? 1 : 0;
            PlayerPrefs.SetInt(LabelsKey, labels);
            PlayerPrefs.Save();
        }
    }

    /// <summary>true = el simulador arranca sin panel de retos (modo libre).</summary>
    public static bool FreeMode;

    /// <summary>Índice del reto que el menú quiere abrir (-1 = el primero pendiente).</summary>
    public static int SelectedChallenge = -1;
}
