using UnityEngine;

/// <summary>
/// Hace visible el estado del interruptor sobre el propio modelo 3D:
///   - Tiñe el modelo: verde cuando está cerrado, rojo cuando está abierto.
///   - Opcionalmente gira una palanca (un hijo del modelo) entre dos ángulos.
///
/// Se agrega en el mismo GameObject que ElectronicComponent (tipo Switch).
/// Lee <see cref="ElectronicComponent.isClosed"/> cada frame, así que reacciona
/// igual si el estado cambia por un tap en pantalla o a mano en el Inspector.
/// </summary>
[RequireComponent(typeof(ElectronicComponent))]
[DisallowMultipleComponent]
public class SwitchVisual : MonoBehaviour
{
    [Header("Tinte del modelo")]
    [Tooltip("Si se deja vacío, usa todos los Renderer del modelo y de sus hijos.")]
    [SerializeField] private Renderer[] tintRenderers;
    [SerializeField] private Color closedColor = new Color(0.2f, 0.85f, 0.3f);
    [SerializeField] private Color openColor = new Color(0.9f, 0.25f, 0.2f);

    [Header("Palanca (opcional)")]
    [Tooltip("Hijo del modelo que se inclina al abrir/cerrar. Si se deja vacío, solo se aplica el tinte.")]
    [SerializeField] private Transform lever;
    [Tooltip("Rotación local (grados) de la palanca con el interruptor cerrado.")]
    [SerializeField] private Vector3 leverClosedEuler = Vector3.zero;
    [Tooltip("Rotación local (grados) de la palanca con el interruptor abierto.")]
    [SerializeField] private Vector3 leverOpenEuler = new Vector3(0f, 0f, 35f);
    [SerializeField] private float leverSpeed = 360f; // grados por segundo

    private static readonly int ColorId = Shader.PropertyToID("_Color");      // pipeline Built-in
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP/HDRP

    private ElectronicComponent component;
    private MaterialPropertyBlock propertyBlock;
    private bool? lastClosed; // null = todavía no se aplicó ningún estado

    private void Awake()
    {
        component = GetComponent<ElectronicComponent>();
        propertyBlock = new MaterialPropertyBlock();

        if (tintRenderers == null || tintRenderers.Length == 0)
            tintRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnEnable()
    {
        // La tarjeta puede reaparecer (el handler de Vuforia activa/desactiva
        // el modelo): se reaplica el estado actual desde cero.
        lastClosed = null;
    }

    private void Update()
    {
        bool closed = component.isClosed;

        if (lastClosed != closed)
        {
            lastClosed = closed;
            ApplyTint(closed ? closedColor : openColor);
        }

        if (lever != null)
        {
            Quaternion target = Quaternion.Euler(closed ? leverClosedEuler : leverOpenEuler);
            lever.localRotation = Quaternion.RotateTowards(lever.localRotation, target, leverSpeed * Time.deltaTime);
        }
    }

    private void ApplyTint(Color color)
    {
        foreach (Renderer r in tintRenderers)
        {
            if (r == null) continue;
            // MaterialPropertyBlock evita crear una instancia de material por interruptor.
            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ColorId, color);
            propertyBlock.SetColor(BaseColorId, color);
            r.SetPropertyBlock(propertyBlock);
        }
    }
}
