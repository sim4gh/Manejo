# Investigación: HORI Truck Control System (HPC-044U) para Unity

## Resumen del dispositivo

- **Modelo:** HORI Force Feedback Truck Control System (HPC-044U)
- **Plataforma:** Windows 10/11 solamente (NO macOS)
- **Protocolo:** DirectInput (no XInput)
- **Conexiones USB:** 2 dispositivos separados:
  - `HORI CONTROLLER SYSTEM WHEEL` — volante + pedales + palancas
  - `HORI CONTROLLER SYSTEM SHIFTER` — panel de shifter + botones

## Hardware

| Componente | Especificaciones |
|------------|-----------------|
| Volante | 40cm diámetro, 1800° rotación, force feedback dual motor |
| Pedales | 3 pedales (clutch, brake, gas), sensores Hall Effect |
| Shifter | H-pattern (6+R) + sequential, resistencia ajustable, 18 velocidades con splitter |
| Botones | 39 en volante + 30 en panel shifter = ~69 botones total |
| Palancas | 2 palancas montadas en columna (direccionales, wipers) |
| Sticks | 2 analog sticks en el volante |

## Mapeo de ejes (DirectInput → Unity)

Según la info encontrada, el HORI usa ejes DIFERENTES al G923:

| Eje DirectInput | Físico | Nota |
|-----------------|--------|------|
| Axis 4 (X) | Volante izq/der | **NO es stick/x como G923** |
| Axis 10 (Dial) | Acelerador | Eje no estándar |
| Axis 12 (Slider) | Freno | Eje no estándar |
| Axis 11 (RZ) | Clutch | |
| Axis 0 (Y) | Left Stick Up/Down | |
| Axis 1 (Z) | Left Stick Left/Right | |
| Axis 2 (RX) | Right Stick Left/Right | |
| Axis 3 (RY) | Right Stick Up/Down | |

**IMPORTANTE:** El steering usa el eje Z (no X), y los pedales usan Dial/Slider/RZ que son ejes no estándar. Unity Input System podría mapearlos diferente.

## Diferencias clave vs G923

| Aspecto | G923 | HORI Truck |
|---------|------|------------|
| Plataforma | macOS + Windows | Windows only |
| USB devices | 1 | 2 (wheel + shifter) |
| Steering axis | stick/x | X o Z (verificar) |
| Gas | z | Dial (verificar) |
| Brake | rz | Slider (verificar) |
| Clutch | stick/y | RZ |
| Botones | 19 | ~69 |
| Direccionales | paddles | palancas de columna |
| Shifter | H-pattern externo | H-pattern + sequential integrado |
| Rotación | 900° | 1800° |
| Force feedback | Sí | Sí (dual motor) |

## Impacto en el código

### UIInputNew.cs necesita:
1. **Detectar ambos dispositivos**: buscar "HORI" en `displayName` de AMBOS devices
2. **Mapear ejes diferentes**: los ejes del HORI no son los mismos que el G923
3. **Más botones**: las palancas de columna reemplazan los paddles para direccionales
4. **Shifter como device separado**: los botones del H-shifter están en un device diferente

### Enfoque recomendado:
- Ampliar la detección en `SetupDesktopInput()` para buscar "HORI" además de "G923"
- Cachear AMBOS devices HORI (wheel + shifter)
- Mapear ejes según el device detectado
- **Necesario**: conectar el HORI a una PC Windows y correr `DetectaControl.cs` para obtener los nombres exactos de controles en Unity Input System

## Siguiente paso crítico

**No se puede implementar sin verificar los ejes reales.** Los nombres de ejes en DirectInput (Dial, Slider) pueden mapearse diferente en Unity Input System. Se necesita:

1. Conectar el HORI a la PC Windows del kiosko
2. Correr `DetectaControl.cs` en Unity
3. Anotar los nombres exactos de todos los controles (`stick/x`, `z`, `rz`, `dial`, `slider`, etc.)
4. Con esa info, implementar el mapeo en UIInputNew

## Sources
- [HORI USA Product Page](https://stores.horiusa.com/HPC-044U)
- [HORI Europe Mapping](https://horieurope.com/pages/hori-force-feedback-truck-control-system-mapping)
- [SCS Software Forum Discussion](https://forum.scssoft.com/viewtopic.php?t=334942&start=50)
- [BikmanTech Overview](https://bikmantech.com/blogs/blogs/hori-truck-control-system-everything-you-need-to-know)
