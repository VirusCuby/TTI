using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Permite alternar un interruptor tocándolo en pantalla: tap en el celular,
/// clic en el Editor. Lanza un rayo desde la cámara hacia el punto tocado;
/// si golpea un ElectronicComponent de tipo Switch, llama a su
/// ToggleSwitch(). Requiere que el modelo del interruptor tenga un Collider
/// (los primitivos de Unity como Cube ya lo traen por defecto).
///
/// Colocar en un único GameObject de la escena (por ejemplo junto al
/// CircuitFeedback), no en las tarjetas.
/// </summary>
public class TapToToggleSwitch : MonoBehaviour
{
    [Tooltip("Si se deja vacío, usa Camera.main (la ARCamera de Vuforia se marca como MainCamera).")]
    [SerializeField] private Camera arCamera;

    private void Update()
    {
        if (!TryGetTapThisFrame(out Vector2 screenPos, out int touchId))
            return;

        // Ignora el toque si cayó sobre un elemento de UI (ej. el StatusText),
        // para no confundir "tocar el texto" con "tocar el interruptor".
        if (IsOverUI(touchId))
            return;

        Camera cam = arCamera != null ? arCamera : Camera.main;
        if (cam == null) return;

        // 1) El interruptor se toca directamente sobre su modelo (collider).
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            var component = hit.collider.GetComponentInParent<ElectronicComponent>();
            if (component != null && component.type == ComponentType.Switch)
            {
                component.ToggleSwitch();
                CircuitSounds.Click();
                return;
            }
        }

        // 2) La resistencia y la batería se tocan "cerca": se elige la tarjeta más próxima al dedo.
        //    Así no dependen de que su modelo tenga collider ni de que sea fácil acertarle.
        ElectronicComponent near = FindNearestOnScreen(cam, screenPos);
        if (near == null) return;
        if (near.type == ComponentType.Resistor) near.CycleResistance();
        else if (near.type == ComponentType.Battery) near.CycleVoltage();
        CircuitSounds.Click();
    }

    private static ElectronicComponent FindNearestOnScreen(Camera cam, Vector2 screenPos)
    {
        var manager = CircuitManager.Instance;
        if (manager == null) return null;

        float best = Mathf.Max(90f, Screen.height * 0.07f); // radio máximo de toque, en píxeles
        ElectronicComponent found = null;

        foreach (var c in manager.Components)
        {
            if (c.type != ComponentType.Resistor && c.type != ComponentType.Battery) continue;

            Transform card = c.transform.parent != null ? c.transform.parent : c.transform;
            Vector3 sp = cam.WorldToScreenPoint(card.position + card.up * 0.02f);
            if (sp.z <= 0f) continue; // detrás de la cámara

            float d = Vector2.Distance(new Vector2(sp.x, sp.y), screenPos);
            if (d < best) { best = d; found = c; }
        }
        return found;
    }

    /// <summary>Detecta un tap/clic nuevo en este frame, ya sea por touch (celular) o mouse (Editor).</summary>
    private static bool TryGetTapThisFrame(out Vector2 screenPos, out int touchId)
    {
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            screenPos = Input.GetTouch(0).position;
            touchId = Input.GetTouch(0).fingerId;
            return true;
        }

        if (Input.GetMouseButtonDown(0))
        {
            screenPos = Input.mousePosition;
            touchId = -1;
            return true;
        }

        screenPos = default;
        touchId = -1;
        return false;
    }

    private static bool IsOverUI(int touchId)
    {
        if (EventSystem.current == null) return false;
        return touchId >= 0
            ? EventSystem.current.IsPointerOverGameObject(touchId)
            : EventSystem.current.IsPointerOverGameObject();
    }
}
