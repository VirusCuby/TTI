# -*- coding: utf-8 -*-
"""
Genera las 6 tarjetas del simulador AR (Image Targets de Vuforia), cuadradas de 9 x 9 cm.

Cada tarjeta usa una FAMILIA DE PATRON distinta, para que Vuforia no las confunda entre si
(p. ej. que el LED aparezca sobre la tarjeta del interruptor):

    Bateria        mosaico poligonal (triangulos)        ambar
    Resistencia    celdas de Voronoi (vitral)            azul
    Interruptor    pistas de circuito impreso            verde
    Interruptor 2  burbujas y anillos superpuestos       violeta
    LED            palitos cruzados en cualquier angulo  rojo
    LED 2          camuflaje organico                    rosa

Reglas de diseno (recomendaciones de Vuforia para Image Targets):
  - Imagen cuadrada, muchos detalles de tamano medio repartidos de forma pareja, sin repeticiones.
  - Margen blanco del 8 % alrededor (esa franja no se analiza).
  - El componente se dibuja sobre el eje horizontal, con los terminales en sus posiciones reales
    (cm desde el centro, iguales a los Node de la escena) y con los colores de las esferas de la app:
    rojo = +, azul = -, amarillo = A, cian = B.
  - Triangulo "ARRIBA" en el margen superior (la etiqueta de abajo marca el lado inferior).

Uso:   python generar_tarjetas.py            (escribe en esta misma carpeta)
Salida: Marcador_*.png (para subir al Target Manager, ancho 0.09 m),
        Tarjetas_A4.pdf (hoja para imprimir al 100 %), Vista_previa.png
"""
import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import binary_dilation, gaussian_filter
from scipy.spatial import Delaunay, cKDTree

S = 1200                  # lado final en pixeles (1 cm = S/9 px)
SS = 2                    # supermuestreo para bordes suaves
W = S * SS
CM = S / 9.0
MARGEN = int(round(S * 0.08))          # 8 %
R0, R1 = MARGEN * SS, (S - MARGEN) * SS
YC = S // 2                            # eje horizontal de los terminales

BLANCO = (255, 255, 255)
NEGRO = (14, 14, 18)
ROJO, AZUL, AMARILLO, CIAN = (226, 40, 40), (46, 84, 232), (250, 206, 30), (30, 200, 212)


def px(v):
    return int(round(v * SS))


def tx(cm):
    """Posicion X (en px de S) de un terminal a 'cm' del centro de la tarjeta."""
    return S / 2 + cm * CM


def recortar_region(capa):
    """Deja solo la region interior; el margen queda blanco."""
    out = Image.new("RGB", (W, W), BLANCO)
    caja = (R0, R0, R1, R1)
    out.paste(capa.crop(caja), caja)
    return out


# ============================================================================================
# Familias de patrones
# ============================================================================================

def patron_poligonos(seed, tonos):
    r = random.Random(seed)
    capa = Image.new("RGB", (W, W), tonos[0])
    d = ImageDraw.Draw(capa)
    celda = px(62)
    n = int((R1 - R0) / celda) + 3
    pts = []
    for i in range(-1, n):
        for j in range(-1, n):
            pts.append((R0 + (i + r.uniform(0.08, 0.92)) * celda, R0 + (j + r.uniform(0.08, 0.92)) * celda))
    tri = Delaunay(np.array(pts))
    oscuro = tuple(int(c * 0.35) for c in tonos[0])
    caras = []
    for s in tri.simplices:
        p = [pts[k] for k in s]
        d.polygon(p, fill=r.choice(tonos))
        caras.append(p)
    for p in caras:
        d.line(p + [p[0]], fill=oscuro, width=px(2.4))
    return recortar_region(capa)


def patron_voronoi(seed, tonos):
    rn = np.random.default_rng(seed)
    celda = 86
    n = int((S - 2 * MARGEN) / celda) + 3
    semillas = []
    for i in range(-1, n):
        for j in range(-1, n):
            semillas.append((MARGEN + (i + rn.uniform(0.1, 0.9)) * celda, MARGEN + (j + rn.uniform(0.1, 0.9)) * celda))
    semillas = np.array(semillas)
    arbol = cKDTree(semillas)
    yy, xx = np.mgrid[0:S, 0:S]
    etiq = arbol.query(np.column_stack([xx.ravel() + 0.5, yy.ravel() + 0.5]))[1].reshape(S, S)
    etiq = np.repeat(np.repeat(etiq, SS, axis=0), SS, axis=1)
    idx_tono = rn.integers(0, len(tonos), len(semillas))
    paleta = np.array(tonos, dtype=np.uint8)
    img = paleta[idx_tono[etiq]]
    borde = np.zeros((W, W), bool)
    borde[:, :-1] |= etiq[:, :-1] != etiq[:, 1:]
    borde[:-1, :] |= etiq[:-1, :] != etiq[1:, :]
    borde = binary_dilation(borde, iterations=px(2.2))
    img[borde] = tuple(int(c * 0.25) for c in tonos[0])
    capa = Image.fromarray(img, "RGB")
    d = ImageDraw.Draw(capa)
    for sx, sy in semillas:
        rr = px(6.5)
        d.ellipse((sx * SS - rr, sy * SS - rr, sx * SS + rr, sy * SS + rr), fill=(240, 246, 255), outline=NEGRO, width=px(1.6))
    return recortar_region(capa)


def patron_pcb(seed):
    r = random.Random(seed)
    fondo, traza, pad = (14, 48, 32), (176, 228, 112), (236, 196, 72)
    capa = Image.new("RGB", (W, W), fondo)
    d = ImageDraw.Draw(capa)
    g = px(36)
    ni = int((R1 - R0) / g)
    nodo = lambda i, j: (R0 + i * g, R0 + j * g)
    dirs = [(1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1)]
    bloques = 9
    paso = ni / bloques
    for bi in range(bloques):
        for bj in range(bloques):
            for _ in range(2):
                i = int(bi * paso + r.random() * paso)
                j = int(bj * paso + r.random() * paso)
                a = r.choice([0, 2, 4, 6]) if r.random() < 0.7 else r.randrange(8)
                puntos = [nodo(i, j)]
                for _seg in range(r.randint(3, 5)):
                    k = r.randint(2, 6)
                    for _s in range(k):
                        ni_, nj_ = i + dirs[a][0], j + dirs[a][1]
                        if not (0 <= ni_ <= ni and 0 <= nj_ <= ni):
                            break
                        i, j = ni_, nj_
                    puntos.append(nodo(i, j))
                    a = (a + r.choice([-1, 1]) * r.choice([1, 2])) % 8
                d.line(puntos, fill=traza, width=px(11), joint="curve")
                for p in puntos[1:-1]:
                    d.ellipse((p[0] - px(5.5), p[1] - px(5.5), p[0] + px(5.5), p[1] + px(5.5)), fill=traza)
                for p, grande in ((puntos[-1], True), (puntos[0], r.random() < 0.55)):
                    rr, hh = (px(18), px(7.5)) if grande else (px(10), px(4))
                    d.ellipse((p[0] - rr, p[1] - rr, p[0] + rr, p[1] + rr), fill=pad)
                    d.ellipse((p[0] - hh, p[1] - hh, p[0] + hh, p[1] + hh), fill=fondo)
    return recortar_region(capa)


def patron_burbujas(seed, tonos):
    r = random.Random(seed)
    capa = Image.new("RGB", (W, W), tonos[-2])
    d = ImageDraw.Draw(capa)
    oscuro = tuple(int(c * 0.3) for c in tonos[0])

    def circulo(cx, cy, rad):
        modo = r.random()
        c = r.choice(tonos[:-1])
        if modo < 0.62:
            d.ellipse((cx - rad, cy - rad, cx + rad, cy + rad), fill=c, outline=oscuro, width=px(4.5))
        elif modo < 0.85:
            d.ellipse((cx - rad, cy - rad, cx + rad, cy + rad), outline=c, width=px(11))
            d.ellipse((cx - rad, cy - rad, cx + rad, cy + rad), outline=oscuro, width=px(2))
        else:
            d.ellipse((cx - rad, cy - rad, cx + rad, cy + rad), fill=c, outline=oscuro, width=px(4.5))
            ri = rad * r.uniform(0.35, 0.6)
            d.ellipse((cx - ri, cy - ri, cx + ri, cy + ri), fill=r.choice(tonos), outline=oscuro, width=px(3.5))

    for lo, hi, cuenta in ((120, 190, 16), (60, 110, 40), (24, 50, 80)):
        for _ in range(cuenta):
            circulo(r.uniform(R0, R1), r.uniform(R0, R1), px(r.uniform(lo, hi)))
    return recortar_region(capa)


def patron_palitos(seed, tonos):
    r = random.Random(seed)
    capa = Image.new("RGB", (W, W), tonos[-1])
    d = ImageDraw.Draw(capa)
    oscuro = tuple(int(c * 0.3) for c in tonos[0])
    n = 17
    paso = (R1 - R0) / n
    lista = []
    for i in range(n):
        for j in range(n):
            lista.append((R0 + (i + r.random()) * paso, R0 + (j + r.random()) * paso))
    r.shuffle(lista)
    for cx, cy in lista:
        largo, ancho, ang = px(r.uniform(90, 300)), px(r.uniform(14, 38)), r.uniform(0, math.pi)
        ux, uy = math.cos(ang), math.sin(ang)
        vx, vy = -uy, ux
        p = [(cx + ux * s * largo / 2 + vx * t * ancho / 2, cy + uy * s * largo / 2 + vy * t * ancho / 2)
             for s, t in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        d.polygon(p, fill=r.choice(tonos[:-1]), outline=oscuro)
        d.line(p + [p[0]], fill=oscuro, width=px(3))
    return recortar_region(capa)


def patron_camuflaje(seed, tonos):
    rn = np.random.default_rng(seed)
    n1 = gaussian_filter(rn.standard_normal((S, S)), 30)
    n2 = gaussian_filter(rn.standard_normal((S, S)), 12)
    f = n1 / n1.std() + 0.9 * n2 / n2.std()          # solo escalas medias: el detalle muy fino se pierde al reducir la imagen
    cortes = np.quantile(f, np.linspace(0, 1, len(tonos) + 1)[1:-1])
    idx = np.digitize(f, cortes)
    idx = np.repeat(np.repeat(idx, SS, axis=0), SS, axis=1)
    paleta = np.array(tonos, dtype=np.uint8)
    img = paleta[idx]
    borde = np.zeros((W, W), bool)
    borde[:, :-1] |= idx[:, :-1] != idx[:, 1:]
    borde[:-1, :] |= idx[:-1, :] != idx[1:, :]
    borde = binary_dilation(borde, iterations=px(2.2))
    img[borde] = tuple(int(c * 0.25) for c in tonos[0])
    capa = Image.fromarray(img, "RGB")
    d = ImageDraw.Draw(capa)
    for _ in range(240):
        x, y = rn.uniform(R0, R1), rn.uniform(R0, R1)
        rr = px(rn.uniform(4.5, 9))
        d.ellipse((x - rr, y - rr, x + rr, y + rr), fill=tuple(int(v) for v in paleta[rn.integers(0, len(tonos))]),
                  outline=tuple(int(c * 0.25) for c in tonos[0]), width=px(1.6))
    return recortar_region(capa)


# ============================================================================================
# Simbolos del componente (sobre el eje de los terminales)
# ============================================================================================

def fuente(tam):
    for ruta in ("C:/Windows/Fonts/arialbd.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "arialbd.ttf"):
        try:
            return ImageFont.truetype(ruta, int(tam * SS))
        except OSError:
            continue
    return ImageFont.load_default()


class Dibujo:
    """Acumula trazos y los pinta con un halo blanco debajo, para que se lean sobre cualquier patron."""

    def __init__(self, img):
        self.img = img
        self.d = ImageDraw.Draw(img)
        self.ops = []

    def linea(self, pts, w=15):
        self.ops.append(("linea", [(x * SS, y * SS) for x, y in pts], w * SS))

    def poligono(self, pts, w=15):
        p = [(x * SS, y * SS) for x, y in pts]
        self.ops.append(("linea", p + [p[0]], w * SS))

    def relleno(self, pts):
        self.ops.append(("relleno", [(x * SS, y * SS) for x, y in pts], 0))

    def pintar(self, halo=13):
        for tipo, pts, w in self.ops:           # halo
            if tipo == "linea":
                self.d.line(pts, fill=BLANCO, width=int(w + 2 * halo * SS), joint="curve")
                for p in (pts[0], pts[-1]):
                    rr = (w + 2 * halo * SS) / 2
                    self.d.ellipse((p[0] - rr, p[1] - rr, p[0] + rr, p[1] + rr), fill=BLANCO)
            else:
                self.d.polygon(pts, fill=BLANCO, outline=BLANCO, width=int(2 * halo * SS))
        for tipo, pts, w in self.ops:           # nucleo
            if tipo == "linea":
                self.d.line(pts, fill=NEGRO, width=int(w), joint="curve")
                for p in (pts[0], pts[-1]):
                    rr = w / 2
                    self.d.ellipse((p[0] - rr, p[1] - rr, p[0] + rr, p[1] + rr), fill=NEGRO)
            else:
                self.d.polygon(pts, fill=NEGRO)

    def texto(self, xy, txt, tam, ancla="mm", halo=9, relleno=NEGRO):
        self.d.text((xy[0] * SS, xy[1] * SS), txt, font=fuente(tam), fill=relleno, anchor=ancla,
                    stroke_width=halo * SS, stroke_fill=BLANCO)

    def terminal(self, cm, color, etiqueta):
        x = min(tx(cm), S - MARGEN * 0.6 - 26)     # el LED tiene un terminal casi en el borde de la tarjeta
        r = 27 * SS
        cx, cy = x * SS, YC * SS
        self.d.ellipse((cx - r - 9 * SS, cy - r - 9 * SS, cx + r + 9 * SS, cy + r + 9 * SS), fill=BLANCO)
        self.d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=color, outline=NEGRO, width=6 * SS)
        self.texto((min(x, S - 70), YC - 74), etiqueta, 62, halo=8)   # sin pegarse al borde


def simbolo_bateria(dib):
    p, n = 2.52, -3.22
    xp, xn = tx(p), tx(n)
    placas = [(430, 100), (492, 190), (584, 100), (646, 190)]      # corta (-), larga (+), corta, larga
    dib.linea([(xn, YC), (placas[0][0], YC)])
    dib.linea([(placas[1][0], YC), (placas[2][0], YC)])
    dib.linea([(placas[3][0], YC), (xp, YC)])
    for x, alto in placas:
        dib.linea([(x, YC - alto / 2), (x, YC + alto / 2)], w=24)
    dib.texto((538, YC + 150), "9 V", 64)
    dib.pintar()
    dib.terminal(p, ROJO, "+")
    dib.terminal(n, AZUL, "\u2212")


def simbolo_resistencia(dib):
    a, b = -3.38, 2.33
    xa, xb = tx(a), tx(b)
    x0, x1, amp = 330, 780, 62
    zz = [(xa, YC), (x0, YC)]
    paso = (x1 - x0) / 8
    for k in range(8):
        zz.append((x0 + paso * (k + 0.5), YC + (amp if k % 2 == 0 else -amp)))
    zz += [(x1, YC), (xb, YC)]
    dib.linea(zz)
    dib.pintar()
    dib.terminal(a, AMARILLO, "A")
    dib.terminal(b, CIAN, "B")


def simbolo_interruptor(dib, variante=1):
    a, b = -2.51, 2.07
    xa, xb = tx(a), tx(b)
    ang = math.radians(27 if variante == 1 else 41)       # el gemelo se dibuja con otro angulo
    largo = (xb - xa) * 1.02
    punta = (xa + largo * math.cos(ang), YC - largo * math.sin(ang))
    dib.linea([(xa, YC), punta], w=20)
    if variante == 2:                                      # mango redondo en la punta de la cuchilla
        dib.linea([(punta[0] - 2, punta[1] + 2), (punta[0] + 2, punta[1] - 2)], w=64)
    dib.linea([(xb, YC), (xb + 95, YC)])
    dib.linea([(xa - 95, YC), (xa, YC)])
    dib.pintar()
    dib.terminal(a, AMARILLO, "A")
    dib.terminal(b, CIAN, "B")


def simbolo_led(dib, variante=1):
    p, n = -3.28, 4.34
    xp, xn = tx(p), tx(n)
    t0, t1 = (540, 740) if variante == 1 else (500, 720)   # el gemelo: triangulo mas grande y tres flechas
    dib.linea([(xp, YC), (t0, YC)])
    dib.poligono([(t0, YC - 110), (t0, YC + 110), (t1, YC)], w=17)
    dib.linea([(t1, YC - 115), (t1, YC + 115)], w=22)
    dib.linea([(t1, YC), (min(xn, S - MARGEN * 0.6 - 26), YC)])
    for ox in ((0, 78) if variante == 1 else (-70, 8, 86)):      # flechas de luz
        ax, ay = 640 + ox, YC - 150
        dib.linea([(ax, ay), (ax + 70, ay - 75)], w=13)
        dib.relleno([(ax + 84, ay - 89), (ax + 38, ay - 69), (ax + 64, ay - 45)])
    dib.pintar()
    dib.terminal(p, ROJO, "+")
    dib.terminal(n, AZUL, "\u2212")


# ============================================================================================
# Tarjetas
# ============================================================================================

TARJETAS = [
    dict(archivo="Marcador_Bateria", titulo="BATER\u00cdA", seed=1101, simbolo=simbolo_bateria,
         patron=lambda s: patron_poligonos(s, [(70, 40, 12), (135, 80, 18), (205, 124, 28), (240, 174, 54), (251, 218, 126), (156, 98, 44)])),
    dict(archivo="Marcador_Resistencia", titulo="RESISTENCIA", seed=2202, simbolo=simbolo_resistencia,
         patron=lambda s: patron_voronoi(s, [(12, 34, 76), (26, 74, 146), (54, 124, 204), (114, 178, 236), (192, 222, 249), (38, 58, 104)])),
    dict(archivo="Marcador_Interruptor", titulo="INTERRUPTOR", seed=3303, simbolo=simbolo_interruptor,
         patron=lambda s: patron_pcb(s)),
    dict(archivo="Marcador_Interruptor_2", titulo="INTERRUPTOR 2", seed=4404, simbolo=lambda d: simbolo_interruptor(d, 2), insignia=("hexagono", "izq"),
         patron=lambda s: patron_burbujas(s, [(46, 20, 82), (96, 52, 152), (152, 102, 207), (206, 172, 241), (240, 226, 251), (72, 36, 112), (232, 214, 248)])),
    dict(archivo="Marcador_LED", titulo="LED", seed=5505, simbolo=simbolo_led,
         patron=lambda s: patron_palitos(s, [(92, 12, 12), (170, 26, 26), (232, 72, 52), (250, 142, 112), (250, 206, 186), (122, 22, 38), (255, 236, 226)])),
    dict(archivo="Marcador_LED_2", titulo="LED 2", seed=6606, simbolo=lambda d: simbolo_led(d, 2), insignia=("circulo", "der"),
         patron=lambda s: patron_camuflaje(s, [(72, 12, 58), (152, 28, 112), (216, 62, 162), (246, 142, 202), (252, 212, 232)])),
]


def insignia_dos(dib, forma, esquina):
    """Numero grande para distinguir de lejos a las tarjetas gemelas (Interruptor 2 y LED 2)."""
    cx = 215 if esquina == "izq" else S - 215
    cy, r = 215, 78
    d = dib.d
    if forma == "hexagono":
        pts = [((cx + r * math.cos(math.radians(60 * k + 30))) * SS, (cy + r * math.sin(math.radians(60 * k + 30))) * SS) for k in range(6)]
    else:
        pts = None
    if pts:
        d.polygon(pts, fill=BLANCO, outline=NEGRO, width=7 * SS)
    else:
        d.ellipse(((cx - r) * SS, (cy - r) * SS, (cx + r) * SS, (cy + r) * SS), fill=BLANCO, outline=NEGRO, width=7 * SS)
    d.text((cx * SS, (cy + 4) * SS), "2", font=fuente(112), fill=NEGRO, anchor="mm")


def construir(t):
    img = t["patron"](t["seed"])
    dib = Dibujo(img)
    t["simbolo"](dib)
    if t.get("insignia"):
        insignia_dos(dib, *t["insignia"])

    # Titulo (abajo = lado inferior de la tarjeta)
    tam = 128
    while dib.d.textlength(t["titulo"], font=fuente(tam)) > 860 * SS and tam > 40:
        tam -= 4
    dib.texto((S / 2, 985), t["titulo"], tam, halo=12)

    # Marcas en los margenes (no se analizan: son para las personas)
    d = dib.d
    d.polygon([(S / 2 * SS, 22 * SS), ((S / 2 - 32) * SS, 80 * SS), ((S / 2 + 32) * SS, 80 * SS)], fill=NEGRO)
    d.text(((S / 2 + 52) * SS, 52 * SS), "ARRIBA", font=fuente(34), fill=NEGRO, anchor="lm")
    d.text((S / 2 * SS, (S - 46) * SS), "TTI  \u00b7  Simulador AR de circuitos  \u00b7  imprimir al 100 % (9 \u00d7 9 cm)",
           font=fuente(30), fill=(90, 90, 90), anchor="mm")
    d.rectangle((R0, R0, R1 - 1, R1 - 1), outline=NEGRO, width=px(3))

    return img.resize((S, S), Image.LANCZOS)


def armar_hoja(tarjetas, dpi=300):
    """Hoja A4 con las 6 tarjetas a tamano real (9 x 9 cm) y una regla de 5 cm para comprobar la impresion."""
    pag = Image.new("RGB", (int(21 / 2.54 * dpi), int(29.7 / 2.54 * dpi)), BLANCO)
    d = ImageDraw.Draw(pag)
    cm = dpi / 2.54
    lado = int(round(9 * cm))
    gap = 0.5 * cm
    izq = (pag.width - (2 * lado + gap)) / 2
    arr = (pag.height - (3 * lado + 2 * gap)) / 2
    f = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 30)
    d.text((izq, arr / 2), "Imprime al 100 % (tama\u00f1o real, sin ajustar a la p\u00e1gina). Cada tarjeta debe medir 9 \u00d7 9 cm; esta l\u00ednea mide 5 cm:",
           font=f, fill=(60, 60, 60), anchor="lm")
    d.line((pag.width - izq - 5 * cm, arr / 2, pag.width - izq, arr / 2), fill=NEGRO, width=5)
    for k, (nombre, im) in enumerate(tarjetas):
        c, f_ = k % 2, k // 2
        x, y = int(izq + c * (lado + gap)), int(arr + f_ * (lado + gap))
        pag.paste(im.resize((lado, lado), Image.LANCZOS), (x, y))
        d.rectangle((x, y, x + lado, y + lado), outline=(150, 150, 150), width=1)
    return pag


def hoja_a4(tarjetas, ruta, dpi=300):
    armar_hoja(tarjetas, dpi).save(ruta, "PDF", resolution=dpi)


def main():
    aqui = os.path.dirname(os.path.abspath(__file__))
    hechas = []
    for t in TARJETAS:
        im = construir(t)
        ruta = os.path.join(aqui, t["archivo"] + ".png")
        im.save(ruta, optimize=True)
        hechas.append((t["archivo"], im))
        print("%-26s %5.0f KB" % (t["archivo"], os.path.getsize(ruta) / 1024))
    hoja_a4(hechas, os.path.join(aqui, "Tarjetas_A4.pdf"))
    vista = Image.new("RGB", (3 * 520, 2 * 520), (235, 235, 235))
    for k, (_, im) in enumerate(hechas):
        vista.paste(im.resize((500, 500), Image.LANCZOS), (10 + (k % 3) * 520, 10 + (k // 3) * 520))
    vista.save(os.path.join(aqui, "Vista_previa.png"))


if __name__ == "__main__":
    main()
