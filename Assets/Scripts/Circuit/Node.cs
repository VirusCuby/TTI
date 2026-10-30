using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Terminal físico/virtual de un componente (polo + o -, o extremo A/B).
/// Se ubica como hijo del Image Target, en el punto exacto donde el arte de
/// la tarjeta dibuja el pin. Cada Node escanea periódicamente la distancia
/// contra todos los demás Node activos en la mesa; si dos quedan lo bastante
/// cerca, se conectan lógicamente y se dibuja un cable (LineRenderer) entre
/// ellos. El CircuitManager solo lee connectedNodes: toda la detección de
/// proximidad vive aquí.
/// </summary>
[DisallowMultipleComponent]
public class Node : MonoBehaviour
{
    [Header("Identidad")]
    [Tooltip("Se autocompleta desde ElectronicComponent.OnValidate; puede asignarse a mano.")]
    public ElectronicComponent owner;
    public NodeRole role;

    [Header("Umbral de conexión (en metros == unidades de escena si el Image Target está calibrado a su tamaño real)")]
    [Tooltip("Solo se usa si el CircuitManager NO sobrescribe las distancias (Override Node Distances desactivado).")]
    [SerializeField] private float connectDistance = 0.03f;   // distancia a la que se CONECTA
    [SerializeField] private float disconnectDistance = 0.045f; // debe ser > connectDistance (evita parpadeo)
    [SerializeField] private float scanInterval = 0.15f;         // no hace falta escanear cada frame

    [Header("Marcador del terminal (esfera visible sobre la tarjeta)")]
    [Tooltip("Rojo = Positivo, Azul = Negativo, Amarillo = Terminal A, Cian = Terminal B; se pone VERDE cuando está unido a otro terminal.")]
    [SerializeField] private bool showMarker = true;
    [SerializeField] private float markerSize = 0.01f; // diámetro en metros (1 cm)

    [Header("Visual")]
    [Tooltip("Opcional. Si se deja vacío se dibuja un cable simple generado por código, coloreado según el estado del circuito.")]
    [SerializeField] private LineRenderer cablePrefab;
    [SerializeField] private float cableWidth = 0.008f; // solo para el cable generado por código
    [SerializeField] private float flowScrollSpeed = 1.5f; // requiere una textura tileable en el material del cable

    private bool flowing;

    // Material compartido por todos los cables generados por código. El color
    // va por vértice (startColor/endColor), así que no hace falta una instancia por cable.
    private static Material defaultCableMaterial;

    /// <summary>Terminales de OTROS componentes con los que este Node está físicamente unido.</summary>
    public IReadOnlyList<Node> ConnectedNodes => connectedNodes;
    private readonly List<Node> connectedNodes = new List<Node>();
    private readonly Dictionary<Node, LineRenderer> ownedCables = new Dictionary<Node, LineRenderer>();

    private float scanTimer;

    private Renderer markerRenderer;
    private MaterialPropertyBlock markerBlock;
    private static Material markerMaterial;
    private static readonly int TintId = Shader.PropertyToID("_Color");

    private void OnEnable()
    {
        CircuitManager.Instance?.RegisterNode(this);
        // Desfasar el primer escaneo entre nodos para no acumular todos los
        // cálculos de distancia en el mismo frame.
        scanTimer = Random.Range(0f, scanInterval);
        EnsureMarker();
    }

    private void OnDisable()
    {
        // La tarjeta perdió tracking (o el componente fue destruido): sus
        // conexiones físicas dejan de existir.
        for (int i = connectedNodes.Count - 1; i >= 0; i--)
            Disconnect(connectedNodes[i]);

        CircuitManager.Instance?.UnregisterNode(this);
    }

    private void Update()
    {
        scanTimer -= Time.deltaTime;
        if (scanTimer > 0f) return;
        scanTimer = scanInterval;

        ScanForConnections();
    }

    private void LateUpdate()
    {
        foreach (var kvp in ownedCables)
        {
            LineRenderer lr = kvp.Value;
            if (lr == null) continue;

            // Los cables siguen a las tarjetas mientras el estudiante las mueve.
            Vector3 p0 = transform.position;
            Vector3 p1 = kvp.Key.transform.position;
            lr.SetPosition(0, p0);
            lr.SetPosition(1, p1);

            if (cablePrefab != null)
            {
                // Cable propio (con su textura): se desplaza el offset cuando circula corriente.
                if (flowing && lr.sharedMaterial != null && lr.sharedMaterial.mainTexture != null)
                {
                    var mat = lr.material;
                    Vector2 offset = mat.mainTextureOffset;
                    offset.x -= flowScrollSpeed * Time.deltaTime;
                    mat.mainTextureOffset = offset;
                }
                continue;
            }

            // Cable generado por código: pulsos que viajan en el sentido de la corriente,
            // más rápido cuanta más corriente pasa. Sin corriente, el cable queda liso.
            if (!cableMaterials.TryGetValue(kvp.Key, out Material cm) || cm == null) continue;

            float flow = cableFlow.TryGetValue(kvp.Key, out float f) ? f : 0f;
            float speed = 0f;
            if (Mathf.Abs(flow) > 0.0005f)
                speed = Mathf.Sign(flow) * Mathf.Lerp(0.5f, 3.5f, Mathf.Clamp01(Mathf.Sqrt(Mathf.Abs(flow) / 0.03f)));

            Texture wanted = speed != 0f ? GetDashTexture() : null;
            if (cm.mainTexture != wanted) cm.mainTexture = wanted;

            if (speed != 0f)
            {
                float length = Vector3.Distance(p0, p1);
                cm.mainTextureScale = new Vector2(Mathf.Max(1f, length / DashLength), 1f);
                Vector2 off = cm.mainTextureOffset;
                off.x = Mathf.Repeat(off.x - speed * Time.deltaTime, 1f);
                cm.mainTextureOffset = off;
            }
        }
    }

    /// <summary>Llamado por CircuitManager para prender/apagar la animación de flujo en los cables que este Node dibuja.</summary>
    public void SetFlowing(bool value) => flowing = value;

    /// <summary>
    /// Corriente (A) que pasa por el cable que une este Node con <paramref name="other"/>:
    /// positiva si va de este hacia el otro. La calcula CircuitManager.
    /// </summary>
    public void SetCableFlow(Node other, float amps) => cableFlow[other] = amps;

    public void ClearCableFlows() => cableFlow.Clear();

    private const float DashLength = 0.012f; // metros por pulso de la animación

    private readonly Dictionary<Node, float> cableFlow = new Dictionary<Node, float>();
    private readonly Dictionary<Node, Material> cableMaterials = new Dictionary<Node, Material>();
    private static Texture2D dashTexture;

    private static Texture2D GetDashTexture()
    {
        if (dashTexture == null)
        {
            const int w = 32;
            dashTexture = new Texture2D(w, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)w;
                dashTexture.SetPixel(x, 0, new Color(1f, 1f, 1f, t < 0.35f ? 1f : 0.2f)); // pulso brillante + cable tenue
            }
            dashTexture.Apply();
        }
        return dashTexture;
    }

    /// <summary>
    /// Fuerza un escaneo de proximidad inmediato, sin esperar a <see cref="scanInterval"/>.
    /// Útil para pruebas automatizadas (ver Assets/Tests) y para revisar conexiones
    /// justo cuando una tarjeta aparece por primera vez en la mesa.
    /// </summary>
    public void ForceScan() => ScanForConnections();

    private const float KeepHysteresis = 1.1f;       // una unión ya hecha tolera un 10 % más que una nueva

    private float EffectiveConnectDistance(CircuitManager m) => m.OverrideNodeDistances ? m.ConnectDistance : connectDistance;
    private float EffectiveDisconnectDistance(CircuitManager m) => m.OverrideNodeDistances ? m.DisconnectDistance : disconnectDistance;

    /// <summary>Distancia al terminal más cercano que pertenezca a OTRA tarjeta (infinito si no hay ninguno).</summary>
    private float NearestOtherDistance(CircuitManager manager)
    {
        float best = float.PositiveInfinity;
        foreach (Node n in manager.AllNodes)
        {
            if (n == this || n.owner == owner) continue;
            float d = Vector3.Distance(transform.position, n.transform.position);
            if (d < best) best = d;
        }
        return best;
    }

    /// <summary>
    /// Regla del "terminal más cercano": una unión solo es válida si el otro terminal
    /// está entre los más cercanos de AMBOS lados (a menos de cercano × tolerancia).
    /// Así se puede usar una distancia máxima grande sin que se unan terminales
    /// que no corresponden. Es simétrica, para que los dos nodos decidan lo mismo.
    /// </summary>
    private bool NearestRuleAllows(CircuitManager manager, Node other, float dist, float margin)
    {
        float tol = manager.NearestTolerance;
        if (tol <= 0f) return true; // regla desactivada
        float slack = manager.NearestSlack;
        float limitMine = NearestOtherDistance(manager) * tol + slack;
        float limitOther = other.NearestOtherDistance(manager) * tol + slack;
        return dist <= limitMine * margin && dist <= limitOther * margin;
    }

    private void ScanForConnections()
    {
        var manager = CircuitManager.Instance;
        if (manager == null) return;

        float connectMax = EffectiveConnectDistance(manager);
        float disconnectMin = EffectiveDisconnectDistance(manager);

        foreach (Node other in manager.AllNodes)
        {
            if (other == this) continue;
            if (other.owner == owner) continue; // los dos terminales de un mismo componente no "conectan" entre sí

            float dist = Vector3.Distance(transform.position, other.transform.position);
            bool isConnected = connectedNodes.Contains(other);

            if (!isConnected)
            {
                if (dist <= connectMax && NearestRuleAllows(manager, other, dist, 1f))
                    Connect(other);
            }
            else if (dist > disconnectMin || !NearestRuleAllows(manager, other, dist, KeepHysteresis))
            {
                Disconnect(other);
            }
        }
    }

    private void Connect(Node other)
    {
        if (connectedNodes.Contains(other)) return;

        connectedNodes.Add(other);
        other.connectedNodes.Add(this);

        // Solo uno de los dos lados instancia el cable visual.
        if (!ownedCables.ContainsKey(other) && !other.ownedCables.ContainsKey(this))
            DrawCable(other);

        UpdateMarker();
        other.UpdateMarker();
        CircuitManager.Instance?.MarkDirty();
    }

    private void Disconnect(Node other)
    {
        connectedNodes.Remove(other);
        other.connectedNodes.Remove(this);

        RemoveCableIfOwned(other);
        other.RemoveCableIfOwned(this);

        UpdateMarker();
        other.UpdateMarker();
        CircuitManager.Instance?.MarkDirty();
    }

    // ---- Marcador visible del terminal ---------------------------------

    private Color RoleColor()
    {
        switch (role)
        {
            case NodeRole.Positive:  return new Color(1f, 0.15f, 0.15f);
            case NodeRole.Negative:  return new Color(0.2f, 0.4f, 1f);
            case NodeRole.TerminalA: return new Color(1f, 0.85f, 0.1f);
            default:                 return new Color(0.1f, 0.9f, 0.95f);
        }
    }

    private void EnsureMarker()
    {
        if (!showMarker || markerRenderer != null || !Application.isPlaying) return;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Marcador";
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col); // no debe interferir con el toque al interruptor

        go.transform.SetParent(transform, false);
        // Compensa la escala del padre (los *_Modelo son muy pequeños, a veces con escala desigual).
        Vector3 ls = transform.lossyScale;
        go.transform.localScale = new Vector3(markerSize / ls.x, markerSize / ls.y, markerSize / ls.z);
        go.transform.position = transform.position + transform.up * 0.004f; // 4 mm sobre la tarjeta

        if (markerMaterial == null) markerMaterial = new Material(Shader.Find("Sprites/Default"));
        markerRenderer = go.GetComponent<Renderer>();
        markerRenderer.sharedMaterial = markerMaterial;
        markerBlock = new MaterialPropertyBlock();
        UpdateMarker();
    }

    private void UpdateMarker()
    {
        if (markerRenderer == null) return;
        Color c = connectedNodes.Count > 0 ? new Color(0.2f, 1f, 0.3f) : RoleColor();
        markerRenderer.GetPropertyBlock(markerBlock);
        markerBlock.SetColor(TintId, c);
        markerRenderer.SetPropertyBlock(markerBlock);
    }

    /// <summary>Pinta de un color todos los cables que este Node dibuja (verde/naranja/rojo según el estado del circuito).</summary>
    public void SetCableColor(Color color)
    {
        foreach (var lr in ownedCables.Values)
            ApplyColor(lr, color);
    }

    private static void ApplyColor(LineRenderer lr, Color color)
    {
        if (lr == null) return;
        lr.startColor = color;
        lr.endColor = color;
    }

    private void DrawCable(Node other)
    {
        LineRenderer lr;
        if (cablePrefab != null)
        {
            lr = Instantiate(cablePrefab, transform.position, Quaternion.identity);
        }
        else
        {
            lr = new GameObject("Cable").AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.widthMultiplier = cableWidth;
            lr.numCapVertices = 4;
            lr.textureMode = LineTextureMode.Stretch;

            // Un material propio por cable: cada uno anima su textura a su velocidad.
            var cableMaterial = new Material(GetDefaultCableMaterial());
            cableMaterials[other] = cableMaterial;
            lr.sharedMaterial = cableMaterial;
        }

        lr.positionCount = 2;
        lr.SetPosition(0, transform.position);
        lr.SetPosition(1, other.transform.position);
        ApplyColor(lr, CircuitManager.Instance != null ? CircuitManager.Instance.CableColor : Color.white);
        ownedCables[other] = lr;
    }

    private static Material GetDefaultCableMaterial()
    {
        if (defaultCableMaterial == null)
            defaultCableMaterial = new Material(Shader.Find("Sprites/Default"));
        return defaultCableMaterial;
    }

    private void RemoveCableIfOwned(Node other)
    {
        if (ownedCables.TryGetValue(other, out LineRenderer lr))
        {
            if (lr != null) Destroy(lr.gameObject);
            ownedCables.Remove(other);
        }
        if (cableMaterials.TryGetValue(other, out Material cm))
        {
            if (cm != null) Destroy(cm);
            cableMaterials.Remove(other);
        }
        cableFlow.Remove(other);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = role == NodeRole.Positive || role == NodeRole.TerminalA ? Color.red : Color.blue;
        // Radio real: el del CircuitManager si sobrescribe las distancias, o el propio del Node.
        var m = CircuitManager.Instance != null ? CircuitManager.Instance : FindAnyObjectByType<CircuitManager>();
        float radius = m != null && m.OverrideNodeDistances ? m.ConnectDistance : connectDistance;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
