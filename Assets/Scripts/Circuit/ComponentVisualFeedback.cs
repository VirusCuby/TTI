using UnityEngine;

/// <summary>
/// Se agrega al mismo GameObject que ElectronicComponent, en cada prefab que
/// necesite reaccionar visualmente a la evaluación del circuito:
///   - El LED usa SetLit() para encender/apagar su emisión.
///   - Cualquier componente puede usar SetFault() para mostrar un halo/flecha
///     cuando el CircuitManager lo señala como responsable de un error
///     (por ahora, un LED en polaridad invertida).
/// Ambos campos son opcionales: un componente que no tenga LED ni halo
/// asignado simplemente ignora la llamada.
/// </summary>
[RequireComponent(typeof(ElectronicComponent))]
public class ComponentVisualFeedback : MonoBehaviour
{
    [Header("Encendido (solo LED)")]
    [Tooltip("Si se deja vacío (o con entradas en blanco), se usan todos los Renderer del modelo y sus hijos, " +
             "excepto los del halo de error. Requiere que el material tenga la casilla Emission activada.")]
    [SerializeField] private Renderer[] emissiveRenderers;
    [SerializeField] private Color emissionOnColor = Color.white;
    [SerializeField] private float emissionOnIntensity = 2f;

    [Header("Halo de error")]
    [Tooltip("GameObject (anillo, flecha, etc.) desactivado por defecto en el prefab.")]
    [SerializeField] private GameObject faultHalo;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock propertyBlock;

    private bool renderersResolved;

    private void Awake() => EnsureInitialized();

    private void EnsureInitialized()
    {
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        if (renderersResolved) return;
        renderersResolved = true;

        // Una lista con solo entradas vacías cuenta como "sin asignar": pasa
        // cuando el renderer vive dentro de un FBX y no se pudo arrastrar al campo.
        bool hasAssigned = false;
        if (emissiveRenderers != null)
            foreach (Renderer r in emissiveRenderers)
                if (r != null) { hasAssigned = true; break; }
        if (hasAssigned) return;

        var found = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            // El halo de error no debe encenderse como si fuera parte del LED.
            if (faultHalo != null && r.transform.IsChildOf(faultHalo.transform)) continue;
            found.Add(r);
        }
        emissiveRenderers = found.ToArray();
    }

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private bool burned;

    public void SetLit(bool on) => SetBrightness(on ? 1f : 0f);

    /// <summary>Enciende el LED con un brillo de 0 (apagado) a 1 (máximo). Un LED quemado no brilla.</summary>
    public void SetBrightness(float brightness)
    {
        EnsureInitialized();
        if (emissiveRenderers == null) return;

        float b = burned ? 0f : Mathf.Clamp01(brightness);
        Color emission = b > 0f ? emissionOnColor * (emissionOnIntensity * b) : Color.black;
        foreach (Renderer r in emissiveRenderers)
        {
            if (r == null) continue;
            // MaterialPropertyBlock evita crear una instancia de material por LED.
            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorId, emission);
            r.SetPropertyBlock(propertyBlock);
        }
    }

    /// <summary>Marca el LED como quemado (oscuro y sin brillo) o lo restaura.</summary>
    public void SetBurned(bool value)
    {
        EnsureInitialized();
        if (burned == value) return;
        burned = value;
        if (emissiveRenderers == null) return;

        foreach (Renderer r in emissiveRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(propertyBlock);
            if (burned)
            {
                Color dark = new Color(0.07f, 0.06f, 0.06f);
                propertyBlock.SetColor(ColorId, dark);
                propertyBlock.SetColor(BaseColorId, dark);
                propertyBlock.SetColor(EmissionColorId, Color.black);
            }
            else
            {
                // Vuelve al color del material (no se puede "borrar" una propiedad del bloque).
                Material m = r.sharedMaterial;
                if (m != null && m.HasProperty(ColorId)) propertyBlock.SetColor(ColorId, m.GetColor(ColorId));
                if (m != null && m.HasProperty(BaseColorId)) propertyBlock.SetColor(BaseColorId, m.GetColor(BaseColorId));
                propertyBlock.SetColor(EmissionColorId, Color.black);
            }
            r.SetPropertyBlock(propertyBlock);
        }
    }

    public bool IsBurned => burned;

    public void SetFault(bool active)
    {
        if (faultHalo != null) faultHalo.SetActive(active);
    }
}
