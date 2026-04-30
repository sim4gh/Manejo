# Manual de Usuario — Simulador de Prueba de Manejo

**Version:** 1.0.0
**Fecha:** 2026-03-22

---

## Inicio del Simulador

Al iniciar la aplicacion, aparece la pantalla **"Prueba de Manejo"** con dos opciones para identificarse:

### Opcion 1: Escanear QR

1. En el lado izquierdo de la pantalla aparece un codigo QR
2. El ciudadano escanea el QR con su celular desde el portal de tramites
3. Al verificarse en el portal, el simulador avanza automaticamente

### Opcion 2: Codigo manual

1. En el lado derecho aparece el prefijo **TLX-** y 5 cajas para ingresar digitos
2. El operador ingresa el codigo de 5 digitos usando el teclado numerico
3. Presionar **"Verificar Codigo"** para continuar

> **Nota:** El input toma foco automaticamente al cargar la pantalla. No se necesita mouse para escribir.

---

## Codigos Demo (para pruebas sin backend)

Estos codigos funcionan sin conexion al servidor. **Los primeros 4 digitos** definen el tipo de licencia / vehiculo; **el 5° digito** controla en que parte de la ciudad arranca el examinado.

| Prefijo (4 digitos) | Tipo de licencia | Escena |
|---|---|---|
| `0000X` | Particular (Automovil) | Sedan / Jetta / Camioneta |
| `1111X` | Publico (Pasajeros) | Bus de Pasajeros |
| `2222X` | Motocicleta | Motocicleta |
| `3333X` | Carga (Camion) | Camion de Carga |
| `4444X` | Emergencia | Ambulancia |

### Sufijo de ubicacion (5° digito)

| Sufijo | Comportamiento |
|---|---|
| `1` | Spawn fijo en zona 1 |
| `2` | Spawn fijo en zona 2 |
| `3` | Spawn fijo en zona 3 (antes de la glorieta) |
| `4` | Spawn fijo en zona 4 |
| `5` | Spawn fijo en zona 5 |
| `0` | Aleatorio entre las 5 zonas |
| `6..9` | No es demo: el sistema lo manda al backend como codigo real |

Ejemplos:
- `11113` → Bus en la zona 3 (antes de la glorieta).
- `22220` → Motocicleta en zona aleatoria.
- `00001` → Auto en zona 1.

Para descubrir o refinar los waypoints de cada zona: durante un examen, posiciona el vehiculo donde quieras que arranque y presiona la tecla **`K`**. En la consola F7 aparece `[WaypointDebug] ... idx=NNN ...` — ese numero se hardcodea en `Assets/Custom/SpawnLocationManager.cs` (`DEFAULT_WAYPOINTS`).

---

## Flujo de Pantallas

### Pantalla 0 — Verificacion (QR o codigo)

Identificacion del ciudadano. Al verificarse, el sistema determina el tipo de licencia y enruta:

- **Particular** → Pantalla 1 (seleccion de vehiculo y transmision)
- **Publico / Motocicleta / Carga** → Pantalla 2 (verificacion de volante)

### Pantalla 1 — Configuracion (solo licencia particular)

1. **Seleccionar modelo de vehiculo:**
   - Sedan (escena "carretera")
   - Jetta (escena "Jetta")
   - Camioneta (escena "Camioneta")

2. **Seleccionar transmision:**
   - Automatica (default)
   - Manual (requiere H-shifter)

3. Presionar **"Continuar"**

### Pantalla 2 — Verificacion de Volante

El sistema verifica que el volante G923 esta conectado:

1. **"Gira el volante hacia la DERECHA"** — girar al 90% del recorrido
2. **"Ahora gira hacia la IZQUIERDA"** — girar al 90% del recorrido
3. Las barras de progreso se llenan en tiempo real (rojo → verde)
4. Al completar ambas direcciones, la prueba inicia automaticamente (1.5s de espera)

> **Timeout:** Si despues de 15 segundos no se detecta volante, aparece el boton **"Iniciar sin volante"** para continuar de todas formas.

---

## Controles del Volante Logitech G923

### Ejes principales

| Control | Funcion |
|---------|---------|
| Volante (steering) | Direccion del vehiculo |
| Pedal derecho | Acelerador |
| Pedal izquierdo | Freno (curva exponencial) |
| Pedal clutch | No implementado aun |

### Paddles (detras del volante)

| Paddle | Funcion |
|--------|---------|
| Paddle izquierdo (L1) | Toggle direccional izquierda |
| Paddle derecho (R1) | Toggle direccional derecha |
| Ambos simultaneos | Luces intermitentes (hazard) |

### H-Shifter (solo transmision manual)

| Posicion | Velocidad |
|----------|-----------|
| 1 | Primera |
| 2 | Segunda |
| 3 | Tercera |
| 4 | Cuarta |
| 5 | Quinta |
| 6 | Sexta |
| Atras | Reversa |
| Centro (ninguno) | Neutral |

### Combos de botones (mantener 1.5 segundos)

| Combo | Accion |
|-------|--------|
| L2 + R2 | Volver al menu principal |
| L3 + R3 | Reiniciar escena actual |

### Controles de teclado (alternativa sin volante)

| Tecla | Funcion |
|-------|---------|
| W / Flecha arriba | Acelerar |
| S / Flecha abajo | Frenar / Reversa |
| A / Flecha izquierda | Girar izquierda |
| D / Flecha derecha | Girar derecha |
| Q | Direccional izquierda |
| E | Direccional derecha |
| Ctrl + Shift + M | Volver al menu |
| Ctrl + Shift + S | Reiniciar escena |

---

## Controles de Motocicleta (Gamepad BLE)

La motocicleta usa un controlador BLE dedicado (ESP32) con sensor IMU:

| Control | Funcion |
|---------|---------|
| Inclinacion (lean) | Direccion — inclinar izquierda/derecha |
| Pitch | Inclinacion adelante/atras |
| Grip derecho | Acelerador |
| Palanca izquierda | Freno delantero |
| Pedal derecho | Freno trasero |
| Boton 1 | Subir cambio |
| Boton 2 | Bajar cambio |

---

## Sistema de Puntuacion

### Puntaje inicial: 100 puntos

### Infracciones y penalizaciones

| Infraccion | Penalizacion | Cooldown |
|------------|-------------|----------|
| Exceso de velocidad | -5 puntos | 5 segundos |
| Colision vehicular | -10 puntos | — |
| Sentido contrario | -15 puntos | 5 segundos |
| Semaforo en rojo | -20 puntos | 10 segundos |
| Atropello de peaton | -25 puntos | — |

### Limites de velocidad por zona

| Zona | Limite |
|------|--------|
| Escolar | 20 km/h |
| Residencial | 30 km/h |
| Hospitalaria | 30 km/h |
| Urbana | 40 km/h |
| Carretera | 80 km/h |
| Autopista | 110 km/h |

> Los limites se detectan automaticamente segun los waypoints de la carretera. El letrero de velocidad en el HUD muestra el limite actual y parpadea en rojo si se excede por mas de 10 km/h.

### Resultado del examen

| Puntaje | Resultado |
|---------|-----------|
| 90 — 100 | **APTO** — Licencia aprobada |
| 80 — 89 | **APTO CONDICIONADO** — Reforzar areas debiles |
| 70 — 79 | **APTO CONDICIONADO** — Requiere reentrenamiento |
| Menor a 70 | **NO APTO** — Licencia negada |

---

## HUD durante la prueba

- **Velocimetro:** Velocidad actual en km/h (aguja animada)
- **Tacometro:** RPM del motor
- **Indicador de cambio:** Muestra N, 1, 2, 3, 4, 5, 6 o R
- **Direccionales:** Flechas verdes parpadeantes (izquierda / derecha)
- **Letrero de velocidad:** Limite de la zona actual, parpadea rojo al exceder

---

## Telemetria

Al finalizar la prueba, se genera un archivo JSON con el registro completo:

- Ubicacion: `Application.persistentDataPath/telemetry_[timestamp].json`
- Contenido: hora inicio/fin, puntaje final, lista de eventos con tipo, descripcion, puntos y velocidad al momento

---

## Escenas disponibles

| Escena | Vehiculo | Tipo de licencia |
|--------|----------|-----------------|
| Carretera | Sedan | Particular |
| Jetta | Jetta | Particular |
| Camioneta | Camioneta | Particular |
| Motocicleta | Moto | Motocicleta |
| Bus Pasajeros | Autobus | Publico |
| Camion D Carga | Camion | Carga |

---

## Solucion de problemas

| Problema | Solucion |
|----------|----------|
| No se detecta el volante | Conectar el G923 por USB antes de iniciar. Si no aparece, usar el boton "Iniciar sin volante" (15s) |
| Los digitos no se escriben | Hacer click en las cajas del PIN o esperar a que la pantalla termine de cargar |
| El codigo no se verifica | Verificar conexion a internet. Usar codigos demo para pruebas offline |
| El freno no responde bien | El pedal tiene curva exponencial — presionar al 75% para frenado completo |
| Direccionales no funcionan | Usar paddles L1/R1 detras del volante. En teclado: Q (izq), E (der) |
| Vehiculo atascado | Mantener L3 + R3 por 1.5s para reiniciar la escena |
