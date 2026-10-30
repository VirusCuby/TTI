using System;
using System.Collections.Generic;
using UnityEngine;

// Permite que Assets/Tests/EditMode use ResetInstanceForTests() sin exponerlo
// como parte de la API pública del componente.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Circuit.Tests")]

/// <summary>
/// Motor de validación del circuito. Modela todo lo que hay sobre la mesa
/// como un grafo:
///   - Vértices  = los Node (terminales) de cada tarjeta activa.
///   - Aristas   = (a) cables físicos creados por proximidad (Node.ConnectedNodes)
///                 y (b) el enlace interno de cada componente (resistencia,
///                 interruptor cerrado, LED en su única dirección de conducción).
///
/// La batería NO añade una arista interna: sus dos terminales son la fuente
/// (+) y el sumidero (-) que buscamos conectar a través del resto del grafo.
///
/// Se ejecutan 3 recorridos BFS sobre variantes del mismo grafo:
///   1) Estricto        -> ¿cierra respetando polaridad e interruptores?
///   2) Relajado (LED)  -> ¿cerraría si ignorásemos la polaridad del LED?
///   3) Sin resistencias -> ¿existe un camino directo +/- sin nada que limite corriente?
/// Comparar los tres resultados basta para diagnosticar: abierto, cerrado,
/// polaridad invertida o cortocircuito.
/// </summary>
public class CircuitManager : MonoBehaviour
{
    public static CircuitManager Instance { get; private set; }

    [Header("Conexión por proximidad (se aplica a TODOS los Node de la mesa)")]
    [Tooltip("Si está activo, estos valores reemplazan los de cada Node individual: se cambian en un solo sitio.")]
    [SerializeField] private bool overrideNodeDistances = true;
    [Tooltip("Distancia máxima (m) a la que dos terminales de tarjetas distintas pueden conectarse. 0.25 = 25 cm.")]
    [SerializeField] private float connectDistance = 0.25f;
    [Tooltip("Distancia (m) a partir de la cual una unión ya hecha se rompe. Debe ser mayor que la de conexión.")]
    [SerializeField] private float disconnectDistance = 0.27f;
    [Tooltip("Regla del terminal más cercano: cada terminal solo se une a los que estén a menos de " +
             "(distancia al más cercano × este factor + margen). Evita uniones falsas aunque la distancia máxima sea grande. " +
             "Usa 0 para desactivar la regla y que solo cuente la distancia máxima.")]
    [SerializeField] private float nearestRatio = 1.05f;
    [Tooltip("Margen fijo (m) que se suma en la regla anterior. 0.02 = 2 cm. Permite que tres terminales formen un punto de unión " +
             "(circuitos en paralelo) aunque no estén exactamente a la misma distancia.")]
    [SerializeField] private float nearestSlack = 0.02f;

    public bool OverrideNodeDistances => overrideNodeDistances;
    public float ConnectDistance => connectDistance;
    public float DisconnectDistance => Mathf.Max(disconnectDistance, connectDistance + 0.005f);
    public float NearestTolerance => nearestRatio;
    public float NearestSlack => nearestSlack;

    /// <summary>Se dispara cada vez que el grafo cambia y se re-evalúa.</summary>
    public event Action<CircuitResult> OnCircuitEvaluated;

    public readonly List<Node> AllNodes = new List<Node>();
    private readonly List<ElectronicComponent> allComponents = new List<ElectronicComponent>();

    private bool dirty;

    private struct Edge
    {
        public Node from;
        public Node to;
        public EdgeKind kind;
        public bool directed; // true SOLO para el LED en modo estricto (Ánodo -> Cátodo)
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        // Evita dejar una referencia colgante si la escena se recarga o el
        // manager se destruye.
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Solo para Assets/Tests/EditMode: fija (o libera, con null) el
    /// singleton a mano. Awake()/OnEnable() no se ejecutan de forma síncrona
    /// al hacer AddComponent dentro de una prueba de Edit Mode (Unity los
    /// difiere a un tick del editor que nunca llega en un [Test] síncrono),
    /// así que las pruebas no pueden depender de que Awake() asigne
    /// Instance solo: hay que asignarlo explícitamente.
    /// </summary>
    internal static void SetInstanceForTests(CircuitManager instance) => Instance = instance;

    // ---- Registro (llamado por Node y ElectronicComponent) -----------------

    public void RegisterNode(Node n)       { if (!AllNodes.Contains(n)) AllNodes.Add(n); MarkDirty(); }
    public void UnregisterNode(Node n)     { AllNodes.Remove(n); MarkDirty(); }

    public void RegisterComponent(ElectronicComponent c)
    {
        if (!allComponents.Contains(c)) allComponents.Add(c);
        EnsureLabel(c);
        MarkDirty();
    }

    public void UnregisterComponent(ElectronicComponent c)
    {
        allComponents.Remove(c);
        if (labels.TryGetValue(c, out ComponentLabel label) && label != null) label.Hide();
        MarkDirty();
    }

    /// <summary>Componentes que están ahora mismo sobre la mesa (tarjetas detectadas).</summary>
    public IReadOnlyList<ElectronicComponent> Components => allComponents;

    public void MarkDirty() => dirty = true;

    // ---- Etiquetas flotantes sobre cada componente ---------------------

    [Header("Etiquetas")]
    [Tooltip("Muestra sobre cada tarjeta su valor y su corriente (ej. '470 ohm', '14.5 mA').")]
    [SerializeField] private bool showLabels = true;

    private readonly Dictionary<ElectronicComponent, ComponentLabel> labels = new Dictionary<ElectronicComponent, ComponentLabel>();

    private void EnsureLabel(ElectronicComponent c)
    {
        if (!showLabels || !Application.isPlaying) return;
        if (labels.TryGetValue(c, out ComponentLabel existing) && existing != null)
        {
            existing.Show();
            return;
        }
        labels[c] = ComponentLabel.Create(c);
    }

    private void RefreshLabels(CircuitResult result)
    {
        foreach (var kvp in labels)
            if (kvp.Value != null) kvp.Value.Refresh(result);
    }

    /// <summary>Primer componente registrado del tipo pedido (ej. la batería), o null si no hay ninguno en mesa.</summary>
    public ElectronicComponent FindComponent(ComponentType type) => allComponents.Find(c => c.type == type);

    /// <summary>Color actual de los cables; los cables nuevos nacen con este color.</summary>
    public Color CableColor { get; private set; } = Color.white;

    /// <summary>Pinta todos los cables de la mesa (verde = corriente fluye, rojo = error, etc.).</summary>
    public void SetCableColor(Color color)
    {
        CableColor = color;
        foreach (Node n in AllNodes)
            n.SetCableColor(color);
    }

    /// <summary>Prende/apaga la animación de flujo eléctrico en todos los cables de la mesa.</summary>
    public void SetGlobalFlow(bool flowing)
    {
        foreach (Node n in AllNodes)
            n.SetFlowing(flowing);
    }

    private void LateUpdate()
    {
        // Los Node ya actualizaron sus conexiones este frame (Update), así
        // que evaluamos en LateUpdate una sola vez, aunque varios nodos
        // hayan marcado "dirty" en el mismo frame.
        if (!dirty) return;
        dirty = false;

        CircuitResult result = Evaluate();
        RefreshLabels(result);
        OnCircuitEvaluated?.Invoke(result);
    }

    // ---- Evaluación del circuito --------------------------------------

    public CircuitResult Evaluate()
    {
        var result = new CircuitResult();

        ElectronicComponent battery = allComponents.Find(c => c.type == ComponentType.Battery);
        if (battery == null)
        {
            result.status = CircuitStatus.NoBattery;
            ClearCableFlows();
            foreach (var led in allComponents)
            {
                if (led.type != ComponentType.LED) continue;
                result.ledStates[led] = false;      // sin batería ningún LED brilla
                result.ledBrightness[led] = 0f;
                if (led.isBurned) result.deadLeds.Add(led);
            }
            return result;
        }

        Node pos = battery.positiveOrA;
        Node neg = battery.negativeOrB;

        // 1) Estricto: respeta polaridad del LED y estado de interruptores.
        var edgesStrict = BuildEdges(relaxLedPolarity: false, excludeResistors: false);
        var reachStrict = BFS(pos, edgesStrict);
        bool closedStrict = reachStrict.Contains(neg);

        // 2) Relajado: igual, pero el LED conduce en ambos sentidos.
        var edgesRelaxed = BuildEdges(relaxLedPolarity: true, excludeResistors: false);
        bool closedRelaxed = BFS(pos, edgesRelaxed).Contains(neg);

        // 3) Sin elementos limitadores (resistencia NI LED): solo cables e
        //    interruptores cerrados. Si esto ya cierra, es un corto directo.
        var edgesShort = BuildEdges(relaxLedPolarity: false, excludeResistors: true);
        edgesShort.RemoveAll(e => e.kind == EdgeKind.LED);
        bool shortCircuit = BFS(pos, edgesShort).Contains(neg);

        result.isShortCircuit = shortCircuit;

        if (shortCircuit)
        {
            // El corto es la falla más grave: se reporta y no hace falta
            // seguir diagnosticando el resto (pero sí se calcula la corriente).
            result.status = CircuitStatus.ShortCircuit;
            Simulate(result, battery);
            return result;
        }

        if (closedStrict)
        {
            result.status = CircuitStatus.Closed;
        }
        else if (closedRelaxed)
        {
            // La única razón por la que no cerró en modo estricto es que
            // algún LED está al revés.
            result.status = CircuitStatus.ReversedPolarity;
            result.faultyLed = FindOffendingLed(pos, neg, edgesStrict);
        }
        else
        {
            result.status = CircuitStatus.Open;
        }

        // Estado individual de cada LED (para encenderlo/apagarlo en la escena):
        // conduce solo si su ánodo es alcanzable desde + Y su cátodo puede
        // llegar hasta - siguiendo el resto del grafo estricto.
        foreach (var comp in allComponents)
        {
            if (comp.type != ComponentType.LED) continue;

            bool anodeFed    = reachStrict.Contains(comp.positiveOrA);
            bool cathodeToNeg = BFS(comp.negativeOrB, edgesStrict).Contains(neg);
            result.ledStates[comp] = closedStrict && anodeFed && cathodeToNeg;
        }

        // Cálculo eléctrico (Ley de Ohm): corriente, voltaje, brillo y LED quemados.
        // Si se pudo resolver, reemplaza el estado encendido/apagado del recorrido anterior.
        Simulate(result, battery);
        return result;
    }

    // ---- Cálculo eléctrico ----------------------------------------------

    private void ClearCableFlows()
    {
        foreach (Node n in AllNodes) n.ClearCableFlows();
    }

    /// <summary>
    /// Convierte la mesa en un circuito eléctrico y lo resuelve con <see cref="CircuitSolver"/>.
    /// Los terminales unidos por un cable son el mismo nodo eléctrico. Cada componente
    /// aporta un elemento: batería (fuente con resistencia interna), resistencia, interruptor
    /// cerrado (resistencia casi nula) o LED (diodo). Un interruptor abierto o un LED quemado
    /// no aportan nada (circuito abierto en ese punto).
    /// </summary>
    private void Simulate(CircuitResult result, ElectronicComponent battery)
    {
        ClearCableFlows();

        // --- 1) Nodos eléctricos (unión de terminales conectados por cable) ---
        var index = new Dictionary<Node, int>();
        var parent = new List<int>();
        foreach (Node n in AllNodes)
        {
            index[n] = parent.Count;
            parent.Add(parent.Count);
        }

        int Find(int x)
        {
            while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
            return x;
        }

        foreach (Node n in AllNodes)
        {
            foreach (Node other in n.ConnectedNodes)
            {
                if (!index.ContainsKey(other)) continue;
                int ra = Find(index[n]);
                int rb = Find(index[other]);
                if (ra != rb) parent[ra] = rb;
            }
        }

        var rootId = new Dictionary<int, int>();
        int NodeId(Node n)
        {
            if (n == null || !index.TryGetValue(n, out int i)) return -1;
            int r = Find(i);
            if (!rootId.TryGetValue(r, out int id)) { id = rootId.Count; rootId[r] = id; }
            return id;
        }

        foreach (Node n in AllNodes) NodeId(n); // numera todos los nodos de forma estable

        int ground = NodeId(battery.negativeOrB);
        if (ground < 0 || NodeId(battery.positiveOrA) < 0) return;

        // --- 2) Resolver (si un LED se quema, se vuelve a resolver sin él) ---
        CircuitSolver solver = null;
        var elements = new List<CircuitSolver.Element>();
        var owners = new List<ElectronicComponent>();

        for (int pass = 0; pass < 4; pass++)
        {
            elements.Clear();
            owners.Clear();

            foreach (var comp in allComponents)
            {
                int a = NodeId(comp.positiveOrA);
                int b = NodeId(comp.negativeOrB);
                if (a < 0 || b < 0) continue;

                switch (comp.type)
                {
                    case ComponentType.Battery:
                        if (comp != battery) break; // solo se simula la primera batería
                        elements.Add(new CircuitSolver.Element { kind = CircuitSolver.Kind.Source, a = a, b = b, value = comp.batteryVoltage, series = comp.internalResistance });
                        owners.Add(comp);
                        break;

                    case ComponentType.Resistor:
                        elements.Add(new CircuitSolver.Element { kind = CircuitSolver.Kind.Resistor, a = a, b = b, value = comp.resistanceOhms });
                        owners.Add(comp);
                        break;

                    case ComponentType.Switch:
                        if (!comp.isClosed) break; // abierto: no conduce
                        elements.Add(new CircuitSolver.Element { kind = CircuitSolver.Kind.Resistor, a = a, b = b, value = 0.01 });
                        owners.Add(comp);
                        break;

                    case ComponentType.LED:
                        if (comp.isBurned) break; // quemado: no conduce
                        elements.Add(new CircuitSolver.Element { kind = CircuitSolver.Kind.Diode, a = a, b = b, value = comp.forwardVoltage, series = comp.ledSeriesResistance });
                        owners.Add(comp);
                        break;
                }
            }

            solver = new CircuitSolver();
            if (!solver.Solve(rootId.Count, ground, elements)) return;

            bool burnedNow = false;
            for (int i = 0; i < elements.Count; i++)
            {
                var comp = owners[i];
                if (comp.type == ComponentType.LED && solver.Current[i] > comp.ledBurnCurrent)
                {
                    comp.isBurned = true;
                    result.burnedLeds.Add(comp);
                    result.burnCurrent = Mathf.Max(result.burnCurrent, (float)solver.Current[i]);
                    burnedNow = true;
                }
            }
            if (!burnedNow) break;
        }

        // --- 3) Medidas por componente ---
        result.simulated = true;
        var terminalOut = new Dictionary<Node, float>(); // corriente que SALE del componente hacia el cable en ese terminal

        for (int i = 0; i < elements.Count; i++)
        {
            var comp = owners[i];
            var el = elements[i];
            float current = (float)solver.Current[i];
            float vab = (float)(solver.NodeVoltage[el.a] - solver.NodeVoltage[el.b]);

            var reading = new ElementReading { current = current, voltage = vab, power = Mathf.Abs(current * vab) };
            result.readings[comp] = reading;

            if (el.kind == CircuitSolver.Kind.Source)
            {
                result.batteryCurrent = current;
                result.batteryTerminalVoltage = vab;
                terminalOut[comp.positiveOrA] = current;   // la batería entrega corriente por el polo +
                terminalOut[comp.negativeOrB] = -current;  // y la recibe por el polo -
            }
            else
            {
                terminalOut[comp.positiveOrA] = -current; // entra por el terminal A (o ánodo)
                terminalOut[comp.negativeOrB] = current;   // sale por el terminal B (o cátodo)
            }

            if (comp.type == ComponentType.Resistor && reading.power > comp.resistorPowerRating)
                result.overheatedResistors.Add(comp);
        }

        // --- 4) LED: brillo, quemados y al límite ---
        foreach (var comp in allComponents)
        {
            if (comp.type != ComponentType.LED) continue;

            float brightness = 0f;
            if (comp.isBurned)
            {
                result.deadLeds.Add(comp);
            }
            else if (result.readings.TryGetValue(comp, out ElementReading r) && r.current > 0.0003f)
            {
                brightness = Mathf.Clamp01(r.current / Mathf.Max(comp.ledFullBrightCurrent, 1e-6f));
                brightness = Mathf.Max(brightness, 0.12f); // que se note aunque pase poca corriente
                if (r.current > comp.ledMaxCurrent) result.overdrivenLeds.Add(comp);
            }

            result.ledBrightness[comp] = brightness;
            result.ledStates[comp] = brightness > 0f;
        }

        if (result.burnedLeds.Count > 0) MarkDirty(); // reevaluar sin el LED quemado en el grafo lógico

        // --- 5) Corriente por cada cable (para animar el flujo) ---
        foreach (Node a in AllNodes)
        {
            int ca = a.ConnectedNodes.Count;
            for (int k = 0; k < ca; k++)
            {
                Node b = a.ConnectedNodes[k];
                if (!index.ContainsKey(b) || index[a] >= index[b]) continue; // cada cable una sola vez

                int cb = b.ConnectedNodes.Count;
                terminalOut.TryGetValue(a, out float oa);
                terminalOut.TryGetValue(b, out float ob);
                float flow = 0.5f * (oa / ca - ob / cb); // A>0: de a hacia b
                a.SetCableFlow(b, flow);
                b.SetCableFlow(a, -flow);
            }
        }
    }

    /// <summary>
    /// Encuentra el LED cuya inversión de polaridad, por sí sola, cerraría
    /// el circuito. Se prueba LED por LED relajando únicamente su arista.
    /// </summary>
    private ElectronicComponent FindOffendingLed(Node pos, Node neg, List<Edge> strictEdges)
    {
        foreach (var comp in allComponents)
        {
            if (comp.type != ComponentType.LED) continue;

            var testEdges = new List<Edge>(strictEdges);
            testEdges.RemoveAll(e => e.kind == EdgeKind.LED && e.from == comp.positiveOrA && e.to == comp.negativeOrB);
            testEdges.Add(new Edge { from = comp.positiveOrA, to = comp.negativeOrB, kind = EdgeKind.LED, directed = false });

            if (BFS(pos, testEdges).Contains(neg))
                return comp;
        }
        return null;
    }

    /// <summary>
    /// Construye la lista de aristas del grafo a partir de las conexiones
    /// físicas (Node.ConnectedNodes) más el enlace interno de cada componente.
    /// </summary>
    private List<Edge> BuildEdges(bool relaxLedPolarity, bool excludeResistors)
    {
        var edges = new List<Edge>();

        // --- Cables físicos (creados por proximidad entre tarjetas distintas) ---
        // Cada cable aparece dos veces (una vez por cada extremo en AllNodes),
        // así que se descarta la segunda vez que se ve el mismo par sin
        // depender del orden en que se recorrieron los nodos.
        var seenPairs = new HashSet<(Node, Node)>();
        foreach (Node n in AllNodes)
        {
            foreach (Node other in n.ConnectedNodes)
            {
                if (seenPairs.Contains((n, other)) || seenPairs.Contains((other, n))) continue;
                seenPairs.Add((n, other));
                edges.Add(new Edge { from = n, to = other, kind = EdgeKind.Wire, directed = false });
            }
        }

        // --- Enlace interno de cada componente ---
        foreach (var comp in allComponents)
        {
            switch (comp.type)
            {
                case ComponentType.Battery:
                    // Sin arista interna: es la fuente/sumidero del recorrido.
                    break;

                case ComponentType.Resistor:
                    if (!excludeResistors)
                        edges.Add(new Edge { from = comp.positiveOrA, to = comp.negativeOrB, kind = EdgeKind.Resistor, directed = false });
                    break;

                case ComponentType.Switch:
                    if (comp.isClosed)
                        edges.Add(new Edge { from = comp.positiveOrA, to = comp.negativeOrB, kind = EdgeKind.Switch, directed = false });
                    break;

                case ComponentType.LED:
                    if (comp.isBurned) break; // un LED quemado es un circuito abierto

                    // Ánodo (+) -> Cátodo (-): único sentido en el que un diodo conduce.
                    edges.Add(new Edge
                    {
                        from = comp.positiveOrA,
                        to = comp.negativeOrB,
                        kind = EdgeKind.LED,
                        directed = !relaxLedPolarity
                    });
                    break;
            }
        }

        return edges;
    }

    /// <summary>Recorrido en anchura genérico sobre una lista de aristas.</summary>
    private static HashSet<Node> BFS(Node source, List<Edge> edges)
    {
        var adjacency = new Dictionary<Node, List<Node>>();
        void AddAdj(Node from, Node to)
        {
            if (!adjacency.TryGetValue(from, out var list))
                adjacency[from] = list = new List<Node>();
            list.Add(to);
        }

        foreach (var e in edges)
        {
            AddAdj(e.from, e.to);
            if (!e.directed) AddAdj(e.to, e.from);
        }

        var visited = new HashSet<Node> { source };
        var queue = new Queue<Node>();
        queue.Enqueue(source);

        while (queue.Count > 0)
        {
            Node current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var neighbors)) continue;

            foreach (Node next in neighbors)
                if (visited.Add(next))
                    queue.Enqueue(next);
        }

        return visited;
    }
}
