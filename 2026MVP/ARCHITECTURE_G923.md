# Arquitectura de Input — Logitech G923 PS/PC

## Flujo de datos

```
G923 USB HID → Unity Input System → UIInputNew → PlayerCar → WheelColliders
                                         ↓
                                   SimpleSpeedGauge (display gear)
```

## Dispositivo

- **Modelo:** Logitech G923 Racing Wheel for PlayStation 4 and PC
- **Layout Unity:** `HID::Logitech G923 Racing Wheel for PlayStation 4 and PC`
- **Deteccion:** Dinamica por nombre (`displayName.Contains("G923")`)

## Mapeo de ejes

| Control Unity | Fisico | Rango raw | Normalizado |
|---------------|--------|-----------|-------------|
| `stick/x` | Volante (steering) | -1 (izq) a 1 (der) | Directo |
| `z` | Pedal acelerador | 1 (suelto) a -1 (pisado) | `(1 - raw) / 2` → 0..1 |
| `rz` | Pedal freno | 1 (suelto) a -1 (pisado) | `(1 - raw) / 2` → 0..1, luego curva exponencial |
| `stick/y` | Pedal clutch | -1 a 1 | No implementado aun |

### Curva del freno

El freno usa una curva exponencial para dar mas respuesta al final del recorrido:

```
brakeLinear = (1 - raw) / 2       // 0..1 lineal
brake = brakeLinear^2 * 2          // exponencial, clamp a 1
```

- Pedal al 25% → frenado 12%
- Pedal al 50% → frenado 50%
- Pedal al 75% → frenado 100%

## Mapeo de botones

| Boton Unity | Fisico G923 PS | Funcion |
|-------------|---------------|---------|
| button1 | □ (Square) | — |
| button2 | × (Cross) | — |
| button3 | ○ (Circle) | — |
| button4 | △ (Triangle) | — |
| button5 | L1 (paddle izq) | — |
| button6 | R1 (paddle der) | — |
| button7 | L2 | Combo: L2+R2 = menu principal |
| button8 | R2 | Combo: L2+R2 = menu principal |
| button9 | Share | — |
| button10 | Options | — |
| button11 | L3 | Combo: L3+R3 = reiniciar escena |
| button12 | R3 | Combo: L3+R3 = reiniciar escena |
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
| L2 + R2 (button7 + button8) | Cargar escena "MainMenu" | 0.5s |
| L3 + R3 (button11 + button12) | Reiniciar escena actual | 0.5s |

## Archivos involucrados

| Archivo | Responsabilidad |
|---------|----------------|
| `Assets/Gley/UrbanAssets/Runtime/Scripts/UI/IUIInput.cs` | Interfaz de input: `GetHorizontalInput()`, `GetVerticalInput()`, `GetBrakeInput()`, `GetCurrentGear()` |
| `Assets/Gley/UrbanAssets/Runtime/Scripts/UI/UIInputNew.cs` | Lee G923 via Unity Input System. Normaliza ejes, detecta H-shifter, maneja combos de botones |
| `Assets/Gley/UrbanAssets/Runtime/Scripts/UI/UIInputOld.cs` | Input legacy (teclado viejo). No tiene soporte G923 |
| `Assets/Gley/UrbanAssets/Runtime/Scripts/Example/PlayerCar.cs` | Controlador del vehiculo. Recibe input de UIInputNew, aplica motorTorque/brakeTorque a WheelColliders, maneja gears y luces |
| `Assets/Custom/SimpleSpeedGauge.cs` | Velocimetro HUD. Muestra velocidad (km/h) y gear actual |
| `Assets/Custom/ViolationDetector.cs` | Detecta infracciones. Lee velocidad del Rigidbody |
| `Assets/pruebas general/DetectaControl.cs` | Debug: lista todos los dispositivos HID y sus controles en consola |

## Notas

- El G923 se registra como `Joystick` (no `Gamepad`) en Unity
- **NO agregar** bindings del G923 a `_moveAction` (Vector2) — causa error `Cannot read value of type Vector2`
- Cada eje/boton del G923 debe tener su propia `InputAction` separada
- Sin G923 conectado, todo funciona con teclado (WASD/flechas) via `_moveAction`
- El vehiculo **no usa RCCP** — usa `PlayerCar` de Gley con WheelColliders de Unity
- Force Feedback no esta implementado (requiere Logitech SDK)
