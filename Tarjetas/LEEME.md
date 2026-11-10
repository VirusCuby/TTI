# Tarjetas del simulador (9 x 9 cm)

Seis tarjetas cuadradas, cada una con una **familia de patrón distinta** para que Vuforia no las confunda
(por ejemplo, que el LED aparezca sobre el interruptor):

| Tarjeta | Archivo | Patrón | Nombre en el Target Manager |
|---|---|---|---|
| Batería | `Marcador_Bateria.png` | mosaico de triángulos (ámbar) | `marcador_bateria` |
| Resistencia | `Marcador_Resistencia.png` | celdas de Voronoi (azul) | `marcador_resistencia` |
| Interruptor | `Marcador_Interruptor.png` | pistas de circuito impreso (verde) | `marcador_interruptor` |
| Interruptor 2 | `Marcador_Interruptor_2.png` | burbujas y anillos (violeta), insignia hexagonal "2" | `marcador_interruptor_2` |
| LED | `Marcador_LED.png` | palitos cruzados (rojo) | `marcador_led` |
| LED 2 | `Marcador_LED_2.png` | camuflaje orgánico (rosa), insignia circular "2" | `marcador_led_2` |

Cada una trae: margen blanco del 8 % (esa franja Vuforia no la analiza), el símbolo del componente sobre el eje
de los terminales con las esferas de color de la app (rojo +, azul −, amarillo A, cian B), el triángulo
**ARRIBA** y el nombre en el lado inferior.

## Cómo ponerlas en la app

1. Developer Portal de Vuforia > Target Manager > base de datos `TTI_Circuitos`.
2. Borra los 6 targets actuales y súbelos de nuevo (*Single Image*), uno por cada PNG de esta carpeta:
   - **Width = 0.09** (metros), **sin cambiar el nombre** (minúsculas, tal como aparece en la tabla).
   - Mira la **puntuación de estrellas** de cada uno: debe ser de 4 o 5. Si alguno saca menos, avísame.
3. *Download Database* > *Unity Editor* e importa el `.unitypackage` en Unity (reemplaza los archivos de
   `Assets/StreamingAssets/Vuforia` y `Assets/Editor/Vuforia`).
4. No hay que tocar la escena: los nombres son los mismos y ahora todas miden 9 x 9 cm (antes `marcador_led`
   medía 9 x 17.7 y `marcador_bateria` 9 x 13.4).

## Imprimir

`Tarjetas_A4.pdf`: imprimir al **100 %** (tamaño real, nunca "ajustar a la página"). Comprueba con regla que
la línea de arriba mide 5 cm y cada tarjeta 9 x 9 cm. Papel **mate**, recortar por el contorno gris y pegar
sobre cartón rígido. Las plantillas de `Guia_de_pruebas/` siguen valiendo (9 x 9 cm, ▲ = arriba de la imagen).

## Regenerar

`python generar_tarjetas.py` (necesita Pillow, numpy y scipy). Los patrones salen de una semilla fija por
tarjeta, así que se obtienen siempre las mismas imágenes; para cambiar el diseño se edita el script.

## Validación

`evaluar_tarjetas.py` simula fotos de cámara (giro, inclinación hasta 50°, desenfoque, ruido, distancia) y mide
con SIFT + RANSAC. Resultado con las tarjetas nuevas frente a las antiguas de `Marcadores_Placeholder` (30 vistas
por tarjeta): identificación correcta **96 % vs 51 %**, ~90-230 coincidencias por vista vs ~7-40. Es una
aproximación (Vuforia usa su propio detector): la prueba real es con el celular.
