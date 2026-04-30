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

## Pendiente

- Verificar cual pedal fisico corresponde a cual eje (rz/slider/slider1)
- Verificar eje del steering en Unity
- Mapear marchas del H-shifter (buttons en SHIFTER device)
- Verificar funcionamiento del force feedback

## Sources
- [HORI USA Product Page](https://stores.horiusa.com/HPC-044U)
- [HORI Europe Mapping](https://horieurope.com/pages/hori-force-feedback-truck-control-system-mapping)
- [SCS Software Forum Discussion](https://forum.scssoft.com/viewtopic.php?t=334942&start=50)
