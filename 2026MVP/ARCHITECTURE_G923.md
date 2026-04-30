# Arquitectura de Input — Logitech G923 PS/PC

## Flujo de datos

```
G923 USB HID → Unity Input System → UIInputNew → PlayerCar → WheelColliders
                                         ↓
                                   SimpleSpeedGauge (display gear + direccionales)
```

## Dispositivo

- **Modelo:** Logitech G923 Racing Wheel for PlayStation 4 and PC
- **Layout Unity:** `HID::Logitech G923 Racing Wheel for PlayStation 4 and PC`
- **Deteccion:** Dinamica por nombre (`displayName.Contains("G923")`)
- **Se registra como:** `Joystick` (no `Gamepad`)

## Mapeo de ejes

Los ejes se leen via `InputAction` (funciona para ejes HID genericos).

| Control Unity | Fisico | Rango raw | Normalizado |
|---------------|--------|-----------|-------------|
| `stick/x` | Volante (steering) | -1 (izq) a 1 (der) | Directo |
| `z` | Pedal acelerador | 1 (suelto) a -1 (pisado) | `(1 - raw) / 2` → 0..1 |
| `rz` | Pedal freno | 1 (suelto) a -1 (pisado) | `(1 - raw) / 2` → 0..1, luego curva exponencial |
| `stick/y` | Pedal clutch | -1 a 1 | **No implementado aun** |

### Curva del freno

El freno usa una curva exponencial para dar mas respuesta al final del recorrido:

```
brakeLinear = (1 - raw) / 2       // 0..1 lineal
brake = brakeLinear^2 * 2          // exponencial, clamp a 1
```

- Pedal al 25% → frenado 12%
- Pedal al 50% → frenado 50%
- Pedal al 75% → frenado 100%

### Calibracion de frenado

- 64 km/h → 21.9m (valor real ~21m) ✓
- `maxBrakeTorque = 3000` en PlayerCar
- Log `[FRENO]` muestra distancia al detenerse (para recalibrar si se cambia masa/friccion)

## Mapeo de botones

Los botones se leen via **acceso directo al device** (`TryGetChildControl`), NO via InputAction bindings.
InputAction bindings no resuelven botones en dispositivos HID genericos.

### IMPORTANTE: Paddles invertidos en G923 PS

En el G923 version PlayStation, los paddles estan invertidos a nivel HID:
- **button5** = paddle **derecho** (R1)
- **button6** = paddle **izquierdo** (L1)

| Boton Unity | Fisico G923 PS | Funcion |
|-------------|---------------|---------|
| button1 | □ (Square) | — |
| button2 | × (Cross) | Reversa (modo automático) |
| button3 | ○ (Circle) | — |
| button4 | △ (Triangle) | Drive (modo automático) |
| button5 | R1 (paddle der) | Toggle direccional derecha |
| button6 | L1 (paddle izq) | Toggle direccional izquierda |
| button7 | L2 | Combo: L2+R2 hold 1.5s = menu principal |
| button8 | R2 | Combo: L2+R2 hold 1.5s = menu principal |
| button9 | Share | — |
| button10 | Options | — |
| button11 | L3 | Combo: L3+R3 hold 1.5s = reiniciar escena |
| button12 | R3 | Combo: L3+R3 hold 1.5s = reiniciar escena |
| hat | D-pad | — |

### H-Shifter

| Boton Unity | Gear | Valor en PlayerCar |
|-------------|------|-------------------|
| button13 | 1ra | `currentGear = 1` |
| button14 | 2da | `currentGear = 2` |
| button15 | 3ra | `currentGear = 3` |
| button16 | 4ta | `currentGear = 4` |
| button17 | 5ta | `currentGear = 5` |
| button18 | 6ta | `currentGear = 6` |
| button19 | Reversa | `currentGear = -1` |
| (ninguno) | Neutral | `currentGear = 0` |

## Combos de botones

| Combo | Accion | Hold time |
|-------|--------|-----------|
| L2 + R2 (button7 + button8) | Cargar escena "MainMenu" | 1.5s |
| L3 + R3 (button11 + button12) | Reiniciar escena actual | 1.5s |

**Nota:** El hold time es 1.5s (no 0.5s) para evitar loops de recarga — al recargar la escena, UIInputNew se reinicializa y si los botones siguen presionados, dispararia otra recarga.

## Reversa en modo automático

En modo automático, los botones del hub del G923 controlan Drive/Reversa:

- **× (Cross / button2):** Poner Reversa (`_currentGear = -1`)
- **△ (Triangle / button4):** Poner Drive (`_currentGear = 1`)
- **Teclado fallback:** R = toggle D↔R
- **Penalización:** Cambiar D↔R a más de 5 km/h = -20 pts ("CAMBIO DE MARCHA PELIGROSO")
- Edge detection: solo se activa al presionar, no mientras se mantiene presionado
- En modo manual estos botones no hacen nada (se usa el H-shifter)

## Direccionales

- **Paddle izquierdo (L1/button6):** Toggle direccional izquierda
- **Paddle derecho (R1/button5):** Toggle direccional derecha
- **Ambos paddles simultaneos:** Hazard (intermitentes)
- **Teclado fallback:** Q = izquierda, E = derecha
- **Visual:** Flechas verdes parpadeantes (◄ ►) a los lados del velocimetro en el HUD
- **Parpadeo:** 0.4s de intervalo

## Archivos involucrados

| Archivo | Responsabilidad |
|---------|----------------|
| `Assets/Gley/UrbanAssets/Runtime/Scripts/UI/IUIInput.cs` | Interfaz: `GetHorizontalInput()`, `GetVerticalInput()`, `GetBrakeInput()`, `GetCurrentGear()`, `GetIndicatorInput()` |
| `Assets/Gley/UrbanAssets/Runtime/Scripts/UI/UIInputNew.cs` | Lee G923 via Input System (ejes) + acceso directo (botones). Normaliza pedales, detecta H-shifter, paddles, combos |
| `Assets/Gley/UrbanAssets/Runtime/Scripts/UI/UIInputOld.cs` | Input legacy (teclado viejo). No tiene soporte G923 |
| `Assets/Gley/UrbanAssets/Runtime/Scripts/Example/PlayerCar.cs` | Controlador del vehiculo. motorTorque + brakeTorque separados, gears, luces, direccionales, debug frenado |
| `Assets/Custom/SimpleSpeedGauge.cs` | Velocimetro HUD: velocidad (km/h), gear (N/1-6/R), flechas direccionales parpadeantes |
| `Assets/Custom/MainMenuController.cs` | Menu principal: selector de transmision auto/manual (PlayerPrefs) |
| `Assets/Custom/ViolationDetector.cs` | Detecta infracciones. Lee velocidad del Rigidbody |
| `Assets/pruebas general/DetectaControl.cs` | Debug: lista todos los dispositivos HID y sus controles en consola |
| `Assets/Custom/LogConsolePanel.cs` | **F7 hold 1.5s** — consola en runtime: devices, inputs en vivo (boton/eje + valores), PlayerPrefs, feed de Debug.Log |
| `Assets/Custom/BindingsPanel.cs` | **F8 hold 1.5s** — remapea reversa/drive/paddles/combos en vivo, guarda en PlayerPrefs `Bind_*` |
| `Assets/Custom/AdvancedInputPanel.cs` | **F9 hold 1.5s** — sliders para curva volante, deadzone, freno por tramos, curva gas (PlayerPrefs `Adv_*`) |

## Paneles de diagnostico en runtime

Para descubrir el nombre tecnico de cualquier control del volante (reversa, paddles, ejes de pedal) sin recompilar:

1. **Mantener F7 1.5s** abre `LogConsolePanel` — muestra todos los devices conectados, todos los botones presionados y todos los ejes que se han movido (con valor actual, baseline al abrir y rango min/max historico).
2. Pisar el pedal o pulsar el boton a investigar — aparece en la columna "INPUTS EN VIVO" con su path tecnico (ej. `button5`, `z`, `stick/x`).
3. Para asignar permanentemente ese path a una accion del juego: cerrar F7, mantener F8 -> `BindingsPanel`, click en `[Detectar]` de la accion y mover/presionar el control. Se guarda en PlayerPrefs y `UIInputNew.ReloadBindings()` lo recoge.

**macOS:** las teclas F7-F12 son media keys por defecto (anterior/play/siguiente/mute/vol). Activar Sys Settings -> Keyboard -> "Use F1, F2, etc. keys as standard function keys", o usar `fn+F8`. Si nada aparece en LogConsolePanel al mantener una F-key, el SO la esta interceptando.

## Lectura de ejes: `ReadUnprocessedValue()` (FIX#27, v1.2.0)

Unity Input System aplica procesadores a nivel de control. En particular, `StickControl.cs` agrega `axisDeadzone` (12.5% default) a `stick/x` y `stick/y`. En el G923 con 900° de rango, esto crea ~56-90° de zona muerta antes de que el pipeline del proyecto (`NormalizeSteer`, deadzone propia de 2%, curva racional) vea cualquier valor.

### `SafeReadFloat` vs `SafeReadFloatRaw`

```
SafeReadFloat(ctrl)     → ctrl.ReadValue()            → CON procesadores (axisDeadzone, etc.)
SafeReadFloatRaw(ctrl)  → ctrl.ReadUnprocessedValue()  → SIN procesadores (valor HID crudo)
```

**Regla:** Usar `SafeReadFloatRaw` para TODOS los ejes de volante/pedales (steering, gas, brake). Usar `SafeReadFloat` para botones y controles digitales donde los procesadores son inofensivos o deseables.

Archivos con ambos metodos: `UIInputNew.cs`, `MenuScreenManager.cs`.

### Archivos alineados con `ReadUnprocessedValue()`

| Archivo | Contexto |
|---------|----------|
| `UIInputNew.cs` | 10 call sites de ejes (steering, gas, brake, debug, overlay) |
| `MenuScreenManager.cs` | 8 call sites + 3 bare `.ReadUnprocessedValue()` + `ReadSteerInput()` |
| `LogConsolePanel.cs` | 3 reads en F7 (para que diagnostico muestre lo mismo que el runtime) |
| `BindingsPanel.cs` | 1 read en `ReadAxis()` de F8 |

### `Cal_FormatVersion` — migracion automatica de calibracion

Los valores de calibracion en PlayerPrefs (`G923_Steer*`, `G923_Gas*`, `G923_Brake*`) se capturan en el espacio de valores que el codigo usa al leerlos. Al cambiar de `ReadValue()` a `ReadUnprocessedValue()`, los valores guardados quedan en espacio "procesado" (con deadzone aplicada) pero el runtime los lee en espacio "raw" — creando una inconsistencia.

`Cal_FormatVersion` (int en PlayerPrefs) resuelve esto:
- Al entrar a Pantalla 2, si `Cal_FormatVersion < 2`, se ejecuta `ClearWheelCalibration()` + `DeleteKey("Cal_ReverseDone")` y se fuerza Discovery completo.
- Esto migra automaticamente todos los kioskos sin intervencion manual.

### `steeringWheelSmoothTime` — clamp en `PlayerCar.Start()`

La rueda visual del cockpit usa `Mathf.SmoothDamp` con `steeringWheelSmoothTime`. Muchas escenas tenian este valor en 1.5s (configurado originalmente para teclado, donde el smooth daba animacion suave). Con volante analogico, 1.5s de lag visual causa que el usuario sobrecompense el input (percibe momentum/overshoot).

**Fix:** `PlayerCar.Start()` aplica `if (steeringWheelSmoothTime > 0.1f) steeringWheelSmoothTime = 0.05f;`. Esto cubre TODAS las escenas sin editar cada .unity/.prefab. La fisica del steering (WheelCollider.steerAngle) se aplica directamente en FixedUpdate sin smooth — solo la visual estaba afectada.

## Notas tecnicas

- **Ejes via InputAction, botones via device directo:** Los InputAction bindings resuelven ejes pero NO botones en HID genericos. Para botones, cachear `InputControl<float>` via `TryGetChildControl()` en setup y poll con `SafeReadFloat` (procesado) en Update. Para ejes de volante/pedales, usar `SafeReadFloatRaw` (`ReadUnprocessedValue()`).
- **NO agregar** bindings del G923 a `_moveAction` (Vector2) — causa error `Cannot read value of type Vector2`
- Sin G923 conectado, todo funciona con teclado (WASD/flechas) via `_moveAction`
- El vehiculo **no usa RCCP** — usa `PlayerCar` de Gley con WheelColliders de Unity
- **InputActions se disposan** en OnDestroy (Disable + Dispose) para evitar memory leaks
- Gear strings cacheados como `static readonly` para evitar GC allocation por frame
- Force Feedback no implementado (requiere Logitech SDK, no prioritario para examen de manejo)
- **`stick/right`, `stick/left`** etc. son `ButtonControl` sinteticos derivados del eje `stick/x` por Unity — aparecen en F7 cuando `stick/x > ~0.5`. Son cosmeticos, no afectan gameplay.
