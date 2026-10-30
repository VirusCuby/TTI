# TTI — Simulador AR de circuitos eléctricos

Aplicación de **realidad aumentada** (Unity + Vuforia) para enseñar electrónica básica. El estudiante coloca tarjetas físicas (batería, resistencia, interruptor, LED) sobre la mesa; la app las reconoce, las conecta por proximidad y **calcula el circuito con la Ley de Ohm**: corriente, caídas de voltaje, brillo del LED, LED quemado, cortocircuito, paralelo y compuertas AND/OR.

Proyecto de la Universidad Nacional de Trujillo (ciclo 2026-II).

## Qué hace

- Reconoce hasta 6 tarjetas a la vez y dibuja cables virtuales entre los terminales cercanos.
- Resuelve el circuito (análisis nodal): batería de 3, 5, 9 o 12 V; resistencias de 100 Ω a 10 kΩ; LED con voltaje mínimo y límite de corriente.
- Muestra valores sobre cada tarjeta, pulsos de corriente en los cables, LED con brillo variable, humo y chispas.
- 11 retos guiados (enciende el LED, corriente segura, cortocircuito, compuertas AND/OR, control individual…) con puntaje.
- Se cambia la resistencia y el voltaje tocando cerca de la tarjeta.

## Requisitos

- **Unity 6000.5.10f1** (módulo Android).
- **Vuforia Engine 11.4.4** y una clave de licencia de desarrollo (gratis en <https://developer.vuforia.com>).
- Celular Android con cámara (probado en un Samsung Galaxy A31).

## Cómo abrir el proyecto

1. Clona el repositorio.
2. **Descarga el SDK de Vuforia 11.4.4** (`com.ptc.vuforia.engine-11.4.4.tgz`) desde el portal de Vuforia y colócalo en la carpeta `Packages/`. No está en el repositorio: pesa 132 MB y es software de terceros.
3. Abre la carpeta con Unity Hub.
4. **Pon tu clave de licencia:** *Window > Vuforia Configuration > App License Key*. En el repositorio el archivo `Assets/Resources/VuforiaConfiguration.asset` trae el marcador `PEGA_AQUI_TU_CLAVE_DE_LICENCIA_DE_VUFORIA` en lugar de la clave real.
5. Verifica que **Delayed Initialization** esté **desmarcado** en esa misma ventana (si no, la cámara se queda en negro).
6. Abre la escena `Assets/Scenes/TestAR.unity`.

> La base de datos de marcadores (`Assets/StreamingAssets/Vuforia/TTI_Circuitos.*`) sí está incluida. Las imágenes de ejemplo de las tarjetas están en `Marcadores_Placeholder/`.

## Estructura

| Carpeta | Contenido |
|---|---|
| `Assets/Scripts/Circuit/` | Lógica: solver de circuitos, conexión por proximidad, retos, etiquetas, efectos |
| `Assets/Tests/EditMode/` | Pruebas automáticas (Test Runner > EditMode) |
| `Assets/Editor/ReemplazarModelos.cs` | Herramienta para cambiar los modelos 3D (menú *Tools > Circuito*) |
| `Assets/Modelos_nuevos/` | Modelos 3D de batería, LED y resistencia |
| `Marcadores_Placeholder/` | Imágenes de las tarjetas de ejemplo |
| `Nuevos modelos/` | Modelos 3D originales (antes de prepararlos) |
| `Guia_de_pruebas/` | Guía de seis pruebas con posiciones, valores de voltaje y resistencia, y plantillas A3 para imprimir |
| `CONTEXTO_PROYECTO.md` | Bitácora técnica del proyecto: decisiones, problemas conocidos y pendientes |

## Pruebas automáticas

*Window > General > Test Runner > EditMode > Run All.* Cubren la lógica de conexión, la Ley de Ohm, el LED quemado, el paralelo y las compuertas.

## Lo que NO está en el repositorio (a propósito)

`Library/`, `Temp/`, `Logs/` (cachés que Unity regenera), los APK compilados, las copias de seguridad, el SDK de Vuforia y la clave de licencia.

## Guía de pruebas

Mira [`Guia_de_pruebas/GUIA_DE_PRUEBAS.md`](Guia_de_pruebas/GUIA_DE_PRUEBAS.md).
