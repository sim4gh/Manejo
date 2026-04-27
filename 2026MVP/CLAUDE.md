# Tlax2026MVP - Simulador de Examen de Manejo

## Resumen

Simulador de examen de manejo para evaluacion de conductores en Tlaxcala, Mexico (2026). Proyecto en Unity 2022.3 LTS con deteccion automatica de infracciones, telemetria en tiempo real, sistema de pasajeros/peatones, y soporte multi-monitor para kiosko.

## Stack Tecnico

- **Engine:** Unity 6 (6000.3.5f2)
- **Render Pipeline:** URP (Universal Render Pipeline) 17.3.0
- **Input:** Unity Input System 1.17.0 (`activeInputHandler: 1` — solo new system, NO legacy `Input.GetKey`)
- **UI:** TextMesh Pro
- **Build Target:** Windows (Intel) - v0.0.4

### Assets de Terceros (comprados, no modificar)
| Asset | Carpeta | Uso |
|-------|---------|-----|
| Realistic Car Controller Pro (RCCP) | `Assets/Realistic Car Controller Pro/` | Fisica de vehiculos, controles |
| Gley Urban Traffic System | `Assets/Gley/` | Trafico AI, semaforos, waypoints |
| Gley Pedestrian System | `Assets/Gley/` | Peatones AI |
| EasyRoads3D v3 | `Assets/EasyRoads3D/` y `EasyRoads3Dv3/` | Creacion de carreteras |
| Fantasy Skybox FREE | `Assets/Fantasy Skybox FREE/` | Cielo |

## Estructura de Scripts

### Core (`Assets/Custom/`)

| Script | Patron | Descripcion |
|--------|--------|-------------|
| `GameManager.cs` | Singleton, DontDestroyOnLoad | Persiste expediente entre escenas |
| `MainMenuController.cs` | - | UI del menu: input expediente, selector transmision auto/manual, carga escena "UrbanExample", abre carpeta telemetria |
| `MainMenuSetup.cs` | Editor-only | Genera UI del menu proceduralmente (botones redondeados, tema azul/cyan) |
| `TelemetryLogger.cs` | Singleton | Registra eventos (tipo, descripcion, puntos, velocidad) y exporta a JSON en `persistentDataPath` |
| `ViolationDetector.cs` | - | Motor principal de infracciones: velocidad, colisiones, peatones, sentido contrario, semaforo rojo. Usa waypoints de Gley para limites dinamicos |
| `WrongWayDetector.cs` | - | Detecta conduccion en sentido contrario via dot product con waypoints Gley (threshold -0.3, cooldown 5s, penalizacion 15 pts) |
| `RedLightDetector.cs` | - | Detecta cruce de semaforo en rojo. Busca "TrafficLightPost" GameObjects, revisa hijos RedLightOn/GreenLightOn (cooldown 10s, penalizacion 20 pts) |
| `Speedometer.cs` | - | Dashboard con aguja animada (velocidad + RPM + marcha). Soporta RCCP via reflection y fallback a Rigidbody |
| `SimpleSpeedometer.cs` | - | Velocimetro basico: busca "Player", lee Rigidbody, convierte m/s a km/h |
| `SimpleSpeedGauge.cs` | - | Gauge circular: velocidad (km/h), gear (N/1-6/R), flechas direccionales parpadeantes. Expone `velocidadActual` |
| `SimpleRPMGauge.cs` | - | Gauge circular de RPM, cambia a rojo en redline (6500 default) |
| `SpeedLimitDisplay.cs` | - | Letrero HUD de limite de velocidad. Se suscribe a `ViolationDetector.OnSpeedLimitChanged`. Parpadea rojo si excede >10 km/h |
| `SpeedometerSetup.cs` | Editor-only | Menu items para crear prefabs de dashboard y letrero en el editor |
| `NotificationManager.cs` | Singleton | Popups de infracciones con color y auto-ocultado (2s default) |

### Paneles de Diagnostico / Debug Overlays (`Assets/Custom/`)

Los 4 paneles se auto-instancian via `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` y persisten entre escenas. Mientras estan abiertos pausan el juego (`Time.timeScale = 0`). Esc o la misma tecla (pulsada, no held) cierra.

| Tecla | Script | Proposito |
|-------|--------|-----------|
| **F7 hold 1.5s** | `LogConsolePanel.cs` | Consola de diagnostico tipo terminal. 4 paneles: devices conectados, PlayerPrefs relevantes (G923_*, Adv_*, Bind_*), inputs en vivo (todo boton presionado + todo eje movido con valor/baseline/rango), feed de `Debug.Log/Warn/Error` con timestamp. Boton "Copiar al portapapeles" exporta texto plano sin tags TMP. Util para descubrir nombres tecnicos de cualquier control sin recompilar. |
| **F8 hold 1.5s** | `BindingsPanel.cs` | Remapeo de controles del volante (reversa, drive, paddles, restart, combos menu). Click en `[Detectar]` y mueve/presiona el control que quieres asignar — guarda en PlayerPrefs `Bind_*` y notifica a `UIInputNew.ReloadBindings()`. Tambien muestra inputs en vivo en la parte inferior. |
| **F9 hold 1.5s** | `AdvancedInputPanel.cs` | Tunear sensibilidades del volante/freno/acelerador en runtime: curva del volante (`Adv_SteerCurveA`), deadzone (`Adv_SteerDeadzone`), punto de quiebre del freno (`Adv_BrakeSoftEnd`/`Adv_BrakeSoftMaxOutput`), curva del gas (`Adv_GasCurveN`). Sliders en vivo, llaman a `UIInputNew.ReloadTuning()`. |
| **F10 hold 1.5s** | `AdminPanel.cs` | Solo en escena `MainMenu`. Navega a la pantalla admin del `MenuScreenManager`. |

**Nota macOS:** las teclas F7/F8/F9 estan mapeadas por el sistema a controles de medios (anterior/play/siguiente). Para que Unity las reciba como F-keys hay que: (a) System Settings -> Keyboard -> "Use F1, F2, etc. keys as standard function keys", o (b) presionar `fn+F8` en vez de F8. Si al mantener F8 no aparece nada en LogConsolePanel ni un log `[BindingsPanel] F8 held ...`, el SO esta interceptando la tecla — no es bug del juego.

### Upload de Logs (`Assets/Custom/LogUploader.cs`)

Counterpart remoto del F7. Mismo singleton `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` que persiste entre escenas. Captura `Application.logMessageReceivedThreaded` (todo thread, no solo main) con stack traces, buffer circular de 5000 lineas + persistencia en disco a `persistentDataPath/logs/current.log` (recupera en proximo arranque si Unity crashea).

Cada 5 minutos hace flush:
1. Snapshot tipo F7 (devices conectados, inputs activos, PlayerPrefs `G923_*`/`Adv_*`/`Bind_*`) en texto plano
2. POST a `{baseUrl}/simulator/logs/upload-url` con `{pcId,size}`, recibe presigned URL
3. PUT bytes gzipeados a S3 con `Content-Type: application/gzip`
4. Trunca el archivo local

Tras 3 fallos consecutivos descarta el buffer (evita memory leak si la red esta caida indefinidamente). Tambien hace flush best-effort en `OnApplicationQuit`.

**Backend:** los archivos terminan en `s3://simtabasco-simulator-logs-{env}/logs/{pcId}/{YYYY-MM-DD}/{ISO-timestamp}.log.gz` con lifecycle 30d Standard -> IA -> 180d expire.

**Portal admin:** `/admin/simuladores/pcs/[pcId]/logs` lista los archivos con paginacion, descarga via presigned GET de 5min.

`bundleVersion` se reporta al backend en `appVersion` via `Application.version` (cambiar en `Edit -> Project Settings -> Player -> Version`). Desde **1.0.11** se manda en cada heartbeat (campo `appVersion` en `HeartbeatRequest`) — el backend lo usa para auto-confirm honesto del install OTA.

### Auto-update OTA (`Assets/Custom/AutoUpdater.cs`)

Singleton que vive en `[AutoUpdater]` GameObject (DontDestroyOnLoad). Flujo:

1. `SimulatorApiClient.SendHeartbeat()` recibe `pendingUpdate` del backend cada ~3 min.
2. `AutoUpdater.ProcessPendingUpdate(data)` filtra por status (PENDING o FAILED — DOWNLOADING/INSTALLING ya en progreso, INSTALLED ignorado).
3. `RequestAndDownload()`: POST `/simulator/request-update` -> recibe `downloadUrl` (CloudFront `cdn.{env}.simuladores.mexicalab.com`), reporta DOWNLOADING, descarga con `UnityWebRequest.Get` + `DownloadHandlerFile`, valida SHA256.
4. Reporta DOWNLOADED, llama `InstallUpdate()`.
5. `InstallUpdate()`: reporta INSTALLING (fire-and-forget), genera `update.bat` en `persistentDataPath/update.bat`, ejecuta y hace `Application.Quit()`.
6. El bat extrae el ZIP a staging, **`xcopy /s /e /y "<staging>\*" "<appDir>\"`** copia archivos root + subdirs preservando estructura, corre `Unblock-File` para limpiar Mark-of-the-Web, lanza `Tlax2026MVP.exe` nuevo.
7. El nuevo `.exe` arranca, su heartbeat reporta `Application.version = nuevaVersion` -> backend matchea `pendingUpdate.version` -> auto-confirma INSTALLED honestamente.

**Bug histórico (resuelto en 1.0.10+):** el bat tenia `for /d %%D in ("staging\*")` que solo iteraba subdirectorios y NUNCA copiaba `Tlax2026MVP.exe` ni `UnityPlayer.dll` (archivos root del ZIP). Por eso el OTA fallaba en silencio entre 1.0.7-1.0.8 — la PC seguia corriendo el .exe viejo.

**Logs locales:** `<appDir>\install.log` con timestamps de cada step (Expand-Archive OK, xcopy OK, etc.) — primer lugar a mirar si un install falla.

**Bootstrap manual:** `Manejo/2026MVP/scripts/bootstrap-install.ps1` para migrar PCs con bat viejo a una build nueva. Una vez por kiosko, despues los OTA siguientes funcionan solos.

### NPCs - Sistema de Camion (`Assets/NPCs/CamionNPC-s/`)

| Script | Descripcion |
|--------|-------------|
| `CamionManager.cs` | Singleton que trackea lista de `pasajerosABordo` |
| `Pasajero.cs` | Comportamiento de pasajero: camina por `puntos`, se sienta, ragdoll al ser golpeado. Animaciones: "Walk", "Sentado", "Idle" |
| `Asiento.cs` | Trigger de asiento, asigna pasajeros con debounce de 2s. Detecta tag "CajaCamion" para carga |
| `ParadaManager.cs` | Sistema de parada de autobus. Detecta tag "puerta", espera velocidad 0, coroutine de abordaje (5s intervalos) |
| `BusAsientosManager.cs` | Desactiva asientos vacios cada frame |
| `RagDollPasajeroManager.cs` | Toggle entre animacion y ragdoll fisico. Guarda/restaura poses iniciales |

### Peatones (`Assets/Peatones/`)

| Script | Descripcion |
|--------|-------------|
| `peatones.cs` | Peaton AI: camina a `puntoDestino`, ragdoll al colisionar con "automovil"/"Player", reset a posicion inicial despues de 3s |
| `activarPeaton.cs` | Trigger que activa movimiento del peaton al detectar `PlayerComponent` o tag "puerta" |

### Vehiculos (`Assets/Carros/`, `Assets/Scripts/`)

| Script | Descripcion |
|--------|-------------|
| `DesactivarBici.cs` | Desactiva bicicleta 2s despues de colision con "automovil"/"Player" |
| `DetenerAnimacion.cs` | Sincroniza animacion de moto con velocidad via Gley `TwoWheelComponent` |
| `LevantaMoto.cs` | Auto-recupera moto si se inclina >80 grados por >5 segundos |
| `DestrabarAutomovil.cs` | Teletransporta vehiculo atascado despues de 5s en zona de colision |
| `BusKiller.cs` | Desactiva bus al ser golpeado por tag "automovil" |

### QR / Kiosko (`Assets/pruebasQR/`)

| Script | Descripcion |
|--------|-------------|
| `QRGenerator.cs` | Genera QR con sesion via AWS API (`d6twaegbhg.execute-api.us-east-1.amazonaws.com/kiosk/sessions`). Pollea verificacion cada 10s, carga escena al verificar |
| `IdSesion.cs` | String estatico `_Mi_ID` para ID de sesion, DontDestroyOnLoad |

### Configuracion y Utilidades

| Script | Ubicacion | Descripcion |
|--------|-----------|-------------|
| `ManagerMainMenu.cs` | `Assets/pruebas general/` | Carga/guarda PlayerPrefs: "NoPeatones", "NoCarros", "Cargolluvia" |
| `CargoLluviaLoader.cs` | `Assets/pruebas general/` | Activa/desactiva lluvia segun PlayerPrefs |
| `MultiPantallaManager.cs` | `Assets/Scripts/` | Activa Display 2 y 3 para setup multi-monitor |
| `Efecto_Sirena.cs` | `Assets/Scripts/` | Rota luces roja/azul para vehiculos de emergencia |
| `DetectaControl.cs` | `Assets/pruebas general/` | Debug: muestra dispositivos de input conectados |
| `BotonCambioEscena.cs` | `Assets/pruebas general/` | Carga escena por nombre (default "UrbanExample") |
| `ShowRoom.cs` | `Assets/Mobile Motorcycles/` | Carrusel de modelos con flechas |

## Escenas (7 total)

| # | Escena | Vehiculo | Descripcion |
|---|--------|----------|-------------|
| 1 | MainMenu | — | Menu principal con input de expediente |
| 2 | Carretera | Player Original | Escena principal del examen de manejo (UrbanExample) |
| 3 | Camioneta | Camioneta | Examen con camioneta (SUV en UI) |
| 5 | Bus Pasajeros | Bus | Examen con autobus de pasajeros |
| 6 | Camion D Carga | Camion | Examen con camion de carga |
| 7 | Motocicleta | Moto | Examen con motocicleta |

## Flujo del Juego (v2 — QR/Tramite, 2026-03-22)

El menu principal (`Assets/Custom/Menu/MenuScreenManager.cs`) se auto-adjunta al Canvas via `MenuBootstrap.cs` y genera toda la UI proceduralmente.

### Pantalla 0 — Verificacion (QR o codigo manual)
- **QR**: POST `/kiosk/sessions` → genera QR → poll cada 10s → al verificarse obtiene `tramiteId`, `citizenName`, `licenseType`
- **Codigo manual**: Input "TLX-XXXXXX" → GET `/simulator/lookup?code={code}` → misma info
- **Demo codes** (5 digitos, normalizados con `.Trim().ToUpper()`):
  - `00000` → `TLX-DEMO00000` "Demo Automovil" `particular` (abre Pantalla 1 de seleccion de modelo)
  - `11111` → `TLX-DEMO11111` "Demo Pasajeros" `publico` (escena `BusPasajeros`)
  - `22222` → `TLX-DEMO22222` "Demo Moto" `motocicleta` (escena `Motocicleta`)
  - `33333` → `TLX-DEMO33333` "Demo Carga" `carga` (escena `CamionDCarga`)
- **Clima**: aleatorio (`PlayerPrefs["Cargolluvia"] = Random.Range(0,2)`)
- Si `licenseType == "particular"` → Pantalla 1. Si otro → directo a Pantalla 2

### Pantalla 1 — Configuracion (solo licenseType=particular)
- Seleccion de modelo: Sedan→`"Sedan"`, SUV→`"Camioneta"` (Jetta removido — UI muestra "SUV", escena real sigue siendo "Camioneta")
- Seleccion de transmision: Automatica/Manual → `PlayerPrefs["TransmisionManual"]`
- Default: Sedan + Automatica

### Pantalla 2 — Verificacion de controles del volante

Maquina de 3 estados decidida en `PrepareWheelScreen()` segun la huella del
dispositivo conectado (`Cal_DeviceFingerprint = product|manufacturer|serial`)
y la calibracion guardada en PlayerPrefs:

| Estado | Cuando | UI |
|--------|--------|----|
| **Verified** | Huella coincide + 5 fases ya calibradas | Splash "Preparando prueba..." 1.5s con sanity check (axes existen y no estan atorados). Boton "Reasignar controles" para forzar Discovery. |
| **Partial** | Huella coincide, faltan algunos roles | Solo prompts las fases pendientes; las hechas aparecen en verde. |
| **Discovery** | Sin calibracion o huella distinta | Flujo completo de 5 fases. Boton "Iniciar sin volante" como bypass. |

Si la huella cambia (cambio de modelo de volante), la calibracion guardada se
invalida automaticamente y la pantalla cae en Discovery.

#### 5 fases (Discovery completo)

1. **DERECHA** — descubre el eje del volante via baseline + delta maximo positivo
   sobre todos los axes del device. Agnostico al modelo (no asume `stick/x`).
   Persiste el path en `Bind_steerAxis` y reconstruye `steerAction`.
2. **IZQUIERDA** — confirma rango negativo del mismo eje. Persiste
   `G923_SteerCenter/Max/Min` + `Cal_DeviceFingerprint`.
3. **ACELERADOR** — descubre el eje de gas via `PEDAL_AXIS_CANDIDATES` + max delta.
4. **FRENO** — idem, excluyendo el eje ya elegido para gas y el del volante.
   Persiste `G923_GasAxis/Rest/Press` + `G923_BrakeAxis/Rest/Press`.
5. **REVERSA** — primer boton presionado o axis discreto (H-shifter) con delta
   ≥ 0.5 vs baseline. Excluye paths de steering/gas/freno. Persiste
   `Bind_reverse` + `Cal_ReverseDone=1`.

#### PlayerPrefs relevantes

| Llave | Tipo | Origen | Uso |
|-------|------|--------|-----|
| `Cal_DeviceFingerprint` | string | Pantalla 2 | Huella del device de la calibracion. Si cambia, invalida todo. |
| `Cal_ReverseDone` | int (0/1) | Pantalla 2 | Marca fase 5 completada. Necesaria porque `Bind_reverse` tiene default no vacio (`button2`). |
| `Bind_steerAxis` | string | Pantalla 2 / F8 | Path del eje del volante, descubierto. Default `stick/x`. |
| `Bind_reverse` | string | Pantalla 2 / F8 | Path del boton/eje de reversa. Default `button2`. |
| `G923_Steer*`, `G923_Gas*`, `G923_Brake*` | float | Pantalla 2 | Rangos de calibracion (rest/press/min/max). Consumidos por `UIInputNew.cs`. |

### Mapeo licenseType → escena
- `particular` → depende de modelo (Sedan/Camioneta)
- `motocicleta` → `"Motocicleta"`
- `publico` → `"BusPasajeros"`
- `carga` → `"CamionDCarga"`

### Post-inicio
1. **Carga:** Se carga la escena con trafico y peatones Gley
3. **Ejecucion:** ViolationDetector monitorea infracciones en tiempo real
   - Velocidad vs limites de waypoints Gley
   - Semaforos rojos (RedLightDetector)
   - Sentido contrario (WrongWayDetector)
   - Colisiones con vehiculos y peatones
4. **Fin:** TelemetryLogger exporta JSON con score final

## Sistema de Puntuacion

- **Puntaje inicial:** 100 puntos
- **90-100:** APTO (licencia inmediata)
- **80-89:** APTO CONDICIONADO (reforzar areas)
- **70-79:** APTO CONDICIONADO (reentrenamiento)
- **<70 o critica:** NO APTO (licencia negada)

### Penalizaciones
| Infraccion | Puntos | Cooldown |
|------------|--------|----------|
| Exceso de velocidad | -5 | 5s |
| Colision vehicular | -10 | - |
| Semaforo en rojo | -20 | 10s |
| Sentido contrario | -15 | 5s |
| Atropello peaton | -25 | - |

### Zonas de Velocidad (via Gley waypoints)
| Zona | Limite |
|------|--------|
| Escolar | 20 km/h |
| Residencial | 30 km/h |
| Hospitalaria | 30 km/h |
| Urbana | 40 km/h |
| Carretera | 80 km/h |
| Autopista | 110 km/h |

## Tags de Unity Requeridos

| Tag | Uso |
|-----|-----|
| `Player` | Vehiculo del jugador |
| `Pedestrian` | Peatones NPC |
| `automovil` | Vehiculos de trafico |
| `puerta` | Puerta del camion |
| `CajaCamion` | Carga del camion |
| `TrafficLightPost` | Postes de semaforo |

## Integracion con RCCP

Se accede via **reflection** (sin dependencia dura):
```csharp
// Propiedades usadas:
float speed;           // km/h
float engineRPM;
int currentGear;
```
Fallback a `Rigidbody.linearVelocity.magnitude * 3.6f` si RCCP no disponible.

## Integracion con Gley Traffic

```csharp
API.IsInitialized()                    // Verificar sistema listo
API.GetClosestWaypoint(position)       // Obtener waypoint mas cercano
waypoint.MaxSpeed                      // Limite de velocidad actual
waypoint.Neighbors                     // Indices de waypoints vecinos
API.GetWaypointFromIndex(index)        // Obtener waypoint por indice
```

## Telemetria - Output JSON

Archivos en `Application.persistentDataPath/telemetry_[timestamp].json`:
```json
{
    "sessionStart": "2026-02-02 10:30:00",
    "sessionEnd": "2026-02-02 10:45:00",
    "finalScore": 85,
    "events": [
        {
            "timestamp": "45.23s",
            "eventType": "VELOCIDAD",
            "description": "Exceso de velocidad",
            "points": -5,
            "speed": 67.5
        }
    ]
}
```

## Convenciones de Codigo

- **Singletons** para managers: `Instance` property, `DontDestroyOnLoad`
- **Eventos:** `System.Action` (e.g., `OnSpeedLimitChanged`)
- **Reflection** para acceso a RCCP (evita dependencia dura)
- **Coroutines** para secuencias temporales (pasajeros, ragdoll reset)
- **PlayerPrefs** para configuracion persistente entre sesiones
- Los scripts custom estan en `Assets/Custom/`, utilidades miscelaneas en `Assets/Scripts/` y `Assets/pruebas general/`

## Assets de Contenido

| Carpeta | Contenido |
|---------|-----------|
| `Assets/Carros/` | Modelos de vehiculos (Aveo, Honda, Jaguar, etc.) y 24+ bicicletas |
| `Assets/Obstaculos/` | Modelos de carros obstaculo (Car_Free_01, Car_Free_02) |
| `Assets/Letreros/` | Senales viales (alto, ciclovia, cruce escolar, velocidad, etc.) |
| `Assets/Luces/` | Fixtures de iluminacion |
| `Assets/Luces Nuevas/` | Luces direccionales (frontal, trasera) |
| `Assets/Ambulance/` | Modelo de ambulancia |
| `Assets/Police Car & Helicopter/` | Modelos de policia |
| `Assets/construcciones/` | Modelos de edificios |
| `Assets/Forst/` | Vegetacion |

## Volante Logitech G923 PS/PC

Ver `ARCHITECTURE_G923.md` para documentacion completa.

### Resumen rapido
- **Vehiculo usa `PlayerCar` de Gley** (NO RCCP)
- **Ejes** via InputAction: stick/x (steering), z (gas), rz (brake)
- **Botones** via device directo (InputAction bindings NO funcionan para botones HID genericos)
- **Paddles invertidos** en G923 PS: button5=R1(der), button6=L1(izq)
- **H-shifter:** buttons 13-19 = gears 1-6 + R
- **Combos:** L2+R2 hold 1.5s = menu, L3+R3 hold 1.5s = reiniciar
- **Direccionales:** paddles toggle, HUD con flechas verdes parpadeantes
- **Freno:** brakeTorque separado, curva exponencial, calibrado (64km/h → 21.9m)
- **Clutch:** stick/y — pendiente de implementar

### Archivos clave
- `UIInputNew.cs` — todo el input del G923
- `PlayerCar.cs` — controlador del vehiculo
- `SimpleSpeedGauge.cs` — HUD (velocidad, gear, direccionales)
- `IUIInput.cs` — interfaz de input

## Build

- **Ejecutable:** `WindowsIntelBuild0.0.4/Tlax2026MVP.exe`
- **Target:** Windows x64
- **Company:** Tlaxcala
- **Product:** Tlax2026MVP
