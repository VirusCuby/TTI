using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menú de pausa del simulador: un botón con tres rayitas (☰) en el borde derecho de la pantalla que abre
/// un panel con Continuar, Ayuda, Sonido, Etiquetas, Reiniciar y Volver al menú.
///
/// Se crea solo al empezar cualquier escena que tenga un CircuitManager (la escena del simulador), así que
/// no hay que agregar nada a la escena. La cámara de AR sigue funcionando por detrás. El botón Atrás de
/// Android (Escape) abre y cierra la pausa. Se adapta a vertical y horizontal.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    // RuntimeInitializeOnLoadMethod corre UNA sola vez al arrancar la app (en el menú); por eso se engancha
    // al evento de carga de escena: así se crea cada vez que se entra al simulador.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AutoCreate();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AutoCreate();

    private static void AutoCreate()
    {
        if (FindAnyObjectByType<PauseMenu>() != null) return;
        if (FindAnyObjectByType<CircuitManager>() == null) return; // no es la escena del simulador
        new GameObject("PauseMenu").AddComponent<PauseMenu>();
    }

    private Canvas canvas;
    private CanvasScaler scaler;
    private RectTransform safeRoot, overlay, card, menuView, helpView;
    private GameObject pauseButton, exitButton, exitDialog;
    private UiKit.ButtonParts soundBtn, labelsBtn;

    private TextMeshProUGUI helpTitle, helpBody, helpCounter;
    private UiKit.ButtonParts helpPrev, helpNext;
    private ScrollRect helpScroll;
    private int helpIndex;

    private int lastW, lastH;
    private bool open;

    private void Awake()
    {
        UiKit.EnsureEventSystem();
        BuildUi();
        lastW = Screen.width;
        lastH = Screen.height;
        SetOpen(false);
    }

    private void Update()
    {
        if (Screen.width != lastW || Screen.height != lastH)
        {
            lastW = Screen.width;
            lastH = Screen.height;
            UiKit.SetReferenceResolution(scaler);
            UiKit.ApplySafeArea(safeRoot);
            if (open) StartCoroutine(LayoutNextFrame());
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (exitDialog != null) CloseExitDialog();
            else if (open && helpView.gameObject.activeSelf) ShowView(false);
            else SetOpen(!open);
        }
    }

    private System.Collections.IEnumerator LayoutNextFrame()
    {
        yield return null;
        yield return null;   // el Canvas Scaler necesita un par de cuadros para conocer el tamaño nuevo
        LayoutCard();
    }

    // ---------------------------------------------------------------------------------------
    // Construcción
    // ---------------------------------------------------------------------------------------

    private void BuildUi()
    {
        canvas = UiKit.NewCanvas("PausaCanvas", transform, 40, out scaler);
        safeRoot = UiKit.NewRect("AreaSegura", canvas.transform);
        UiKit.Stretch(safeRoot);
        UiKit.ApplySafeArea(safeRoot);

        BuildPauseButton();
        BuildOverlay();
    }

    private void BuildPauseButton()
    {
        var img = UiKit.NewPanel("BotonPausa", safeRoot, new Color(0f, 0f, 0f, 0.55f));
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => SetOpen(true));
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.6f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(120f, 120f);
        rt.anchoredPosition = new Vector2(-14f, 0f);

        // Tres rayitas (el tipo de letra no trae el símbolo ☰)
        for (int i = -1; i <= 1; i++)
        {
            var bar = UiKit.NewPanel("Raya", rt, Color.white);
            bar.raycastTarget = false;
            bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            bar.rectTransform.sizeDelta = new Vector2(60f, 9f);
            bar.rectTransform.anchoredPosition = new Vector2(0f, i * 22f);
        }
        pauseButton = img.gameObject;

        // Botón "Salir", siempre a la vista debajo del de pausa (modo libre y retos)
        if (AppSettings.MenuAvailable)
        {
            var exit = UiKit.NewButton("BotonSalir", safeRoot, "Salir", new Color(0.85f, 0.25f, 0.25f, 0.92f), AskExit, 80f, 34f);
            var ert = (RectTransform)exit.button.transform;
            ert.anchorMin = ert.anchorMax = new Vector2(1f, 0.6f);
            ert.pivot = new Vector2(1f, 1f);
            ert.sizeDelta = new Vector2(120f, 80f);
            ert.anchoredPosition = new Vector2(-14f, -76f);
            exitButton = exit.button.gameObject;
        }
    }

    /// <summary>Pide confirmación antes de volver al menú principal (se pierde el estado del circuito).</summary>
    private void AskExit()
    {
        if (exitDialog != null) return;
        CircuitSounds.Click();
        exitDialog = UiKit.NewDialog("¿Salir al menú principal?",
            "Se cerrará el simulador y se perderá el estado actual del circuito. Los retos completados se conservan.",
            new UiKit.DialogButton("Sí, salir", UiKit.Red, () => { CloseExitDialog(); AppSettings.GoToMenu(); }),
            new UiKit.DialogButton("Cancelar", UiKit.Gray, CloseExitDialog));
    }

    private void CloseExitDialog()
    {
        if (exitDialog != null) Destroy(exitDialog);
        exitDialog = null;
    }

    private void OnDestroy()
    {
        if (exitDialog != null) Destroy(exitDialog);
    }

    private void BuildOverlay()
    {
        // Fondo oscuro que también bloquea los toques hacia la escena
        var dim = UiKit.NewPanel("Fondo", safeRoot, new Color(0f, 0f, 0f, 0.74f), false);
        UiKit.Stretch(dim.rectTransform);
        overlay = dim.rectTransform;

        var cardImg = UiKit.NewPanel("Tarjeta", overlay, UiKit.PanelStrong);
        card = cardImg.rectTransform;
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);

        BuildMenuView();
        BuildHelpView();
        ShowView(false);
    }

    private void BuildMenuView()
    {
        menuView = UiKit.NewRect("Menu", card);
        UiKit.Stretch(menuView, 30f, 30f, 30f, 30f);

        var title = UiKit.NewText("Titulo", menuView, "Pausa", 58f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        UiKit.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), 0f, -100f, 0f, 0f);

        var content = UiKit.NewScroll("Lista", menuView, 16f, out ScrollRect sr);
        UiKit.Anchor((RectTransform)content.parent, Vector2.zero, Vector2.one, 0f, 0f, 0f, 112f);

        UiKit.NewButton("Continuar", content, "Continuar", UiKit.Green, () => SetOpen(false), 112f, 42f);
        UiKit.NewButton("Ayuda", content, "Cómo se usa", UiKit.Blue, () => { ShowHelpPage(0); ShowView(true); }, 112f, 42f);
        soundBtn = UiKit.NewButton("Sonido", content, "", UiKit.Gray, () =>
        {
            AppSettings.SoundEnabled = !AppSettings.SoundEnabled;
            RefreshToggles();
            if (AppSettings.SoundEnabled) CircuitSounds.Click();
        }, 112f, 40f);
        labelsBtn = UiKit.NewButton("Etiquetas", content, "", UiKit.Gray, () =>
        {
            AppSettings.LabelsEnabled = !AppSettings.LabelsEnabled;
            RefreshToggles();
        }, 112f, 40f);
        UiKit.NewButton("Reiniciar", content, "Reiniciar escena", UiKit.Amber, RestartScene, 112f, 40f);

        if (AppSettings.MenuAvailable)
            UiKit.NewButton("Menu", content, "Volver al menú", UiKit.Red, BackToMenu, 112f, 40f);
    }

    private void BuildHelpView()
    {
        helpView = UiKit.NewRect("Ayuda", card);
        UiKit.Stretch(helpView, 30f, 30f, 30f, 30f);

        // Abajo: anterior / contador / siguiente
        helpPrev = UiKit.NewButton("Anterior", helpView, "<", UiKit.Blue, () => ShowHelpPage(helpIndex - 1), 96f, 44f);
        var prt = (RectTransform)helpPrev.button.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0f, 0f);
        prt.pivot = new Vector2(0f, 0f);
        prt.sizeDelta = new Vector2(150f, 96f);
        prt.anchoredPosition = Vector2.zero;

        helpNext = UiKit.NewButton("Siguiente", helpView, ">", UiKit.Blue, () => ShowHelpPage(helpIndex + 1), 96f, 44f);
        var nrt = (RectTransform)helpNext.button.transform;
        nrt.anchorMin = nrt.anchorMax = new Vector2(1f, 0f);
        nrt.pivot = new Vector2(1f, 0f);
        nrt.sizeDelta = new Vector2(150f, 96f);
        nrt.anchoredPosition = Vector2.zero;

        var close = UiKit.NewButton("Cerrar", helpView, "Volver", UiKit.Gray, () => ShowView(false), 96f, 38f);
        var crt = (RectTransform)close.button.transform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
        crt.pivot = new Vector2(0.5f, 0f);
        crt.sizeDelta = new Vector2(260f, 96f);
        crt.anchoredPosition = Vector2.zero;

        helpCounter = UiKit.NewText("Contador", helpView, "", 30f, FontStyles.Bold, UiKit.TextSoft, TextAlignmentOptions.Center);
        UiKit.Anchor(helpCounter.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), 0f, 100f, 0f, -140f);

        var content = UiKit.NewScroll("Texto", helpView, 20f, out helpScroll);
        UiKit.Anchor((RectTransform)content.parent, Vector2.zero, Vector2.one, 0f, 150f, 0f, 0f);
        content.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;

        helpTitle = UiKit.NewText("Titulo", content, "", 50f, FontStyles.Bold, UiKit.Accent, TextAlignmentOptions.TopLeft);
        helpBody = UiKit.NewText("Cuerpo", content, "", 36f, FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft);
        helpBody.lineSpacing = 6f;
    }

    // ---------------------------------------------------------------------------------------
    // Acciones
    // ---------------------------------------------------------------------------------------

    private void SetOpen(bool value)
    {
        open = value;
        overlay.gameObject.SetActive(value);
        pauseButton.SetActive(!value);
        if (exitButton != null) exitButton.SetActive(!value);
        if (!value) return;

        RefreshToggles();
        ShowView(false);
        UiKit.ApplySafeArea(safeRoot);
        LayoutCard();
    }

    private void ShowView(bool help)
    {
        menuView.gameObject.SetActive(!help);
        helpView.gameObject.SetActive(help);
    }

    private void RefreshToggles()
    {
        SetToggle(soundBtn, "Sonido", AppSettings.SoundEnabled);
        SetToggle(labelsBtn, "Etiquetas flotantes", AppSettings.LabelsEnabled);
    }

    private static void SetToggle(UiKit.ButtonParts b, string name, bool on)
    {
        b.label.text = name + ": " + (on ? "sí" : "no");
        b.image.color = on ? UiKit.Green : UiKit.Gray;
    }

    private void ShowHelpPage(int index)
    {
        helpIndex = Mathf.Clamp(index, 0, HelpContent.Pages.Length - 1);
        var page = HelpContent.Pages[helpIndex];
        helpTitle.text = page.title;
        helpBody.text = page.body;
        helpCounter.text = (helpIndex + 1) + " / " + HelpContent.Pages.Length;
        helpPrev.button.interactable = helpIndex > 0;
        helpNext.button.interactable = helpIndex < HelpContent.Pages.Length - 1;
        helpScroll.verticalNormalizedPosition = 1f;
    }

    private void RestartScene()
    {
        CircuitSounds.Click();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void BackToMenu()
    {
        CircuitSounds.Click();
        AppSettings.GoToMenu();
    }

    /// <summary>Tamaño de la tarjeta según la pantalla: ancho limitado en horizontal, casi todo el alto en vertical.</summary>
    private void LayoutCard()
    {
        Canvas.ForceUpdateCanvases();
        Rect area = safeRoot.rect;
        float w = Mathf.Min(area.width - 60f, 900f);
        float h = Mathf.Min(area.height - 60f, 1250f);
        card.sizeDelta = new Vector2(Mathf.Max(w, 300f), Mathf.Max(h, 300f));
        card.anchoredPosition = Vector2.zero;
    }
}
