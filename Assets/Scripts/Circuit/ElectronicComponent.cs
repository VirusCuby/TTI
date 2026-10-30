using UnityEngine;

/// <summary>
/// Se coloca en la raíz de cada prefab de componente (Batería, Resistencia, LED,
/// Interruptor), sobre el mismo GameObject que trae el Image Target de Vuforia.
/// Es el "glue" entre el reconocimiento AR y el grafo del CircuitManager: se
/// registra cuando la tarjeta se detecta y se da de baja cuando se pierde el
/// tracking, y expone los dos Node que forman sus terminales.
/// </summary>
[DisallowMultipleComponent]
public class ElectronicComponent : MonoBehaviour
{
    [Header("Identidad")]
    public ComponentType type;

    [Header("Terminales")]
    [Tooltip("Positivo (Batería/LED) o Terminal A (Resistencia/Interruptor)")]
    public Node positiveOrA;

    [Tooltip("Negativo (Batería/LED) o Terminal B (Resistencia/Interruptor)")]
    public Node negativeOrB;

    [Header("Solo Interruptor")]
    public bool isClosed = false;

    [Header("Solo Resistencia")]
    [Tooltip("Valor en ohmios. En la app se cambia tocando la resistencia (recorre los valores de ResistorSteps).")]
    public float resistanceOhms = 220f;
    [Tooltip("Potencia máxima (W) que soporta antes de sobrecalentarse. Las de carbón típicas: 0.25 W.")]
    public float resistorPowerRating = 0.25f;

    [Header("Solo Batería")]
    [Tooltip("Voltaje sin carga (V). Una pila de 9 V.")]
    public float batteryVoltage = 9f;
    [Tooltip("Resistencia interna (ohm). Limita la corriente en un cortocircuito.")]
    public float internalResistance = 1f;

    [Header("Solo LED")]
    [Tooltip("Voltaje mínimo para que conduzca (V). Un LED rojo: ~2 V.")]
    public float forwardVoltage = 2f;
    [Tooltip("Resistencia del LED mientras conduce (ohm).")]
    public float ledSeriesResistance = 10f;
    [Tooltip("Corriente (A) con la que brilla al máximo. 0.020 = 20 mA.")]
    public float ledFullBrightCurrent = 0.020f;
    [Tooltip("Por encima de esta corriente (A) el LED está al límite y se avisa. 0.035 = 35 mA.")]
    public float ledMaxCurrent = 0.035f;
    [Tooltip("Por encima de esta corriente (A) el LED se quema. 0.050 = 50 mA.")]
    public float ledBurnCurrent = 0.050f;

    /// <summary>LED quemado: no vuelve a conducir hasta retirar la tarjeta y volver a colocarla.</summary>
    [System.NonSerialized] public bool isBurned;

    /// <summary>Valores de resistencia (ohm) por los que se pasa al tocar la resistencia en la app.</summary>
    public static readonly float[] ResistorSteps = { 100f, 220f, 330f, 470f, 1000f, 2200f, 4700f, 10000f };

    /// <summary>Voltajes (V) por los que se pasa al tocar la batería en la app.</summary>
    public static readonly float[] VoltageSteps = { 3f, 5f, 9f, 12f };

    /// <summary>Pasa al siguiente voltaje de la batería (y vuelve al primero tras el último).</summary>
    public void CycleVoltage()
    {
        if (type != ComponentType.Battery) return;

        float next = VoltageSteps[0];
        for (int i = 0; i < VoltageSteps.Length; i++)
        {
            if (VoltageSteps[i] > batteryVoltage + 0.01f) { next = VoltageSteps[i]; break; }
        }
        batteryVoltage = next;
        CircuitManager.Instance?.MarkDirty();
    }

    /// <summary>Pasa al siguiente valor de resistencia (y vuelve al primero tras el último).</summary>
    public void CycleResistance()
    {
        if (type != ComponentType.Resistor) return;

        float next = ResistorSteps[0];
        for (int i = 0; i < ResistorSteps.Length; i++)
        {
            if (ResistorSteps[i] > resistanceOhms + 0.5f) { next = ResistorSteps[i]; break; }
        }
        resistanceOhms = next;
        CircuitManager.Instance?.MarkDirty();
    }

    /// <summary>
    /// Llamar desde un botón/gesto de tap en AR (o desde el evento de Vuforia
    /// si se prefiere accionarlo tocando la propia tarjeta) para alternar el
    /// interruptor entre abierto y cerrado.
    /// </summary>
    public void ToggleSwitch()
    {
        if (type != ComponentType.Switch) return;
        isClosed = !isClosed;
        CircuitManager.Instance?.MarkDirty();
    }

    private void OnEnable()
    {
        // Con Vuforia: engancha esto a OnTargetFound del ObserverBehaviour,
        // o simplemente configura el handler para que active/desactive TODO
        // el GameObject (no solo el render) al encontrar/perder el target.
        CircuitManager.Instance?.RegisterComponent(this);
    }

    private void OnDisable()
    {
        // Con Vuforia: equivalente a OnTargetLost.
        // Un LED quemado se "reemplaza" retirando la tarjeta y volviéndola a colocar.
        isBurned = false;
        CircuitManager.Instance?.UnregisterComponent(this);
    }

    private void OnValidate()
    {
        if (positiveOrA != null) positiveOrA.owner = this;
        if (negativeOrB != null) negativeOrB.owner = this;
    }
}
