using System.Globalization;

/// <summary>
/// Da formato a corrientes, voltajes y resistencias para mostrarlos en pantalla.
/// La fuente de los textos solo tiene caracteres Latin-1, así que no se usa el
/// símbolo Ω: se escribe "ohm".
/// </summary>
public static class CircuitUnits
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Amps(float amps)
    {
        float a = System.Math.Abs(amps);
        if (a >= 1f) return a.ToString("0.00", Inv) + " A";
        if (a >= 0.0995f) return (a * 1000f).ToString("0", Inv) + " mA";
        if (a >= 0.001f) return (a * 1000f).ToString("0.0", Inv) + " mA";
        if (a >= 0.000001f) return (a * 1000000f).ToString("0", Inv) + " µA";
        return "0 mA";
    }

    public static string Volts(float volts)
    {
        return System.Math.Abs(volts).ToString(System.Math.Abs(volts) >= 10f ? "0" : "0.0", Inv) + " V";
    }

    public static string Ohms(float ohms)
    {
        if (ohms >= 1000f)
        {
            float k = ohms / 1000f;
            return k.ToString(k >= 10f || k == System.Math.Floor(k) ? "0" : "0.0", Inv) + " k ohm";
        }
        return ohms.ToString("0", Inv) + " ohm";
    }

    public static string Watts(float watts)
    {
        float w = System.Math.Abs(watts);
        if (w >= 1f) return w.ToString("0.0", Inv) + " W";
        if (w >= 0.001f) return (w * 1000f).ToString("0", Inv) + " mW";
        return "0 mW";
    }
}
