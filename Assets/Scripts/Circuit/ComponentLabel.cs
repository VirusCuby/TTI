using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Etiqueta de texto flotante sobre una tarjeta: muestra el valor del componente y la
/// corriente que pasa por él (ej. "470 ohm / 14.5 mA"). La crea CircuitManager en tiempo
/// de ejecución para cada componente detectado, así que no hay que agregar nada a la escena.
/// Siempre mira hacia la cámara y solo se ve mientras la tarjeta está detectada.
/// </summary>
public class ComponentLabel : MonoBehaviour
{
    private const float HeightAboveCard = 0.075f; // metros (la resistencia nueva mide ~5 cm de alto)
    private const float WorldScale = 0.016f;      // con fontSize 10, ~1.6 cm de alto por línea

    private static readonly Color Normal = Color.white;
    private static readonly Color Warning = new Color(1f, 0.65f, 0.15f);
    private static readonly Color Danger = new Color(1f, 0.3f, 0.3f);
    private static readonly Color Good = new Color(0.4f, 1f, 0.5f);

    private ElectronicComponent comp;
    private Transform anchor;
    private TextMeshPro text;
    private bool hidden;

    public static ComponentLabel Create(ElectronicComponent component)
    {
        var go = new GameObject("Etiqueta_" + component.name);
        var label = go.AddComponent<ComponentLabel>();
        label.comp = component;
        label.anchor = component.transform.parent != null ? component.transform.parent : component.transform;

        label.text = go.AddComponent<TextMeshPro>();
        label.text.fontSize = 10f;
        label.text.alignment = TextAlignmentOptions.Center;
        label.text.textWrappingMode = TextWrappingModes.NoWrap;
        label.text.color = Normal;
        label.text.outlineWidth = 0.25f;
        label.text.outlineColor = new Color32(0, 0, 0, 255);
        label.text.text = DefaultText(component);

        go.transform.localScale = Vector3.one * WorldScale;
        return label;
    }

    public void Show() { hidden = false; }
    public void Hide() { hidden = true; if (text != null) text.enabled = false; }

    private void LateUpdate()
    {
        if (comp == null) { Destroy(gameObject); return; }

        bool visible = !hidden && comp.gameObject.activeInHierarchy;
        if (text.enabled != visible) text.enabled = visible;
        if (!visible) return;

        transform.position = anchor.position + anchor.up * HeightAboveCard;

        Camera cam = Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, cam.transform.up);
    }

    private static string DefaultText(ElectronicComponent c)
    {
        switch (c.type)
        {
            case ComponentType.Battery:  return CircuitUnits.Volts(c.batteryVoltage);
            case ComponentType.Resistor: return CircuitUnits.Ohms(c.resistanceOhms);
            case ComponentType.Switch:   return c.isClosed ? "cerrado" : "abierto";
            default:                     return "LED";
        }
    }

    /// <summary>Actualiza el texto con el último resultado del cálculo eléctrico.</summary>
    public void Refresh(CircuitResult result)
    {
        if (comp == null || text == null) return;

        var sb = new StringBuilder();
        Color color = Normal;
        ElementReading r = default;
        bool hasReading = result.simulated && result.readings.TryGetValue(comp, out r);

        switch (comp.type)
        {
            case ComponentType.Battery:
                sb.Append(CircuitUnits.Volts(comp.batteryVoltage));
                if (hasReading && r.current > 0.0005f)
                {
                    sb.Append('\n').Append(CircuitUnits.Amps(r.current));
                    if (result.isShortCircuit) color = Danger;
                }
                break;

            case ComponentType.Resistor:
                sb.Append(CircuitUnits.Ohms(comp.resistanceOhms));
                if (hasReading && Mathf.Abs(r.current) > 0.0005f)
                {
                    sb.Append('\n').Append(CircuitUnits.Amps(r.current));
                    if (result.overheatedResistors.Contains(comp))
                    {
                        sb.Append("\n¡caliente!");
                        color = Danger;
                    }
                }
                break;

            case ComponentType.LED:
                if (comp.isBurned)
                {
                    sb.Append("¡QUEMADO!");
                    color = Danger;
                }
                else if (hasReading && r.current > 0.0003f)
                {
                    sb.Append(CircuitUnits.Amps(r.current));
                    if (result.overdrivenLeds.Contains(comp))
                    {
                        sb.Append("\n¡al límite!");
                        color = Warning;
                    }
                    else color = Good;
                }
                else
                {
                    sb.Append("LED");
                }
                break;

            case ComponentType.Switch:
                if (comp.isClosed) { sb.Append("cerrado"); color = Good; }
                else { sb.Append("abierto"); color = Warning; }
                break;
        }

        text.text = sb.ToString();
        text.color = color;
    }
}
