using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Pruebas del solver puro (CircuitSolver): Ley de Ohm con valores conocidos.
/// </summary>
public class CircuitSolverTests
{
    private static CircuitSolver.Element Src(int plus, int minus, double v, double rint) =>
        new CircuitSolver.Element { kind = CircuitSolver.Kind.Source, a = plus, b = minus, value = v, series = rint };
    private static CircuitSolver.Element Res(int a, int b, double r) =>
        new CircuitSolver.Element { kind = CircuitSolver.Kind.Resistor, a = a, b = b, value = r };
    private static CircuitSolver.Element Led(int anode, int cathode) =>
        new CircuitSolver.Element { kind = CircuitSolver.Kind.Diode, a = anode, b = cathode, value = 2.0, series = 10.0 };

    [Test]
    public void SerieResistenciaYLed_CorrienteDeLaLeyDeOhm()
    {
        var s = new CircuitSolver();
        s.Solve(3, 0, new List<CircuitSolver.Element> { Src(1, 0, 9, 1), Res(1, 2, 470), Led(2, 0) });
        Assert.AreEqual(7.0 / 481.0, s.Current[0], 1e-7);          // (9 - 2) / (1 + 470 + 10)
        Assert.AreEqual(7.0 / 481.0, s.Current[2], 1e-7);
        Assert.AreEqual(470.0 * 7.0 / 481.0, s.NodeVoltage[1] - s.NodeVoltage[2], 1e-5); // V = I * R
    }

    [Test]
    public void LedInvertido_NoConduce()
    {
        var s = new CircuitSolver();
        s.Solve(3, 0, new List<CircuitSolver.Element> { Src(1, 0, 9, 1), Res(1, 2, 470), Led(0, 2) });
        Assert.AreEqual(0.0, s.Current[2], 1e-9);
        Assert.IsFalse(s.DiodeOn[2]);
    }

    [Test]
    public void BateriaDe1_5V_NoAlcanzaParaUnLed()
    {
        var s = new CircuitSolver();
        s.Solve(3, 0, new List<CircuitSolver.Element> { Src(1, 0, 1.5, 1), Res(1, 2, 100), Led(2, 0) });
        Assert.IsFalse(s.DiodeOn[2]);
        Assert.AreEqual(0.0, s.Current[2], 1e-9);
    }

    [Test]
    public void DosResistenciasIgualesEnParalelo_ReparteLaCorriente()
    {
        var s = new CircuitSolver();
        s.Solve(2, 0, new List<CircuitSolver.Element> { Src(1, 0, 9, 1), Res(1, 0, 100), Res(1, 0, 100) });
        Assert.AreEqual(9.0 / 51.0, s.Current[0], 1e-7);            // Req = 50 ohm + 1 ohm internos
        Assert.AreEqual(s.Current[1], s.Current[2], 1e-9);
    }

    [Test]
    public void TrozoSinConexionALaFuente_NoDaNaN()
    {
        var s = new CircuitSolver();
        bool ok = s.Solve(5, 0, new List<CircuitSolver.Element> { Src(1, 0, 9, 1), Res(1, 0, 1000), Res(3, 4, 470) });
        Assert.IsTrue(ok);
        Assert.IsFalse(double.IsNaN(s.NodeVoltage[3]));
        Assert.AreEqual(0.0, s.Current[2], 1e-6);
    }
}

/// <summary>
/// Pruebas del cálculo eléctrico integrado en CircuitManager (voltajes, corrientes, brillo,
/// LED quemado). Mismo estilo que CircuitManagerTests: se arma el grafo con GameObjects y se
/// fuerzan las conexiones con Node.ForceScan() (en Edit Mode no corren Awake/OnEnable solos).
/// </summary>
public class CircuitSimulationTests
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

    private ElectronicComponent Make(string name, ComponentType type, float xA, float xB, bool closed = false)
    {
        var go = new GameObject(name);
        spawned.Add(go);
        var comp = go.AddComponent<ElectronicComponent>();
        comp.type = type;
        comp.isClosed = closed;

        bool polarized = type == ComponentType.Battery || type == ComponentType.LED;
        comp.positiveOrA = MakeNode(go, new Vector3(xA, 0, 0), polarized ? NodeRole.Positive : NodeRole.TerminalA, comp);
        comp.negativeOrB = MakeNode(go, new Vector3(xB, 0, 0), polarized ? NodeRole.Negative : NodeRole.TerminalB, comp);
        manager.RegisterComponent(comp);
        return comp;
    }

    private Node MakeNode(GameObject parent, Vector3 pos, NodeRole role, ElectronicComponent owner)
    {
        var nodeGo = new GameObject(parent.name + "_" + role);
        nodeGo.transform.SetParent(parent.transform);
        nodeGo.transform.position = pos;
        var node = nodeGo.AddComponent<Node>();
        node.role = role;
        node.owner = owner;
        manager.RegisterNode(node);
        return node;
    }

    private static void Link(params Node[] nodes)
    {
        foreach (var n in nodes) n.ForceScan();
    }

    /// <summary>Batería (0..3) -> resistencia (0..1) -> LED (1..3): la serie más simple que cierra.</summary>
    private void BuildSeries(out ElectronicComponent battery, out ElectronicComponent resistor, out ElectronicComponent led, float ohms)
    {
        battery = Make("Bateria", ComponentType.Battery, 0, 3);
        resistor = Make("Resistencia", ComponentType.Resistor, 0, 1);
        resistor.resistanceOhms = ohms;
        led = Make("LED", ComponentType.LED, 1, 3);
        Link(battery.positiveOrA, resistor.positiveOrA, resistor.negativeOrB, led.positiveOrA, led.negativeOrB, battery.negativeOrB);
    }

    [Test]
    public void SerieConResistenciaDe470_UsaLaLeyDeOhm()
    {
        BuildSeries(out var battery, out var resistor, out var led, 470f);

        var result = manager.Evaluate();

        Assert.AreEqual(CircuitStatus.Closed, result.status);
        Assert.IsTrue(result.simulated);
        float expected = 7f / 481f; // (9 V - 2 V) / (1 + 470 + 10) ohm
        Assert.AreEqual(expected, result.batteryCurrent, 1e-4f);
        Assert.AreEqual(expected, result.readings[led].current, 1e-4f);
        Assert.AreEqual(470f * expected, Mathf.Abs(result.readings[resistor].voltage), 1e-2f); // V = I * R
        Assert.AreEqual(expected / led.ledFullBrightCurrent, result.ledBrightness[led], 1e-2f);
        Assert.IsTrue(result.ledStates[led]);
        Assert.IsFalse(led.isBurned);
        Assert.AreEqual(0, result.overdrivenLeds.Count);
    }

    [Test]
    public void SerieConResistenciaDe150_DejaElLedAlLimitePeroNoLoQuema()
    {
        BuildSeries(out _, out _, out var led, 150f);

        var result = manager.Evaluate();

        Assert.AreEqual(CircuitStatus.Closed, result.status);
        Assert.AreEqual(7f / 161f, result.readings[led].current, 1e-4f); // 43.5 mA: sobre 35 mA (aviso), bajo 50 mA (no se quema)
        Assert.Contains(led, result.overdrivenLeds);
        Assert.IsFalse(led.isBurned);
    }

    [Test]
    public void LedSinResistencia_SeQuema_YDespuesElCircuitoQuedaAbierto()
    {
        var battery = Make("Bateria", ComponentType.Battery, 0, 1);
        var led = Make("LED", ComponentType.LED, 0, 1);
        Link(battery.positiveOrA, battery.negativeOrB, led.positiveOrA, led.negativeOrB);

        var first = manager.Evaluate();
        Assert.Contains(led, first.burnedLeds);
        Assert.IsTrue(led.isBurned);
        Assert.IsFalse(first.ledStates[led]);

        var second = manager.Evaluate();
        Assert.Contains(led, second.deadLeds);
        Assert.AreEqual(CircuitStatus.Open, second.status);
        Assert.AreEqual(0f, second.ledBrightness[led], 1e-6f);
    }

    [Test]
    public void LedQuemado_SeReemplazaRetirandoLaTarjeta()
    {
        var battery = Make("Bateria", ComponentType.Battery, 0, 1);
        var led = Make("LED", ComponentType.LED, 0, 1);
        Link(battery.positiveOrA, battery.negativeOrB, led.positiveOrA, led.negativeOrB);
        manager.Evaluate();
        Assert.IsTrue(led.isBurned);

        // Retirar la tarjeta = OnDisable; en Edit Mode no corre solo, así que se simula a mano.
        led.isBurned = false;
        manager.UnregisterComponent(led);
        manager.RegisterComponent(led);

        Assert.IsFalse(led.isBurned);
    }

    [Test]
    public void LedInvertido_NoConduceNiSeQuema()
    {
        var battery = Make("Bateria", ComponentType.Battery, 0, 3);
        var resistor = Make("Resistencia", ComponentType.Resistor, 0, 1);
        resistor.resistanceOhms = 470f;
        var led = Make("LED", ComponentType.LED, 3, 1); // ánodo y cátodo intercambiados
        Link(battery.positiveOrA, resistor.positiveOrA, resistor.negativeOrB, led.positiveOrA, led.negativeOrB, battery.negativeOrB);

        var result = manager.Evaluate();

        Assert.AreEqual(CircuitStatus.ReversedPolarity, result.status);
        Assert.IsFalse(result.ledStates[led]);
        Assert.IsFalse(led.isBurned);
    }

    [Test]
    public void BateriaDe3V_ConResistenciaDe100_EncendidoSuave()
    {
        BuildSeries(out var battery, out _, out var led, 100f);
        battery.batteryVoltage = 3f;

        var result = manager.Evaluate();

        float expected = 1f / 111f; // (3 - 2) / (1 + 100 + 10) = 9 mA
        Assert.AreEqual(expected, result.readings[led].current, 1e-4f);
        Assert.Less(result.ledBrightness[led], 1f);
        Assert.Greater(result.ledBrightness[led], 0f);
    }

    [Test]
    public void CortocircuitoConInterruptorCerrado_DaCorrienteEnorme()
    {
        var battery = Make("Bateria", ComponentType.Battery, 0, 1);
        var sw = Make("Interruptor", ComponentType.Switch, 0, 1, closed: true);
        Link(battery.positiveOrA, battery.negativeOrB, sw.positiveOrA, sw.negativeOrB);

        var result = manager.Evaluate();

        Assert.AreEqual(CircuitStatus.ShortCircuit, result.status);
        Assert.Greater(result.batteryCurrent, 5f); // ~8.9 A
    }

    [Test]
    public void ParaleloConUnInterruptorPorRama_CadaInterruptorEnciendeSuLed()
    {
        // Batería (0..4) -> resistencia (0..1) -> [sw1 (1..2) + LED1 (2..4)] || [sw2 (1..3) + LED2 (3..4)]
        var battery = Make("Bateria", ComponentType.Battery, 0, 4);
        var resistor = Make("Resistencia", ComponentType.Resistor, 0, 1);
        resistor.resistanceOhms = 470f;
        var sw1 = Make("Interruptor1", ComponentType.Switch, 1, 2);
        var led1 = Make("LED1", ComponentType.LED, 2, 4);
        var sw2 = Make("Interruptor2", ComponentType.Switch, 1, 3);
        var led2 = Make("LED2", ComponentType.LED, 3, 4);
        Link(battery.positiveOrA, resistor.positiveOrA, resistor.negativeOrB, sw1.positiveOrA, sw2.positiveOrA,
             sw1.negativeOrB, led1.positiveOrA, sw2.negativeOrB, led2.positiveOrA,
             led1.negativeOrB, led2.negativeOrB, battery.negativeOrB);

        sw1.isClosed = false; sw2.isClosed = false;
        var none = manager.Evaluate();
        Assert.IsFalse(none.ledStates[led1]);
        Assert.IsFalse(none.ledStates[led2]);

        sw1.isClosed = true; sw2.isClosed = false;
        var onlyFirst = manager.Evaluate();
        Assert.IsTrue(onlyFirst.ledStates[led1], "solo el primero cerrado: enciende LED1");
        Assert.IsFalse(onlyFirst.ledStates[led2], "solo el primero cerrado: LED2 apagado");

        sw1.isClosed = false; sw2.isClosed = true;
        var onlySecond = manager.Evaluate();
        Assert.IsFalse(onlySecond.ledStates[led1]);
        Assert.IsTrue(onlySecond.ledStates[led2]);

        sw1.isClosed = true; sw2.isClosed = true;
        var both = manager.Evaluate();
        Assert.IsTrue(both.ledStates[led1]);
        Assert.IsTrue(both.ledStates[led2]);
        Assert.Greater(both.batteryCurrent, onlyFirst.batteryCurrent, "con dos ramas circula más corriente");
    }

    [Test]
    public void DosLedEnSerie_LaMismaCorrientePorLosDos()
    {
        var battery = Make("Bateria", ComponentType.Battery, 0, 4);
        var resistor = Make("Resistencia", ComponentType.Resistor, 0, 1);
        resistor.resistanceOhms = 470f;
        var led1 = Make("LED1", ComponentType.LED, 1, 2);
        var led2 = Make("LED2", ComponentType.LED, 2, 4);
        Link(battery.positiveOrA, resistor.positiveOrA, resistor.negativeOrB, led1.positiveOrA,
             led1.negativeOrB, led2.positiveOrA, led2.negativeOrB, battery.negativeOrB);

        var result = manager.Evaluate();

        Assert.AreEqual(CircuitStatus.Closed, result.status);
        Assert.AreEqual(5f / 491f, result.readings[led1].current, 1e-4f); // (9 - 2 - 2) / (1 + 470 + 10 + 10)
        Assert.AreEqual(result.readings[led1].current, result.readings[led2].current, 1e-5f);
    }

    [Test]
    public void CiclarResistencia_RecorreLosValoresYVuelveAlPrimero()
    {
        var r = Make("Resistencia", ComponentType.Resistor, 0, 1);
        r.resistanceOhms = 220f;

        var seen = new List<float>();
        for (int i = 0; i < ElectronicComponent.ResistorSteps.Length; i++)
        {
            r.CycleResistance();
            seen.Add(r.resistanceOhms);
        }

        Assert.AreEqual(330f, seen[0]);
        Assert.AreEqual(10000f, seen[ElectronicComponent.ResistorSteps.Length - 3]);
        Assert.AreEqual(220f, seen[ElectronicComponent.ResistorSteps.Length - 1], "tras 8 pasos vuelve al valor inicial");
        Assert.Contains(100f, seen);
    }

    [Test]
    public void CiclarVoltajeDeLaBateria_PasaPor3_5_9_12()
    {
        var b = Make("Bateria", ComponentType.Battery, 0, 1);
        b.batteryVoltage = 9f;
        b.CycleVoltage(); Assert.AreEqual(12f, b.batteryVoltage);
        b.CycleVoltage(); Assert.AreEqual(3f, b.batteryVoltage);
        b.CycleVoltage(); Assert.AreEqual(5f, b.batteryVoltage);
    }
}
