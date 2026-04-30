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
| **F7 hold 1.5s** | `LogConsolePanel.cs` | Consola de diagnostico tipo terminal. 4 paneles: devices conectados, PlayerPrefs relevantes (G923_*, Adv_*, Bind_*, Cal_*), inputs en vivo (todo boton presionado + todo eje movido con valor/baseline/rango), feed de `Debug.Log/Warn/Error` con timestamp. Boton "Copiar al portapapeles" exporta texto plano sin tags TMP. Util para descubrir nombres tecnicos de cualquier control sin recompilar. Los valores de ejes se leen con `ReadUnprocessedValue()` para mostrar lo mismo que el runtime. |
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

### Collision Feedback (`Assets/Custom/CollisionFeedback.cs`, `LogitechFFB.cs`)

Sistema de feedback visceral de colisión, agregado en 2026-04 sobre el `ViolationDetector` existente. Da al examinado un golpe inmediato y obvio cuando choca, en vez del texto pequeño del `NotificationManager`.

**Flujo:**
1. `ViolationDetector.OnCollisionEnter()` (línea 202) detecta colisión → cooldown 3s + dedupe por root + clasifica tipo (Pedestrian/Bicycle/Vehicle/Sign/Obstacle).
2. Antes de `lastCollisionTime = Time.time`, dispara `ViolationDetector.OnCollisionImpact` con `CollisionImpactInfo` (contactPoint, impulseWorld, lateralLocal, magnitude, violationType, speedKmh).
3. `CollisionFeedback` (singleton bootstrapped con `RuntimeInitializeOnLoadMethod`) consume el evento y dispara en paralelo:
   - **Overlay de cristal roto**: RawImage full-screen con `Resources/Custom/CrackedGlass.png` (procedural, 2048×1024 RGBA). Fade in 50ms → hold 1.2-1.8s → fade out 500ms.
   - **Camera shake**: jitter de `Camera.main.transform.localPosition` durante 6-14 frames con decay lineal.
   - **Flash rojo**: RawImage rojo full-screen, alpha 0.25-0.45 → fade out 400ms.
   - **Audio**: PlayOneShot de uno de los 4 `Impact*.wav` del RCCP (referenciados via `CollisionImpactClips.asset` en Resources).
   - **FFB G923**: `LogitechPlayConstantForce` direccional (signo según `lateralLocal`) por 80ms + `LogitechPlayBumpyRoadEffect` por 250ms.

**Magnitudes** (escaladas por `t = Clamp01(impulse.magnitude / 50)`):
- Overlay alpha: 0.85-1.0
- Shake amplitude: 0.03-0.08m
- Flash alpha: 0.25-0.45
- Audio volume: 0.3-1.0
- FFB constant: 40-100% (con signo)
- FFB bumpy: 30-80%

Coroutines usan `Time.unscaledDeltaTime` para que feedback siga animándose si los paneles F7-F10 abren `Time.timeScale = 0`.

**Gate**: ninguno. El feedback de colisión es feedback de juego esencial para el examen (el examinado DEBE saber que chocó), no es decoración como las notificaciones del `NotificationManager`. Si en el futuro se necesita un toggle, agregar un campo dedicado a `SimulatorConfig.ConfigData` (ej. `disableCollisionFeedback`) — NO reusar `showNotifications`.

**Logitech SDK** (`LogitechFFB.cs`): wrapper P/Invoke a `LogitechSteeringWheelEnginesWrapper.dll`. Toda la API en `#if UNITY_STANDALONE_WIN`. En Mac/Linux y Windows sin DLL, no-op silencioso (catch `DllNotFoundException`). DLL Windows-only marcada en su `.meta`. Bajar de https://www.logitechg.com/en-us/innovation/developer-lab.html y depositar en `Assets/Plugins/x86_64/` (ver `README_LogitechSDK.txt`).

**HORI Truck**: el HPC-044U **no tiene motor de FFB** (solo springs mecánicos). El feedback no-FFB sigue funcionando normal en escenas de camión/bus.

**Archivos:**
- `Assets/Custom/ViolationDetector.cs` — evento `OnCollisionImpact` + struct `CollisionImpactInfo`
- `Assets/Custom/CollisionFeedback.cs` — singleton orchestrator
- `Assets/Custom/LogitechFFB.cs` — wrapper P/Invoke
- `Assets/Custom/CollisionImpactClips.cs` — ScriptableObject container
- `Assets/Resources/Custom/CrackedGlass.png` — overlay procedural
- `Assets/Resources/Custom/CollisionImpactClips.asset` — refs a Impact2-5.wav
- `Assets/Plugins/x86_64/LogitechSteeringWheelEnginesWrapper.dll` (no commiteado, ver README)

**Para extender**: si se necesita variar el feedback por tipo de violación (no solo magnitud), `info.violationType` está disponible en el handler. Ej: peatón siempre máximo, sin importar velocidad.

### Auto-update OTA (`Assets/Custom/AutoUpdater.cs`)

Singleton que vive en `[AutoUpdater]` GameObject (DontDestroyOnLoad). Hardened en 1.2.2 (abr 2026) tras analisis exhaustivo + revision Codex.

**Flujo:**
1. `SimulatorApiClient.SendHeartbeat()` recibe `pendingUpdate` del backend cada ~3 min.
2. `ProcessPendingUpdate(data)` filtra por status reanudable (PENDING, FAILED, DOWNLOADING, DOWNLOADED, INSTALLING). Setea `isDownloading`/`isProcessing` ANTES de `StartCoroutine` para prevenir race con heartbeats rapidos.
3. `RequestAndDownload()`: POST `/simulator/request-update` con 1 retry (3s delay). Rechaza si SHA256 vacio. Reporta DOWNLOADING, descarga con `UnityWebRequest.Get` + `DownloadHandlerFile`, valida SHA256.
4. Reporta DOWNLOADED, llama `InstallUpdateCoroutine()` (coroutine que espera a que INSTALLING report llegue al backend antes de quit).
5. `InstallUpdateCoroutine()`: genera `update.bat` hardened, ejecuta via `cmd.exe /c` (WDAC-safe), `Application.Quit()`.
6. Bat espera a que Unity cierre (tasklist loop, max 30s), extrae ZIP, xcopy, Unblock-File, relanza exe. Si CUALQUIER paso falla → `:fail` label relanza el exe original (el kiosko NUNCA queda muerto).
7. El nuevo `.exe` arranca, heartbeat reporta `Application.version = nuevaVersion` → backend auto-confirma INSTALLED.

**Mecanismos de proteccion (1.2.2+):**
- `FailUpdate(reason)` centralizado: resetea `isDownloading`/`isProcessing` + reporta FAILED con version. Todas las rutas de error pasan por aqui.
- Try-catch alrededor de toda I/O en coroutines (C# mata coroutines silenciosamente en excepciones no capturadas). Flag pattern para `yield break` fuera del catch (CS1626).
- `ReportUpdateStatus` incluye `version` en body — backend ignora reports de rollouts viejos.
- Bat sin `enabledelayedexpansion` (rompe paths con `!`). Usa `if errorlevel 1` + `goto fail`.
- Paths normalizados (`/` → `\`) y escapados (`%` → `%%` para bat, `'` → `''` para PowerShell).

**Logs locales:** `<appDir>\install.log` con timestamps de cada step — primer lugar a mirar si un install falla.

**Bootstrap manual:** `Manejo/2026MVP/scripts/bootstrap-install.ps1` para migrar PCs con codigo pre-1.2.2 a una build nueva. Una vez por kiosko, despues los OTA siguientes funcionan solos. Cualquier kiosko corriendo >= 1.2.2 ya no necesita bootstrap.

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
  - `44444` → `TLX-DEMO44444` "Demo Ambulancia" `emergencia` (escena `Ambulancia`)
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
| `Cal_FormatVersion` | int | Pantalla 2 | Version del formato de calibracion. Si < 2, fuerza re-Discovery completo (migracion automatica tras cambio a `ReadUnprocessedValue`). |

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

## Volante Logitech G923

Ver `ARCHITECTURE_G923.md` para documentacion completa.

### IMPORTANTE: G923 viene en DOS variantes hardware-locked

**No tiene switch PS/Xbox.** Son productos físicos distintos con HID layouts diferentes:

| Variante | Logitech part | Unity displayName |
|---|---|---|
| **G923 PS** | 941-000147 | `Logitech G923 Racing Wheel for PlayStation 4 and PC` |
| **G923 Xbox** | 941-000158 | `Logitech G923 Racing Wheel for Xbox One and PC` |

`UIInputNew.EnsureG923PSDefaults()` detecta variante por `displayName.Contains("Xbox")` y aplica defaults específicos. Llamado al boot desde `AttachToWheelDevice` y desde `MenuScreenManager.PrepareWheelScreen`.

### Mapping por variante (FIX#26, verificado en F7 en ambos kioskos)

| Función | PS variant | Xbox variant |
|---|---|---|
| Volante (eje) | `stick/x` | `stick/x` |
| Acelerador | `z` (idle=1, press=-1) | `stick/y` (idle=-1, press=+1) |
| Freno | `rz` (idle=1, press=-1) | `z` (idle=1, press=-1) |
| Reversa (palanca H R) | `button19` | `button12` |
| Paddles G923 PS | button5=R1(der), button6=L1(izq) | (verificar) |
| H-shifter PS | buttons 13-18 = gears 1-6, button19 = R | (verificar) |
| Combos | L2+R2 (btn7+8) menú · L3+R3 (btn11+12) reset | (verificar) |

### Kioskos identificados

- **Casa Aramis** (pcId `d603a85840752414e264d4dc47ec76db5a511135`): **PS variant**
- **Demo gobernadora** (2026-04-26): **Xbox variant**

Mismo binary funciona en ambos gracias al dual-detection.

### Resumen general
- **Vehiculo usa `PlayerCar` de Gley** (NO RCCP)
- **Botones** via device directo con `TryGetChildControl` (InputAction bindings NO funcionan para botones HID genericos)
- **Lectura de ejes**: `SafeReadFloatRaw` con `ReadUnprocessedValue()` para volante/pedales (bypassa `axisDeadzone` de Unity). `SafeReadFloat` con `ReadValue()` solo para botones. Ver `ARCHITECTURE_G923.md` seccion "Lectura de ejes" (FIX#27)
- **Steering**: stick/x con `NormalizeSteer` por rango calibrado + curva `f(x)=x/(a+(1-a)x)` con `_steerCurveA=0.65` (FIX#20)
- **Pedales**: `NormalizePedal(raw, rest, press) = (raw-rest)/(press-rest)` clamp01
- **Direccionales**: paddles toggle indicator, HUD con flechas verdes parpadeantes
- **Freno**: brakeTorque separado, curva por tramos `BrakeCurve` con `_brakeSoftEnd=0.5`/`_brakeSoftMaxOutput=0.5` (FIX#20)
- **Rueda visual cockpit**: `PlayerCar.Start()` clampea `steeringWheelSmoothTime` a 0.05s si > 0.1s (muchas escenas tienen 1.5s de fabrica, que con volante analogico causa lag visual devastador) (FIX#27)
- **Clutch**: pendiente de implementar
- **Reversa en automático**: lógica híbrida edge+debounce 300ms (FIX#24) — robusto a pulsos transitorios del HID

### Phantom signals filtrados (BindingsPanel.PHANTOM_PATHS)

Algunos paths siempre reportan estado fijo y confunden la calibración:
- `stick/y` cuando NO se usa (varía según variante PS/Xbox)
- `stick/up`/`stick/down`/`stick/left`/`stick/right` son ButtonControl derivados del eje stick — no son botones independientes

NO se filtra `button19` porque en PS variant es la posición R real del H-shifter (parece "always on" solo si dejas el shifter en R).

### Archivos clave
- `UIInputNew.cs` — todo el input del G923, dual-detection PS/Xbox
- `PlayerCar.cs` — controlador del vehiculo
- `SimpleSpeedGauge.cs` — HUD (velocidad, gear, direccionales)
- `IUIInput.cs` — interfaz de input
- `MenuScreenManager.cs:PrepareWheelScreen` — fast-path Logitech salta calibración Pantalla 2

## HORI Truck Control System (HPC-044U)

Ver `PLAN_HORI_TRUCK.md` para documentacion completa.

- **Plataforma:** Windows only (NO macOS)
- **USB:** 2 devices separados — `HORI TRUCK CONTROL SYSTEM WHEEL` + `HORI TRUCK CONTROL SYSTEM SHIFTER`
- **Deteccion:** `UIInputNew.IsHORITruck()` por displayName/product que contenga "HORI"
- **Defaults automaticos** en `AttachToWheelDevice()` al detectar HORI (no usa `EnsureG923PSDefaults`)

### Mapping verificado (F7, 2026-04-29)

| Funcion | Path | Device | Reposo |
|---------|------|--------|--------|
| Pedales (3) | `rz`, `slider`, `slider1` | WHEEL | -1.0 (press hacia +1.0) |
| Direccional izq | `button40` | WHEEL | — |
| Direccional der | `button41` | WHEEL | — |
| Intermitentes | `shifter:button27` | SHIFTER | — |
| Reversa | `shifter:button7` | SHIFTER | — |

- **Pedales**: descubiertos por Pantalla 2 Discovery (`slider`/`slider1` agregados a `PEDAL_AXIS_CANDIDATES`)
- **Intermitentes**: binding dedicado `Bind_hazard` (G923 usa combo L1+R1, HORI tiene boton fisico)
- **Steering**: descubierto dinamicamente por Pantalla 2 (no verificado el eje exacto)

### Pendiente
- Mapear marchas H-shifter (buttons en SHIFTER device)
- Verificar force feedback

## Build

- **Ejecutable:** `build/<version>/Tlax2026-RC.exe` (productName = `Tlax2026-RC`, NO `Tlax2026MVP`)
- **Target:** Windows x64
- **Company:** Tlaxcala
- **Product:** Tlax2026-RC

**CRÍTICO:** El productName en `ProjectSettings/ProjectSettings.asset` debe quedarse en `Tlax2026-RC` para coincidir con los exes que ya corren en los kioskos. Si cambia (ej. alguien lo pone a `Tlax2026MVP` por error), el OTA entra en loop infinito porque `update.bat` extrae el ZIP nuevo pero relanza el exe viejo (lookup por nombre vía `tasklist`). Ver `feedback_unity_build_checklist.md` en `~/.claude/projects/-Users-sim4r4-sim4r4-repos-simulador/memory/`.
