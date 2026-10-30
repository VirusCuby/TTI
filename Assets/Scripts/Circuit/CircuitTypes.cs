using System.Collections.Generic;

/// <summary>
/// Tipos y estructuras compartidas por Node, ElectronicComponent y CircuitManager.
/// Mantenerlas en un archivo aparte evita dependencias circulares entre scripts.
/// </summary>

public enum ComponentType
{
    Battery,
    Resistor,
    LED,
    Switch
}

/// <summary>
/// Rol del terminal dentro de su componente. Positive/Negative se usa en
/// componentes polarizados (Batería, LED); TerminalA/TerminalB en los que
/// no tienen polaridad (Resistencia, Interruptor).
/// </summary>
public enum NodeRole
{
    Positive,
    Negative,
    TerminalA,
    TerminalB
}

public enum EdgeKind
{
    Wire,       // cable físico creado por proximidad entre dos tarjetas
    Resistor,
    Switch,
    LED
}

public enum CircuitStatus
{
    NoBattery,          // no hay ninguna tarjeta de batería activa en la mesa
    Open,               // no existe camino entre + y - (falta conexión o interruptor abierto)
    Closed,             // circuito cerrado y válido
    ReversedPolarity,   // cerraría si no fuera por un LED al revés
    ShortCircuit        // existe un camino directo +/- sin ningún elemento limitador
}

/// <summary>
/// Resultado de una evaluación del circuito. El CircuitManager lo publica
/// mediante el evento OnCircuitEvaluated para que la UI / VFX reaccionen.
/// </summary>
public class CircuitResult
{
    public CircuitStatus status;
    public bool isShortCircuit;

    /// <summary>LED responsable de bloquear el cierre por polaridad invertida (si aplica).</summary>
    public ElectronicComponent faultyLed;

    /// <summary>Estado encendido/apagado de cada LED presente en el circuito.</summary>
    public readonly Dictionary<ElectronicComponent, bool> ledStates = new Dictionary<ElectronicComponent, bool>();

    // ---- Resultados del cálculo eléctrico (Ley de Ohm) ----------------------

    /// <summary>True si se pudo resolver el circuito (hay batería y la matriz no era singular).</summary>
    public bool simulated;

    /// <summary>Corriente, voltaje y potencia de cada componente (batería, resistencia, LED, interruptor cerrado).</summary>
    public readonly Dictionary<ElectronicComponent, ElementReading> readings = new Dictionary<ElectronicComponent, ElementReading>();

    /// <summary>Brillo de cada LED, de 0 (apagado) a 1 (brillo completo).</summary>
    public readonly Dictionary<ElectronicComponent, float> ledBrightness = new Dictionary<ElectronicComponent, float>();

    /// <summary>LED que superaron su corriente máxima y se quemaron en esta evaluación.</summary>
    public readonly List<ElectronicComponent> burnedLeds = new List<ElectronicComponent>();

    /// <summary>LED que ya están quemados (de evaluaciones anteriores o de esta).</summary>
    public readonly List<ElectronicComponent> deadLeds = new List<ElectronicComponent>();

    /// <summary>LED conduciendo por encima de su corriente recomendada, pero sin quemarse todavía.</summary>
    public readonly List<ElectronicComponent> overdrivenLeds = new List<ElectronicComponent>();

    /// <summary>Resistencias que disipan más potencia de la que soportan.</summary>
    public readonly List<ElectronicComponent> overheatedResistors = new List<ElectronicComponent>();

    /// <summary>Mayor corriente (A) que pasó por un LED en el instante en que se quemó (0 si no se quemó ninguno ahora).</summary>
    public float burnCurrent;

    /// <summary>Corriente total que entrega la batería (A).</summary>
    public float batteryCurrent;

    /// <summary>Voltaje entre los polos de la batería, ya con la caída por su resistencia interna (V).</summary>
    public float batteryTerminalVoltage;
}

/// <summary>Medidas de un componente: corriente de a hacia b (A), caída de voltaje a-b (V) y potencia (W).</summary>
public struct ElementReading
{
    public float current;
    public float voltage;
    public float power;
}
