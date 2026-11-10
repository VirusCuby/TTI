using UnityEngine;

/// <summary>
/// Hace visible el estado del interruptor sobre el propio modelo 3D:
///   - Anima una palanca (un hijo del modelo) entre cerrada y abierta, con un
///     pequeño rebote de resorte al llegar (se siente como un interruptor real).
///   - Tiñe y hace brillar un indicador: verde cuando está cerrado, rojo cuando
///     está abierto. Solo se tiñen los renderers indicados en <c>tintRenderers</c>
///     (el resto del modelo conserva sus colores). Si se deja vacío y no hay
///     palanca, se tiñe todo el modelo (comportamiento del modelo antiguo).
///
/// Se agrega en el mismo GameObject que ElectronicComponent (tipo Switch).
/// Lee <see cref="ElectronicComponent.isClosed"/> cada frame, así que reacciona
/// igual si el estado cambia por un tap en pantalla o a mano en el Inspector.
/// </summary>
[RequireComponent(typeof(ElectronicComponent))]
[DisallowMultipleComponent]
public class SwitchVisual : MonoBehaviour
{
    [Header("Tinte / indicador")]
    [Tooltip("Renderers que se tiñen (el indicador). Si se deja vacío y no hay palanca, se tiñen todos los del modelo.")]
    [SerializeField] private Renderer[] tintRenderers;
    [SerializeField] private Color closedColor = new Color(0.2f, 0.85f, 0.3f);
    [SerializeField] private Color openColor = new Color(0.9f, 0.25f, 0.2f);
    [Tooltip("Multiplicador del brillo (emisión) del indicador. 0 = sin brillo.")]
    [SerializeField] private float glow = 1.2f;

    [Header("Palanca (opcional)")]
    [Tooltip("Hijo del modelo que gira al abrir/cerrar. Su origen debe estar en la bisagra. Vacío = solo tinte.")]
    [SerializeField] private Transform lever;
    [Tooltip("Eje de giro, en el espacio del padre de la palanca (la tarjeta). La bisagra de este modelo es el eje Z.")]
    [SerializeField] private Vector3 leverAxis = Vector3.forward;
    [Tooltip("Grados que gira desde la posición cerrada hasta la abierta. El signo decide hacia dónde sube.")]
    [SerializeField] private float leverOpenAngle = 50f;
    [Tooltip("Rigidez del resorte: más alto = más rápido.")]
    [SerializeField] private float leverStiffness = 420f;
    [Tooltip("Amortiguación: más bajo = más rebote. ~2*sqrt(rigidez) = sin rebote.")]
    [SerializeField] private float leverDamping = 24f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");              // pipeline Built-in
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");      // URP/HDRP
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private ElectronicComponent component;
    private MaterialPropertyBlock propertyBlock;
    private bool? lastClosed; // null = todavía no se aplicó ningún estado

    private Quaternion leverRest;   // rotación local de la palanca con el interruptor cerrado
    private float leverAngle;       // ángulo actual (0 = cerrado, leverOpenAngle = abierto)
    private float leverVelocity;
    private bool leverInitialized;

    private void Awake()
    {
        component = GetComponent<ElectronicComponent>();
        propertyBlock = new MaterialPropertyBlock();

        if (lever != null)
            leverRest = lever.localRotation;

        if ((tintRenderers == null || tintRenderers.Length == 0) && lever == null)
            tintRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnEnable()
    {
        // La tarjeta puede reaparecer (el handler de Vuforia activa/desactiva
        // el modelo): se reaplica el estado actual desde cero, sin animar.
        lastClosed = null;
        leverInitialized = false;
    }

    private void Update()
    {
        bool closed = component.isClosed;

        if (lastClosed != closed)
        {
            lastClosed = closed;
            ApplyTint(closed ? closedColor : openColor);
        }

        UpdateLever(closed);
    }

    private void UpdateLever(bool closed)
    {
        if (lever == null) return;

        float target = closed ? 0f : leverOpenAngle;

        if (!leverInitialized)
        {
            // Primera vez que se ve la tarjeta: aparece ya en su posición, sin moverse.
            leverAngle = target;
            leverVelocity = 0f;
            leverInitialized = true;
        }
        else
        {
            // Resorte amortiguado. Se limita dt para que un frame lento no desestabilice la integración.
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            float accel = (target - leverAngle) * leverStiffness - leverVelocity * leverDamping;
            leverVelocity += accel * dt;
            leverAngle += leverVelocity * dt;
        }

        lever.localRotation = Quaternion.AngleAxis(leverAngle, leverAxis.normalized) * leverRest;
    }

    private void ApplyTint(Color color)
    {
        if (tintRenderers == null) return;

        foreach (Renderer r in tintRenderers)
        {
            if (r == null) continue;
            // MaterialPropertyBlock evita crear una instancia de material por interruptor.
            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ColorId, color);
            propertyBlock.SetColor(BaseColorId, color);
            if (glow > 0f) propertyBlock.SetColor(EmissionId, color * glow);
            r.SetPropertyBlock(propertyBlock);
        }
    }
}
