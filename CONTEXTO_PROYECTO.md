# Contexto del Proyecto — Simulador AR de Circuitos Eléctricos

> Documento de referencia para no perder el hilo entre sesiones de trabajo.
> Última actualización: 2026-10-30 (simulación eléctrica, guía de pruebas, repositorio GitHub).

## 000. Sesión 2026-10-30 — cambio de modelos 3D (Batería, LED, Resistencia)

**Estado:** preparados y copiados a `Assets/Modelos_nuevos/` (`Bateria_9V.fbx`,
`LED_5mm.fbx`, `Resistencia_axial.fbx`), **pero la escena todavía NO se ha
modificado**: falta ejecutar en Unity el menú **Tools > Circuito > Reemplazar
modelos (Batería, LED, Resistencia)** (`Assets/Editor/ReemplazarModelos.cs`,
escrito sin poder compilarlo: si Unity da error de compilación, pegarlo).
Backup previo: `Backups/2026-10-30_antes-de-modelos-nuevos/`. El interruptor NO
se cambia (decisión del usuario).

**Cómo se prepararon** (Blender 5.2 por línea de comandos; scripts temporales):
- Archivos originales en `Nuevos modelos/` (fuera de Assets, se pueden conservar).
- **Batería** (`Battery1_Blendswap.fbx`): era una escena con DOS baterías de 9 V,
  un suelo y cámaras, 1.9 millones de triángulos / 38 MB. Se dejó UNA (la
  acostada), decimada a ~5900 triángulos (258 KB), 6.0×3.2×2.05 cm, contactos
  hacia el +X (lado del terminal +). Contacto + rojo, − azul oscuro.
- **LED** (`LED_2.fbx`): FBX ASCII 6.1 de 2012 que Blender no abre; se convirtió
  a OBJ con un conversor propio. Bombilla (`LED_Bulbo_Emisivo`, única pieza que
  brilla), interior y **patas rehechas** como tiras planas L que llegan a los
  terminales (+ −3.28 cm, − +4.34 cm). ~7000 triángulos.
- **Resistencia** (`resistor5.fbx`): girada −21.2° para alinearla con X, escalada
  (×0.0498) para que las patas caigan en A −3.38 / B +2.33 cm. 21 000
  triángulos, 6 materiales, 5.76 cm de largo y **5.2 cm de alto** (patas
  dobladas hacia abajo, forma de puente).
- Los FBX están en metros, ejes -Z adelante / Y arriba, ya centrados sobre la
  tarjeta (origen = centro de la tarjeta, base en y=0).

**Qué hace la herramienta de Unity** (todo con Undo, reejecutable): oculta el
modelo viejo como `(viejo) ...` sin borrarlo; instancia `Nuevo_*` con escala
mundial 1; crea materiales Standard en `Assets/Modelos_nuevos/Materiales/`;
quita los colliders de los `*_Modelo` de batería/LED/resistencia (los viejos no
coinciden y podían tapar un toque al interruptor); **pasa la escala de
`Bateria_Modelo` de (0.09,0.01,0.0225) a uniforme 0.01 conservando la posición
mundial de los Node**; apunta `Emissive Renderers` del LED a la bombilla (color
de brillo rojo, intensidad 2.5). También hay `Tools > Circuito > Volver a los
modelos viejos`.

**Por verificar en Unity tras ejecutarla:** (1) que las patas del LED y de la
resistencia terminen exactamente en los círculos de gizmo de los Node (si el
modelo sale espejado o girado 90°, hay que re-exportar); (2) tamaños en el
diálogo final; (3) Play con la webcam; (4) guardar con Ctrl+S; (5) reconstruir
el APK. Ojo: la resistencia mide 5.2 cm de alto, puede verse alta.

### 000.e Repositorio en GitHub, tarjetas y pendientes (2026-10-30)
**Repositorio:** https://github.com/VirusCuby/TTI (público, rama `main`, commit inicial `c0ee171`, 547
archivos, 46 MB). Se subió el proyecto completo salvo lo siguiente (ver `.gitignore`):
`Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/` (cachés, 5+ GB); `Builds/` y `*.apk`; `Backups/`;
las carpetas basura anidadas dentro de `Assets/` (copias del 12-sep); `QCAR/`, `build/`, `.utmp/`;
y **los paquetes del SDK de Vuforia** (`Packages/*.tgz` y `Assets/Editor/Migration/*.tgz`, 131.9 MB
cada uno: GitHub rechaza archivos de más de 100 MB y es software de terceros). Quien clone debe
descargar Vuforia 11.4.4 del portal y dejarlo en `Packages/` (explicado en `README.md`).
**Clave de licencia de Vuforia:** el repo es público, así que en GitHub el archivo
`Assets/Resources/VuforiaConfiguration.asset` lleva el marcador `PEGA_AQUI_TU_CLAVE_DE_LICENCIA_DE_VUFORIA`
en `vuforiaLicenseKey` y `ufoLicenseKey`. El archivo real en disco conserva la clave y Git NO lo toca:
se logró con `git update-index --skip-worktree` sobre esa ruta (el índice tiene una versión sanitizada).
Si algún día hay que cambiar de verdad ese asset: `git update-index --no-skip-worktree <ruta>`, editar
sanitizando a mano, y volver a poner `--skip-worktree`. Nunca usar `git add -f` sobre ella.
**Flujo para subir cambios:** `git add -A` → `git commit -m "..."` → `git push` (Git Credential Manager
ya está autenticado). No hay GitHub CLI instalado.
**APK:** quedan en `Builds/` (locales). Idea pendiente: publicarlos como Release de GitHub.

**Tamaño de las tarjetas (respuesta a una duda):** NO tienen que ser cuadradas. Vuforia solo exige que la
proporción impresa sea la de la imagen subida y que el ancho impreso sea el declarado en el Target
Manager (hoy 9 cm = `0.09`; el alto sale de la proporción). Pero en este proyecto el 9×9 importa en dos
sitios: (1) los terminales de la app están a distancias fijas del centro (−3.4 a +4.3 cm sobre la línea
horizontal de la imagen), así que la tarjeta debe medir ≥ ~9 cm de ancho; (2) las plantillas/posiciones
de `Guia_de_pruebas/` suponen huellas de 9×9 cm. En la base de datos `marcador_led` es 9×17.7 y
`marcador_bateria` 9×13.4 (imágenes altas): funcionan pero ocupan más y no encajan en las plantillas.
**Recomendado:** exportar esas dos imágenes a 1:1, subirlas con ancho 9 cm y bajar la base de datos de nuevo.
Tarjetas más chicas (p. ej. 6 cm) exigirían escalar en proporción las posiciones de los terminales en Unity
y recalcular las plantillas.
**¿Las plantillas valen con las imágenes propias del usuario?** Sí, no dependen del dibujo, sino de: tarjeta
de 9×9, cuál es el borde de ARRIBA del archivo de imagen (el ▲ de la plantilla) y que los terminales (que
viven sobre el eje horizontal de la imagen) coincidan con lo dibujado. Si el componente está dibujado en
vertical en la imagen, el dibujo y los terminales quedan girados 90°. Comprobación rápida: poner una tarjeta
sola en la app y ver la línea entre sus dos esferas de colores. No se encontraron en el equipo los archivos
de las tarjetas propias (las de las fotos); no se pudo revisar su arte.

**Estado de verificación:** todo el código compila con el Roslyn de Unity (verificado fuera de Unity) y el
solver pasa 24/24 casos de Ley de Ohm fuera de Unity. **NO se han corrido las pruebas Edit Mode ni se ha
probado en el celular** el código de simulación, retos, etiquetas y la regla de conexión nueva. La escena
`TestAR.unity` guardada es anterior a la herramienta de reemplazo de modelos (no se ha ejecutado).

**Pendientes, en orden recomendado:**
1. Abrir Unity, revisar la Console y correr el Test Runner (EditMode > Run All; ~25 pruebas).
2. Ejecutar *Tools > Circuito > Reemplazar modelos (Batería, LED, Resistencia)*, revisar que las patas del LED
   y la resistencia terminen en los círculos de gizmo de los Node, y guardar la escena (Ctrl+S).
3. Compilar un APK nuevo y probar en el celular: etiquetas flotantes, panel de retos (posición/tamaño),
   pulsos en los cables, toque cerca de la resistencia/batería, sonidos, LED quemado.
4. Imprimir `Guia_de_pruebas/plantillas/Plantillas_A3.pdf` al 100 % y probar las 6 pruebas (las dos en
   paralelo solo funcionan fiables con plantilla).
5. Rehacer las tarjetas LED y batería cuadradas (9×9) y/o generar el **set final de tarjetas** (componente
   dibujado a lo largo del eje de los terminales, marca de "arriba" impresa, fondo con muchos detalles):
   ataca el problema de reconocimiento y la incoherencia dibujo/terminales.
6. Panel de diagnóstico en pantalla (FPS y estado de seguimiento por tarjeta) para depurar el reconocimiento.
7. Ideas a futuro: tarjeta "unión" de 3 terminales (paralelo fiable sin plantilla), esquemático en vivo,
   tutorial inicial, registro de actividad del estudiante (para la evaluación con compañeros), informe y
   diapositivas para el profesor, borrar las carpetas basura de `Assets/`, iOS (requiere Mac).

### 000.d Guía de pruebas y regla de conexión nueva (2026-10-30)
**Entregable:** `Guia_de_pruebas/GUIA_DE_PRUEBAS.md` + `Guia_de_pruebas/plantillas/` (6 PNG y
`Plantillas_A3.pdf`, hoja A3 apaisada al 100 %, con contorno de cada tarjeta, ▲ de "arriba de la
imagen", terminales con los colores de las esferas de la app y regla de 10 cm). Pruebas: 1 básico
(4 tarjetas), 2 cortocircuito (batería + 2 interruptores), 3 dos LED en serie, 4 AND, 5 OR (paralelo),
6 control individual (paralelo). Valores recomendados: 9 V y 470 ohm (básico, AND, OR), 9 V y 330 ohm
(dos LED y control individual). Tabla maestra resistencia × voltaje en la guía.
**Cómo se obtuvieron las posiciones:** búsqueda (recocido simulado, `scratchpad/layouts/search.py`) con
un simulador que replica la regla de conexión, evaluando cada disposición con errores aleatorios de
colocación. Restricción: que quepa en A3 (38×25.5 cm). Éxito al colocar sobre la plantilla (±0.5 cm,
±4°): 99-100 % en las 6; a ojo (±1.5 cm, ±10°) las de serie 91-99 % pero **las de paralelo ~30 %**
(un punto con 3 terminales exige distancias casi iguales). Un cortocircuito de 2 tarjetas no es
viable con tarjetas de 9 cm (por eso la prueba 2 usa 3).
**Cambio de código:** la regla del terminal más cercano pasó de `cercano × 1.12 + 1 mm` a
`cercano × 1.05 + 2 cm` (campos `nearestRatio` = 1.05 y `nearestSlack` = 0.02 en `CircuitManager`;
se renombró el campo viejo `nearestTolerance` porque la escena guardó 1.12 y pisaba el nuevo
valor; `Node` usa `manager.NearestSlack`). Mejora el paralelo sin perjudicar la serie. Test nuevo:
`PuntoDeUnionConTresTerminales_ToleraDiferenciasDeDistancia`. **Pendiente correr las pruebas.**
**Mensaje nuevo:** circuito cerrado pero sin corriente (ej. 3 V con 2 LED, o 10 k ohm) ya no dice
"corriente fluyendo correctamente": avisa que el voltaje no alcanza (cables naranja).
**Aviso importante:** en la base de datos de Vuforia `marcador_led` mide 9 × 17.7 cm y
`marcador_bateria` 9 × 13.4 cm (no cuadradas); las plantillas suponen 9 × 9. Rehacer esas dos
tarjetas cuadradas o imprimirlas con la proporción de la base de datos. Posible causa de mal
reconocimiento si se imprimieron con otra proporción.
**Idea pendiente para el paralelo físico fiable sin plantilla:** tarjeta "unión" con 3 terminales
unidos (tipo `Junction`) o tarjetas más chicas (6 cm).

### 000.c Simulación eléctrica, etiquetas, flujo, retos y efectos (2026-10-30)
**Todo escrito sin Unity abierto para compilar, pero verificado así:** (1) el solver puro se
probó fuera de Unity con 24 casos de Ley de Ohm (24/24 OK); (2) todos los scripts de
`Assets/Scripts/Circuit`, `Assets/Editor` y `Assets/Tests` compilan sin errores ni avisos con el
mismo Roslyn y las mismas DLL de Unity (`dotnet csc.dll` de `Editor/Data/DotNetSdk`, ver abajo);
(3) **las pruebas Edit Mode NO se han corrido** (necesitan el motor): Window > General > Test Runner.
Backup: `Backups/2026-10-30_simulacion-y-retos/`.

**Archivos nuevos:** `CircuitSolver.cs` (análisis nodal, sin Unity), `ComponentLabel.cs`,
`CircuitUnits.cs`, `CircuitEffects.cs` (chispas/humo por código), `CircuitSounds.cs` (sonidos
sintetizados), `ChallengeManager.cs` (retos + panel UI creado por código; se auto-crea con
`RuntimeInitializeOnLoadMethod`, no hay que tocar la escena), `Tests/EditMode/CircuitSimulationTests.cs`.
**Modificados:** `CircuitManager` (Simulate, etiquetas, `Components`), `CircuitTypes` (CircuitResult con
lecturas), `ElectronicComponent` (parámetros eléctricos, `CycleResistance/CycleVoltage`, `isBurned`),
`ComponentVisualFeedback` (`SetBrightness`, `SetBurned`), `Node` (flujo animado), `CircuitFeedback`
(mensajes con números), `TapToToggleSwitch` (resistencia y batería por cercanía), tests existentes
(los LED ya no se queman en `CircuitManagerTests`: `ledBurnCurrent = 1000` en el helper).

**Modelo eléctrico:** batería 9 V con 1 ohm internos; resistencia de `resistanceOhms` (220 por
defecto en la escena); interruptor cerrado = 0.01 ohm, abierto = nada; LED = diodo con Vf 2 V y
10 ohm en conducción. Terminales unidos por cable = mismo nodo. Brillo del LED = I / 20 mA.
**Aviso "al límite" > 35 mA; se QUEMA > 50 mA** (queda quemado hasta retirar y volver a poner la
tarjeta). Resistencia que disipa > 0.25 W = "caliente". Con 9 V y 220 ohm el LED recibe 30 mA
(brilla, sin aviso); con 100 ohm recibe 63 mA y se quema.
**Interacción nueva:** tocar CERCA de la resistencia cambia su valor (100, 220, 330, 470, 1k,
2.2k, 4.7k, 10k); tocar CERCA de la batería cambia el voltaje (3, 5, 9, 12 V). El interruptor
sigue por collider. Hay etiquetas flotantes (valor y corriente) sobre cada tarjeta, pulsos que se
mueven por los cables según la corriente, y sonidos (clic, acierto, zap, quemado).
**Retos (11):** enciende el LED, corriente segura, abre y cierra, poca corriente, cortocircuito,
polaridad, quema un LED, dos LED en serie, AND, OR (tablas de verdad observadas), control individual.
Progreso en `PlayerPrefs` (clave `reto_completado_N`).
**Por verificar en Unity/celular:** tamaño y legibilidad de las etiquetas (`ComponentLabel`:
`WorldScale 0.016`, `HeightAboveCard 0.075`), posición/tamaño del panel de retos, que las pruebas pasen.
**Verificar compilación sin abrir Unity:** `dotnet "<Unity>/Editor/Data/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll"`
con `-nostdlib` y referencias a `NetStandard/ref/2.1.0/netstandard.dll`, `Managed/UnityEngine/*.dll`,
`Library/ScriptAssemblies/Unity.TextMeshPro.dll` y `UnityEngine.UI.dll`; el ensamblado de producción debe
llamarse `Circuit.dll` y el de pruebas `Circuit.Tests.dll` (por `InternalsVisibleTo`).

### 000.b Prueba en el celular (2026-10-30) y cambios de conexión
**Hallazgo de las fotos:** en la prueba en línea (batería–resistencia–interruptor–LED) los
cables salían naranja = correcto: una fila no cierra el circuito. En la prueba en
rombo seguía abierto por mala colocación: LED e interruptor a ~9 cm entre sí (deben
estar a ~20 cm de centro a centro), la resistencia abajo con el mismo giro que las
tarjetas laterales (debe ir con el texto horizontal), y el LED con el texto de abajo
hacia arriba (= giro antihorario; debe ser **horario**, para que el ánodo apunte hacia
el + de la batería). Con el umbral fijo de 12 cm, a 12 cm entre tarjetas solo
conectaba 1 de 4 uniones y a 14 cm ninguna.

**Cambios de código (sin compilar aún; Unity los compila al abrir):**
- `CircuitManager`: nuevos campos `Override Node Distances` (true), `Connect Distance`
  **0.25**, `Disconnect Distance` **0.27** y `Nearest Tolerance` **1.12**. Valen para
  TODOS los Node (ya no hay que tocar los 12 uno por uno; los valores de cada Node solo
  se usan si se desactiva el override). Al ser campos nuevos, la escena existente toma
  estos valores por defecto sin editarla.
- `Node`: **regla del terminal más cercano**: una unión solo se hace si cada lado la
  tiene entre sus más cercanos (distancia ≤ cercano × 1.12, simétrica y con histéresis
  ×1.1 para romper). Permite distancias grandes sin uniones falsas. Con errores de
  colocación de ±1.5 cm y ±10° el anillo da exactamente las 4 uniones correctas el
  ~99-100 % de las veces para S = 9-14 cm, ~94 % a 16 cm; a 20 cm hay que subir
  `Connect Distance`. Con tolerancia 0 la regla se desactiva.
- `Node`: **marcadores visibles de terminal** (esfera de 1 cm sobre la tarjeta):
  rojo = +, azul = −, amarillo = A, cian = B; **verde = ya unido**. Se apagan con
  `Show Marker` del Node. Sirven para ver dónde están realmente los terminales.
- `CircuitManagerTests`: +3 pruebas (unión a 20 cm, regla del más cercano, más lejos
  del máximo). Pendiente correrlas en Test Runner.
- Anillo con la batería ARRIBA (como en la foto): Batería (0,+S) giro 180°; Interruptor
  (±S,0) vertical; Resistencia (0,−S) sin girar (texto horizontal); LED vertical con el
  ánodo (+) hacia arriba (giro horario), en el lado por donde sale el + de la batería.

## 00. Dónde quedamos (fin de sesión 2026-10-14)

**Estado:** la escena `TestAR` quedó **verificada en disco** y se lanzó el
*Build And Run* a Android (Galaxy A31, `RF8N830D74H`). **El resultado en el
celular está por confirmar** — es lo primero que hay que revisar al retomar.

**Backups** (fuera de `Assets/`, Unity los ignora): `Backups/2026-10-14_antes-del-build/`
(Assets sin la basura anidada, ProjectSettings, Packages, marcadores, este
documento, y `TestAR_antes-de-disconnect.unity`).

### Hecho y verificado esta sesión
- **Cables visibles:** `Node.DrawCable` ya no depende de `cablePrefab` (estaba
  vacío, por eso nunca salía cable). Si está vacío genera un `LineRenderer` por
  código (shader `Sprites/Default`). Color vía `CircuitManager.SetCableColor`
  desde `CircuitFeedback`: **verde** = cerrado, **naranja** = abierto / sin
  batería, **rojo** = cortocircuito o LED invertido.
- **"El modelo se queda al cambiar de tarjeta":** causa = `Status Filter` en
  `Tracked_ExtendedTracked` (1). Ahora **`Tracked` (0) en los 6 ImageTarget**.
- **Tocar el interruptor:** `TapToToggleSwitch` ya está en la escena y los
  `BoxCollider` de los 2 interruptores miden 8×6×6 (antes 1 cm, imposible
  acertar con el dedo).
- **`SwitchVisual.cs`:** tiñe el interruptor (verde cerrado / rojo abierto) y
  opcionalmente gira una palanca. Puesto en los 2 `Interruptor_Modelo`.
- **LED que no brillaba:** `ComponentVisualFeedback` autodetecta los renderers si
  `Emissive Renderers` está vacío (el renderer vive dentro del FBX). `Mat_LED`
  tiene Emission activada. **Truco:** con `_EmissionColor` en negro puro el
  shader Standard apaga solo la casilla Emission; por eso el color es `0.02`
  (casi negro) y el script lo pone a negro en runtime con `MaterialPropertyBlock`.
  Sin esa casilla la variante del shader ni entra al build.
- **Ya son 6 tarjetas** (límite `maxSimultaneousImageTargets: 6`): Batería,
  Resistencia, Interruptor, **Interruptor 2** (`marcador_interruptor_2`), LED y
  **LED 2** (`marcador_led_2`). Imágenes en `Marcadores_Placeholder/`, cada una
  con ruido de fondo propio para que Vuforia no las confunda.
- **Distancias de conexión:** `Connect` **0.12** / `Disconnect` **0.13** en los 12
  `Node` (antes 0.15/0.2: con 0.15 se conectarían terminales que no
  corresponden, a ~13.7 cm).
- **LED corregido:** el `Terminal_Positivo` quedó a la **izquierda** (igual que el
  dibujo de la tarjeta) y el Negativo a la derecha.
- **iOS:** evaluado y **descartado por ahora** (requiere Mac + Xcode y cuenta
  Apple Developer para distribuir; faltarían `Camera Usage Description` y
  Bundle Identifier de iPhone).

### ⚠️ Incidente y regla nueva: componentes de circuito solo en los `*_Modelo`
Se encontraron componentes **de más en los 6 `ImageTarget` raíz** (un `Node` en
cada uno; en los 2 de interruptor además `Switch Visual` + un `Electronic
Component` tipo Batería que Unity agregó solo por el `RequireComponent`). Eso
habría roto el circuito: una "batería fantasma" sin terminales podía ganar
`FindComponent(Battery)` y los `Node` fantasma quedan siempre activos. Ya se
limpió. **Regla:** `ElectronicComponent`, `ComponentVisualFeedback`,
`SwitchVisual` y los `Node` van en el modelo hijo (`Bateria_Modelo`,
`LED_Modelo`, `Interruptor_Modelo`, `Resistencia_Modelo`) y sus `Terminal_*`;
**nunca en el `ImageTarget` raíz**. (Ojo: la tabla de la sección 6 dice "mismo
GameObject que el Image Target"; en esta escena quedó en el modelo hijo.)
Varios ImageTarget se llaman igual — identificarlos por su hijo.

### Geometría física (tarjetas 9×9 cm, `mWidth: 0.09`)
Terminales sobre el eje X local de cada tarjeta, en cm desde el centro:
Batería **+2.52 / −3.22** · Interruptor **A −2.51 / B +2.07** (modelo a escala
0.009) · Resistencia **A −3.38 / B +2.33** · LED **+ −3.28 / − +4.34** (modelo
a escala 0.005).

**Anillo de 4 tarjetas** (vista desde arriba, X derecha, Y al frente, cm):
| Tarjeta | Centro | Giro |
|---|---|---|
| Batería | (0, −10) | 0° (+ a la derecha) |
| Interruptor | (+10, 0) | 90° antihorario |
| Resistencia | (0, +10) | 180° |
| LED | (−10, 0) | **90° horario** (ánodo hacia arriba) |

Uniones correctas: 8.8–10.6 cm; la incorrecta más cercana: 13.7 cm → umbral
0.12/0.13. **Con el LED antihorario el circuito NO cierra.** Un circuito de 3
tarjetas no es viable con tarjetas de 9 cm (margen de 0.2 cm).

### Ejemplos de prueba con el anillo (mensajes esperados de `CircuitFeedback`)
1. **Armado paso a paso:** batería sola → "Circuito abierto"; +interruptor →
   abierto (cable naranja); +resistencia → abierto; +LED → **"Circuito cerrado"**,
   cables verdes, LED brilla.
2. **Interruptor abierto:** tocarlo en pantalla → "Circuito abierto", naranja, LED apagado.
3. **LED invertido:** girar el LED 180° en su sitio → "polaridad invertida", cables
   rojos, halo de error.
4. **Cortocircuito:** solo batería + interruptor cerrado, uno bajo el otro a 10 cm,
   sin girar → "¡Cortocircuito!", rojo. (La chispa aún no existe.)
5. **Falla de tarjeta:** quitar una → "abierto"; quitar la batería → "Coloca la
   tarjeta de la batería".

### Plan con 6 tarjetas (1 batería, 1 resistencia, 2 interruptores, 2 LED)
Serie con 2 LED, AND, OR, paralelo con un interruptor por rama
(batería → resistencia → [SW1+LED1 ‖ SW2+LED2]) y mixto con interruptor general.
La lógica del `CircuitManager` ya los soporta; **faltan las posiciones físicas**
(el paralelo necesita puntos donde se juntan 3 terminales: decidir si el arte
final lleva los terminales en el borde o si se crea una tarjeta "unión").
Cada tarjeta extra = imagen distinta (Vuforia no sigue dos copias iguales).

### Pendiente / siguiente
1. **Confirmar en el celular:** modelo desaparece al cambiar de tarjeta, salen
   cables, el toque cambia el interruptor (y su color), el LED brilla con el
   circuito cerrado, anillo de 4 funcionando.
2. Borrar de `Assets/` las copias basura del 12-sep: `Assets/Assets`, `Library`,
   `Logs`, `Packages`, `ProjectSettings`, `Temp`, `UserSettings` (desde el
   Project de Unity, para limpiar también los `.meta`).
3. Decidir tamaño final de tarjetas (hoy 9 cm declarado) y dónde van los
   terminales; luego calcular posiciones de los circuitos de 6 tarjetas.
4. Escribir pruebas Edit Mode para los circuitos de 6 tarjetas (2 LED en serie,
   paralelo con control individual).
5. Reemplazar modelos placeholder restantes (Batería, LED, Resistencia) por 3D
   reales; chispa de cortocircuito (`ParticleSystem` + `sparkPrefab`).

### Gotchas nuevos de esta sesión
- Editar el `.unity` desde fuera con Unity abierto muestra "Scene modified
  externally": elegir **Reload** y no guardar antes (con *Ignore* + Ctrl+S se
  pierde el cambio). Mejor: copia de seguridad antes y comprobar que el título de
  la ventana no tenga `*`.
- Al agregar un componente que tiene `RequireComponent`, Unity crea el requerido
  con valores por defecto (aquí: `ElectronicComponent` tipo Batería) — verificar
  siempre en qué objeto se está agregando.

## 0. Dónde quedamos (sesión anterior, 2026-09-29)

Se decidió usar **modelos 3D descargados de internet** (no Blender desde
cero) para reemplazar los placeholders. Se importó el primero (interruptor,
"Power Switch" de un sitio de CAD/impresión 3D — venía en SLDPRT/STEP/STL, se
convirtió a `.fbx`). Se mejoró también el `StatusText` con un panel de fondo
semi-transparente (`StatusBackground`) para que no se pierda sobre la imagen
de la cámara. **Se hizo un build a Android exitoso** (`Builds/prueba 2.apk`,
instalado y lanzado en el Samsung Galaxy A31 de prueba) — pendiente confirmar
en la próxima sesión cómo se ve/funciona realmente en el celular.

**⚠️ Se perdió trabajo una vez esta sesión** (modelos + panel de texto) por
cerrar Unity sin guardar la escena antes de borrar la caché de build — se
rehizo. Ver el aviso al final de esta sección: **guardar (Ctrl+S) seguido**,
sobre todo antes de cerrar el Editor por cualquier motivo.

**Para retomar, en este orden:**
1. Confirmar en el celular (con la build ya instalada) que se vean bien el
   modelo del interruptor y el panel de texto nuevo; probar reconocimiento
   con las 4 tarjetas y un circuito cerrado completo en el celular real.
2. Seguir reemplazando los placeholders restantes con modelos de internet
   (Batería, LED; la Resistencia puede que ya se haya hecho antes de la
   pérdida de trabajo — confirmar su estado al abrir el proyecto).
3. Decidir el **tamaño físico final de las tarjetas impresas** (se habló de
   6-10cm por lado; sugerencia dada: 8cm). Declarar ese mismo ancho en
   metros en cada target del Target Manager de Vuforia, e imprimir siempre a
   "Tamaño real / 100%" (nunca "Ajustar a página").

**Recordatorio permanente:** guardar la escena (Ctrl+S) después de cada
cambio importante en el Editor, y SIEMPRE antes de cerrar Unity por
cualquier motivo (incluido limpiar caché de build). No hay autosave.

Ver sección 6 (**Estado actual de la implementación**) para el detalle
completo de qué se armó y qué falta.

## 1. Qué es

Aplicación de Realidad Aumentada (Unity) para enseñar electrónica básica en el aula.
El estudiante coloca tarjetas físicas (Image Targets) sobre el pupitre; la app
reconoce varias a la vez, superpone los modelos 3D de cada componente y evalúa
en tiempo real si el circuito que armaron es válido.

Componentes iniciales (4 tipos de tarjeta física):

- **Batería** (fuente de alimentación)
- **Resistencia**
- **LED** (diodo, polarizado)
- **Interruptor** (abierto/cerrado)

Los **cables NO son tarjetas físicas**: se generan automáticamente como cables
virtuales cuando dos terminales de componentes distintos quedan lo bastante
cerca en la mesa (conexión por proximidad).

## 2. Tecnologías y herramientas

- **Motor**: Unity 3D.
- **SDK de AR**: Vuforia Engine (recomendado por su estabilidad en rastreo
  simultáneo de múltiples Image Targets) o, alternativamente, AR Foundation
  (ARCore/ARKit). Requiere habilitar **Simultaneous Tracked Images**.
- **Marcadores**: tarjetas con patrones asimétricos y de alto contraste,
  diseñadas para que el reconocimiento de imágenes no las confunda entre sí;
  se necesitan varias cartas por tipo (varias resistencias, varios LEDs, etc.).

## 3. Lógica de funcionamiento (diseño objetivo)

1. **Rastreo simultáneo**: la cámara detecta a la vez las tarjetas de batería,
   interruptor, LED, resistencia, etc., y superpone sus modelos 3D.
2. **Nodos de conexión (terminales)**: cada componente tiene "nodos" invisibles
   en sus extremos (polo +/− o terminal A/B).
3. **Cálculo de proximidad**: un script mide constantemente la distancia entre
   nodos de tarjetas distintas; por debajo de un umbral (≈3 cm) se conectan
   lógica y visualmente (cable dibujado con LineRenderer).
4. **Circuit Manager**: algoritmo central que interpreta todas las conexiones
   activas como un grafo y evalúa si existe un camino cerrado entre el polo +
   y el polo − de la batería, respetando la polaridad de componentes sensibles
   (LED) y el estado de los interruptores.
5. **Retroalimentación inteligente**: si el circuito es válido, se anima el
   flujo eléctrico sobre los cables; si hay errores (circuito abierto,
   polaridad invertida, cortocircuito), se muestran alertas y pistas visuales
   para guiar al estudiante a corregirlos.

## 4. Casos pedagógicos a soportar

### 4.1 Conexiones estructurales
- **Circuito abierto/cerrado**: mediante el interruptor.
- **Serie**: componentes alineados uno tras otro (ej. resistencia antes del
  LED, dos interruptores en la misma línea).
- **Paralelo**: ramificaciones donde la corriente se divide (ej. varias ramas
  alimentadas por la misma batería).

### 4.2 Validaciones técnicas y de seguridad
- **Polaridad invertida en el LED**: se detecta y se explica visualmente por
  qué no conduce (naturaleza de los semiconductores).
- **Prevención de cortocircuitos**: conexión directa +/− sin resistencia de
  por medio dispara una advertencia visual (chispa/humo digital), sin riesgo
  real — permite entrenar situaciones peligrosas de forma segura.

### 4.3 Lógica booleana básica (con interruptores, sin microchips)
- **AND**: dos interruptores en serie → el LED enciende solo si ambos están
  cerrados.
- **OR**: dos interruptores en paralelo → el LED enciende con que uno solo
  esté cerrado.

## 5. Plan de acción paso a paso

1. Configurar el entorno: instalar Unity, importar Vuforia o AR Foundation,
   configurar la cámara AR y habilitar Simultaneous Tracked Images.
2. Crear la base de datos de marcadores: diseñar las cartas (varias
   resistencias, LEDs, baterías, interruptores), subirlas al SDK y descargar
   la base de datos para integrarla en Unity.
3. Configurar prefabs 3D: un prefab por componente, con su modelo 3D y objetos
   vacíos en los extremos que actúan como Nodos de Conexión.
4. Sistema de conexión: script que mide distancia entre nodos y, si están
   cerca, los enlaza lógicamente y dibuja el cable con LineRenderer.
5. Lógica del circuito: script principal que recorre las conexiones para
   verificar si se forma un bucle cerrado y seguro.
6. Efectos y validaciones: feedback visual — LED "encendido" (emisión de
   material) y sistema de partículas simulando la corriente/chispa.

## 6. Estado actual de la implementación

Ya existe una primera versión funcional de la lógica (sin integración de
Vuforia todavía) en `Assets/Scripts/Circuit/`:

| Script | Rol | Corresponde al plan como |
|---|---|---|
| [CircuitTypes.cs](Assets/Scripts/Circuit/CircuitTypes.cs) | Enums compartidos (`ComponentType`, `NodeRole`, `EdgeKind`, `CircuitStatus`) y `CircuitResult`. | Modelo de datos común. |
| [Node.cs](Assets/Scripts/Circuit/Node.cs) | Terminal de un componente. Escanea proximidad contra el resto de nodos de la mesa, conecta/desconecta con histéresis (`connectDistance` / `disconnectDistance`), dibuja el cable (LineRenderer) y anima el flujo de corriente. | El "Nodo de Conexión" + parte de la lógica que el plan asignaba a `ConnectionManager.cs` (aquí la detección de proximidad vive directamente en `Node`, no en un manager aparte). |
| [ElectronicComponent.cs](Assets/Scripts/Circuit/ElectronicComponent.cs) | Va en la raíz de cada prefab (mismo GameObject que el Image Target). Se registra/da de baja en el `CircuitManager` al aparecer/perder tracking. Expone `positiveOrA` / `negativeOrB` y `isClosed` (interruptor). | El "glue" entre reconocimiento AR y el grafo lógico. |
| [CircuitManager.cs](Assets/Scripts/Circuit/CircuitManager.cs) | Modela la mesa como grafo y corre 3 BFS (estricto / relajando polaridad del LED / sin resistencias) para diagnosticar: sin batería, abierto, cerrado, polaridad invertida o cortocircuito. Publica `OnCircuitEvaluated`. | El "Circuit Manager" del plan. |
| [CircuitFeedback.cs](Assets/Scripts/Circuit/CircuitFeedback.cs) | Traduce `CircuitResult` en mensaje de texto (TMP_Text), enciende/apaga LEDs, activa halo de error sobre el componente responsable, dispara chispa en cortocircuito y prende/apaga la animación de flujo. | La "Retroalimentación inteligente". |
| [ComponentVisualFeedback.cs](Assets/Scripts/Circuit/ComponentVisualFeedback.cs) | Se agrega junto a `ElectronicComponent` en cada prefab. `SetLit()` cambia la emisión del material (LED) vía `MaterialPropertyBlock`; `SetFault()` activa/desactiva un halo de error. | Los "efectos y validaciones" a nivel de cada tarjeta. |
| [TapToToggleSwitch.cs](Assets/Scripts/Circuit/TapToToggleSwitch.cs) | Detecta tap (celular) / clic (Editor) en pantalla, lanza un rayo desde la cámara y llama `ToggleSwitch()` si golpea un `ElectronicComponent` tipo Switch. Ignora toques sobre la UI. Se agrega en un único GameObject de la escena. | Interacción real del estudiante con el interruptor (reemplaza tocar el checkbox `Is Closed` a mano). |
| [SwitchVisual.cs](Assets/Scripts/Circuit/SwitchVisual.cs) | Se agrega junto a `ElectronicComponent` en cada interruptor. Tiñe el modelo (verde cerrado / rojo abierto) leyendo `isClosed` cada frame y, opcionalmente, gira una palanca hija. | Retroalimentación visual del estado del interruptor sobre la propia tarjeta. |

Cubierto por el diseño actual del `CircuitManager`:
- Circuito abierto/cerrado (interruptor).
- Serie (cualquier cantidad de componentes encadenados por nodos).
- Cortocircuito (camino directo +/− sin resistencia ni LED).
- Polaridad invertida del LED (con identificación de qué LED es el culpable).
- Estado individual de cada LED según si realmente conduce corriente.

**⚠️ Gotcha importante de Vuforia (nos costó horas encontrarlo):** en
`Window > Vuforia Configuration`, el checkbox **"Delayed Initialization"**
viene marcado por defecto en el paquete descargado del Developer Portal. Con
eso marcado, Vuforia **nunca se inicializa solo** (ni cámara, ni tracking, ni
un solo log) — se queda esperando a que un script llame a
`VuforiaApplication.Instance.Initialize()` manualmente. Como no tenemos ese
código, se manifestaba como **pantalla negra silenciosa, sin ningún error en
Console**, tanto en el Editor (con webcam) como en el build de Android.
**Debe quedar SIEMPRE desmarcado** mientras no implementemos una pantalla de
carga con inicialización manual. Si en algún momento vuelve a aparecer
pantalla negra sin errores, este es el primer sospechoso a revisar.

**✅ Integración con Vuforia verificada (2026-09-12), funcionando en el
Editor de punta a punta:** Vuforia Engine 11.4.4 instalado, base de datos
`TTI_Circuitos` con marcadores placeholder (ver `Marcadores_Placeholder/` en
la raíz), `ImageTarget` con `Default Observer Event Handler` cuyos eventos
**On Target Found / On Target Lost** llaman `SetActive(true/false)` sobre
`Bateria_Modelo` (cilindro placeholder con `ElectronicComponent` +
`ComponentVisualFeedback` + dos `Node` hijos). Confirmado: al mostrarle la
imagen `Marcador_Bateria.png` a la cámara, el modelo aparece y desaparece
correctamente, lo que dispara `OnEnable`/`OnDisable` → registro automático en
`CircuitManager`. Pendiente: hacer lo mismo para Resistencia, LED e
Interruptor (mismo patrón, solo cambia el `Type` y qué Node se agregan).

**✅ Los 4 componentes armados y el loop completo verificado (2026-09-12):**
Batería, Resistencia, LED e Interruptor tienen cada uno su `ImageTarget` +
modelo placeholder (primitivas de Unity) + `ElectronicComponent` + `Node`s +
eventos On Target Found/Lost conectados. Se agregó `CircuitManager` y
`CircuitFeedback` (con un `Canvas` + `TextMeshPro Text`) a la escena `TestAR`.
Confirmado con la cámara real: al mostrar la Batería aparece el mensaje
"Circuito abierto: falta una conexión o un interruptor está abierto." — el
pipeline completo (AR → registro → evaluación → UI) funciona end-to-end.

**Bug encontrado y corregido:** `CircuitFeedback` se suscribía al evento
`OnCircuitEvaluated` en `OnEnable()`, pero el orden de `Awake()` entre
GameObjects de la escena no está garantizado en Unity — si `CircuitFeedback`
corría antes que `CircuitManager.Awake()`, la suscripción fallaba en
silencio y el texto se quedaba pegado en el mensaje inicial para siempre. Se
movió la suscripción a `Start()` (que Unity garantiza que corre después de
TODOS los `Awake()`/`OnEnable()` de la escena), y la desuscripción a
`OnDestroy()`.

**✅ Interacción real del interruptor (2026-09-12):** se agregó
`TapToToggleSwitch.cs` — tocar/clicar el modelo del interruptor en pantalla
alterna `Is Closed` (requiere que el modelo tenga Collider; los primitivos de
Unity ya lo traen). Reemplaza tener que tocar el checkbox a mano en el
Inspector, que solo servía para depurar en el Editor.

**✅ Primer modelo 3D real reemplazado (2026-09-29):** el Interruptor ya usa
un modelo descargado ("Power Switch", formato original SLDPRT/STEP/STL de un
sitio de CAD/impresión 3D — se convirtió a `.fbx` con un conversor online).
Procedimiento que funcionó: importar el `.fbx` → en su Inspector, pestaña
**Model > Normals > Calculate > Apply** (el modelo salía invisible hasta
hacer esto — normales mal orientadas, común en archivos STL/CAD) → quitar
`Mesh Filter`/`Mesh Renderer` del placeholder → meter el modelo nuevo como
hijo en Position 0,0,0 → ajustar Scale a ojo → crear y asignar un material
propio (el que traía el archivo, "Crate - Surface Shader Scene", estaba
roto) → reubicar los `Node` de terminal en los extremos reales del modelo.

**✅ Panel de fondo para el texto de estado (2026-09-29):** se agregó
`StatusBackground` (una `Image` de UI semi-transparente, anclada arriba con
stretch horizontal) como padre de `StatusText`, más `Auto Size` en el TMP
para que el mensaje largo no se corte en columna vertical. Resuelve el
problema de que el texto se perdía sobre el fondo de la cámara.

**✅ Primer build a Android exitoso y verificado en dispositivo real
(2026-09-29):** `Builds/prueba 2.apk` se instaló y lanzó en un Samsung
Galaxy A31 (Android, SM-A315G) vía `adb`/Build And Run. Pendiente: revisar
en la próxima sesión qué tan bien se ve/comporta en el celular (cámara real,
detección de marcadores, tamaño de modelos, legibilidad del texto).

**⚠️ Gotcha recurrente: caché de build corrupta ("Bee/Tundra").** Ya pasó
dos veces (12 y 29 de septiembre) el mismo error:
`bee_backend: error: Failed to open file "Library/Bee/tundra.log.json"...`,
que Unity reporta engañosamente como "scripts have compiler errors" sin que
haya ningún `error CS` real. Se soluciona: cerrar Unity → borrar la carpeta
`Library/Bee` (se regenera sola, es solo caché) → reabrir. Suele pasar
después de varios intentos de build seguidos o builds interrumpidos.

**⚠️ Gotcha crítico: Unity NO tiene autosave de escena.** El 29 de
septiembre se perdió el trabajo de una sesión completa (modelo del
interruptor + panel de texto) por cerrar el Editor sin guardar antes de
limpiar la caché de build. **Guardar con Ctrl+S seguido**, y siempre antes
de cerrar Unity por cualquier motivo — no hay forma de recuperarlo después.

**Pendiente / no verificado todavía:**
- Probar un circuito completo cerrado (batería + interruptor + LED, con o
  sin resistencia) — como la conexión es por proximidad física real (3cm por
  defecto en `Node.connectDistance`), acercar tarjetas en pantalla/papel
  puede ser incómodo; se subió a `0.15`/`0.2` en los 8 `Node` de la escena de
  prueba solo para facilitar las pruebas de escritorio (recordar bajarlo de
  nuevo, o dejarlo así si las tarjetas reales van a estar igual de separadas).
- Terminar de reemplazar los placeholders restantes (Batería, LED, y
  confirmar Resistencia) con modelos 3D descargados de internet — mismo
  procedimiento ya probado con el Interruptor (ver arriba). Para el LED en
  particular: reasignar el Mesh Renderer nuevo en
  `Component Visual Feedback > Emissive Renderers` con un material con
  Emission activado.
- Reemplazar las imágenes placeholder de `Marcadores_Placeholder/` por el
  diseño final de las tarjetas: reemplazar el target en el Target Manager de
  Vuforia (mismo nombre) → volver a descargar/importar la base de datos. No
  requiere tocar la escena.
- Pulir el `RectTransform` del `StatusText` (quedó muy angosto, el texto se
  corta en columna).
- Partículas de chispa/humo: el script ya tiene el hook (`sparkPrefab` en
  `CircuitFeedback`), pero falta crear el `ParticleSystem` real.

### 6.1 Pruebas automatizadas (Edit Mode) — ya escritas

Para validar la lógica del `CircuitManager` sin depender de tarjetas AR ni de
armar una escena a mano, se agregó una suite de pruebas de Unity Test
Framework que arma el grafo con GameObjects creados por código:

- [Assets/Scripts/Circuit/Circuit.asmdef](Assets/Scripts/Circuit/Circuit.asmdef) — assembly de producción (necesario para
  que las pruebas puedan referenciarlo).
- [Assets/Tests/EditMode/Circuit.Tests.asmdef](Assets/Tests/EditMode/Circuit.Tests.asmdef) — assembly de pruebas (solo Editor).
- [Assets/Tests/EditMode/CircuitManagerTests.cs](Assets/Tests/EditMode/CircuitManagerTests.cs) — casos cubiertos: sin batería,
  interruptor abierto, serie con resistencia (cerrado), cortocircuito sin
  resistencia, LED en polaridad invertida (con identificación del LED
  culpable), LED en polaridad correcta (enciende), compuerta AND (2
  interruptores en serie) y compuerta OR (2 interruptores en paralelo).

También se agregaron cambios pequeños de soporte en el código de producción:
- `Node.ForceScan()`: fuerza el escaneo de proximidad sin esperar el
  `scanInterval` (las pruebas lo usan para conectar nodos al instante; también
  sirve en runtime real para revisar conexiones apenas aparece una tarjeta).
- `CircuitManager.OnDestroy()`: limpia `Instance` al destruirse, para no dejar
  una referencia colgante.
- `CircuitManager.SetInstanceForTests(...)` (internal, visible solo para el
  ensamblado `Circuit.Tests` vía `InternalsVisibleTo`): fija el singleton a
  mano en las pruebas.

**Hallazgo importante sobre Edit Mode Tests:** en esta versión de Unity,
`Awake()`/`OnEnable()` **no se ejecutan de forma síncrona** cuando se hace
`AddComponent<T>()` dentro de un `[Test]` de Edit Mode — Unity los difiere a
un tick del editor que nunca llega dentro de un test síncrono (se comprobó
viendo que `CircuitManager.Instance` seguía en `null` justo después de
`AddComponent<CircuitManager>()`). Por eso las pruebas NO pueden depender de
que `Node.OnEnable`/`ElectronicComponent.OnEnable` se registren solos: cada
prueba registra explícitamente cada `Node`/`ElectronicComponent` llamando a
`manager.RegisterNode(...)` / `manager.RegisterComponent(...)` justo después
de crearlo, y fija el singleton con `CircuitManager.SetInstanceForTests(manager)`
en vez de esperar a `Awake()`. Tenerlo en cuenta para cualquier prueba nueva
que se agregue más adelante.

**Cómo correrlas:** una vez el proyecto esté abierto en Unity (con el paquete
"Test Framework" instalado, que Unity ofrece instalar solo al abrir el Test
Runner) → `Window > General > Test Runner` → pestaña **EditMode** → *Run All*.
No requiere entrar a Play Mode ni tener escenas, prefabs ni Vuforia
configurados.

**Estado: ✅ las 8 pruebas pasan (2026-09-12).** El Paso 1 del plan (validar la
lógica sin AR) quedó cerrado. De paso, correr esto en un proyecto recién
creado destapó dos problemas reales del entorno que ya se resolvieron:
- `Object.GetInstanceID()` está marcado obsoleto/error en esta versión de
  Unity 6 → se reemplazó por una comparación estructural del par de nodos en
  `CircuitManager.BuildEdges`.
- Faltaban los paquetes `com.unity.ugui` (que en Unity 6 incluye lo que antes
  era TextMeshPro) y `com.unity.test-framework` → instalados vía Package
  Manager.

## 7. Convenciones del proyecto

- Comentarios y mensajes de UI en **español**.
- Un componente = un GameObject raíz con `ElectronicComponent` +
  `ComponentVisualFeedback`, e hijos `Node` en cada terminal.
- Terminales polarizados (Batería, LED) usan `NodeRole.Positive/Negative`;
  no polarizados (Resistencia, Interruptor) usan `NodeRole.TerminalA/TerminalB`.
- `CircuitManager` es un singleton (`Instance`) y reevalúa una sola vez por
  frame en `LateUpdate` cuando algo se marca `dirty`.
