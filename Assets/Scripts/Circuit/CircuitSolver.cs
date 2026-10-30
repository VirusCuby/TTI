using System;
using System.Collections.Generic;

/// <summary>
/// Resuelve un circuito de corriente continua por análisis nodal (matriz de
/// conductancias). No depende de Unity, por eso se puede probar sola.
///
/// Elementos soportados:
///   - Fuente: batería real = fuente ideal de V voltios en serie con una resistencia interna.
///   - Resistencia: R ohmios (también sirve para un interruptor cerrado, con R muy pequeña).
///   - Diodo (LED): no conduce hasta superar su voltaje directo Vf; después se comporta
///     como Vf en serie con una resistencia pequeña. Se resuelve probando qué diodos
///     conducen hasta que el resultado sea coherente.
///
/// Un interruptor abierto simplemente no se agrega. Los trozos del circuito que no
/// están conectados a la fuente quedan a 0 V (se les agrega una conductancia mínima a tierra).
/// </summary>
public sealed class CircuitSolver
{
    public enum Kind { Source, Resistor, Diode }

    public struct Element
    {
        public Kind kind;
        public int a;          // Fuente: polo (+) · Diodo: ánodo · Resistencia: un extremo
        public int b;          // Fuente: polo (-) · Diodo: cátodo · Resistencia: el otro extremo
        public double value;   // Fuente: voltios · Resistencia: ohmios · Diodo: voltaje directo Vf
        public double series;  // Fuente: resistencia interna · Diodo: resistencia en conducción
    }

    /// <summary>Voltaje de cada nodo respecto a tierra.</summary>
    public double[] NodeVoltage { get; private set; }

    /// <summary>
    /// Corriente de cada elemento, en el orden en que se pasaron.
    /// Resistencia y diodo: de a hacia b. Fuente: la que sale del polo (+) hacia el circuito.
    /// </summary>
    public double[] Current { get; private set; }

    /// <summary>Para cada diodo: true si está conduciendo.</summary>
    public bool[] DiodeOn { get; private set; }

    public const double MinResistance = 1e-3;
    private const double Gmin = 1e-9;
    private const int MaxIterations = 60;

    public bool Solve(int nodeCount, int ground, IList<Element> elements)
    {
        int ne = elements.Count;
        NodeVoltage = new double[Math.Max(nodeCount, 0)];
        Current = new double[ne];
        DiodeOn = new bool[ne];
        if (nodeCount <= 1 || ground < 0 || ground >= nodeCount) return false;

        for (int iter = 0; iter < MaxIterations; iter++)
        {
            if (!SolveLinear(nodeCount, ground, elements)) return false;

            bool changed = false;
            for (int e = 0; e < ne; e++)
            {
                var el = elements[e];
                if (el.kind != Kind.Diode) continue;

                double v = NodeVoltage[el.a] - NodeVoltage[el.b];
                double g = 1.0 / Math.Max(el.series, MinResistance);
                if (!DiodeOn[e])
                {
                    if (v > el.value + 1e-9) { DiodeOn[e] = true; changed = true; }
                }
                else if ((v - el.value) * g < -1e-12)
                {
                    DiodeOn[e] = false; changed = true;
                }
            }
            if (!changed) break;
        }

        ComputeCurrents(elements);
        return true;
    }

    private bool SolveLinear(int n, int ground, IList<Element> elements)
    {
        int m = n - 1;
        var A = new double[m, m];
        var z = new double[m];
        Func<int, int> row = node => node < ground ? node : node - 1;

        for (int i = 0; i < n; i++)
            if (i != ground) A[row(i), row(i)] += Gmin;

        for (int e = 0; e < elements.Count; e++)
        {
            var el = elements[e];
            double g;
            double shift = 0; // corriente inyectada en 'a' (y extraída de 'b')

            switch (el.kind)
            {
                case Kind.Source:
                    g = 1.0 / Math.Max(el.series, MinResistance);
                    shift = el.value * g;
                    break;
                case Kind.Resistor:
                    g = 1.0 / Math.Max(el.value, MinResistance);
                    break;
                default: // Diode
                    if (!DiodeOn[e]) continue;
                    g = 1.0 / Math.Max(el.series, MinResistance);
                    shift = el.value * g;
                    break;
            }

            if (el.a == el.b) continue; // los dos extremos están en el mismo nodo (cortocircuitado por cable)

            if (el.a != ground) { A[row(el.a), row(el.a)] += g; z[row(el.a)] += shift; }
            if (el.b != ground) { A[row(el.b), row(el.b)] += g; z[row(el.b)] -= shift; }
            if (el.a != ground && el.b != ground)
            {
                A[row(el.a), row(el.b)] -= g;
                A[row(el.b), row(el.a)] -= g;
            }
        }

        double[] x = GaussSolve(A, z, m);
        if (x == null) return false;

        for (int i = 0; i < n; i++)
            NodeVoltage[i] = i == ground ? 0.0 : x[row(i)];
        return true;
    }

    private void ComputeCurrents(IList<Element> elements)
    {
        for (int e = 0; e < elements.Count; e++)
        {
            var el = elements[e];
            double vab = NodeVoltage[el.a] - NodeVoltage[el.b];
            switch (el.kind)
            {
                case Kind.Source:
                    Current[e] = (el.value - vab) / Math.Max(el.series, MinResistance);
                    break;
                case Kind.Resistor:
                    Current[e] = el.a == el.b ? 0.0 : vab / Math.Max(el.value, MinResistance);
                    break;
                default:
                    Current[e] = DiodeOn[e] && el.a != el.b
                        ? (vab - el.value) / Math.Max(el.series, MinResistance)
                        : 0.0;
                    break;
            }
        }
    }

    /// <summary>Eliminación gaussiana con pivoteo parcial. Devuelve null si la matriz es singular.</summary>
    private static double[] GaussSolve(double[,] A, double[] b, int n)
    {
        var x = new double[n];
        if (n == 0) return x;

        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            double best = Math.Abs(A[col, col]);
            for (int r = col + 1; r < n; r++)
            {
                double v = Math.Abs(A[r, col]);
                if (v > best) { best = v; pivot = r; }
            }
            if (best < 1e-18) return null;

            if (pivot != col)
            {
                for (int c = 0; c < n; c++)
                {
                    double t = A[col, c]; A[col, c] = A[pivot, c]; A[pivot, c] = t;
                }
                double tb = b[col]; b[col] = b[pivot]; b[pivot] = tb;
            }

            for (int r = col + 1; r < n; r++)
            {
                double f = A[r, col] / A[col, col];
                if (f == 0.0) continue;
                for (int c = col; c < n; c++) A[r, c] -= f * A[col, c];
                b[r] -= f * b[col];
            }
        }

        for (int r = n - 1; r >= 0; r--)
        {
            double s = b[r];
            for (int c = r + 1; c < n; c++) s -= A[r, c] * x[c];
            x[r] = s / A[r, r];
        }
        return x;
    }
}
