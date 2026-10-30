using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Pruebas de integración (Edit Mode, sin necesidad de entrar a Play Mode) para
/// CircuitManager. Arman el grafo con GameObjects reales (Node + ElectronicComponent)
/// tal como ocurriría en la mesa, y verifican que Evaluate() diagnostique
/// correctamente cada escenario descrito en CONTEXTO_PROYECTO.md (punto 4):
/// abierto/cerrado, cortocircuito, polaridad invertida en el LED, serie,
/// paralelo y las compuertas AND/OR con interruptores.
///
/// Como corren en Edit Mode, Update()/LateUpdate() no se ejecutan solos: las
/// conexiones por proximidad se fuerzan a mano con Node.ForceScan() y el
/// circuito se evalúa llamando directamente a CircuitManager.Evaluate()
/// (sin depender del evento OnCircuitEvaluated ni del flag "dirty").
///
/// Tampoco se puede depender de Awake()/OnEnable(): en Edit Mode Tests,
/// Unity difiere esos callbacks a un tick del editor que nunca llega dentro
/// de un [Test] síncrono (se comprobó viendo que CircuitManager.Instance
/// seguía en null justo después de AddComponent). Por eso el singleton se
/// fija a mano (SetInstanceForTests) y cada Node/ElectronicComponent se
/// registra explícitamente en vez de esperar a que su propio OnEnable lo haga.
/// </summary>
public class CircuitManagerTests
{
    private CircuitManager manager;
    private readonly List<GameObject> spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        manager = new GameObject("CircuitManager").AddComponent<CircuitManager>();
        spawned.Add(manager.gameObject);
        CircuitManager.SetInstanceForTests(manager);
    }

    [TearDown]
    public void TearDown()
    {
        CircuitManager.SetInstanceForTests(null);
        foreach (var go in spawned)
            if (go != null) Object.DestroyImmediate(go);
        spawned.Clear();
    }

    // ---- Helpers de construcción del grafo ------------------------------

    private ElectronicComponent CreateComponent(string name, ComponentType type, Vector3 posA, Vector3 posB, bool isClosed = false)
    {
        var go = new GameObject(name);
        spawned.Add(go);
        var comp = go.AddComponent<ElectronicComponent>();
        comp.type = type;
        comp.isClosed = isClosed;

        // Estas pruebas verifican la LÓGICA de conexión (abierto/cerrado/polaridad/compuertas),
        // no la corriente. Varias arman un LED sin resistencia, que el simulador quemaría;
        // se desactiva la quemadura aquí. Las pruebas eléctricas están en CircuitSimulationTests.
        if (type == ComponentType.LED) comp.ledBurnCurrent = 1000f;

        bool polarized = type == ComponentType.Battery || type == ComponentType.LED;
        comp.positiveOrA = CreateNode(go, posA, polarized ? NodeRole.Positive : NodeRole.TerminalA, comp);
        comp.negativeOrB = CreateNode(go, posB, polarized ? NodeRole.Negative : NodeRole.TerminalB, comp);

        manager.RegisterComponent(comp); // explícito: OnEnable no es fiable en Edit Mode tests
        return comp;
    }

    private Node CreateNode(GameObject parent, Vector3 pos, NodeRole role, ElectronicComponent owner)
    {
        var nodeGo = new GameObject(parent.name + "_" + role);
        nodeGo.transform.SetParent(parent.transform);
        nodeGo.transform.position = pos;
        var node = nodeGo.AddComponent<Node>();
        node.role = role;
        node.owner = owner;
        manager.RegisterNode(node); // explícito, mismo motivo que en CreateComponent
        return node;
    }

    /// <summary>Fuerza el escaneo de proximidad de cada nodo dado (cada uno barre contra toda la mesa).</summary>
    private void ConnectAll(params Node[] nodes)
    {
        foreach (var n in nodes)
            n.ForceScan();
    }

    // ---- Casos base: sin batería, abierto, cerrado, corto, polaridad ----

    [Test]
    public void SinBateria_DevuelveNoBattery()
    {
        var result = manager.Evaluate();
        Assert.AreEqual(CircuitStatus.NoBattery, result.status);
    }

    [Test]
    public void InterruptorAbierto_CircuitoAbierto()
    {
        var battery = CreateComponent("Bateria", ComponentType.Battery, Vector3.zero, new Vector3(1, 0, 0));
        var sw = CreateComponent("Interruptor", ComponentType.Switch, Vector3.zero, new Vector3(1, 0, 0), isClosed: false);

        ConnectAll(battery.positiveOrA, sw.positiveOrA);
        ConnectAll(battery.negativeOrB, sw.negativeOrB);

        var result = manager.Evaluate();
        Assert.AreEqual(CircuitStatus.Open, result.status);
    }

    [Test]
    public void SerieConResistenciaEInterruptorCerrado_CircuitoCerrado()
    {
        var battery = CreateComponent("Bateria", ComponentType.Battery, Vector3.zero, new Vector3(3, 0, 0));
        var resistor = CreateComponent("Resistencia", ComponentType.Resistor, Vector3.zero, new Vector3(1, 0, 0));
        var sw = CreateComponent("Interruptor", ComponentType.Switch, new Vector3(1, 0, 0), new Vector3(3, 0, 0), isClosed: true);

        ConnectAll(battery.positiveOrA, resistor.positiveOrA);
        ConnectAll(resistor.negativeOrB, sw.positiveOrA);
        ConnectAll(sw.negativeOrB, battery.negativeOrB);

        var result = manager.Evaluate();
        Assert.AreEqual(CircuitStatus.Closed, result.status);
    }

    [Test]
    public void InterruptorCerradoSinResistencia_Cortocircuito()
    {
        // + de la batería directo al interruptor y de vuelta al - sin nada que limite la corriente.
        var battery = CreateComponent("Bateria", ComponentType.Battery, Vector3.zero, new Vector3(1, 0, 0));
        var sw = CreateComponent("Interruptor", ComponentType.Switch, Vector3.zero, new Vector3(1, 0, 0), isClosed: true);

        ConnectAll(battery.positiveOrA, sw.positiveOrA);
        ConnectAll(battery.negativeOrB, sw.negativeOrB);

        var result = manager.Evaluate();
        Assert.IsTrue(result.isShortCircuit);
        Assert.AreEqual(CircuitStatus.ShortCircuit, result.status);
    }

    [Test]
    public void LedInvertido_PolaridadInvertida()
    {
        var battery = CreateComponent("Bateria", ComponentType.Battery, Vector3.zero, new Vector3(1, 0, 0));
        // Cátodo (negativeOrB) del lado del + de la batería: polaridad invertida.
        var led = CreateComponent("LED", ComponentType.LED, new Vector3(1, 0, 0), Vector3.zero);

        ConnectAll(battery.positiveOrA, led.negativeOrB);
        ConnectAll(battery.negativeOrB, led.positiveOrA);

        var result = manager.Evaluate();
        Assert.AreEqual(CircuitStatus.ReversedPolarity, result.status);
        Assert.AreEqual(led, result.faultyLed);
    }

    [Test]
    public void LedEnPolaridadCorrecta_EnciendeEnCircuitoCerrado()
    {
        var battery = CreateComponent("Bateria", ComponentType.Battery, Vector3.zero, new Vector3(1, 0, 0));
        var led = CreateComponent("LED", ComponentType.LED, Vector3.zero, new Vector3(1, 0, 0));

        ConnectAll(battery.positiveOrA, led.positiveOrA);
        ConnectAll(battery.negativeOrB, led.negativeOrB);

        var result = manager.Evaluate();
        Assert.AreEqual(CircuitStatus.Closed, result.status);
        Assert.IsTrue(result.ledStates[led]);
    }

    // ---- Serie / paralelo con interruptores == compuertas AND / OR ------

    [Test]
    public void DosInterruptoresEnSerie_CompuertaAND()
    {
        var battery = CreateComponent("Bateria", ComponentType.Battery, Vector3.zero, new Vector3(4, 0, 0));
        var sw1 = CreateComponent("Interruptor1", ComponentType.Switch, new Vector3(0, 0, 0), new Vector3(1, 0, 0));
        var sw2 = CreateComponent("Interruptor2", ComponentType.Switch, new Vector3(1, 0, 0), new Vector3(2, 0, 0));
        var led = CreateComponent("LED", ComponentType.LED, new Vector3(2, 0, 0), new Vector3(4, 0, 0));

        ConnectAll(battery.positiveOrA, sw1.positiveOrA);
        ConnectAll(sw1.negativeOrB, sw2.positiveOrA);
        ConnectAll(sw2.negativeOrB, led.positiveOrA);
        ConnectAll(led.negativeOrB, battery.negativeOrB);

        sw1.isClosed = false; sw2.isClosed = false;
        Assert.AreEqual(CircuitStatus.Open, manager.Evaluate().status, "ambos abiertos");

        sw1.isClosed = true; sw2.isClosed = false;
        Assert.AreEqual(CircuitStatus.Open, manager.Evaluate().status, "solo uno cerrado");

        sw1.isClosed = false; sw2.isClosed = true;
        Assert.AreEqual(CircuitStatus.Open, manager.Evaluate().status, "solo el otro cerrado");

        sw1.isClosed = true; sw2.isClosed = true;
        var result = manager.Evaluate();
        Assert.AreEqual(CircuitStatus.Closed, result.status, "ambos cerrados");
        Assert.IsTrue(result.ledStates[led]);
    }

    [Test]
    public void DosInterruptoresEnParalelo_CompuertaOR()
    {
        var battery = CreateComponent("Bateria", ComponentType.Battery, Vector3.zero, new Vector3(2, 0, 0));
        var sw1 = CreateComponent("Interruptor1", ComponentType.Switch, Vector3.zero, new Vector3(1, 0, 0));
        var sw2 = CreateComponent("Interruptor2", ComponentType.Switch, Vector3.zero, new Vector3(1, 0, 0));
        var led = CreateComponent("LED", ComponentType.LED, new Vector3(1, 0, 0), new Vector3(2, 0, 0));

        ConnectAll(battery.positiveOrA, sw1.positiveOrA, sw2.positiveOrA);
        ConnectAll(sw1.negativeOrB, sw2.negativeOrB, led.positiveOrA);
        ConnectAll(led.negativeOrB, battery.negativeOrB);

        sw1.isClosed = false; sw2.isClosed = false;
        Assert.AreEqual(CircuitStatus.Open, manager.Evaluate().status, "ambos abiertos");

        sw1.isClosed = true; sw2.isClosed = false;
        Assert.AreEqual(CircuitStatus.Closed, manager.Evaluate().status, "solo el primero cerrado");

        sw1.isClosed = false; sw2.isClosed = true;
        Assert.AreEqual(CircuitStatus.Closed, manager.Evaluate().status, "solo el segundo cerrado");

        sw1.isClosed = true; sw2.isClosed = true;
        Assert.AreEqual(CircuitStatus.Closed, manager.Evaluate().status, "ambos cerrados");
    }

    // ---- Distancias grandes y regla del terminal más cercano ------------

    [Test]
    public void TerminalesA20cm_ConectanPorqueElMaximoEs25cm()
    {
        // Con el umbral antiguo de 3-12 cm esto no conectaba.
        var battery = CreateComponent("Bateria", ComponentType.Battery, Vector3.zero, new Vector3(1, 0, 0));
        var sw = CreateComponent("Interruptor", ComponentType.Switch, new Vector3(0.20f, 0, 0), new Vector3(1.20f, 0, 0));

        ConnectAll(battery.positiveOrA, battery.negativeOrB);

        Assert.AreEqual(1, battery.positiveOrA.ConnectedNodes.Count);
        Assert.AreSame(sw.positiveOrA, battery.positiveOrA.ConnectedNodes[0]);
        Assert.AreEqual(1, battery.negativeOrB.ConnectedNodes.Count);
        Assert.AreSame(sw.negativeOrB, battery.negativeOrB.ConnectedNodes[0]);
    }

    [Test]
    public void ReglaDelMasCercano_NoUneUnTerminalMasLejano()
    {
        // La batería (+) tiene el terminal A del interruptor 1 a 10 cm y el del interruptor 2 a 20 cm:
        // ambos caen dentro del máximo, pero solo debe unirse al más cercano.
        var battery = CreateComponent("Bateria", ComponentType.Battery, Vector3.zero, new Vector3(5, 0, 0));
        var sw1 = CreateComponent("Interruptor1", ComponentType.Switch, new Vector3(0.10f, 0, 0), new Vector3(3, 0, 0));
        var sw2 = CreateComponent("Interruptor2", ComponentType.Switch, new Vector3(0.20f, 0, 0), new Vector3(4, 0, 0));

        ConnectAll(battery.positiveOrA, sw1.positiveOrA, sw2.positiveOrA);

        Assert.AreEqual(1, battery.positiveOrA.ConnectedNodes.Count);
        Assert.AreSame(sw1.positiveOrA, battery.positiveOrA.ConnectedNodes[0]);
    }

    [Test]
    public void PuntoDeUnionConTresTerminales_ToleraDiferenciasDeDistancia()
    {
        // La batería (+) en el origen, un terminal a 10 cm a un lado y otro a 11.5 cm al lado contrario
        // (15 % de diferencia): ambos deben unirse, como en un circuito en paralelo.
        var battery = CreateComponent("Bateria", ComponentType.Battery, Vector3.zero, new Vector3(5, 0, 0));
        var sw1 = CreateComponent("Interruptor1", ComponentType.Switch, new Vector3(0.10f, 0, 0), new Vector3(3, 0, 0));
        var sw2 = CreateComponent("Interruptor2", ComponentType.Switch, new Vector3(-0.115f, 0, 0), new Vector3(4, 0, 0));

        ConnectAll(battery.positiveOrA, sw1.positiveOrA, sw2.positiveOrA);

        Assert.AreEqual(2, battery.positiveOrA.ConnectedNodes.Count);
    }

    [Test]
    public void TerminalesMasAlejadosQueElMaximo_NoConectan()
    {
        var battery = CreateComponent("Bateria", ComponentType.Battery, Vector3.zero, new Vector3(5, 0, 0));
        var sw = CreateComponent("Interruptor", ComponentType.Switch, new Vector3(0.40f, 0, 0), new Vector3(6, 0, 0));

        ConnectAll(battery.positiveOrA);

        Assert.AreEqual(0, battery.positiveOrA.ConnectedNodes.Count);
    }
}
