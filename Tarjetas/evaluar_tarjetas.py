# Evalua las tarjetas con fotos de camara simuladas (giro, inclinacion, desenfoque, ruido, distancia):
# mide con SIFT + RANSAC si cada tarjeta se reconoce sola y si se confunde con las demas.
# Requiere opencv-python-headless, numpy.   Uso:  python evaluar_tarjetas.py <carpeta_nueva> <carpeta_antigua>
# (es una aproximacion: Vuforia usa su propio detector; la prueba real es con el celular).
# Simula fotos de camara de las tarjetas y mide (con SIFT + RANSAC) si se reconocen y si se confunden entre si.
import sys, math, glob, os
import numpy as np, cv2
rng = np.random.default_rng(7)
cv2.setRNGSeed(7)

def cargar(carpeta, patron):
    out = {}
    for f in sorted(glob.glob(os.path.join(carpeta, patron))):
        im = cv2.imread(f, cv2.IMREAD_GRAYSCALE)
        im = cv2.resize(im, (640, 640), interpolation=cv2.INTER_AREA)
        out[os.path.splitext(os.path.basename(f))[0].replace("Marcador_", "")] = im
    return out

sift = cv2.SIFT_create(nfeatures=2500)

def vista(ref, rz, rx, ry, lado, cx, cy, blur, ruido, gain):
    f = 800.0
    h = 1.0
    pts = np.array([[-h, -h, 0], [h, -h, 0], [h, h, 0], [-h, h, 0]], dtype=np.float64)
    Rz = np.array([[math.cos(rz), -math.sin(rz), 0], [math.sin(rz), math.cos(rz), 0], [0, 0, 1]])
    Rx = np.array([[1, 0, 0], [0, math.cos(rx), -math.sin(rx)], [0, math.sin(rx), math.cos(rx)]])
    Ry = np.array([[math.cos(ry), 0, math.sin(ry)], [0, 1, 0], [-math.sin(ry), 0, math.cos(ry)]])
    P = pts @ (Rx @ Ry @ Rz).T
    Z0 = f * 2 * h / lado
    dst = np.stack([f * P[:, 0] / (P[:, 2] + Z0) + cx, f * P[:, 1] / (P[:, 2] + Z0) + cy], 1).astype(np.float32)
    src = np.array([[0, 0], [639, 0], [639, 639], [0, 639]], np.float32)
    M = cv2.getPerspectiveTransform(src, dst)
    fondo = cv2.GaussianBlur(rng.integers(60, 140, (480, 640)).astype(np.uint8), (0, 0), 6)
    mask = cv2.warpPerspective(np.full((640, 640), 255, np.uint8), M, (640, 480))
    warp = cv2.warpPerspective(ref, M, (640, 480), flags=cv2.INTER_AREA)
    img = np.where(mask > 128, warp, fondo).astype(np.float32)
    img = cv2.GaussianBlur(img, (0, 0), blur)
    img = img * gain + rng.normal(0, ruido, img.shape)
    return np.clip(img, 0, 255).astype(np.uint8)

def inliers(kp_r, des_r, kp_v, des_v):
    if des_r is None or des_v is None or len(kp_r) < 5 or len(kp_v) < 5:
        return 0
    bf = cv2.BFMatcher(cv2.NORM_L2)
    good = []
    for m in bf.knnMatch(des_r, des_v, k=2):
        if len(m) == 2 and m[0].distance < 0.75 * m[1].distance:
            good.append(m[0])
    if len(good) < 8:
        return 0
    a = np.float32([kp_r[m.queryIdx].pt for m in good]); b = np.float32([kp_v[m.trainIdx].pt for m in good])
    H, mk = cv2.findHomography(a, b, cv2.RANSAC, 6.0)
    if H is None:
        return 0
    # La homografia debe ser plausible: el cuadrado de la tarjeta debe quedar como un cuadrilatero convexo
    # de tamano razonable (descarta las homografias degeneradas que "colapsan" todo a un punto).
    esq = cv2.perspectiveTransform(np.float32([[[0, 0], [639, 0], [639, 639], [0, 639]]]), H)[0]
    area = 0.5 * abs(sum(esq[i][0] * esq[(i + 1) % 4][1] - esq[(i + 1) % 4][0] * esq[i][1] for i in range(4)))
    signos = []
    for i in range(4):
        u = esq[(i + 1) % 4] - esq[i]; w = esq[(i + 2) % 4] - esq[(i + 1) % 4]
        signos.append(np.sign(u[0] * w[1] - u[1] * w[0]))
    lados = [np.linalg.norm(esq[(i + 1) % 4] - esq[i]) for i in range(4)]
    if len(set(signos)) != 1 or not (6000 < area < 300000) or max(lados) / max(min(lados), 1e-6) > 6:
        return 0
    return int(mk.sum())

def evaluar(nombre, refs, vistas_por_tarjeta=30):
    print("\n=== %s ===" % nombre)
    nombres = list(refs)
    feats = {}
    for n in nombres:
        kp, des = sift.detectAndCompute(refs[n], None)
        feats[n] = (kp, des)
        # reparto: celdas 4x4 con al menos 1 punto
        celdas = set((int(k.pt[0] // 160), int(k.pt[1] // 160)) for k in kp)
        print("  %-14s puntos SIFT: %4d   celdas cubiertas (de 16): %2d" % (n, len(kp), len(celdas)))
    ok = {n: 0 for n in nombres}; media = {n: [] for n in nombres}
    conf = {n: [] for n in nombres}   # max inliers de vistas de OTRA tarjeta contra la ref n
    correcto = 0; total = 0
    for n in nombres:
        for _ in range(vistas_por_tarjeta):
            v = vista(refs[n], rng.uniform(0, 2 * math.pi), math.radians(rng.uniform(-50, 50)), math.radians(rng.uniform(-50, 50)),
                      rng.uniform(220, 420), rng.uniform(250, 390), rng.uniform(180, 300), rng.uniform(0.8, 2.0), rng.uniform(2, 9), rng.uniform(0.7, 1.15))
            kv, dv = sift.detectAndCompute(v, None)
            puntajes = {m: inliers(feats[m][0], feats[m][1], kv, dv) for m in nombres}
            mejor = max(puntajes, key=puntajes.get)
            total += 1
            propio = puntajes[n]
            ajeno = max(p for m, p in puntajes.items() if m != n)
            media[n].append(propio)
            if propio >= 15 and propio > ajeno:
                ok[n] += 1
            if mejor == n and propio >= 15:
                correcto += 1
            for m in nombres:
                if m != n:
                    conf[m].append(puntajes[m])
    print("  Reconocimiento (>=15 inliers y sin confundirse):")
    for n in nombres:
        print("    %-14s %3.0f %%   inliers medios %5.1f   peor confusion con otra tarjeta: %2d inliers" % (
            n, 100.0 * ok[n] / vistas_por_tarjeta, np.mean(media[n]), max(conf[n]) if conf[n] else 0))
    print("  TOTAL identificadas bien: %.0f %%" % (100.0 * correcto / total))

nuevas = cargar(sys.argv[1], "Marcador_*.png")
viejas = cargar(sys.argv[2], "Marcador_*.png")
evaluar("TARJETAS NUEVAS", nuevas)
evaluar("TARJETAS ANTIGUAS (Marcadores_Placeholder)", viejas)
