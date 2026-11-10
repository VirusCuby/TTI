using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Piezas de interfaz (uGUI) creadas por código para el menú principal y el menú de pausa:
/// botones redondeados, textos, paneles, listas con scroll y fondo degradado.
/// No usa ningún archivo de imagen: los sprites se generan al vuelo, así que funcionan igual
/// en el Editor y en el APK.
/// </summary>
public static class UiKit
{
    // ---- Paleta ---------------------------------------------------------------
    public static readonly Color BgTop = new Color(0.05f, 0.11f, 0.24f);
    public static readonly Color BgBottom = new Color(0.02f, 0.04f, 0.10f);
    public static readonly Color Panel = new Color(1f, 1f, 1f, 0.07f);
    public static readonly Color PanelStrong = new Color(0.10f, 0.17f, 0.32f, 0.98f);
    public static readonly Color Blue = new Color(0.20f, 0.45f, 0.90f);
    public static readonly Color Green = new Color(0.16f, 0.68f, 0.36f);
    public static readonly Color Amber = new Color(0.95f, 0.65f, 0.15f);
    public static readonly Color Red = new Color(0.85f, 0.25f, 0.25f);
    public static readonly Color Gray = new Color(0.32f, 0.38f, 0.50f);
    public static readonly Color TextSoft = new Color(0.72f, 0.80f, 0.95f);
    public static readonly Color Accent = new Color(1f, 0.85f, 0.25f);

    // ---- Sprites generados ------------------------------------------------------
    private static Sprite rounded, circle, gradient;

    /// <summary>Rectángulo redondeado (9-slice): se usa con Image.type = Sliced.</summary>
    public static Sprite Rounded
    {
        get
        {
            if (rounded != null) return rounded;
            const int size = 64, radius = 24;
            var tex = NewTexture(size, size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, CornerAlpha(x, y, size, radius)));
            tex.Apply();
            rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return rounded;
        }
    }

    public static Sprite Circle
    {
        get
        {
            if (circle != null) return circle;
            const int size = 64;
            var tex = NewTexture(size, size);
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(c - d + 0.5f)));
                }
            tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return circle;
        }
    }

    /// <summary>Degradado vertical (arriba BgTop, abajo BgBottom).</summary>
    public static Sprite Gradient
    {
        get
        {
            if (gradient != null) return gradient;
            const int h = 128;
            var tex = NewTexture(2, h);
            for (int y = 0; y < h; y++)
            {
                Color c = Color.Lerp(BgBottom, BgTop, y / (float)(h - 1));
                tex.SetPixel(0, y, c);
                tex.SetPixel(1, y, c);
            }
            tex.Apply();
            gradient = Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 100f);
            return gradient;
        }
    }

    private static Texture2D NewTexture(int w, int h)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private static float CornerAlpha(int x, int y, int size, int radius)
    {
        float px = x + 0.5f, py = y + 0.5f;
        float dx = Mathf.Max(radius - px, px - (size - radius), 0f);
        float dy = Mathf.Max(radius - py, py - (size - radius), 0f);
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        return Mathf.Clamp01(radius - d + 0.5f);
    }

    // ---- Estructura -----------------------------------------------------------------

    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    public static void Anchor(RectTransform rt, Vector2 min, Vector2 max, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    public static Image NewPanel(string name, Transform parent, Color color, bool rounded = true)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        if (rounded) { img.sprite = Rounded; img.type = Image.Type.Sliced; }
        return img;
    }

    // ---- Texto ----------------------------------------------------------------------

    public static TextMeshProUGUI NewText(string name, Transform parent, string text, float size,
        FontStyles style = FontStyles.Normal, Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color ?? Color.white;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    // ---- Botones --------------------------------------------------------------------

    public class ButtonParts
    {
        public Button button;
        public Image image;
        public TextMeshProUGUI label;
    }

    /// <summary>Botón redondeado con texto centrado. Su altura se fija con LayoutElement.</summary>
    public static ButtonParts NewButton(string name, Transform parent, string label, Color color,
        UnityEngine.Events.UnityAction onClick, float height = 130f, float fontSize = 42f)
    {
        var img = NewPanel(name, parent, color);
        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        if (onClick != null) button.onClick.AddListener(onClick);

        var le = img.gameObject.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;

        var t = NewText("Texto", img.transform, label, fontSize, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        Stretch(t.rectTransform, 16f, 6f, 16f, 6f);
        t.enableAutoSizing = true;
        t.fontSizeMax = fontSize;
        t.fontSizeMin = fontSize * 0.55f;
        t.textWrappingMode = TextWrappingModes.NoWrap;

        return new ButtonParts { button = button, image = img, label = t };
    }

    // ---- Lista con scroll -----------------------------------------------------------

    /// <summary>
    /// Zona con scroll vertical. Devuelve el contenido: ahí se agregan filas (el VerticalLayoutGroup
    /// las apila y el ContentSizeFitter ajusta la altura).
    /// </summary>
    public static RectTransform NewScroll(string name, Transform parent, float spacing, out ScrollRect scrollRect)
    {
        var root = NewRect(name, parent);
        // Imagen invisible: deja arrastrar con el dedo también sobre el texto y los huecos.
        var catcher = root.gameObject.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);
        root.gameObject.AddComponent<RectMask2D>();
        scrollRect = root.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.scrollSensitivity = 40f;
        scrollRect.decelerationRate = 0.12f;

        var content = NewRect("Contenido", root);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = spacing;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(0, 0, 4, 4);

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = content;
        scrollRect.viewport = root;
        return content;
    }

    // ---- Misc -----------------------------------------------------------------------

    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    /// <summary>Canvas de pantalla completa que escala por el lado corto (igual en vertical y horizontal).</summary>
    public static Canvas NewCanvas(string name, Transform parent, int sortingOrder, out CanvasScaler scaler)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.matchWidthOrHeight = 0.5f;
        SetReferenceResolution(scaler);
        return canvas;
    }

    /// <summary>
    /// Resolución de referencia según la orientación actual: el lado corto siempre vale 1080,
    /// así los botones y textos tienen el mismo tamaño físico en vertical y en horizontal.
    /// </summary>
    public static void SetReferenceResolution(CanvasScaler scaler)
    {
        bool landscape = Screen.width > Screen.height;
        scaler.referenceResolution = landscape ? new Vector2(1920f, 1080f) : new Vector2(1080f, 1920f);
    }

    /// <summary>Ajusta un RectTransform raíz al área segura de la pantalla (muescas, barras del sistema).</summary>
    public static void ApplySafeArea(RectTransform rt)
    {
        Rect safe = Screen.safeArea;
        if (Screen.width <= 0 || Screen.height <= 0) return;
        rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ---- Diálogos --------------------------------------------------------------------

    public struct DialogButton
    {
        public string label;
        public Color color;
        public UnityEngine.Events.UnityAction onClick;
        public DialogButton(string label, Color color, UnityEngine.Events.UnityAction onClick)
        {
            this.label = label;
            this.color = color;
            this.onClick = onClick;
        }
    }

    /// <summary>
    /// Aviso modal centrado: título, mensaje y una columna de botones. Crea su propio Canvas por encima de
    /// todo (también del botón de pausa), con fondo oscuro que bloquea los toques. Para cerrarlo basta
    /// con destruir el GameObject que devuelve.
    /// </summary>
    public static GameObject NewDialog(string title, string message, params DialogButton[] buttons)
    {
        EnsureEventSystem();
        var canvas = NewCanvas("Dialogo", null, 60, out CanvasScaler scaler);
        canvas.gameObject.AddComponent<CanvasOrientationWatcher>().scaler = scaler;

        var dim = NewPanel("Fondo", canvas.transform, new Color(0f, 0f, 0f, 0.80f), false);
        Stretch(dim.rectTransform);

        var card = NewPanel("Tarjeta", dim.transform, PanelStrong);
        var rt = card.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(880f, 0f);

        var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(44, 44, 44, 44);
        vlg.spacing = 24f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        NewText("Titulo", card.transform, title, 54f, FontStyles.Bold, Accent, TextAlignmentOptions.Center);
        NewText("Mensaje", card.transform, message, 38f, FontStyles.Normal, Color.white, TextAlignmentOptions.Center);

        foreach (var b in buttons)
            NewButton("Boton", card.transform, b.label, b.color, b.onClick, 112f, 42f);

        return canvas.gameObject;
    }
}

/// <summary>Mantiene la resolución de referencia de un Canvas según la orientación (vertical u horizontal).</summary>
public class CanvasOrientationWatcher : MonoBehaviour
{
    public CanvasScaler scaler;
    private int w, h;

    private void Update()
    {
        if (scaler == null || (Screen.width == w && Screen.height == h)) return;
        w = Screen.width;
        h = Screen.height;
        UiKit.SetReferenceResolution(scaler);
    }
}
