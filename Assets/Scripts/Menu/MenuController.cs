using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menú principal de la aplicación (escena "Menu"). Toda la interfaz se crea por código, así que
/// la escena solo necesita este componente (la crea la herramienta Tools > Circuito > Crear escena de menú).
///
/// Pantallas: Principal, Retos, Cómo se usa, Ajustes y Acerca de.
/// Se adapta solo a vertical y horizontal: en vertical el título va arriba y los botones abajo;
/// en horizontal el título queda a la izquierda y los botones a la derecha. Respeta el área segura
/// (muescas y barras del sistema) y reacciona al giro del celular en vivo.
/// El botón Atrás de Android (Escape) vuelve a la pantalla anterior.
/// </summary>
public class MenuController : MonoBehaviour
{
    // ===== Datos del proyecto (editar aquí) ==============================================
    private const string AppTitle = "Simulador AR de Circuitos Eléctricos";
    private const string AppTagline = "Aprende electrónica armando circuitos con tarjetas y realidad aumentada";
    private const string University = "Universidad Nacional de Trujillo";
    private const string Course = "X Ciclo · 2026-II · TTI";
    private static readonly string[] Members =
    {
        "LIÑER SAGASTEGUI JEAN PIERRE CAMILO",
        "LOPEZ LOZANO SERGE JIMMY II",
        "MORY CAMONES KEVIN YARED",
        "OLIVARES SANCHEZ SEBASTIAN MARTIN",
        "RUIZ CHIMBOR EBERT ALEJANDRO",
    };
    private const string SimulatorScene = "TestAR";
    // =====================================================================================

    private enum PageId { Main, Challenges, Help, Settings, About }

    private class SubScreen
    {
        public RectTransform root;
        public RectTransform body;
    }

    private Canvas canvas;
    private CanvasScaler scaler;
    private RectTransform safeRoot;
    private CanvasGroup safeGroup;

    private RectTransform mainScreen, brand, buttonsArea, buttonsScroll, buttonsContent, logo;
    private TextMeshProUGUI titleText, taglineText, progressText;
    private Image ledGlow;

    private readonly Dictionary<PageId, RectTransform> screens = new Dictionary<PageId, RectTransform>();
    private readonly List<SubScreen> subScreens = new List<SubScreen>();
    private PageId current = PageId.Main;

    // Retos
    private RectTransform challengesContent;
    private TextMeshProUGUI challengesSummary;
    private RectTransform progressFill;
    private ScrollRect challengesScroll;

    // Ayuda
    private TextMeshProUGUI helpTitle, helpBody, helpCounter;
    private UiKit.ButtonParts helpPrev, helpNext;
    private ScrollRect helpScroll;
    private int helpIndex;

    // Ajustes
    private UiKit.ButtonParts soundToggle, labelsToggle, resetButton;
    private bool resetArmed;

    // Avisos y carga
    private GameObject toast;
    private TextMeshProUGUI toastText;
    private GameObject loading;
    private RectTransform spinner;
    private bool isLoading;
    private GameObject confirmDialog;   // aviso de confirmación abierto (si hay)

    private int lastW, lastH, layoutFrames;
    private bool layoutReady;   // ya se acomodó al menos una vez con el tamaño real

    // ---------------------------------------------------------------------------------------
    // Ciclo de vida
    // ---------------------------------------------------------------------------------------

    private void Awake()
    {
        // Este menú no usa la cámara de AR: la escena del simulador es la que la enciende.
        AppSettings.FreeMode = false;
        AppSettings.SelectedChallenge = -1;
        Screen.sleepTimeout = SleepTimeout.SystemSetting;

        EnsureCamera();
        UiKit.EnsureEventSystem();
        BuildUi();

        lastW = Screen.width;
        lastH = Screen.height;
        layoutFrames = 3; // el Canvas Scaler tarda un par de cuadros en conocer su tamaño
        Show(PageId.Main);
    }

    private void Update()
    {
        if (Screen.width != lastW || Screen.height != lastH)
        {
            lastW = Screen.width;
            lastH = Screen.height;
            layoutFrames = 3;
        }

        // Brillo suave del "LED" del logo
        if (ledGlow != null)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f);
            var c = UiKit.Accent;
            c.a = Mathf.Lerp(0.35f, 0.95f, k);
            ledGlow.color = c;
        }

        if (spinner != null && loading != null && loading.activeSelf)
            spinner.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 300f);

        if (Input.GetKeyDown(KeyCode.Escape)) Back();
    }

    private void LateUpdate()
    {
        if (layoutFrames <= 0) return;

        // Mientras el Canvas Scaler se actualiza se oculta la interfaz para no mostrar un cuadro mal acomodado.
        safeGroup.alpha = 0f;
        layoutFrames--;
        if (layoutFrames == 0)
        {
            Relayout();
            layoutReady = true;
            safeGroup.alpha = 1f;
        }
    }

    private static void EnsureCamera()
    {
        if (Camera.main != null || FindAnyObjectByType<Camera>() != null) return;
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = UiKit.BgBottom;
    }

    // ---------------------------------------------------------------------------------------
    // Construcción
    // ---------------------------------------------------------------------------------------

    private void BuildUi()
    {
        canvas = UiKit.NewCanvas("MenuCanvas", transform, 0, out scaler);

        // Fondo degradado a pantalla completa (ignora el área segura)
        var bg = UiKit.NewPanel("Fondo", canvas.transform, Color.white, false);
        bg.sprite = UiKit.Gradient;
        bg.raycastTarget = false;
        UiKit.Stretch(bg.rectTransform);

        safeRoot = UiKit.NewRect("AreaSegura", canvas.transform);
        UiKit.Stretch(safeRoot);
        safeGroup = safeRoot.gameObject.AddComponent<CanvasGroup>();

        BuildMain();
        BuildChallengesScreen();
        BuildHelpScreen();
        BuildSettingsScreen();
        BuildAboutScreen();
        BuildToast();
        BuildLoading();
    }

    // ---- Principal ------------------------------------------------------------------------

    private void BuildMain()
    {
        mainScreen = UiKit.NewRect("Principal", safeRoot);
        UiKit.Stretch(mainScreen);
        screens[PageId.Main] = mainScreen;

        // Marca (logo + título)
        brand = UiKit.NewRect("Marca", mainScreen);
        BuildLogo();

        titleText = UiKit.NewText("Titulo", brand, AppTitle, 84f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        titleText.enableAutoSizing = true;
        titleText.fontSizeMax = 84f;
        titleText.fontSizeMin = 40f;
        UiKit.Anchor(titleText.rectTransform, new Vector2(0f, 0.20f), new Vector2(1f, 0.46f), 40f, 0f, 40f, 0f);

        taglineText = UiKit.NewText("Subtitulo", brand, AppTagline, 38f, FontStyles.Normal, UiKit.TextSoft, TextAlignmentOptions.Top);
        taglineText.enableAutoSizing = true;
        taglineText.fontSizeMax = 38f;
        taglineText.fontSizeMin = 24f;
        UiKit.Anchor(taglineText.rectTransform, new Vector2(0f, 0.06f), new Vector2(1f, 0.20f), 60f, 0f, 60f, 0f);

        progressText = UiKit.NewText("Progreso", brand, "", 34f, FontStyles.Bold, UiKit.Accent, TextAlignmentOptions.Center);
        UiKit.Anchor(progressText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.06f), 40f, 0f, 40f, 0f);

        // Botones
        buttonsArea = UiKit.NewRect("Botones", mainScreen);
        buttonsContent = UiKit.NewScroll("Lista", buttonsArea, 18f, out ScrollRect sr);
        buttonsScroll = (RectTransform)buttonsContent.parent;

        AddMainButton("Modo libre", "Experimenta sin misiones ni puntaje", UiKit.Green, () => StartSimulator(true, -1));
        AddMainButton("Retos", "11 misiones guiadas con puntaje", UiKit.Blue, () => Show(PageId.Challenges));
        AddMainButton("Cómo se usa", "Tarjetas, cables y avisos", UiKit.Blue, () => Show(PageId.Help));
        AddMainButton("Ajustes", "Sonido y etiquetas", UiKit.Gray, () => Show(PageId.Settings));
        AddMainButton("Acerca de", "Créditos del proyecto", UiKit.Gray, () => Show(PageId.About));
        AddMainButton("Salir", null, UiKit.Red, QuitApp);
    }

    private void AddMainButton(string label, string subtitle, Color color, UnityEngine.Events.UnityAction onClick)
    {
        string text = string.IsNullOrEmpty(subtitle) ? label : label + "\n<size=58%><b><color=#D8E4FF>" + subtitle + "</color></b></size>";
        var b = UiKit.NewButton("Boton_" + label, buttonsContent, text, color, onClick, 132f, 46f);
        b.label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    /// <summary>Logo: un lazo de circuito con los cuatro terminales de la app y un LED que late en el centro.</summary>
    private void BuildLogo()
    {
        logo = UiKit.NewRect("Logo", brand);

        var ring = UiKit.NewPanel("Anillo", logo, new Color(0.35f, 0.55f, 1f, 0.95f));
        UiKit.Stretch(ring.rectTransform, 12f, 12f, 12f, 12f);
        var inner = UiKit.NewPanel("Interior", logo, new Color(0.04f, 0.09f, 0.20f, 1f));
        UiKit.Stretch(inner.rectTransform, 26f, 26f, 26f, 26f);

        AddLogoNode("Pos", new Vector2(0.10f, 0.90f), new Color(1f, 0.35f, 0.35f));   // rojo  +
        AddLogoNode("Neg", new Vector2(0.90f, 0.90f), new Color(0.42f, 0.55f, 1f));   // azul  -
        AddLogoNode("B", new Vector2(0.90f, 0.10f), new Color(0.29f, 0.89f, 0.89f));  // cian  B
        AddLogoNode("A", new Vector2(0.10f, 0.10f), new Color(1f, 0.85f, 0.29f));     // amarillo A

        // LED central
        var led = UiKit.NewPanel("Led", logo, UiKit.Accent, false);
        led.sprite = UiKit.Circle;
        led.raycastTarget = false;
        led.rectTransform.anchorMin = led.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        led.rectTransform.sizeDelta = new Vector2(150f, 150f);
        ledGlow = led;
    }

    private void AddLogoNode(string name, Vector2 anchor, Color color)
    {
        var n = UiKit.NewPanel("Nodo_" + name, logo, color, false);
        n.sprite = UiKit.Circle;
        n.raycastTarget = false;
        n.rectTransform.anchorMin = n.rectTransform.anchorMax = anchor;
        n.rectTransform.sizeDelta = new Vector2(64f, 64f);
    }

    // ---- Subpantallas (marco común) ---------------------------------------------------------

    private SubScreen NewSub(PageId id, string title)
    {
        var s = new SubScreen { root = UiKit.NewRect(id.ToString(), safeRoot) };
        UiKit.Stretch(s.root);
        screens[id] = s.root;

        // Barra superior: botón Volver + título
        var bar = UiKit.NewRect("Barra", s.root);
        bar.anchorMin = new Vector2(0f, 1f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.anchoredPosition = new Vector2(0f, -24f);
        bar.sizeDelta = new Vector2(-48f, 110f);

        var back = UiKit.NewButton("Volver", bar, "< Volver", UiKit.Gray, () => Show(PageId.Main), 100f, 36f);
        var brt = (RectTransform)back.button.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(0f, 0.5f);
        brt.pivot = new Vector2(0f, 0.5f);
        brt.sizeDelta = new Vector2(250f, 96f);
        brt.anchoredPosition = Vector2.zero;

        var t = UiKit.NewText("Titulo", bar, title, 54f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        t.enableAutoSizing = true;
        t.fontSizeMax = 54f;
        t.fontSizeMin = 30f;
        UiKit.Stretch(t.rectTransform, 270f, 0f, 270f, 0f);

        s.body = UiKit.NewRect("Cuerpo", s.root);
        subScreens.Add(s);
        return s;
    }

    // ---- Retos --------------------------------------------------------------------------------

    private void BuildChallengesScreen()
    {
        var s = NewSub(PageId.Challenges, "Retos");

        // Resumen con barra de progreso
        var summary = UiKit.NewPanel("Resumen", s.body, UiKit.Panel);
        UiKit.Anchor(summary.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), 0f, -130f, 0f, 0f);

        challengesSummary = UiKit.NewText("Texto", summary.transform, "", 38f, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
        challengesSummary.enableAutoSizing = true;
        challengesSummary.fontSizeMax = 38f;
        challengesSummary.fontSizeMin = 24f;
        UiKit.Anchor(challengesSummary.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 1f), 30f, 0f, 30f, 8f);

        var track = UiKit.NewPanel("Pista", summary.transform, new Color(1f, 1f, 1f, 0.15f));
        UiKit.Anchor(track.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), 30f, 22f, 30f, -46f);
        var fill = UiKit.NewPanel("Relleno", track.transform, UiKit.Green);
        progressFill = fill.rectTransform;
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(0f, 1f);
        progressFill.offsetMin = Vector2.zero;
        progressFill.offsetMax = Vector2.zero;

        challengesContent = UiKit.NewScroll("Lista", s.body, 16f, out challengesScroll);
        UiKit.Anchor((RectTransform)challengesContent.parent, Vector2.zero, Vector2.one, 0f, 0f, 0f, 150f);
    }

    private void RefreshChallenges()
    {
        foreach (Transform child in challengesContent)
        {
            child.gameObject.SetActive(false); // que el layout lo ignore ya
            Destroy(child.gameObject);
        }

        var catalog = ChallengeManager.Catalog();
        int done = ChallengeManager.CompletedTotal(catalog.Count);
        challengesSummary.text = done + " de " + catalog.Count + " retos completados  ·  " +
                                 done * ChallengeManager.PointsPerChallenge + " pts";
        float frac = catalog.Count == 0 ? 0f : done / (float)catalog.Count;
        progressFill.anchorMax = new Vector2(Mathf.Max(frac, 0.001f), 1f);

        for (int i = 0; i < catalog.Count; i++)
        {
            int index = i;
            bool isDone = ChallengeManager.IsDone(i);

            string retoTitulo = catalog[i].Key;
            var row = UiKit.NewButton("Reto_" + (i + 1), challengesContent, "", UiKit.PanelStrong,
                () => OnChallengeTapped(index, retoTitulo), 170f, 30f);
            row.label.gameObject.SetActive(false);

            // Número
            var badge = UiKit.NewPanel("Numero", row.image.transform, isDone ? UiKit.Green : UiKit.Gray, false);
            badge.sprite = UiKit.Circle;
            badge.raycastTarget = false;
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            badge.rectTransform.pivot = new Vector2(0f, 0.5f);
            badge.rectTransform.sizeDelta = new Vector2(92f, 92f);
            badge.rectTransform.anchoredPosition = new Vector2(24f, 0f);
            var num = UiKit.NewText("N", badge.transform, (i + 1).ToString(), 42f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            UiKit.Stretch(num.rectTransform);

            // Título y descripción
            var title = UiKit.NewText("Titulo", row.image.transform, catalog[i].Key, 40f, FontStyles.Bold, Color.white, TextAlignmentOptions.BottomLeft);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;
            UiKit.Anchor(title.rectTransform, new Vector2(0f, 0.52f), new Vector2(1f, 1f), 140f, 0f, 250f, 8f);

            var desc = UiKit.NewText("Descripcion", row.image.transform, catalog[i].Value, 28f, FontStyles.Normal, UiKit.TextSoft, TextAlignmentOptions.TopLeft);
            desc.overflowMode = TextOverflowModes.Ellipsis;
            desc.maxVisibleLines = 2;
            UiKit.Anchor(desc.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.52f), 140f, 8f, 250f, 0f);

            // Estado
            var pill = UiKit.NewPanel("Estado", row.image.transform, isDone ? UiKit.Green : new Color(1f, 1f, 1f, 0.16f));
            pill.raycastTarget = false;
            pill.rectTransform.anchorMin = pill.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            pill.rectTransform.pivot = new Vector2(1f, 0.5f);
            pill.rectTransform.sizeDelta = new Vector2(200f, 62f);
            pill.rectTransform.anchoredPosition = new Vector2(-22f, 0f);
            var pt = UiKit.NewText("T", pill.transform, isDone ? "Hecho" : "Pendiente", 30f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            UiKit.Stretch(pt.rectTransform);
        }

        challengesScroll.verticalNormalizedPosition = 1f;
    }

    // ---- Cómo se usa ----------------------------------------------------------------------------

    private void BuildHelpScreen()
    {
        var s = NewSub(PageId.Help, "Cómo se usa");

        // Navegación (abajo)
        helpPrev = UiKit.NewButton("Anterior", s.body, "< Anterior", UiKit.Blue, () => ShowHelpPage(helpIndex - 1), 100f, 38f);
        var prt = (RectTransform)helpPrev.button.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0f, 0f);
        prt.pivot = new Vector2(0f, 0f);
        prt.sizeDelta = new Vector2(300f, 100f);
        prt.anchoredPosition = Vector2.zero;

        helpNext = UiKit.NewButton("Siguiente", s.body, "Siguiente >", UiKit.Blue, () => ShowHelpPage(helpIndex + 1), 100f, 38f);
        var nrt = (RectTransform)helpNext.button.transform;
        nrt.anchorMin = nrt.anchorMax = new Vector2(1f, 0f);
        nrt.pivot = new Vector2(1f, 0f);
        nrt.sizeDelta = new Vector2(300f, 100f);
        nrt.anchoredPosition = Vector2.zero;

        helpCounter = UiKit.NewText("Contador", s.body, "", 36f, FontStyles.Bold, UiKit.TextSoft, TextAlignmentOptions.Center);
        UiKit.Anchor(helpCounter.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), 310f, 0f, 310f, -100f);

        // Tarjeta con el texto (con scroll por si la pantalla es baja)
        var card = UiKit.NewPanel("Tarjeta", s.body, UiKit.Panel);
        UiKit.Anchor(card.rectTransform, Vector2.zero, Vector2.one, 0f, 120f, 0f, 0f);

        var content = UiKit.NewScroll("Texto", card.transform, 22f, out helpScroll);
        UiKit.Stretch((RectTransform)content.parent, 34f, 24f, 34f, 24f);
        content.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;

        helpTitle = UiKit.NewText("Titulo", content, "", 54f, FontStyles.Bold, UiKit.Accent, TextAlignmentOptions.TopLeft);
        helpBody = UiKit.NewText("Texto", content, "", 40f, FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft);
        helpBody.lineSpacing = 6f;
    }

    private void ShowHelpPage(int index)
    {
        // Pasar de la última página vuelve al menú
        if (index >= HelpContent.Pages.Length) { Show(PageId.Main); return; }
        helpIndex = Mathf.Clamp(index, 0, HelpContent.Pages.Length - 1);

        var page = HelpContent.Pages[helpIndex];
        helpTitle.text = page.title;
        helpBody.text = page.body;
        helpCounter.text = (helpIndex + 1) + " / " + HelpContent.Pages.Length;
        helpPrev.button.interactable = helpIndex > 0;
        helpNext.label.text = helpIndex == HelpContent.Pages.Length - 1 ? "Listo" : "Siguiente >";
        helpScroll.verticalNormalizedPosition = 1f;
    }

    // ---- Ajustes ----------------------------------------------------------------------------------

    private void BuildSettingsScreen()
    {
        var s = NewSub(PageId.Settings, "Ajustes");
        var content = UiKit.NewScroll("Lista", s.body, 20f, out ScrollRect sr);
        UiKit.Stretch((RectTransform)content.parent);

        soundToggle = AddToggleRow(content, "Sonido", "Clics, aciertos y efectos del circuito",
            () => { AppSettings.SoundEnabled = !AppSettings.SoundEnabled; RefreshSettings(); if (AppSettings.SoundEnabled) CircuitSounds.Click(); });
        labelsToggle = AddToggleRow(content, "Etiquetas flotantes", "Valor y corriente sobre cada tarjeta",
            () => { AppSettings.LabelsEnabled = !AppSettings.LabelsEnabled; RefreshSettings(); });

        resetButton = UiKit.NewButton("Reiniciar", content, "Reiniciar progreso de retos", UiKit.Red, OnResetPressed, 120f, 40f);

        var info = UiKit.NewText("Info", content, "Los retos completados y los ajustes se guardan en este dispositivo.",
            30f, FontStyles.Normal, UiKit.TextSoft, TextAlignmentOptions.TopLeft);
        var infoLe = info.gameObject.AddComponent<LayoutElement>();
        infoLe.minHeight = 90f;
    }

    private UiKit.ButtonParts AddToggleRow(RectTransform parent, string title, string subtitle, UnityEngine.Events.UnityAction onClick)
    {
        var row = UiKit.NewPanel("Fila_" + title, parent, UiKit.Panel);
        var le = row.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 150f;
        le.preferredHeight = 150f;

        var t = UiKit.NewText("Titulo", row.transform, title, 42f, FontStyles.Bold, Color.white, TextAlignmentOptions.BottomLeft);
        UiKit.Anchor(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), 30f, 0f, 300f, 12f);
        var sub = UiKit.NewText("Detalle", row.transform, subtitle, 28f, FontStyles.Normal, UiKit.TextSoft, TextAlignmentOptions.TopLeft);
        sub.enableAutoSizing = true;
        sub.fontSizeMax = 28f;
        sub.fontSizeMin = 20f;
        UiKit.Anchor(sub.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), 30f, 12f, 300f, 0f);

        var b = UiKit.NewButton("Interruptor", row.transform, "", UiKit.Green, onClick, 80f, 32f);
        var brt = (RectTransform)b.button.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
        brt.pivot = new Vector2(1f, 0.5f);
        brt.sizeDelta = new Vector2(260f, 84f);
        brt.anchoredPosition = new Vector2(-24f, 0f);
        return b;
    }

    private void RefreshSettings()
    {
        SetToggle(soundToggle, AppSettings.SoundEnabled);
        SetToggle(labelsToggle, AppSettings.LabelsEnabled);
        resetArmed = false;
        resetButton.label.text = "Reiniciar progreso de retos";
        resetButton.image.color = UiKit.Red;
    }

    private static void SetToggle(UiKit.ButtonParts b, bool on)
    {
        b.label.text = on ? "Activado" : "Desactivado";
        b.image.color = on ? UiKit.Green : UiKit.Gray;
    }

    private void OnResetPressed()
    {
        if (!resetArmed)
        {
            // Primera pulsación: pide confirmación (se cancela sola a los 3 s).
            resetArmed = true;
            resetButton.label.text = "¿Seguro? Toca otra vez";
            resetButton.image.color = UiKit.Amber;
            StopCoroutine(nameof(DisarmReset));
            StartCoroutine(nameof(DisarmReset));
            return;
        }

        StopCoroutine(nameof(DisarmReset));
        ChallengeManager.ResetAllProgress(ChallengeManager.Catalog().Count);
        RefreshSettings();
        ShowToast("Progreso de retos reiniciado");
    }

    private IEnumerator DisarmReset()
    {
        yield return new WaitForSecondsRealtime(3f);
        if (resetArmed) RefreshSettings();
    }

    // ---- Acerca de -----------------------------------------------------------------------------------

    private void BuildAboutScreen()
    {
        var s = NewSub(PageId.About, "Acerca de");

        var card = UiKit.NewPanel("Tarjeta", s.body, UiKit.Panel);
        UiKit.Stretch(card.rectTransform);

        var content = UiKit.NewScroll("Texto", card.transform, 18f, out ScrollRect sr);
        UiKit.Stretch((RectTransform)content.parent, 34f, 24f, 34f, 24f);
        content.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;

        UiKit.NewText("Nombre", content, AppTitle, 54f, FontStyles.Bold, UiKit.Accent, TextAlignmentOptions.TopLeft);
        UiKit.NewText("Descripcion", content,
            "Aplicación de realidad aumentada para aprender electrónica básica. Los estudiantes colocan " +
            "tarjetas de papel sobre la mesa y la app superpone cada componente, simula el circuito en tiempo " +
            "real (Ley de Ohm) y explica qué ocurre: circuito abierto, cortocircuito, LED al revés, LED quemado, " +
            "serie, paralelo y compuertas lógicas AND y OR.",
            38f, FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft);

        UiKit.NewText("Datos", content,
            "<b>Universidad</b>\n" + University + "\n\n" +
            "<b>Curso</b>\n" + Course + "\n\n" +
            "<b>Integrantes</b>\n" + string.Join("\n", Members) + "\n\n" +
            "<b>Tecnologías</b>\nUnity " + Application.unityVersion + " · Vuforia Engine\n\n" +
            "<b>Versión</b>\n" + Application.version,
            38f, FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft);
    }

    // ---- Avisos y pantalla de carga -------------------------------------------------------------------

    private void BuildToast()
    {
        var img = UiKit.NewPanel("Aviso", safeRoot, new Color(0f, 0f, 0f, 0.85f));
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(820f, 110f);
        rt.anchoredPosition = new Vector2(0f, 60f);

        toastText = UiKit.NewText("Texto", rt, "", 36f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        toastText.enableAutoSizing = true;
        toastText.fontSizeMax = 36f;
        toastText.fontSizeMin = 22f;
        UiKit.Stretch(toastText.rectTransform, 20f, 10f, 20f, 10f);
        toast = img.gameObject;
        toast.SetActive(false);
    }

    private void ShowToast(string message)
    {
        toastText.text = message;
        toast.SetActive(true);
        StopCoroutine(nameof(HideToast));
        StartCoroutine(nameof(HideToast));
    }

    private IEnumerator HideToast()
    {
        yield return new WaitForSecondsRealtime(2.5f);
        toast.SetActive(false);
    }

    private void BuildLoading()
    {
        var img = UiKit.NewPanel("Cargando", canvas.transform, new Color(0.02f, 0.04f, 0.10f, 0.96f), false);
        UiKit.Stretch(img.rectTransform);
        loading = img.gameObject;

        var t = UiKit.NewText("Texto", img.transform, "Iniciando la cámara...\n<size=60%><color=#B8C8E8>Apunta a las tarjetas cuando aparezca la imagen</color></size>",
            58f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        UiKit.Anchor(t.rectTransform, new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.48f));
        t.enableAutoSizing = true;
        t.fontSizeMax = 58f;
        t.fontSizeMin = 28f;

        // Ruedita: 8 puntos que se desvanecen
        spinner = UiKit.NewRect("Ruedita", img.transform);
        spinner.anchorMin = spinner.anchorMax = new Vector2(0.5f, 0.58f);
        spinner.sizeDelta = new Vector2(220f, 220f);
        for (int i = 0; i < 8; i++)
        {
            float ang = i * Mathf.PI * 2f / 8f;
            var dot = UiKit.NewPanel("Punto" + i, spinner, new Color(1f, 0.85f, 0.25f, Mathf.Lerp(0.15f, 1f, i / 7f)), false);
            dot.sprite = UiKit.Circle;
            dot.raycastTarget = false;
            dot.rectTransform.sizeDelta = new Vector2(34f, 34f);
            dot.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 85f;
        }
        loading.SetActive(false);
    }

    // ---------------------------------------------------------------------------------------
    // Navegación y acciones
    // ---------------------------------------------------------------------------------------

    private void Show(PageId id)
    {
        current = id;
        foreach (var kv in screens) kv.Value.gameObject.SetActive(kv.Key == id);

        switch (id)
        {
            case PageId.Main:
                int total = ChallengeManager.Catalog().Count;
                int done = ChallengeManager.CompletedTotal(total);
                progressText.text = done == 0 ? "" : "Retos completados: " + done + "/" + total + "  ·  " + done * ChallengeManager.PointsPerChallenge + " pts";
                break;
            case PageId.Challenges: RefreshChallenges(); break;
            case PageId.Help: ShowHelpPage(0); break;
            case PageId.Settings: RefreshSettings(); break;
        }
        if (layoutReady) Relayout(); // la pantalla nueva se acomoda al instante (sin parpadeo)
    }

    /// <summary>Un reto pendiente se abre directo; uno ya completado pide confirmación antes de repetirlo.</summary>
    private void OnChallengeTapped(int index, string title)
    {
        if (isLoading || confirmDialog != null) return;

        if (!ChallengeManager.IsDone(index))
        {
            StartSimulator(false, index);
            return;
        }

        string name = "reto " + (index + 1);
        confirmDialog = UiKit.NewDialog("¿Empezar de nuevo?",
            "Ya completaste el " + name + " (" + title + ").\n¿Seguro que quieres empezar el " + name + " de nuevo?\nTu puntaje se conserva.",
            new UiKit.DialogButton("Sí, empezar el " + name, UiKit.Green, () => { CloseConfirm(); StartSimulator(false, index); }),
            new UiKit.DialogButton("Cancelar", UiKit.Gray, CloseConfirm));
    }

    private void CloseConfirm()
    {
        if (confirmDialog != null) Destroy(confirmDialog);
        confirmDialog = null;
    }

    private void Back()
    {
        if (confirmDialog != null) { CloseConfirm(); return; }
        if (isLoading) return;
        if (current == PageId.Main) QuitApp();
        else Show(PageId.Main);
    }

    private void QuitApp()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void StartSimulator(bool freeMode, int challengeIndex)
    {
        if (isLoading) return;

        if (!Application.CanStreamedLevelBeLoaded(SimulatorScene))
        {
            ShowToast("No se encontró la escena \"" + SimulatorScene + "\" en Build Settings");
            return;
        }

        isLoading = true;
        AppSettings.FreeMode = freeMode;
        AppSettings.SelectedChallenge = challengeIndex;
        CircuitSounds.Click();
        StartCoroutine(LoadSimulator());
    }

    private IEnumerator LoadSimulator()
    {
        loading.SetActive(true);
        loading.transform.SetAsLastSibling();
        yield return null;   // deja dibujar la pantalla de carga antes de que Vuforia bloquee el hilo
        yield return null;
        SceneManager.LoadSceneAsync(SimulatorScene);
    }

    // ---------------------------------------------------------------------------------------
    // Adaptación a vertical / horizontal
    // ---------------------------------------------------------------------------------------

    private void Relayout()
    {
        UiKit.SetReferenceResolution(scaler);
        UiKit.ApplySafeArea(safeRoot);
        Canvas.ForceUpdateCanvases();

        bool landscape = Screen.width > Screen.height;
        float w = safeRoot.rect.width;
        float h = safeRoot.rect.height;

        // --- Principal ---
        if (landscape)
        {
            UiKit.Anchor(brand, new Vector2(0f, 0f), new Vector2(0.46f, 1f), 40f, 20f, 10f, 20f);
            UiKit.Anchor(buttonsArea, new Vector2(0.46f, 0f), new Vector2(1f, 1f), 10f, 20f, 50f, 20f);
        }
        else
        {
            UiKit.Anchor(brand, new Vector2(0f, 0.52f), new Vector2(1f, 1f), 30f, 0f, 30f, 20f);
            UiKit.Anchor(buttonsArea, new Vector2(0f, 0f), new Vector2(1f, 0.52f), 60f, 30f, 60f, 10f);
        }

        // Logo cuadrado, en la parte de arriba de la marca
        Canvas.ForceUpdateCanvases();
        float bw = brand.rect.width, bh = brand.rect.height;
        float logoSize = Mathf.Clamp(Mathf.Min(bw * 0.55f, bh * 0.44f), 140f, 520f);
        logo.anchorMin = logo.anchorMax = new Vector2(0.5f, 0.73f);
        logo.pivot = new Vector2(0.5f, 0.5f);
        logo.sizeDelta = new Vector2(logoSize, logoSize);
        logo.anchoredPosition = Vector2.zero;

        // Lista de botones: centrada verticalmente si cabe, con scroll si no
        LayoutRebuilder.ForceRebuildLayoutImmediate(buttonsContent);
        float need = LayoutUtility.GetPreferredHeight(buttonsContent) + 8f;
        float avail = buttonsArea.rect.height;
        buttonsScroll.anchorMin = new Vector2(0f, 0.5f);
        buttonsScroll.anchorMax = new Vector2(1f, 0.5f);
        buttonsScroll.pivot = new Vector2(0.5f, 0.5f);
        buttonsScroll.anchoredPosition = Vector2.zero;
        buttonsScroll.sizeDelta = new Vector2(0f, Mathf.Min(need, avail));

        // --- Subpantallas: columna central de ancho limitado ---
        float bodyW = Mathf.Min(w - 60f, 1040f);
        foreach (var s in subScreens)
        {
            var rt = s.body;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            const float top = 160f, bottom = 30f;
            rt.sizeDelta = new Vector2(bodyW, -(top + bottom));
            rt.anchoredPosition = new Vector2(0f, (bottom - top) / 2f);
        }
    }
}
