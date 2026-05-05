# HORI Truck Control System (HPC-044U) — Mapeo verificado

## Resumen del dispositivo

- **Modelo:** HORI Force Feedback Truck Control System (HPC-044U)
- **Plataforma:** Windows 10/11 solamente (NO macOS)
- **Protocolo:** DirectInput (no XInput)
- **Conexiones USB:** 2 dispositivos separados:
  - `HORI TRUCK CONTROL SYSTEM WHEEL` — volante + pedales + palancas
  - `HORI TRUCK CONTROL SYSTEM SHIFTER` — panel de shifter + botones

## Hardware

| Componente | Especificaciones |
|------------|-----------------|
| Volante | 40cm diametro, 1800° rotacion, force feedback dual motor |
| Pedales | 3 pedales (clutch, brake, gas), sensores Hall Effect |
| Shifter | H-pattern (6+R) + sequential, resistencia ajustable, 18 velocidades con splitter |
| Botones | 39 en volante + 30 en panel shifter = ~69 botones total |
| Palancas | 2 palancas montadas en columna (direccionales, wipers) |
| Sticks | 2 analog sticks en el volante |

## Mapeo verificado (F7 en Unity Input System, 2026-04-29)

### Pedales (WHEEL device, verificados en reposo)

| Eje Unity | Fisico | Reposo | Rango |
|-----------|--------|--------|-------|
| `rz` | Pedal (verificar cual) | -1.000 | [-1.00, 1.00] |
| `slider` | Pedal (verificar cual) | -1.000 | [-1.00, 1.00] |
| `slider1` | Pedal (verificar cual) | -1.000 | [-1.00, 1.00] |

Los 3 pedales tienen rest=-1.0 y press hacia +1.0 (inverso al G923 que tiene rest=1.0, press=-1.0).

### Botones verificados

| Boton | Device | Funcion |
|-------|--------|---------|
| `button40` | WHEEL | Direccional izquierda |
| `button41` | WHEEL | Direccional derecha |
| `button27` | SHIFTER | Intermitentes (hazard) |
| `button7` | SHIFTER | Reversa |

### H-shifter del SHIFTER device (verificado en F7 Norberto, 2026-05-06)

| Posición física | Path Unity | Marcha |
|---|---|---|
| Trigger del palo | `shifter:trigger` | 1ª |
| 2ª pos | `shifter:button2` | 2ª |
| 3ª pos | `shifter:button3` | 3ª |
| 4ª pos | `shifter:button4` | 4ª |
| 5ª pos | `shifter:button5` | 5ª |
| 6ª pos | `shifter:button6` | 6ª |
| R | `shifter:button7` | -1 |

**Nota crítica:** Unity nombra el control de la 1ª como `trigger` (no `button1`)
porque el HID descriptor del HORI marca ese botón con un usage especial. Si se
intenta `shifter:button1` el control no resuelve y la 1ª no funciona.

### Steering

Pendiente de verificar el eje exacto. Pantalla 2 lo descubre dinamicamente.

## Implementacion (completada 2026-04-29)

### Cambios realizados

1. **`MenuScreenManager.cs`**: Agregados `"slider"` y `"slider1"` a `PEDAL_AXIS_CANDIDATES` para que Pantalla 2 pueda descubrir los pedales del HORI.

2. **`UIInputNew.cs`**:
   - `IsHORITruck()`: detecta HORI por displayName/product
   - Defaults automaticos en `AttachToWheelDevice()` al detectar HORI:
     - `Bind_paddleLeft = "button40"` (direccional izquierda)
     - `Bind_paddleRight = "button41"` (direccional derecha)
     - `Bind_hazard = "shifter:button27"` (intermitentes)
     - `Bind_reverse = "shifter:button7"` (reversa)
   - Nuevo binding `Bind_hazard` con boton dedicado de intermitentes (G923 usa combo L1+R1)

### Que NO necesito cambio

- Pantalla 2 Discovery: ya maneja rest/press dinamicamente
- `NormalizePedal()`: agnostico a la direccion del eje
- LogConsolePanel (F7): ya muestra slider/slider1
- Prefijo `shifter:` en bindings: ya soportado por `CacheBindingCtrl()`

## Diferencias clave vs G923

| Aspecto | G923 | HORI Truck |
|---------|------|------------|
| Plataforma | macOS + Windows | Windows only |
| USB devices | 1 | 2 (wheel + shifter) |
| Pedales reposo | 1.0 (press hacia -1.0) | -1.0 (press hacia +1.0) |
| Pedal axes | z, rz, stick/y | rz, slider, slider1 |
| Direccionales | paddles (button5/6) | palancas columna (button40/41) |
| Intermitentes | combo L1+R1 | boton dedicado (shifter:button27) |
| Reversa | button19 (PS) / button12 (Xbox) | shifter:button7 |
| Rotacion | 900° | 1800° |
| Botones | 19 | ~69 |

## Modo manual (HORI + Manual + clutch, 2026-05-06)

Al detectar HORI en `UIInputNew.AttachToWheelDevice()`, el bloque escribe los
defaults del H-shifter de forma idempotente (`if (!HasKey)`) para que las
reasignaciones F8 sobrevivan re-attaches:

- `Bind_gear1 = "shifter:trigger"` (1ª)
- `Bind_gear2..6 = "shifter:button2..6"` (2ª..6ª)
- `Bind_reverse = "shifter:button7"` (R, ya cubre tanto automático como manual)

El **clutch** NO se hardcodea: el HID descriptor del HORI alias `slider`/`slider1`/`rz`
arbitrariamente entre brake y clutch (ver `HORI_THROTTLE_BUG_RESOLUTION.md:170-171`).
Por eso `MenuScreenManager.PrepareWheelScreen()` agrega una **Phase 6 (CLUTCH)**
condicional que corre solo si:
- `TransmisionManual == 1`, **y**
- `IsHORITruck(dev) == true`, **y**
- No hay calibración válida del clutch persistida.

La fase reusa la barra brake (sin nueva UI) y prompt "Pisa el EMBRAGUE a fondo".
Excluye los axes ya elegidos para gas y brake — el restante (slider/slider1/rz)
queda como `G923_ClutchAxis` con rest/press calibrados.

### Detección de "rechino" en manual

Mismo flujo que G923 PS: `UIInputNew` compara `_currentGear` contra
`_lastNonNeutralGear`; si `clutchInput < 0.5` al cambiar, incrementa
`_pendingGearShiftWithoutClutchCount`. `ViolationDetector` consume el contador
y penaliza (−5 pts, audio `GearGrindingFeedback.PlayGrind()`) cuando
`HasPhysicalClutch() == true`. Para HORI manual, `_clutchCtrl` queda no-null
después de Phase 6 → `HasPhysicalClutch() == true`.

### Cambio de hardware sin recalibrar

Caso HORI→G923: los `Bind_gear*` con paths `shifter:*` no resuelven en G923.
- `ReCacheGearControls()` filtra paths inválidos y, si `ctrls.Count == 0`,
  cae al fallback legacy 13-19 → H-shifter G923 funciona.
- `SanityCheckThenLoad` detecta paths `shifter:*` huérfanos (sin shifter
  device) y los limpia de `Bind_gear*`/`Bind_reverse` → defaults del nuevo
  hardware se reescriben en el siguiente attach.

## Pendiente

- Verificar cual pedal fisico corresponde a cual eje (rz/slider/slider1) en F7
- Verificar funcionamiento del force feedback (HORI no tiene FFB —
  `LogitechFFB.cs` continúa siendo no-op)

## Deuda técnica

- **Alias del clutch en Phase 6 Discovery**: el HID parser de Unity alias
  `slider`/`slider1`/`rz` arbitrariamente entre brake y clutch en el HORI
  HPC-044U (lockstep parcial documentado en `HORI_THROTTLE_BUG_RESOLUTION.md:170-171`).
  La exclusión por índice en `SamplePedalCandidates(gasIdx, brakeIdx, ...)`
  evita reusar exactamente el mismo candidato pero NO previene que un
  segundo alias del mismo byte físico gane la fase 6. **Síntomas observables**
  si pasa: la barra del clutch avanza al pisar freno; en gameplay el clutch
  se desacopla cuando el operador frena. **Mitigaciones futuras** (out of
  scope inicial):
  1. Verificación en dos pasos: detectar candidato al pisar clutch, soltar,
     repisar y validar que el mismo eje vuelve a moverse fuerte mientras
     los demás se quedan cerca del baseline.
  2. Si Discovery falla en producción, agregar un sentinel
     `HORI_RAW_CLUTCH_PATH` con lectura raw del byte HID análogo al throttle
     (extender `HoriThrottleReader.cs`).

## Sources
- [HORI USA Product Page](https://stores.horiusa.com/HPC-044U)
- [HORI Europe Mapping](https://horieurope.com/pages/hori-force-feedback-truck-control-system-mapping)
- [SCS Software Forum Discussion](https://forum.scssoft.com/viewtopic.php?t=334942&start=50)
