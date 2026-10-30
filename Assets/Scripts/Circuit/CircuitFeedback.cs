using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Traduce el CircuitResult publicado por CircuitManager en feedback
/// perceptible para el estudiante: mensaje en pantalla con los números del cálculo
/// (corriente, caídas de voltaje, Ley de Ohm), LEDs con brillo según su corriente,
/// LEDs quemados con humo, halo sobre el componente responsable de un error de
/// polaridad, chispas en cortocircuito y color de los cables según el estado.
///
/// Colocar en un único GameObject de la escena (no en las tarjetas) y
/// enlazar el Text de UI desde el Inspector. El prefab de chispa es opcional:
/// si no se asigna, se generan chispas y humo por código.
/// </summary>
public class CircuitFeedback : MonoBehaviour
{
    [Header("Mensaje")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Color colorInfo = Color.white;
    [SerializeField] private Color colorSuccess = new Color(0.2f, 0.9f, 0.3f);
    [SerializeField] private Color colorWarning = new Color(1f, 0.6f, 0.1f);
    [SerializeField] private Color colorDanger = new Color(0.9f, 0.15f, 0.15f);

    [Header("Cortocircuito")]
    [Tooltip("Opcional. Si se deja vacío se crean chispas por código.")]
    [SerializeField] private ParticleSystem sparkPrefab;
    [SerializeField] private float sparkLifetime = 2f;

    private ElectronicComponent lastFaultyComponent;
    private CircuitStatus lastStatus = CircuitStatus.NoBattery;
    private ParticleSystem activeSpark;
    private readonly Dictionary<ElectronicComponent, ParticleSystem> smokes = new Dictionary<ElectronicComponent, ParticleSystem>();

    /// <summary>Último resultado mostrado (lo usa, por ejemplo, el gestor de retos).</summary>
    public CircuitResult LastResult { get; private set; }

    private void Start()
    {
        // Start() corre después de que TODOS los Awake()/OnEnable() de la
        // escena ya terminaron, así que aquí CircuitManager.Instance ya
        // existe seguro (a diferencia de OnEnable, cuyo orden respecto al
        // Awake() del CircuitManager no está garantizado).
        if (CircuitManager.Instance != null)
            CircuitManager.Instance.OnCircuitEvaluated += HandleResult;
    }

    private void OnDestroy()
    {
        if (CircuitManager.Instance != null)
            CircuitManager.Instance.OnCircuitEvaluated -= HandleResult;
    }

    private void HandleResult(CircuitResult result)
    {
        LastResult = result;

        // El componente marcado como responsable en la evaluación anterior
        // puede ya no serlo (el estudiante corrigió el error o cambió algo más).
        SetFault(lastFaultyComponent, false);
        lastFaultyComponent = null;

        var manager = CircuitManager.Instance;
        bool anyDead = result.deadLeds.Count > 0;

        // Sonidos solo al ENTRAR en cada situación (no en cada reevaluación).
        if (result.status == CircuitStatus.ShortCircuit && lastStatus != CircuitStatus.ShortCircuit) CircuitSounds.Zap();
        if (result.burnedLeds.Count > 0) CircuitSounds.Burn();
        lastStatus = result.status;

        switch (result.status)
        {
            case CircuitStatus.NoBattery:
                ShowMessage("Coloca la tarjeta de la batería sobre la mesa.", colorInfo);
                StopSpark();
                manager.SetGlobalFlow(false);
                manager.SetCableColor(colorWarning);
                break;

            case CircuitStatus.Open:
                ShowMessage("Circuito abierto: falta una conexión o un interruptor está abierto.", colorWarning);
                StopSpark();
                manager.SetGlobalFlow(false);
                manager.SetCableColor(colorWarning); // conectado pero aún sin camino completo
                break;

            case CircuitStatus.ReversedPolarity:
                ShowMessage("El LED no enciende: tiene la polaridad invertida. Gira la tarjeta.", colorWarning);
                StopSpark();
                manager.SetGlobalFlow(false);
                manager.SetCableColor(colorDanger);
                SetFault(result.faultyLed, true);
                lastFaultyComponent = result.faultyLed;
                break;

            case CircuitStatus.ShortCircuit:
            {
                var sb = new StringBuilder("¡Cortocircuito! Falta una resistencia entre la batería y el resto del circuito.");
                if (result.simulated)
                    sb.Append("\nCorriente: ").Append(CircuitUnits.Amps(result.batteryCurrent)).Append(" (¡demasiada!)");
                ShowMessage(sb.ToString(), colorDanger);
                manager.SetGlobalFlow(false);
                manager.SetCableColor(colorDanger);
                PlaySpark();
                break;
            }

            case CircuitStatus.Closed:
            {
                StopSpark();
                // Cerrado pero sin corriente: el voltaje no alcanza (ej. 3 V con dos LED) o la resistencia es enorme.
                bool noCurrent = result.simulated && result.batteryCurrent < 0.0003f;
                bool warn = noCurrent || result.overdrivenLeds.Count > 0 || result.overheatedResistors.Count > 0;
                manager.SetGlobalFlow(!noCurrent);
                manager.SetCableColor(warn ? colorWarning : colorSuccess);
                ShowMessage(noCurrent
                    ? "Circuito cerrado, pero casi no pasa corriente: el voltaje no alcanza para encender el LED (cada LED necesita ~2 V) o la resistencia es demasiado grande."
                    : BuildClosedMessage(result), warn ? colorWarning : colorSuccess);
                break;
            }
        }

        // Un LED quemado manda sobre cualquier otro mensaje (excepto el cortocircuito, que ya es grave).
        if (anyDead && result.status != CircuitStatus.ShortCircuit)
        {
            string msg = result.burnedLeds.Count > 0
                ? "¡Se quemó el LED! Pasaron " + CircuitUnits.Amps(result.burnCurrent) + " y aguanta como máximo " + CircuitUnits.Amps(result.deadLeds[0].ledBurnCurrent) + ".\nAumenta la resistencia (tócala) y vuelve a colocar la tarjeta del LED."
                : "El LED está quemado. Retira su tarjeta y vuelve a colocarla (con una resistencia mayor).";
            ShowMessage(msg, colorDanger);
            manager.SetCableColor(colorDanger);
            manager.SetGlobalFlow(false);
        }

        // Brillo de cada LED según la corriente que realmente pasa, y LED quemados.
        foreach (var kvp in result.ledBrightness)
        {
            var visual = kvp.Key != null ? kvp.Key.GetComponent<ComponentVisualFeedback>() : null;
            if (visual == null) continue;
            visual.SetBurned(kvp.Key.isBurned);
            visual.SetBrightness(kvp.Value);
        }

        UpdateSmoke(result);
    }

    private string BuildClosedMessage(CircuitResult result)
    {
        var sb = new StringBuilder();

        if (result.overdrivenLeds.Count > 0)
        {
            ElectronicComponent led = result.overdrivenLeds[0];
            sb.Append("LED al límite: ").Append(CircuitUnits.Amps(result.readings[led].current))
              .Append(" (lo seguro es menos de ").Append(CircuitUnits.Amps(led.ledMaxCurrent)).Append("). Toca la resistencia para subir su valor.");
        }
        else
        {
            sb.Append("Circuito cerrado. Corriente fluyendo correctamente.");
        }

        if (!result.simulated) return sb.ToString();

        // Línea de números: corriente total y Ley de Ohm de la primera resistencia.
        sb.Append("\nI = ").Append(CircuitUnits.Amps(result.batteryCurrent));

        var manager = CircuitManager.Instance;
        foreach (var comp in manager.Components)
        {
            if (comp.type == ComponentType.Resistor && result.readings.TryGetValue(comp, out ElementReading r) && Mathf.Abs(r.current) > 0.0005f)
            {
                sb.Append("  ·  ").Append(CircuitUnits.Volts(Mathf.Abs(r.voltage))).Append(" = ")
                  .Append(CircuitUnits.Ohms(comp.resistanceOhms)).Append(" × ").Append(CircuitUnits.Amps(r.current));
                break;
            }
        }

        foreach (var comp in manager.Components)
        {
            if (comp.type == ComponentType.LED && result.readings.TryGetValue(comp, out ElementReading r) && r.current > 0.0003f)
            {
                sb.Append("  ·  LED ").Append(CircuitUnits.Volts(r.voltage));
                break;
            }
        }

        if (result.overheatedResistors.Count > 0)
            sb.Append("\n¡Una resistencia se calienta demasiado!");

        return sb.ToString();
    }

    private void ShowMessage(string text, Color color)
    {
        if (messageText == null) return;
        messageText.text = text;
        messageText.color = color;
    }

    private void SetFault(ElectronicComponent component, bool active)
    {
        if (component == null) return;
        var visual = component.GetComponent<ComponentVisualFeedback>();
        if (visual != null) visual.SetFault(active);
    }

    // ---- Chispas (cortocircuito) y humo (LED quemado) ---------------------

    private void PlaySpark()
    {
        if (activeSpark != null) return;

        // La chispa aparece junto a la batería, que es donde ocurre el corto.
        ElectronicComponent battery = CircuitManager.Instance.FindComponent(ComponentType.Battery);
        Vector3 pos = battery != null ? battery.transform.position + Vector3.up * 0.015f : transform.position;

        activeSpark = sparkPrefab != null
            ? Instantiate(sparkPrefab, pos, Quaternion.identity)
            : CircuitEffects.CreateSparks(pos);
        activeSpark.Play();
        Destroy(activeSpark.gameObject, sparkLifetime);
    }

    private void StopSpark()
    {
        if (activeSpark == null) return;
        activeSpark.Stop();
        activeSpark = null;
    }

    private void UpdateSmoke(CircuitResult result)
    {
        // Humo sobre cada LED quemado que sigue en la mesa; se quita cuando ya no está quemado.
        var stale = new List<ElectronicComponent>();
        foreach (var kvp in smokes)
            if (kvp.Key == null || !result.deadLeds.Contains(kvp.Key)) stale.Add(kvp.Key);

        foreach (var key in stale)
        {
            if (smokes[key] != null) Destroy(smokes[key].gameObject);
            smokes.Remove(key);
        }

        foreach (var led in result.deadLeds)
        {
            if (led == null || smokes.ContainsKey(led)) continue;

            Transform card = led.transform.parent != null ? led.transform.parent : led.transform;
            ParticleSystem smoke = CircuitEffects.CreateSmoke(card.position + card.up * 0.02f);
            smoke.transform.SetParent(card, true); // sigue a la tarjeta y se oculta con ella
            smokes[led] = smoke;
        }
    }
}
