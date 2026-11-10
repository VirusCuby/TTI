/// <summary>
/// Texto de la ayuda "Cómo se usa". Lo comparten el menú principal y el menú de pausa del simulador.
/// Usa las etiquetas de color de TextMeshPro (&lt;color=#RRGGBB&gt;).
/// </summary>
public static class HelpContent
{
    public struct Page
    {
        public string title;
        public string body;
        public Page(string title, string body) { this.title = title; this.body = body; }
    }

    private const string Pos = "<color=#FF5A5A><b>rojo</b></color>";
    private const string Neg = "<color=#6C8CFF><b>azul</b></color>";
    private const string TA = "<color=#FFD84A><b>amarillo</b></color>";
    private const string TB = "<color=#4AE3E3><b>cian</b></color>";
    private const string Ok = "<color=#5BE37D><b>verde</b></color>";
    private const string Warn = "<color=#FFA63A><b>naranja</b></color>";

    public static readonly Page[] Pages =
    {
        new Page("1. Las tarjetas",
            "Cada componente es una <b>tarjeta de papel</b> que pones sobre la mesa. La cámara las reconoce y les superpone su modelo 3D.\n\n" +
            "- <b>Batería</b>: la fuente de energía.\n" +
            "- <b>Resistencia</b>: limita la corriente.\n" +
            "- <b>Interruptor</b> (hay 2): abre o cierra el paso.\n" +
            "- <b>LED</b> (hay 2): se enciende si la corriente pasa en el sentido correcto.\n\n" +
            "Imprime las tarjetas al <b>100 %</b> de tamaño (9 × 9 cm), nunca con \"ajustar a la página\". Usa una mesa con buena luz."),

        new Page("2. Armar un circuito",
            "Acerca las tarjetas hasta que sus <b>terminales</b> queden cerca. Los terminales se ven como esferas de colores sobre cada tarjeta:\n\n" +
            "- " + Pos + " = positivo (+) y " + Neg + " = negativo (-)\n" +
            "- " + TA + " = terminal A y " + TB + " = terminal B\n" +
            "- " + Ok + " = terminal ya conectado\n\n" +
            "Cuando dos terminales de tarjetas distintas están cerca, la app dibuja un <b>cable virtual</b> entre ellos. Cierra el anillo: batería > interruptor > resistencia > LED > de vuelta a la batería.\n\n" +
            "Cuidado con el LED: tiene <b>polaridad</b>. Si lo giras al revés no enciende."),

        new Page("3. Interactuar",
            "Toca la pantalla sobre el modelo 3D:\n\n" +
            "- <b>Interruptor</b>: tócalo para abrirlo o cerrarlo. La tecla cambia de posición y de color.\n" +
            "- <b>Resistencia</b>: toca cerca de ella para cambiar su valor (100 ohm, 220 ohm, 330 ohm ... 10 k ohm).\n" +
            "- <b>Batería</b>: toca cerca para cambiar el voltaje (3, 5, 9 o 12 V).\n\n" +
            "Sobre cada tarjeta flota una etiqueta con su <b>valor</b> y la <b>corriente</b> que pasa por ella. Puedes desactivarlas en Ajustes."),

        new Page("4. Colores y avisos",
            "Los cables cambian de color según lo que pasa:\n\n" +
            "- " + Ok + ": circuito cerrado, la corriente fluye.\n" +
            "- " + Warn + ": circuito abierto o sin batería.\n" +
            "- <color=#FF5A5A><b>rojo</b></color>: cortocircuito o LED al revés.\n\n" +
            "Las pequeñas luces que corren por el cable muestran el <b>sentido y la intensidad</b> de la corriente.\n\n" +
            "Más de <b>35 mA</b> pone al LED \"al límite\"; más de <b>50 mA</b> lo <b>quema</b>. Para repararlo, retira su tarjeta y vuelve a ponerla."),

        new Page("5. Ley de Ohm y retos",
            "Para calcular la corriente de un LED:\n\n" +
            "<b>I = (V - Vled) / R</b>\n\n" +
            "Con una batería de 9 V, un LED de 2 V y 470 ohm circulan unos 15 mA: brilla sin peligro. Con 100 ohm pasarían más de 60 mA y se quemaría.\n\n" +
            "En <b>Retos</b> tienes 11 misiones guiadas: encender el LED, medir la corriente, el cortocircuito, la polaridad, dos LED en serie y las compuertas lógicas <b>AND</b> y <b>OR</b> con interruptores.\n\n" +
            "En <b>Modo libre</b> experimentas sin misiones ni puntaje."),
    };
}
