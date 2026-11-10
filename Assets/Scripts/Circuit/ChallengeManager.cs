using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Retos guiados: una lista de misiones que el estudiante resuelve armando circuitos con las
/// tarjetas. Valida cada una mirando el resultado del CircuitManager, muestra el progreso y
/// lleva el puntaje (se guarda en el dispositivo).
///
/// Se crea sola al empezar la escena (junto con su panel en la parte de abajo de la pantalla),
/// así que no hay que agregar nada a la escena. Para quitarla basta con borrar este script.
/// </summary>
public class ChallengeManager : MonoBehaviour
{
    private const string PrefKey = "reto_completado_";
    public const int PointsPerChallenge = 10;
    private const float ExpandedHeight = 450f;   // alto del panel con la descripción visible

    // RuntimeInitializeOnLoadMethod corre UNA sola vez al arrancar la app (ahora en el menú); por eso se
    // engancha al evento de carga de escena: así se crea cada vez que se entra al simulador.
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
        if (FindAnyObjectByType<ChallengeManager>() != null) return;
        if (FindAnyObjectByType<CircuitManager>() == null) return; // otra escena sin circuito
        if (AppSettings.FreeMode) return;                          // modo libre: sin retos
        new GameObject("ChallengeManager").AddComponent<ChallengeManager>();
    }

    // ---- Modelo de los retos ----------------------------------------------

    private class Context
    {
        public CircuitResult result;
        public List<ElectronicComponent> switches = new List<ElectronicComponent>();
        public List<ElectronicComponent> leds = new List<ElectronicComponent>();
        public List<ElectronicComponent> resistors = new List<ElectronicComponent>();

        public bool Lit(ElectronicComponent led) =>
            result.ledBrightness.TryGetValue(led, out float b) && b > 0.15f && !led.isBurned;

        public bool AnyLit()
        {
            foreach (var l in leds) if (Lit(l)) return true;
            return false;
        }

        public int CountLit()
        {
            int n = 0;
            foreach (var l in leds) if (Lit(l)) n++;
            return n;
        }

        public float Current(ElectronicComponent c) =>
            result.readings.TryGetValue(c, out ElementReading r) ? r.current : 0f;
    }

    private class Challenge
    {
        public string title;
        public string description;
        public Func<Challenge, Context, bool> check;
        public Func<Challenge, Context, string> progress;
        public readonly HashSet<string> seen = new HashSet<string>();
        public readonly Dictionary<string, bool> table = new Dictionary<string, bool>();
        public bool done;    // completado alguna vez (se guarda en el dispositivo)
        public bool solved;  // superado en este intento (para poder repetir un reto ya completado)
        public bool armed;   // ya se vio el circuito SIN cumplir el reto (evita que un circuito ya armado lo resuelva solo)

        public void ResetProgress() { seen.Clear(); table.Clear(); }
    }

    private readonly List<Challenge> challenges = new List<Challenge>();
    private int current;
    private Context lastContext;
    private GameObject dialog;   // aviso de reto superado (si está abierto)

    // ---- UI ---------------------------------------------------------------

    private TextMeshProUGUI titleText, bodyText, progressText, navText;
    private RectTransform panel;
    private GameObject content;
    private bool collapsed;

    private void Awake()
    {
        BuildChallenges();
        LoadProgress();
        BuildUi();
        // Si el menú eligió un reto, se abre ese; si no, el primero pendiente.
        int wanted = AppSettings.SelectedChallenge;
        AppSettings.SelectedChallenge = -1;
        current = wanted >= 0 && wanted < challenges.Count ? wanted : FirstPending();
        ShowCurrent();
    }

    private void Start()
    {
        if (CircuitManager.Instance != null)
        {
            CircuitManager.Instance.OnCircuitEvaluated += OnEvaluated;
            CircuitManager.Instance.MarkDirty(); // fuerza una primera evaluación
        }
    }

    private void OnDestroy()
    {
        if (dialog != null) Destroy(dialog);
        if (CircuitManager.Instance != null)
            CircuitManager.Instance.OnCircuitEvaluated -= OnEvaluated;
    }

    // ---- Definición de los retos -------------------------------------------

    private void BuildChallenges() => FillChallenges(challenges);

    // ---- Consulta desde el menú (sin necesitar la escena del simulador) --------

    /// <summary>Título y descripción de cada reto, en orden.</summary>
    public static List<KeyValuePair<string, string>> Catalog()
    {
        var all = new List<Challenge>();
        FillChallenges(all);
        var list = new List<KeyValuePair<string, string>>();
        foreach (var c in all) list.Add(new KeyValuePair<string, string>(c.title, c.description));
        return list;
    }

    public static bool IsDone(int index) => PlayerPrefs.GetInt(PrefKey + index, 0) == 1;

    public static int CompletedTotal(int total)
    {
        int n = 0;
        for (int i = 0; i < total; i++) if (IsDone(i)) n++;
        return n;
    }

    public static void ResetAllProgress(int total)
    {
        for (int i = 0; i < total; i++) PlayerPrefs.DeleteKey(PrefKey + i);
        PlayerPrefs.Save();
    }

    private static void FillChallenges(List<Challenge> challenges)
    {
        challenges.Add(new Challenge
        {
            title = "Enciende el LED",
            description = "Arma un circuito cerrado con la batería, la resistencia, el interruptor y el LED, y deja el LED encendido.",
            check = (c, x) => x.result.status == CircuitStatus.Closed && x.AnyLit()
        });

        challenges.Add(new Challenge
        {
            title = "Corriente segura",
            description = "Haz que el LED brille con entre 5 y 25 mA. Toca la resistencia para cambiar su valor.",
            check = (c, x) =>
            {
                foreach (var led in x.leds)
                {
                    float i = x.Current(led);
                    if (x.result.status == CircuitStatus.Closed && x.Lit(led) && i >= 0.005f && i <= 0.025f) return true;
                }
                return false;
            },
            progress = (c, x) => x.leds.Count == 0 ? "" : "Corriente del LED ahora: " + CircuitUnits.Amps(x.Current(x.leds[0]))
        });

        challenges.Add(new Challenge
        {
            title = "Abre y cierra",
            description = "Toca el interruptor: el LED debe encenderse al cerrarlo y apagarse al abrirlo.",
            check = (c, x) =>
            {
                if (x.switches.Count == 0 || x.leds.Count == 0) return false;
                bool allClosed = true, anyOpen = false;
                foreach (var s in x.switches) { if (s.isClosed) continue; allClosed = false; anyOpen = true; }

                if (allClosed && x.result.status == CircuitStatus.Closed && x.AnyLit()) c.seen.Add("on");
                if (anyOpen && x.result.status == CircuitStatus.Open && !x.AnyLit()) c.seen.Add("off");
                return c.seen.Contains("on") && c.seen.Contains("off");
            },
            progress = (c, x) => Mark(c.seen.Contains("on")) + " LED encendido con el interruptor cerrado\n" +
                                 Mark(c.seen.Contains("off")) + " LED apagado con el interruptor abierto"
        });

        challenges.Add(new Challenge
        {
            title = "Poca corriente",
            description = "Logra que el LED siga encendido pero con menos de 10 mA. ¿Qué resistencia necesitas? (Ley de Ohm: I = V / R).",
            check = (c, x) =>
            {
                foreach (var led in x.leds)
                {
                    float i = x.Current(led);
                    if (x.result.status == CircuitStatus.Closed && x.Lit(led) && i > 0.0005f && i < 0.010f) return true;
                }
                return false;
            },
            progress = (c, x) => x.leds.Count == 0 ? "" : "Corriente del LED ahora: " + CircuitUnits.Amps(x.Current(x.leds[0]))
        });

        challenges.Add(new Challenge
        {
            title = "Cortocircuito controlado",
            description = "Provoca un cortocircuito a propósito: conecta la batería directo al interruptor cerrado, sin resistencia ni LED. Mira la corriente.",
            check = (c, x) => x.result.status == CircuitStatus.ShortCircuit
        });

        challenges.Add(new Challenge
        {
            title = "Polaridad del LED",
            description = "Gira la tarjeta del LED para ponerlo al revés y mira el aviso. Después vuelve a ponerlo bien para que encienda.",
            check = (c, x) =>
            {
                if (x.result.status == CircuitStatus.ReversedPolarity) c.seen.Add("rev");
                return c.seen.Contains("rev") && x.result.status == CircuitStatus.Closed && x.AnyLit();
            },
            progress = (c, x) => Mark(c.seen.Contains("rev")) + " LED al revés visto\n" + Mark(c.solved) + " LED encendido de nuevo"
        });

        challenges.Add(new Challenge
        {
            title = "Quema un LED",
            description = "Descubre el límite de un LED: usa una resistencia pequeña (100 ohm) o sube el voltaje tocando la batería hasta que se queme. Luego repáralo retirando su tarjeta y volviéndola a poner con una resistencia mayor.",
            check = (c, x) =>
            {
                if (x.result.burnedLeds.Count > 0) c.seen.Add("burn");
                return c.seen.Contains("burn") && x.result.status == CircuitStatus.Closed && x.AnyLit();
            },
            progress = (c, x) => Mark(c.seen.Contains("burn")) + " LED quemado\n" + Mark(c.solved) + " LED reemplazado y encendido"
        });

        challenges.Add(new Challenge
        {
            title = "Dos LED en serie",
            description = "Conecta dos LED uno detrás de otro, con una resistencia. Deben encender los dos.",
            check = (c, x) => x.result.status == CircuitStatus.Closed && x.leds.Count >= 2 && x.CountLit() >= 2,
            progress = (c, x) => "LED encendidos: " + x.CountLit() + " de " + x.leds.Count
        });

        challenges.Add(MakeGate("Compuerta AND",
            "Con dos interruptores EN SERIE y un LED, el LED debe encender solo cuando AMBOS estén cerrados. Prueba las 4 combinaciones.",
            (a, b) => a && b));

        challenges.Add(MakeGate("Compuerta OR",
            "Con dos interruptores EN PARALELO y un LED, el LED debe encender cuando AL MENOS UNO esté cerrado. Prueba las 4 combinaciones.",
            (a, b) => a || b));

        challenges.Add(new Challenge
        {
            title = "Control individual",
            description = "Arma dos ramas en paralelo, cada una con su interruptor y su LED. Logra: solo el LED 1, solo el LED 2, y los dos a la vez.",
            check = (c, x) =>
            {
                if (x.leds.Count < 2 || x.switches.Count < 2) return false;
                if (x.result.status != CircuitStatus.Closed && x.result.status != CircuitStatus.Open) return false;
                bool l1 = x.Lit(x.leds[0]), l2 = x.Lit(x.leds[1]);
                if (l1 && !l2) c.seen.Add("solo1");
                if (!l1 && l2) c.seen.Add("solo2");
                if (l1 && l2) c.seen.Add("ambos");
                return c.seen.Contains("solo1") && c.seen.Contains("solo2") && c.seen.Contains("ambos");
            },
            progress = (c, x) => Mark(c.seen.Contains("solo1")) + " solo el LED 1\n" +
                                 Mark(c.seen.Contains("solo2")) + " solo el LED 2\n" +
                                 Mark(c.seen.Contains("ambos")) + " los dos LED"
        });
    }

    private static string Mark(bool ok) => ok ? "[x]" : "[ ]";

    /// <summary>Reto de tabla de verdad: hay que observar las 4 combinaciones de dos interruptores.</summary>
    private static Challenge MakeGate(string title, string description, Func<bool, bool, bool> expected)
    {
        return new Challenge
        {
            title = title,
            description = description,
            check = (c, x) =>
            {
                if (x.switches.Count < 2 || x.leds.Count < 1) return false;
                if (x.result.status != CircuitStatus.Closed && x.result.status != CircuitStatus.Open) return false;

                string key = (x.switches[0].isClosed ? "1" : "0") + (x.switches[1].isClosed ? "1" : "0");
                c.table[key] = x.AnyLit();

                if (c.table.Count < 4) return false;
                foreach (var kvp in c.table)
                {
                    bool a = kvp.Key[0] == '1', b = kvp.Key[1] == '1';
                    if (kvp.Value != expected(a, b)) return false;
                }
                return true;
            },
            progress = (c, x) =>
            {
                var sb = new StringBuilder("Interruptor 1 / 2 -> LED\n");
                foreach (string key in new[] { "00", "01", "10", "11" })
                {
                    bool a = key[0] == '1', b = key[1] == '1';
                    sb.Append(a ? "cerrado" : "abierto").Append(" / ").Append(b ? "cerrado" : "abierto").Append(" -> ");
                    if (c.table.TryGetValue(key, out bool lit))
                        sb.Append(lit ? "enciende" : "apagado").Append(lit == expected(a, b) ? " [ok]" : " [no coincide]");
                    else
                        sb.Append("sin probar");
                    sb.Append('\n');
                }
                return sb.ToString().TrimEnd();
            }
        };
    }

    // ---- Evaluación ---------------------------------------------------------

    private void OnEvaluated(CircuitResult result)
    {
        var ctx = new Context { result = result };
        foreach (var comp in CircuitManager.Instance.Components)
        {
            switch (comp.type)
            {
                case ComponentType.Switch: ctx.switches.Add(comp); break;
                case ComponentType.LED: ctx.leds.Add(comp); break;
                case ComponentType.Resistor: ctx.resistors.Add(comp); break;
            }
        }
        lastContext = ctx;

        if (current >= challenges.Count) return;
        var ch = challenges[current];

        // check() se ejecuta siempre (va anotando lo observado). Un reto solo cuenta como superado si antes
        // se vio el circuito sin cumplirlo: así, al pasar al siguiente reto con el circuito ya armado, hay
        // que hacer algo (cambiar la resistencia, abrir el interruptor...) y no se salta solo.
        bool met = ch.check(ch, ctx);
        if (!ch.armed && !met) ch.armed = true;
        bool success = met && ch.armed && !ch.solved;

        if (success)
        {
            bool firstTime = !ch.done;
            ch.solved = true;
            ch.done = true;
            PlayerPrefs.SetInt(PrefKey + current, 1);
            PlayerPrefs.Save();
            CircuitSounds.Success();
            ShowCompletionDialog(firstTime);
        }

        RefreshText();
    }

    // ---- Aviso al superar un reto ---------------------------------------------------

    /// <summary>
    /// Al superar un reto: Continuar con el siguiente pendiente o Salir al menú. Si ya no queda ninguno
    /// pendiente (se completaron todos) ofrece Reiniciar los retos o Salir.
    /// </summary>
    private void ShowCompletionDialog(bool firstTime)
    {
        if (dialog != null) return;

        int total = challenges.Count;
        int done = CompletedCount();
        int next = NextPending(current);
        string name = "Reto " + (current + 1) + ": " + challenges[current].title;
        var buttons = new List<UiKit.DialogButton>();
        string title, message;

        if (next >= 0)
        {
            title = "¡Reto superado!";
            message = name + "\n" + (firstTime ? "+" + PointsPerChallenge + " puntos. " : "Ya lo habías completado. ") +
                      "Llevas " + done + " de " + total + " retos.";
            buttons.Add(new UiKit.DialogButton("Continuar con los demás retos", UiKit.Green, () => { CloseDialog(); GoTo(next); }));
        }
        else
        {
            title = "¡Completaste todos los retos!";
            message = "Tienes " + done * PointsPerChallenge + " puntos (" + done + " de " + total + " retos). ¡Felicidades!";
            buttons.Add(new UiKit.DialogButton("Reiniciar los retos", UiKit.Amber, RestartAll));
        }

        if (AppSettings.MenuAvailable)
            buttons.Add(new UiKit.DialogButton("Salir al menú", UiKit.Gray, () => { CloseDialog(); AppSettings.GoToMenu(); }));
        else
            buttons.Add(new UiKit.DialogButton("Cerrar", UiKit.Gray, CloseDialog));

        dialog = UiKit.NewDialog(title, message, buttons.ToArray());
    }

    private void CloseDialog()
    {
        if (dialog != null) Destroy(dialog);
        dialog = null;
    }

    /// <summary>Borra todo el progreso y vuelve a empezar por el reto 1.</summary>
    private void RestartAll()
    {
        CloseDialog();
        ResetAllProgress(challenges.Count);
        foreach (var c in challenges) { c.done = false; c.solved = false; }
        GoTo(0);
    }

    /// <summary>Primer reto pendiente después de <paramref name="from"/> (dando la vuelta); -1 si no queda ninguno.</summary>
    private int NextPending(int from)
    {
        for (int i = 1; i <= challenges.Count; i++)
        {
            int idx = (from + i) % challenges.Count;
            if (!challenges[idx].done) return idx;
        }
        return -1;
    }

    // ---- Navegación -----------------------------------------------------------

    private void Go(int delta)
    {
        if (challenges.Count == 0) return;
        GoTo((current + delta + challenges.Count) % challenges.Count);
    }

    private void GoTo(int index)
    {
        if (challenges.Count == 0) return;
        current = Mathf.Clamp(index, 0, challenges.Count - 1);
        challenges[current].solved = false;   // se vuelve a validar aunque ya estuviera completado
        challenges[current].armed = false;
        challenges[current].ResetProgress();
        ShowCurrent();
        if (lastContext != null) OnEvaluated(lastContext.result);
    }

    private int FirstPending()
    {
        for (int i = 0; i < challenges.Count; i++)
            if (!challenges[i].done) return i;
        return 0;
    }

    private int CompletedCount()
    {
        int n = 0;
        foreach (var c in challenges) if (c.done) n++;
        return n;
    }

    private void LoadProgress()
    {
        for (int i = 0; i < challenges.Count; i++)
            challenges[i].done = PlayerPrefs.GetInt(PrefKey + i, 0) == 1;
    }

    private void ShowCurrent() => RefreshText();

    private void RefreshText()
    {
        if (titleText == null || challenges.Count == 0) return;

        int done = CompletedCount();
        var ch = challenges[current];
        string stars = done * PointsPerChallenge + " pts";

        if (collapsed)
        {
            titleText.text = "Retos " + done + "/" + challenges.Count + "  ·  " + stars;
            titleText.color = Color.white;
            navText.text = "Mostrar";
            return;
        }

        navText.text = "Ocultar";
        titleText.text = "Reto " + (current + 1) + "/" + challenges.Count + ": " + ch.title + "   (" + stars + ")";
        titleText.color = ch.done ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.9f, 0.3f);

        if (ch.solved)
        {
            bodyText.text = "¡Reto superado!";
            bodyText.color = new Color(0.4f, 1f, 0.5f);
        }
        else
        {
            bodyText.text = ch.description + (ch.done ? "\n(Ya lo completaste antes.)" : "");
            bodyText.color = Color.white;
        }

        string progress = ch.progress != null && lastContext != null ? ch.progress(ch, lastContext) : "";
        progressText.text = progress;
    }

    // ---- Construcción del panel -------------------------------------------------

    private void BuildUi()
    {
        if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        var canvasGo = new GameObject("RetosCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.matchWidthOrHeight = 0.5f;
        UiKit.SetReferenceResolution(scaler);                   // vertical u horizontal
        canvasGo.AddComponent<CanvasOrientationWatcher>().scaler = scaler;

        // Panel de abajo
        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);
        panel = (RectTransform)panelGo.transform;
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 20f);
        panel.sizeDelta = new Vector2(-40f, ExpandedHeight);
        panelGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.66f);

        // Contenido (se oculta al plegar)
        content = new GameObject("Contenido", typeof(RectTransform));
        content.transform.SetParent(panel, false);
        var contentRt = (RectTransform)content.transform;
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        titleText = MakeText(panel, "Titulo", 40, FontStyles.Bold, 10f, 56f);
        bodyText = MakeText(content.transform, "Descripcion", 32, FontStyles.Normal, 78f, 150f);
        progressText = MakeText(content.transform, "Progreso", 30, FontStyles.Normal, 232f, 104f);
        progressText.color = new Color(0.75f, 0.9f, 1f);
        bodyText.enableAutoSizing = true; bodyText.fontSizeMin = 24; bodyText.fontSizeMax = 32;
        progressText.enableAutoSizing = true; progressText.fontSizeMin = 22; progressText.fontSizeMax = 30;

        // Botones
        MakeButton(panel, "Anterior", "<", new Vector2(0f, 0f), new Vector2(20f, 14f), new Vector2(150f, 80f), () => Go(-1));
        MakeButton(panel, "Siguiente", ">", new Vector2(0f, 0f), new Vector2(190f, 14f), new Vector2(150f, 80f), () => Go(+1));
        navText = MakeButton(panel, "Plegar", "Ocultar", new Vector2(1f, 0f), new Vector2(-20f, 14f), new Vector2(260f, 80f), TogglePanel);
    }

    private void TogglePanel()
    {
        collapsed = !collapsed;
        content.SetActive(!collapsed);
        panel.sizeDelta = new Vector2(-40f, collapsed ? 170f : ExpandedHeight);
        RefreshText();
    }

    /// <summary>Texto que ocupa todo el ancho del panel, a <paramref name="top"/> px del borde de arriba.</summary>
    private static TextMeshProUGUI MakeText(Transform parent, string name, float size, FontStyles style, float top, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -top);
        rt.sizeDelta = new Vector2(-40f, height); // 20 px de margen a cada lado

        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.color = Color.white;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    private static TextMeshProUGUI MakeButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.9f, 0.95f);
        go.GetComponent<Button>().onClick.AddListener(onClick);

        var textGo = new GameObject("Texto", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        var t = textGo.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.fontSize = 38;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }
}
