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

### Weather System (`Assets/Custom/WeatherManager.cs`)

Sistema de clima aleatorio (Sol / Lluvia / Granizo). Singleton bootstrapped (`AfterSceneLoad`) que activa/desactiva los GO `LLuvia` y `Granizo` preexistentes en cada escena según `PlayerPrefs.GetInt("Clima")`. Sorteo + override demo codes en `MenuScreenManager.PickAndSetWeather`.

Ver [`WEATHER_SYSTEM.md`](WEATHER_SYSTEM.md) para arquitectura completa, gotchas (`size3D`, `AfterSceneLoad`, `rateOverTimeMultiplier`), demo codes `TTTTXY` y TODOs.

### Wiper Control (`Assets/Custom/WiperAutoController.cs`, v1.3.9)

Singleton bootstrapped (`AfterSceneLoad`, `DontDestroyOnLoad`) que maneja los limpiaparabrisas en escenas con `ControllWipers` (Sedan, Camioneta, Ambulancia, CamionDCarga; agregados en commit `486906e4` "Espejos con wipers"). Tres responsabilidades:

1. **Auto-on por clima**: al cargar escena lee `PlayerPrefs.GetInt("Clima")` y llama `ControllWipers.SetMode(2)` si hay lluvia/granizo, o `SetMode(0)` si sol. El examinado nunca tiene que tocar nada para encender wipers cuando llueve.

2. **Override del binding `<Keyboard>/e`**: el demo del asset `WindshieldRainAsset` tenía `Wipe → <Keyboard>/e`, lo cual chocaba con direccional derecha (`PlayerCar.cs:286` lee `eKey` directo del Keyboard). El controller anula ese binding en runtime con `ApplyBindingOverride(i, "")` — no se modifica el `.inputactions` vendor.

3. **HORI buttons**: polling directo del device (no path strings) — **button42 → wipers OFF**, **button43 → ON latcheado en velocidad media (mode 2)**. Detecta el WHEEL filtrando `displayName.Contains("HORI")` y `!Contains("SHIFTER")`. No usa el sistema de bindings de `UIInputNew` por simplicidad; si en el futuro se quiere remapear, agregar `PREF_BIND_WIPER_ON/OFF` en `UIInputNew` y leer esos paths en lugar de los hardcoded `button42/43`.

**Excluye** `MainMenu` y `Motocicleta` (early return en `HandleScene`).

**Cambio vendor mínimo**: `Assets/WindshieldRainAsset/Common/DemoResources/Scripts/ControllWipers.cs` recibe un método público `SetMode(int)` con clamp defensivo. Si el asset se reimporta limpio, este método se pierde y el `WiperAutoController` falla en compile-time. Re-aplicar el patch tras cualquier upgrade del asset.

**Atajos de teclado (debug)**: `0` = off, `1`/`2`/`3`/`4` = modos 1-4. Heredados del demo, sobreviven al override.

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

**Cap a 3 intentos + alerta (may-02 2026, commits `3201fc43`/`23a6acbd`):**
- **Backend autoritativo**: `pendingUpdate.attemptCount` se incrementa solo en transición a FAILED (idempotente). `simulator-request-update` retorna 409 si ≥ 3. `simulator-heartbeat` filtra `pendingUpdate` del response cuando abandonada (no borra de DB). Recovery natural via subir versión nueva (replace completo de `pendingUpdate` map).
- **Cliente defensivo**: PlayerPrefs `Update_LocalAttempts_{version}` + `Update_LastAttemptedVersion`. Crash recovery en `ProcessPendingUpdate`: si la versión pendiente coincide con la última intentada, asume que el intento previo crasheó y bumpea el counter (flag `_localAttemptBumpedThisSession` previene doble-bump). Si llega a 3 local, return early. Cleanup automático en `Start()` cuando `Application.version == LastAttemptedVersion`.
- **Alerta**: métrica CloudWatch `UnityBuildAbandoned` (sin dimensions), alarma `licencias-{env}-unity-build-abandoned` threshold ≥1 en 5min → email + SMS via notifier existente (P1 default por estado ALARM). Log estructurado con pcId/version.
- **UI admin**: badge gris "Abandonada" + indicador `× N` en `/admin/simuladores/pcs`.

**Main thread fixes (may-02 2026, commits `3201fc43`/`23a6acbd`):**
- `ComputeFileSHA256` en `OnDownloadComplete` y validación de ZIP existente: corre en `Task.Run` (worker thread). Antes bloqueaba 5-15s al 99% CPU sobre ZIP de 600MB.
- `Process.Start(powershell)+WaitForExit(5000)` para Unblock-File del bat: corre en `Task.Run`. Antes bloqueaba ~1s (PowerShell cold start). Profile de Jason mostraba 1031ms self-time aquí.
- Patrón canónico: `var task = Task.Run(...); while (!task.IsCompleted) yield return null; if (task.IsFaulted) ...`. `Debug.LogWarning` desde worker thread es safe (Unity rutea por `Application.logMessageReceivedThreaded`).

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
- **Codigo manual**: Input "TLX-NNNNNN" (6 digitos numericos) → GET `/simulator/lookup?code={code}` → misma info. El backend reconstruye `tramiteId = 'TLX-' + code` y consulta por primary key.
- **Demo codes** (6 digitos, formato `TTTTXY`, normalizados con `.Trim().ToUpper()`):
  - **Primeros 4 digitos = tipo de licencia/vehiculo**, **5° digito = override de clima (0=sol, 1=lluvia, 2=granizo; 3-9 = random ponderado)**, **6° digito = ubicacion de spawn (0..5)**.
  - `0000XY` → `TLX-DEMO00000` "Demo Automovil" `particular` (abre Pantalla 1 de seleccion de modelo)
  - `1111XY` → `TLX-DEMO11111` "Demo Pasajeros" `publico` (escena `BusPasajeros`)
  - `2222XY` → `TLX-DEMO22222` "Demo Moto" `motocicleta` (escena `Motocicleta`)
  - `3333XY` → `TLX-DEMO33333` "Demo Carga" `carga` (escena `CamionDCarga`)
  - `4444XY` → `TLX-DEMO44444` "Demo Ambulancia" `emergencia` (escena `Ambulancia`)
  - **Sufijo Y**: `1..5` = waypoint Gley fijo (ver `SpawnLocationManager.DEFAULT_WAYPOINTS`), `0` = aleatorio entre los 5. Cualquier otro digito (`6..9`) cae al flujo backend normal.

### Spawn por sufijo (ubicaciones en la ciudad)

`Assets/Custom/SpawnLocationManager.cs` (singleton bootstrap, post-merge feature/spawn-locations-by-suffix):

- Lee `GameManager.LocationId` (1..5 = fijo, 0 = random) y teleporta al `Player` (busca por `Gley.UrbanSystem.PlayerCar` → tag `Player` → name `Player`) al waypoint Gley correspondiente.
- Reusa la red de Gley TrafficSystem ya cargada — no requiere editar las escenas en el editor.
- Tabla `DEFAULT_WAYPOINTS = { 3170, 4588, 3170, 6765, 4588 }` (slots 1..5; slots 1 y 5 son espejo temporal de 3 y 2 hasta capturar puntos definitivos).
- Solo activo en escenas whitelist: `Sedan`, `Camioneta`, `Motocicleta`, `BusPasajeros`, `CamionDCarga`, `Ambulancia`. En MainMenu nunca se inyecta.
- Usa `UNASSIGNED = -1` como sentinel: si el slot vale `-1`, el runner deja el spawn original (legacy).
- `LocationId` se resetea a `1` antes de `OnSessionVerified` en flujos backend (LookupByCode + QR poll) para no heredar sufijo de demos previos.

**Helper de debug**: tecla `K` en escena de manejo loguea `[WaypointDebug] ... idx=NNN ...` con el waypoint Gley mas cercano al Player. Util para descubrir indices y refinar `DEFAULT_WAYPOINTS` sin abrir el editor.

**Bug evitado** (no repetir): la version inicial usaba `DontDestroyOnLoad` en el debugger/runner → al volver al MainMenu seguian vivos y `WaypointDebugger.Update()` tocaba `TrafficAPI.GetClosestWaypoint()` con Gley descargado → race / NRE / crash. Solucion actual: sin `DontDestroyOnLoad` + guard whitelist en `Update()`.
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

`UIInputNew.SeedG923VariantDefaultsIfMissing()` detecta variante por `displayName.Contains("Xbox")` y siembra defaults específicos sin pisar lo ya seteado en PlayerPrefs (preserva remaps F8). Llamado al boot desde `AttachToWheelDevice` y desde `MenuScreenManager.PrepareWheelScreen`. Para forzar reset (recovery / "Reasignar controles"), `ForceResetG923VariantDefaults()`.

### Mapping por variante (FIX#26 ext. v1.5.7, verificado en F7 en ambos kioskos)

Las 3 axes están **rotadas** entre PS y Xbox (mismas funciones físicas, distintos paths HID):

| Función | PS variant | Xbox variant |
|---|---|---|
| Volante (eje) | `stick/x` | `stick/x` |
| Acelerador | `z` (idle=1, press=-1) | `stick/y` (idle=-1, press=+1) |
| Freno | `rz` (idle=1, press=-1) | `z` (idle=1, press=-1) |
| **Clutch** | `stick/y` (idle=-1, press=+1) | **`rz` (idle=1, press=-1)** ← v1.5.7 |
| Reversa (palanca H R) | `button19` | `button12` |
| Paddles G923 PS | button5=R1(der), button6=L1(izq) | (verificar) |
| H-shifter PS | buttons 13-18 = gears 1-6, button19 = R | (verificar; fallback legacy 13-18 también activo en Xbox) |
| Combos | L2+R2 (btn7+8) menú · L3+R3 (btn11+12) reset | (verificar) |

⚠️ **Pre-v1.5.7**: la asunción "G923 Xbox no tiene clutch físico" causaba `DeleteKey(PREF_G923_CLUTCH_AXIS)` en el branch Xbox, dejando `_clutchCtrl=null` y forzando silent fallback Manual→Auto. **Falsa asunción**: el SKU 941-000158 trae pedalera 3-pedal idéntica al SKU PS. Verificado por F7 del operador en Sedán 1 (2026-05-06): pisar clutch movió eje `rz` de 1.0 → -1.0.

### Kioskos identificados

- **Casa Aramis** (pcId `d603a85840752414e264d4dc47ec76db5a511135`): **PS variant**
- **Demo gobernadora** (2026-04-26): **Xbox variant**
- **Sedán 1** (Norberto, 2026-05-06): **Xbox variant** — confirmó el bug del silent fallback en v1.5.4/v1.5.5

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
- **Deteccion (v1.6.5+):** `UIInputNew.IsHORITruck()` matchea por VID + PID + interfaceName="HID" usando `HIDDeviceDescriptor.FromJson` (VID=0x0F0D + PID=0x017A wheel / 0x0186 shifter). Fallback a displayName/product.Contains("HORI") preserva v1.6.4.
- **Defaults automaticos** en `AttachToWheelDevice()` al detectar HORI (no usa `EnsureG923PSDefaults`)

### Mapping verificado (F7, 2026-05-06)

| Funcion | Path | Device | Reposo |
|---------|------|--------|--------|
| Pedales (3) | `rz`, `slider`, `slider1` | WHEEL | -1.0 (press hacia +1.0) |
| Direccional izq | `button40` | WHEEL | — |
| Direccional der | `button41` | WHEEL | — |
| Intermitentes | `shifter:button27` | SHIFTER | — |
| Reversa | `shifter:button7` | SHIFTER | — |
| H-shifter 1ª | `shifter:trigger` | SHIFTER | — |
| H-shifter 2ª | `shifter:button2` | SHIFTER | — |
| H-shifter 3ª | `shifter:button3` | SHIFTER | — |
| H-shifter 4ª | `shifter:button4` | SHIFTER | — |
| H-shifter 5ª | `shifter:button5` | SHIFTER | — |
| H-shifter 6ª | `shifter:button6` | SHIFTER | — |

- **Pedales**: descubiertos por Pantalla 2 Discovery (`slider`/`slider1` agregados a `PEDAL_AXIS_CANDIDATES`)
- **Intermitentes**: binding dedicado `Bind_hazard` (G923 usa combo L1+R1, HORI tiene boton fisico)
- **Steering**: descubierto dinamicamente por Pantalla 2 (no verificado el eje exacto)
- **H-shifter**: Unity nombra la 1ª como `trigger` (no `button1`) por HID usage especial. Defaults idempotentes en `AttachToWheelDevice` (sobreviven reasignaciones F8). Ver `PLAN_HORI_TRUCK.md` sección "Modo manual".

### Modo manual + clutch
- Funcional desde 2026-05-06. `Bind_gear1..6` y `Bind_reverse` defaults idempotentes en `AttachToWheelDevice` cuando se detecta HORI.
- **Clutch axis**: Discovery dinámico (Phase 6) en Pantalla 2 condicional a `TransmisionManual==1 && IsHORITruck`. NO hardcoded — Unity alias slider/slider1/rz arbitrariamente entre brake y clutch (ver `HORI_THROTTLE_BUG_RESOLUTION.md:170-171`).
- Detección de "rechino" funciona igual que G923 PS (mismo edge-trigger en `UIInputNew`, mismo gating con `HasPhysicalClutch()` en `ViolationDetector`).
- **Cambio HORI→G923 sin recalibrar**: `ReCacheGearControls` cae al fallback legacy 13-19 si los paths `shifter:*` no resuelven; `SanityCheckThenLoad` adicionalmente limpia binds huérfanos.
- **Reverse (`shifter:button7`) es PULSE** — solo on por 1-2 frames mientras la palanca cruza R, luego OFF aunque el lever quede físicamente en R (mecánica del H-shifter).
  - **v1.5.8 (insuficiente)**: edge-trigger latch de 300 ms (`MANUAL_REVERSE_LATCH_SECONDS`). Solo difería la pérdida de la reversa 300 ms — al expirar, `desiredGear=0` + `toNeutral=true` reseteaba a Neutral aunque el lever siguiera en R. Norberto reportó persistencia del bug post-deploy en Sedán 1.
  - **v1.5.9 (actual)**: sticky bool `_manualReverseLatched` HORI-only en `UIInputNew.cs`. Se arma en flanco OFF→ON de `_crossCtrls` solo cuando `IsHORITruck(_wheelDevice)`, persiste hasta detectar gear 1-6 o teardown del wheel/shifter. Resets defensivos en `OnDeviceChange` (wheel y shifter Removed/Disconnected/Disabled), `AttachToWheelDevice` (cambio de device) y rama Manual→Auto degradation (line ~1378). G923 Xbox button12 (HOLD) sigue cubierto por `crossNow` directo — armar sticky en G923 dejaría reversa pegada al soltar el botón. Logs de armado/cancelación van a LogUploader (sin spam por frame, solo en transiciones).
  - **Limitación conocida (v1.5.9)**: si HORI button7 también pulsea al SALIR de R (in/out del gate), el sticky se cancelaría incorrectamente al estilo "armé y desarmé en el cruce sin tocar gear 1-6". No descartado empíricamente — verificar log de Sedán 1 post-deploy con test R→N→1→N→R buscando líneas `[UIInputNew] Manual reverse latched` y `Manual reverse latch cancelado: gear N` en S3 (`s3://simtabasco-simulator-logs-prod/logs/{pcId}/`).
  - **v1.5.10 (band-aid + diagnostic probe)**: Norberto reportó (2026-05-07) que el sticky se queda **pegado** tras un R→N físico. F7 confirma que button7 NO pulsea al salir de R (solo al entrar). Lever R→N sin pasar por gears 1-6 deja `_manualReverseLatched=true` indefinidamente; pisar gas mueve el camión hacia atrás aunque el operador no lo pidió. Mitigación: si el sticky lleva >3s armado Y el operador sostiene brake+clutch sin pisar gas (`brakeInput≥0.5 && clutchInput≥CLUTCH_ENGAGE_THRESHOLD && verticalInput<0.1`) Y `_currentGear==-1`, cancelar el sticky. Trade-off: legítimo R-engage con shoulder-check >3s sin gas se cae; aceptado vs el bug actual. Diagnostic probe `Assets/Custom/HoriShifterProbe.cs` enumera HID paths VID 0F0D, salta col01 (wheel/HoriThrottleReader), corre reader thread por canal restante (shifter), loggea cambios de bytes a Debug.Log (rate-limited 100ms/canal). El log se sube a S3 vía LogUploader. Norberto recorre N→1..6→R (3s en cada posición); descargamos y identificamos el byte de posición de palanca para `HoriShifterReader` (raw HID poller continuo) en v1.6.0.
  - **v1.5.11 (root cause real, no era pulse-on-exit)**: descarga de logs S3 de SIM-005 (HORI Pasajeros 1) tras deploy v1.5.10 reveló que `Bind_reverse = "wheel:slider1"`. NO era el sticky pulse-on-exit que sospechábamos — Pantalla 2 Phase 5 (Discovery de reversa) había guardado un eje de pedal del wheel como path de R. Cada presión de brake → `IsPressed(slider1) > 0.5` → `crossNow=true` → sticky armado → R engaged. El log reportaba `crossNow=True` con `brake=0.753 clutch=1.000` — confirmado. El v1.5.10 band-aid no podía mitigar porque la condición re-arma cada frame que se pisa brake. Fix: `ForceHoriBind(prefKey, canonical)` helper en `UIInputNew.cs`, sobrescritura **incondicional** en el bloque `IsHORITruck` de `AttachToWheelDevice` para `Bind_reverse` + `Bind_gear1..6` (rutas hardware-fijas en HORI, no F8-configurables). Log warning si se sobrescribe non-canonical (diagnostic para detectar si Pantalla 2 sigue corrompiendo). El band-aid del sticky timer y `HoriShifterProbe.cs` se mantienen como defensa secundaria mientras llega v1.6.0. Pendiente investigar Pantalla 2: por qué Phase 5 picks pedal axis para HORI cuando el seed ya tiene `shifter:button7`.
  - **v1.5.12 (hotfix integral, post-deploy v1.5.10)**: cinco issues encadenados. (1) v1.5.11 force-override no estaba buildeada → kiosko corre con `Bind_reverse=wheel:slider1` contaminado; build de v1.5.12 hereda automáticamente. (2) Modal "El volante HORI Truck no tiene el clutch calibrado" en `MenuScreenManager.PrepareWheelScreen` era catch-22 — bloqueaba ANTES de Phase 6 (que es la calibración del clutch). Fix: special-case en `PrepareWheelScreen` (no en `GetManualBlockReason`) que skipea el modal cuando `reason == NoPhysicalClutch_HORINotCalibrated` y deja correr Phase 6. (3) Sanity check del splash (`SanityCheckLoop`) era destructivo: cualquier delta > 0.5 vs rest → `ClearWheelCalibration()` global. Si el operador apoyaba el pie en pedal durante el splash, perdía toda la calibración (caso real: SIM-005 v1.5.10 a las 22:21Z). Fix: prompt "Suelta los pedales" + retry corto; tras retries invalidar SOLO la pref del axis afectado (no wipe global, no loop infinito). (4) Discovery hardening en `SnapshotReverseBaseline`: ampliar `_reverseExcludedPaths` a clutch axis + TODOS los `Bind_*` (incluyendo restart/menuA/B/restartA/B) + `PEDAL_AXIS_CANDIDATES` cuando `G923_GasAxis == HORI_RAW_GAS_PATH`. Phase 5 reverse persist rechaza `isAxis=true` o paths que no empiecen con `shifter:` cuando es HORI (con throttle 5s del log para evitar spam). (5) Pre-flight HORI sin shifter: nuevo `ShowHoriShifterMissingDialog` para indicar al operador que conecte el shifter USB (antes Phase 5 entraba en loop sin explicación). Adicional: clutch resolve guard en `clutchAlreadyCal` (si clutchPath persistido pero el control no resuelve → invalidar prefs y forzar Phase 6 re-calibración; previene degrade silencioso a Auto post-USB-port-change). `HoriShifterProbe.Bootstrap` gateado por `PlayerPrefs.GetInt("Diag_HoriShifterProbe", 0) == 1` — en producción default no arranca (ni handles ni threads); para activarlo en kiosko de diagnóstico setear a 1 manualmente. v1.5.10 band-aid `_stuckIndicatorStartedAt` se conserva para R→N stuck-state (limitación documentada).
  - **v1.6.0 (planned, retira el sticky)**: `HoriShifterReader.cs` (mismo patrón P/Invoke de HoriThrottleReader) leerá el byte continuo de posición. Reemplaza `crossNow || _manualReverseLatched` por `HoriShifterReader.IsLeverInR()`. Elimina `_manualReverseLatched`, `_lastCrossPressedManual`, `_stuckIndicatorStartedAt` y todos sus resets. Retira `HoriShifterProbe.cs`.

### Pendiente
- Verificar force feedback (HORI no tiene FFB — `LogitechFFB.cs` no-op)

### v1.6.4 — pedales hardware-fijos (NO leer rest/press de PlayerPrefs)

Tras 4 builds de debug (v1.6.1→v1.6.4) y 4h de root cause analysis (ver
`HORI_CALIBRATION_LESSONS.md`), `UIInputNew.AttachToWheelDevice` hardcodea
los rest/press de los pedales HORI cuando detecta el wheel:

```csharp
if (IsHORITruck(device)) {
    _gasRest = 0f; _gasPress = 1f;        // Throttle: sentinel via reader
    _brakeRest = -1f; _brakePress = 1f;   // rz HID byte 23-24 LE16 → -1..+1
    _clutchRest = -1f; _clutchPress = 1f; // slider/slider1 HID byte 19-20
}
```

Razón: Phase 4/6 Discovery captura los rest/press dinámicamente, pero si el
operador tiene un pie en el pedal durante `SnapshotPedalRests`, captura
`rest=+1` (pedal pisado) → `press = rest + delta = 0` → `NormalizePedal(raw=-1, 1, 0) = 1.0`
→ **freno stuck pisado al 100% sin pedal físico** → carro inmóvil. Bug real
en Pasajeros 2 documentado.

### v1.6.5 — hardening adicional

- **F7 muestra readers HORI** (`LogConsolePanel.cs`): nueva sección "CUSTOM HID READERS" lee `HoriThrottleReader.Instance.Value` y `HoriShifterReader.Instance` directo. Antes el throttle no aparecía en F7 porque Unity nunca crea `AxisControl` para el byte huérfano.
- **`IsHandleOpen` en HoriThrottleReader**: nuevo getter expone si el handle HID está abierto. F7 imprime `handle=open|CLOSED`.
- **VID/PID detection** en `IsHORITruck` (ver línea arriba).
- **Phase 4/6 NO escribe `*Rest/Press` para HORI** + gates `pedalsCal`/`clutchPathPersisted` axis-only para HORI en `PrepareWheelScreen`. Combinado con v1.6.4 elimina contaminación del registry.
- **Sanity check hardware-aware** (`SanityCheckThenLoad`): si HORI brake/clutch raw cae fuera de `[-1.05, +1.05]` durante 3 frames consecutivos → invalida solo el axis path para forzar re-Discovery dirigida.
- **`SimulatorApiClient.BuildCalibrationPayload`** reporta canónico (-1/+1) al backend cuando detecta HORI sentinel.
- **Phase 3 ya no auto-pasa para HORI** (Fase B5). Verifica en vivo que `HoriThrottleReader.Value > 0.7` con handle abierto antes de marcar throttleDone. Si el reader no inicializa tras 8s, prompt cambia a "HoriThrottleReader sin handle - verifica USB". Cierra el hueco silencioso de v1.5.12 donde un reader muerto pasaba de calibración a gameplay sin diagnóstico.
- **`InputMath.NormalizePedal` extraído** a `Assets/Custom/InputMath/` con tests EditMode (10 casos) que cubren divide-by-zero, polaridad invertida, span boundary, y el regression test del bug v1.6.3.

### v1.6.6 — Fase B6: HoriShifterStateProvider integrado en desiredGear

Bug Aramis 2026-05-11: 3ra/4ta/5ta/6ta intermitentes con HORI — el carro se quedaba "clavado a 20-30 km/h" sin avanzar tras 2da. Causa: `shifter:button2..6` son PULSE 1-2 frames en HPC-044U (no HOLD como `shifter:trigger`), `_gearControls[i].IsPressed()` los perdía y `desiredGear→0=Neutral`.

Fix: `UIInputNew.cs:1989+` ahora consume `HoriShifterStateProvider` (asignado por `HoriShifterReader.Bootstrap`) cuando el device es HORI y el reader retorna valor válido (`≠ int.MinValue`). El reader lee `byte[1] bits 0-5+6` directo del HID — persistente mientras la palanca está en gear. Pulse-based queda como fallback si el reader muere.

### v1.6.7 — Fase B7: Neutral debounce HORI (fix "atorado a 20 km/h")

Bug introducido por la integración v1.6.6: el reader continuo expuso una trampa en la lógica de gear apply. `HoriShifterReader.byte[1]` reporta 0 brevemente durante el cruce mecánico entre gates (~50-200ms). `UIInputNew.cs` permite `toNeutral=true` (Neutral sin clutch) → `_currentGear=0` durante el transit → al llegar al siguiente gear, `clutchInput=0` lo bloquea forever → vehículo cae a `motorTorque=0`.

Fix: `UIInputNew.cs:2046+` agrega debounce HORI-only de 300ms — si reader reporta 0 mientras `_currentGear≠0`, enmascarar `desiredGear` como sameGear hasta que el lever termine en gate o sostenga N >300ms. Preserva pedagogía: sin clutch → rechino + vehículo en 2da, no atorado.

Ver `HORI_CALIBRATION_LESSONS.md` sección "v1.6.7 — Fase B7" para detalle completo, edge cases y limitaciones conocidas.

### v1.7.0 — Calibración immutable + Pantalla 2 verify-only

Reescritura del flujo de calibración HORI. JSON file en `<persistentDataPath>/hori_mapping.json` es source of truth (no PlayerPrefs para HORI). F8 panel `HoriCalibrationPanel` es el único writer. Pantalla 2 rama HORI ahora es verify-only.

- **Archivos nuevos**: `Assets/Custom/HoriCalibration/{HoriMapping,HoriControlMapping,HoriMappingMigration,HoriPreflightCheck}.cs` + `Assets/Custom/HoriCalibrationPanel.cs`.
- **Migración**: al primer boot post-OTA, lee PlayerPrefs HORI legacy → produce JSON → flag `auto-migrated`. PlayerPrefs siguen intactos como backup.
- **Modal pre-flight**: si JSON ausente o preflight falla, modal bloqueante con lista de faltantes + "F8 sostén 1.5s para calibrar".
- **Heartbeat**: `controlMapping` field JSON-stringified — portal admin muestra read-only en `/admin/simuladores/pcs/[pcId]/calibracion`.
- **Folder**: install canónico `C:\Tlax2026-RC\` (script `scripts/consolidate-install-path.ps1`).
- **Path canónico claxon HORI**: `wheel:button7` (verificado por SSH a Aramis Phase 0).
- **Scope**: HORI Truck only. G923 PS/Xbox/Moto sin cambios.

**Bug catastrófico cerrado durante smoke testing** (PR #162 merged 2026-05-11):
- Pre-fix: `_brakeCtrl` y `_clutchCtrl` ambos cacheados al axis `rz` (PlayerPrefs legacy de Discovery viejo). Pisar clutch → freno fantasma ~2000 Nm → operator perdía 60 km/h en 2 seg por shift.
- Fix r14: para HORI, `brakePath`/`clutchPath` se leen del JSON en `AttachToWheelDevice`, NO de PlayerPrefs.
- Audit r15: 2 phantoms más encontrados (steer rangos + manual block gate) → ahora también desde JSON.

**Principio cementado v1.7.0**: para HORI, **ninguna lectura de `PlayerPrefs.Get*("G923_*")` o `Bind_*` debe sobrevivir sin estar gated por `IsHORITruck(device)` y override desde `HoriControlMapping.Active`**. G923/Moto siguen leyendo PlayerPrefs como antes.

Ver `HORI_CALIBRATION_LESSONS.md` sección v1.7.0 y v1.7.0 lecciones smoke testing, y `docs/superpowers/specs/2026-05-11-hori-v170-immutable-calibration-design.md`.

### v1.8.0 — G923 calibración immutable con rollback feature flag

Mirror del pattern HORI v1.7.0 aplicado a G923 (PS + Xbox variants), pero con **rollback strategy** para no romper productivos. Productivos G923 (Sedán 1, Sedán 2) NO pueden quedarse rotos, así que el flag default es 0 (legacy) y la migración es opt-in por kiosko.

- **Archivos nuevos**: `Assets/Custom/G923Calibration/{G923Mapping,G923ControlMapping,G923MappingMigration,G923PreflightCheck}.cs` + `Assets/Custom/G923CalibrationPanel.cs` + asmdef `TlaxSim.G923Calibration`.
- **Feature flag**: PlayerPref `G923_UseJsonMapping` (int)
  - `0` (default) = legacy PlayerPrefs mode. Comportamiento idéntico a v1.7.x — sin cambios.
  - `1` = JSON immutable mode. UIInputNew lee `G923ControlMapping.Active`. F8 abre `G923CalibrationPanel` nuevo. Pantalla 2 entra en verify-only.
- **Migración**: al primer flip 0→1, lee PlayerPrefs G923_*/Bind_*, detecta variant por `Cal_DeviceFingerprint` (fingerprint contiene "Xbox" o "PlayStation"/"Logitech") o fallback por gas axis (z=PS, stick/y=Xbox), valida rest/press (±0.1 de canonical ±1 y signos opuestos), guarda JSON `g923_mapping.json`. Si validación falla, modal F8 pide calibrar manual.
- **Rollback**: F8 → "Volver a modo legacy" pone flag=0 (no toca PlayerPrefs ni borra JSON). SSH alternativa: `reg add ... G923_UseJsonMapping_h<hash> /t REG_DWORD /d 0 /f`. PlayerPrefs NUNCA se borran durante JSON mode → rollback inmediato siempre disponible.
- **Heartbeat**: `controlMapping` field con priority HORI > G923 (single blob JSON-stringified). Solo envía G923 cuando flag=1 + Active válido.
- **F8 panel**: 3 tabs (Conducción / Luces / Marchas Manual). Botón "Volver a modo legacy" en header. Gas pedal usa `DetectAxis` regular (no `HoriThrottleReader` sentinel).
- **Portal admin**: `WheelMappingTable.tsx` (genérico) detecta tipo HORI vs G923 por discriminator (variant field) y renderiza tabla correspondiente.
- **Out of scope**: FFB tuning (F9 `Adv_*`), Moto, HORI Truck (sin cambios).
- **Bloqueado para productivos sin autorización explícita**. Smoke test en Casa Aramis (PS variant) primero.

Spec: `docs/superpowers/specs/2026-05-11-g923-v180-immutable-calibration-design.md`
Plan: `docs/superpowers/plans/2026-05-11-g923-v180-implementation.md`

**Principio cementado v1.8.0**: para G923 con flag=1, cada read de `PlayerPrefs.Get*("G923_*")` o `Bind_*` en el branch G923 está gated por el ternary `(IsLogitechG923Family(device) && G923ControlMapping.IsJsonModeEnabled()) ? Active : null`. Cuando Active!=null, los valores vienen del JSON; cuando Active=null o flag=0, fallback a PlayerPrefs legacy.

## Build

- **Ejecutable:** `build/<version>/Tlax2026-RC.exe` (productName = `Tlax2026-RC`, NO `Tlax2026MVP`)
- **Target:** Windows x64
- **Company:** Tlaxcala
- **Product:** Tlax2026-RC

**CRÍTICO:** El productName en `ProjectSettings/ProjectSettings.asset` debe quedarse en `Tlax2026-RC` para coincidir con los exes que ya corren en los kioskos. Si cambia (ej. alguien lo pone a `Tlax2026MVP` por error), el OTA entra en loop infinito porque `update.bat` extrae el ZIP nuevo pero relanza el exe viejo (lookup por nombre vía `tasklist`). Ver `feedback_unity_build_checklist.md` en `~/.claude/projects/-Users-sim4r4-sim4r4-repos-simulador/memory/`.
