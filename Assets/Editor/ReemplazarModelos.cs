using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Herramienta de Editor: cambia el modelo visible de la Batería, el LED y la
/// Resistencia por los de Assets/Modelos_nuevos, sin tocar la lógica del circuito.
///
/// Por cada *_Modelo de la escena:
///   - Oculta (NO borra) el modelo anterior y lo renombra "(viejo) ...".
///   - Instancia el FBX nuevo como hijo, con escala mundial 1 y centrado en la
///     tarjeta (los FBX ya vienen preparados para encajar en los terminales).
///   - Asigna materiales propios (Standard) en Assets/Modelos_nuevos/Materiales.
///   - Quita los colliders del *_Modelo (solo el interruptor necesita ser tocable
///     y los anteriores no coinciden con el modelo nuevo).
///   - Batería: pasa su escala desigual a uniforme conservando la posición mundial
///     de los Node. LED: apunta Emissive Renderers a la bombilla.
/// No toca ElectronicComponent, Node, eventos de Vuforia ni el ImageTarget.
/// Todo queda en el historial de Undo (Ctrl+Z). Después hay que guardar la escena.
/// </summary>
public static class ReemplazarModelos
{
    private const string Carpeta = "Assets/Modelos_nuevos";
    private const string CarpetaMateriales = Carpeta + "/Materiales";
    private const string PrefijoNuevo = "Nuevo_";
    private const string PrefijoViejo = "(viejo) ";
    private const float EscalaUniforme = 0.01f;

    private struct Spec
    {
        public Color color;   // color en el espacio en que se definió en Blender (se convierte a sRGB al crear el material)
        public float metal;
        public float brillo;
        public bool emisivo;

        public Spec(float r, float g, float b, float metal = 0f, float brillo = 0.4f, bool emisivo = false)
        {
            color = new Color(r, g, b);
            this.metal = metal;
            this.brillo = brillo;
            this.emisivo = emisivo;
        }
    }

    // Clave = nombre del objeto dentro del FBX
    private static readonly Dictionary<string, Spec> Materiales = new Dictionary<string, Spec>
    {
        { "Bateria_Cuerpo",      new Spec(0.80f, 0.50f, 0.12f, 0.35f, 0.55f) },
        { "Bateria_Cara",        new Spec(0.05f, 0.05f, 0.06f, 0.00f, 0.30f) },
        { "Bateria_ContactoPos", new Spec(0.85f, 0.12f, 0.12f, 0.60f, 0.60f) },
        { "Bateria_ContactoNeg", new Spec(0.12f, 0.14f, 0.30f, 0.60f, 0.60f) },

        { "LED_Bulbo_Emisivo",   new Spec(0.90f, 0.18f, 0.18f, 0.00f, 0.75f, emisivo: true) },
        { "LED_Interior",        new Spec(0.55f, 0.55f, 0.58f, 0.60f, 0.50f) },
        { "LED_Patas",           new Spec(0.72f, 0.72f, 0.75f, 0.80f, 0.60f) },

        { "Resistencia_blue",    new Spec(0.09f, 0.32f, 0.58f, 0.00f, 0.55f) },
        { "Resistencia_gold",    new Spec(0.80f, 0.28f, 0.03f, 0.00f, 0.45f) },
        { "Resistencia_green",   new Spec(0.01f, 0.12f, 0.08f, 0.00f, 0.45f) },
        { "Resistencia_red",     new Spec(0.16f, 0.01f, 0.00f, 0.00f, 0.45f) },
        { "Resistencia_red_1",   new Spec(0.16f, 0.01f, 0.00f, 0.00f, 0.45f) },
        { "Resistencia_silver",  new Spec(0.65f, 0.62f, 0.45f, 0.80f, 0.55f) },

        // Interruptor de balancín (modelo original "Switch Fig" con colores; se busca por prefijo: ver ObtenerMaterial)
        { "Interruptor_Base",           new Spec(0.10f, 0.22f, 0.45f, 0.00f, 0.50f) },
        { "Interruptor_Marco",          new Spec(0.05f, 0.06f, 0.08f, 0.00f, 0.45f) },
        { "Interruptor_Cuerpo",         new Spec(0.12f, 0.13f, 0.16f, 0.00f, 0.40f) },
        { "Interruptor_Pata",           new Spec(0.75f, 0.76f, 0.80f, 0.85f, 0.65f) },
        { "Interruptor_Metal",          new Spec(0.80f, 0.50f, 0.18f, 0.85f, 0.65f) },
        { "Interruptor_Tornillo",       new Spec(0.75f, 0.76f, 0.80f, 0.85f, 0.65f) },
        { "Interruptor_Palanca_Tecla",  new Spec(0.50f, 0.50f, 0.50f, 0.00f, 0.60f, emisivo: true) }, // se tiñe verde/rojo desde SwitchVisual
    };

    [MenuItem("Tools/Circuito/Reemplazar modelos (Batería, LED, Resistencia)")]
    private static void Ejecutar()
    {
        var log = new StringBuilder();
        int hechos = 0;

        var componentes = Object.FindObjectsByType<ElectronicComponent>(FindObjectsInactive.Include);
        foreach (var comp in componentes)
        {
            string fbx = null;
            switch (comp.type)
            {
                case ComponentType.Battery:  fbx = "Bateria_9V.fbx"; break;
                case ComponentType.LED:      fbx = "LED_5mm.fbx"; break;
                case ComponentType.Resistor: fbx = "Resistencia_axial.fbx"; break;
            }
            if (fbx == null) continue; // el interruptor no se cambia

            if (Reemplazar(comp, fbx, log))
            {
                hechos++;
                EditorSceneManager.MarkSceneDirty(comp.gameObject.scene);
            }
        }

        log.Insert(0, "Modelos reemplazados: " + hechos + "\n\n");
        log.AppendLine("\nAhora guarda la escena con Ctrl+S.");
        Debug.Log("[ReemplazarModelos]\n" + log);
        EditorUtility.DisplayDialog("Reemplazo de modelos", log.ToString(), "OK");
    }

    [MenuItem("Tools/Circuito/Reemplazar interruptor (balancín con tecla animada)")]
    private static void EjecutarInterruptor()
    {
        var log = new StringBuilder();
        int hechos = 0;

        var componentes = Object.FindObjectsByType<ElectronicComponent>(FindObjectsInactive.Include);
        foreach (var comp in componentes)
        {
            if (comp.type != ComponentType.Switch) continue;
            if (Reemplazar(comp, "Interruptor_balancin.fbx", log))
            {
                hechos++;
                EditorSceneManager.MarkSceneDirty(comp.gameObject.scene);
            }
        }

        log.Insert(0, "Interruptores reemplazados: " + hechos + "\n\n");
        log.AppendLine("\nAhora guarda la escena con Ctrl+S.");
        Debug.Log("[ReemplazarModelos]\n" + log);
        EditorUtility.DisplayDialog("Reemplazo del interruptor", log.ToString(), "OK");
    }

    [MenuItem("Tools/Circuito/Volver a los modelos viejos (solo la parte visual)")]
    private static void Revertir()
    {
        int n = 0;
        var componentes = Object.FindObjectsByType<ElectronicComponent>(FindObjectsInactive.Include);
        foreach (var comp in componentes)
        {
            var hijos = new List<Transform>();
            foreach (Transform h in comp.transform) hijos.Add(h);

            foreach (Transform h in hijos)
            {
                if (h.name.StartsWith(PrefijoNuevo))
                {
                    Undo.DestroyObjectImmediate(h.gameObject);
                    n++;
                }
                else if (h.name.StartsWith(PrefijoViejo))
                {
                    Undo.RecordObject(h.gameObject, "Restaurar modelo viejo");
                    h.name = h.name.Substring(PrefijoViejo.Length);
                    h.gameObject.SetActive(true);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(h.gameObject);
                }
            }
            EditorSceneManager.MarkSceneDirty(comp.gameObject.scene);
        }
        Debug.Log("[ReemplazarModelos] Modelos nuevos quitados: " + n + ". (Los colliders quitados y la escala uniforme de la batería no se restauran: usa Ctrl+Z o la copia de seguridad.)");
    }

    private static bool Reemplazar(ElectronicComponent comp, string fbxNombre, StringBuilder log)
    {
        string ruta = Carpeta + "/" + fbxNombre;
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (asset == null)
        {
            log.AppendLine("✘ " + comp.name + ": no se encontró " + ruta + " (¿Unity ya terminó de importarlo?)");
            return false;
        }

        Transform raiz = comp.transform;
        Transform tarjeta = raiz.parent;
        if (tarjeta == null)
        {
            log.AppendLine("✘ " + comp.name + ": no tiene ImageTarget padre.");
            return false;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Reemplazar modelo " + comp.name);

        // 1) Quitar un reemplazo anterior, para poder volver a ejecutar la herramienta.
        var hijos = new List<Transform>();
        foreach (Transform h in raiz) hijos.Add(h);
        foreach (Transform h in hijos)
            if (h.name.StartsWith(PrefijoNuevo)) Undo.DestroyObjectImmediate(h.gameObject);

        // 2) Escala uniforme (la batería traía escala desigual) sin mover los terminales.
        NormalizarEscala(raiz);

        // 3) Ocultar (no borrar) el modelo anterior: todo hijo con renderers que no sea un terminal.
        hijos.Clear();
        foreach (Transform h in raiz) hijos.Add(h);
        foreach (Transform h in hijos)
        {
            if (h == null) continue;
            if (h.GetComponent<Node>() != null || h.name.StartsWith("Terminal")) continue;
            if (h.name.StartsWith(PrefijoViejo)) continue;
            if (h.GetComponentInChildren<Renderer>(true) == null) continue;

            Undo.RecordObject(h.gameObject, "Ocultar modelo viejo");
            h.name = PrefijoViejo + h.name;
            h.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(h.gameObject);
        }

        // 4) Instanciar el modelo nuevo, con escala mundial 1, sobre el origen de la tarjeta.
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, raiz);
        Undo.RegisterCreatedObjectUndo(go, "Nuevo modelo");
        go.name = PrefijoNuevo + Path.GetFileNameWithoutExtension(fbxNombre);
        go.transform.position = tarjeta.position;
        go.transform.rotation = tarjeta.rotation;
        Vector3 ls = raiz.lossyScale;
        go.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);

        // 5) Materiales propios.
        AsegurarCarpeta();
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            Material m = ObtenerMaterial(r.gameObject.name);
            if (m == null) continue;
            Undo.RecordObject(r, "Material");
            r.sharedMaterial = m;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }

        // 6) Colliders del *_Modelo: los viejos no coinciden con el modelo nuevo y pueden tapar un toque al interruptor.
        //    El interruptor SÍ conserva el suyo (es lo que se toca para abrir/cerrar).
        if (comp.type != ComponentType.Switch)
        {
            foreach (var col in raiz.GetComponents<Collider>())
                Undo.DestroyObjectImmediate(col);
        }

        // 7) LED: que solo brille la bombilla. Interruptor: palanca animada e indicador.
        if (comp.type == ComponentType.LED) ConfigurarLed(comp, go);
        if (comp.type == ComponentType.Switch) ConfigurarInterruptor(comp, go, log);

        log.AppendLine("✔ " + comp.name + " → " + go.name + "   tamaño aprox. (cm): " + MedidaCm(go));
        return true;
    }

    private static void NormalizarEscala(Transform raiz)
    {
        Vector3 s = raiz.localScale;
        if (Mathf.Abs(s.x - s.y) < 1e-6f && Mathf.Abs(s.y - s.z) < 1e-6f) return;

        var nodos = raiz.GetComponentsInChildren<Node>(true);
        var posiciones = new Vector3[nodos.Length];
        for (int i = 0; i < nodos.Length; i++) posiciones[i] = nodos[i].transform.position;

        Undo.RecordObject(raiz, "Escala uniforme");
        raiz.localScale = Vector3.one * EscalaUniforme;

        for (int i = 0; i < nodos.Length; i++)
        {
            Undo.RecordObject(nodos[i].transform, "Conservar terminal");
            nodos[i].transform.position = posiciones[i]; // la posición mundial del terminal no cambia
        }
    }

    private static void ConfigurarLed(ElectronicComponent comp, GameObject modelo)
    {
        var fb = comp.GetComponent<ComponentVisualFeedback>();
        if (fb == null) return;

        var bulbos = new List<Renderer>();
        foreach (var r in modelo.GetComponentsInChildren<Renderer>(true))
            if (r.gameObject.name.Contains("Emisivo")) bulbos.Add(r);

        var so = new SerializedObject(fb);
        var lista = so.FindProperty("emissiveRenderers");
        lista.arraySize = bulbos.Count;
        for (int i = 0; i < bulbos.Count; i++)
            lista.GetArrayElementAtIndex(i).objectReferenceValue = bulbos[i];
        so.FindProperty("emissionOnColor").colorValue = new Color(1f, 0.25f, 0.2f);
        so.FindProperty("emissionOnIntensity").floatValue = 2.5f;
        so.ApplyModifiedProperties();
    }

    /// <summary>
    /// Conecta SwitchVisual con el modelo nuevo: palanca = "Interruptor_Palanca" (pivote de la tecla),
    /// indicador = la propia tecla ("Interruptor_Palanca_Tecla", que se tiñe de verde/rojo).
    /// El signo del giro se decide probando que, al abrir, suba el lado +X de la tarjeta.
    /// </summary>
    private static void ConfigurarInterruptor(ElectronicComponent comp, GameObject modelo, StringBuilder log)
    {
        var visual = comp.GetComponent<SwitchVisual>();
        if (visual == null) visual = Undo.AddComponent<SwitchVisual>(comp.gameObject);

        Transform palanca = null;
        Renderer tecla = null;
        foreach (var t in modelo.GetComponentsInChildren<Transform>(true))
            if (t.name == "Interruptor_Palanca") palanca = t;
        foreach (var r in modelo.GetComponentsInChildren<Renderer>(true))
            if (r.gameObject.name.StartsWith("Interruptor_Palanca_Tecla")) tecla = r;

        if (palanca == null || tecla == null)
            log.AppendLine("   ⚠ no se encontró 'Interruptor_Palanca' / la tecla dentro del FBX: solo habrá tinte.");

        // Signo del giro: se prueba +22° y se mira si el lado +X de la tarjeta de la tecla sube; si baja, se invierte.
        float angulo = 22f;
        Vector3 eje = Vector3.forward;
        var mf = tecla != null ? tecla.GetComponent<MeshFilter>() : null;
        if (palanca != null && mf != null && mf.sharedMesh != null)
        {
            Transform tarjeta = comp.transform.parent;
            Vector3 derecha = tarjeta != null ? tarjeta.right : Vector3.right;
            Vector3 arriba = tarjeta != null ? tarjeta.up : Vector3.up;

            // esquina de la caja de la tecla más hacia +X de la tarjeta (en reposo)
            Bounds bb = mf.sharedMesh.bounds;
            Vector3 esquina = bb.center;
            float mejor = float.NegativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = bb.center + Vector3.Scale(bb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                float d = Vector3.Dot(tecla.transform.TransformPoint(c), derecha);
                if (d > mejor) { mejor = d; esquina = c; }
            }

            Quaternion reposo = palanca.localRotation;
            float altoCerrado = Vector3.Dot(tecla.transform.TransformPoint(esquina), arriba);
            palanca.localRotation = Quaternion.AngleAxis(angulo, eje) * reposo;
            float altoAbierto = Vector3.Dot(tecla.transform.TransformPoint(esquina), arriba);
            palanca.localRotation = reposo;
            if (altoAbierto < altoCerrado) angulo = -angulo;
            log.AppendLine("   tecla: eje Z, inclinación al abrir " + angulo + "°");
        }

        var so = new SerializedObject(visual);
        so.FindProperty("lever").objectReferenceValue = palanca;
        so.FindProperty("leverAxis").vector3Value = eje;
        so.FindProperty("leverOpenAngle").floatValue = angulo;
        var lista = so.FindProperty("tintRenderers");
        lista.arraySize = tecla != null ? 1 : 0;
        if (tecla != null) lista.GetArrayElementAtIndex(0).objectReferenceValue = tecla;
        so.ApplyModifiedProperties();

        // Zona táctil: que cubra toda la placa y la tecla (valores locales del *_Modelo, escala 0.009).
        var caja = comp.GetComponent<BoxCollider>();
        if (caja != null)
        {
            Undo.RecordObject(caja, "Collider del interruptor");
            caja.size = new Vector3(8f, 6f, 4.5f);
            caja.center = new Vector3(0f, 2.5f, 0f);
        }
    }

    private static Vector3 CentroMundo(Renderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        return mf != null && mf.sharedMesh != null
            ? r.transform.TransformPoint(mf.sharedMesh.bounds.center)
            : r.transform.position;
    }

    private static void AsegurarCarpeta()
    {
        if (!AssetDatabase.IsValidFolder(CarpetaMateriales))
            AssetDatabase.CreateFolder(Carpeta, "Materiales");
    }

    private static Material ObtenerMaterial(string nombre)
    {
        // Coincidencia exacta; si no, por prefijo más largo (p. ej. "Interruptor_Metal_A" -> "Interruptor_Metal").
        string clave = nombre;
        if (!Materiales.ContainsKey(clave))
        {
            clave = null;
            foreach (var k in Materiales.Keys)
                if (nombre.StartsWith(k) && (clave == null || k.Length > clave.Length)) clave = k;
            if (clave == null) return null;
        }
        Spec s = Materiales[clave];
        nombre = clave;

        string ruta = CarpetaMateriales + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, ruta);
        }

        m.SetColor("_Color", s.color.gamma);
        m.SetFloat("_Metallic", s.metal);
        m.SetFloat("_Glossiness", s.brillo);
        if (s.emisivo)
        {
            // Casi negro (no negro puro): con negro, el shader Standard apaga solo la casilla Emission.
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.02f, 0.02f, 0.02f));
        }
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    /// <summary>Tamaño aproximado del modelo en cm (a partir de las mallas, aunque estén inactivas).</summary>
    private static string MedidaCm(GameObject go)
    {
        bool iniciado = false;
        Bounds b = new Bounds();
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            Matrix4x4 m = mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 w = m.MultiplyPoint3x4(c);
                if (!iniciado) { b = new Bounds(w, Vector3.zero); iniciado = true; }
                else b.Encapsulate(w);
            }
        }
        if (!iniciado) return "?";
        Vector3 t = b.size * 100f;
        return t.x.ToString("0.0") + " × " + t.z.ToString("0.0") + " × " + t.y.ToString("0.0") + " (largo × ancho × alto)";
    }
}
