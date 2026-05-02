# Weather System

Sistema de clima aleatorio (Sol / Lluvia / Granizo) en escenas de manejo. Mergeado a `main` el 2026-04-30 (PR #114, branch `feature/weather-system` preservada como referencia).

## Por qué

El sistema previo (`CargoLluviaLoader` + PlayerPref `Cargolluvia`) estaba roto — el campo `objetoLluvia` estaba sin asignar (`fileID: 0`) en las 6 escenas, así que el `SetActive` jamás funcionó. Reescritura completa para ofrecer Sol/Lluvia/Granizo con audio y override por demo code.

## Arquitectura

### Bootstrap

`Assets/Custom/WeatherManager.cs` es un singleton bootstrapped con `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]`. Crea el GO `[WeatherManager]` con `DontDestroyOnLoad`.

**CRÍTICO — NO usar `BeforeSceneLoad`**: en pruebas el GO no sobrevivía la transición MainMenu → escena de manejo (`DontDestroyOnLoad` es inestable cuando se llama antes de tener escena activa). El log "Skip MainMenu" aparecía pero `OnSceneLoaded` para Sedan no disparaba. `SpawnLocationManager` usa `AfterSceneLoad` y funciona consistente — usar el mismo patrón.

Si la escena inicial es directamente Sedan (caso "play desde Sedan en Editor"), `Bootstrap` llama `Instance.ApplyClima()` manualmente porque `sceneLoaded` no dispara para escenas ya cargadas en el momento del `AfterSceneLoad`.

### PlayerPrefs

| Key | Tipo | Origen | Notas |
|---|---|---|---|
| `Clima` | int 0/1/2 | `MenuScreenManager.PickAndSetWeather` | Fuente de verdad (Sol/Lluvia/Granizo). |
| `Cargolluvia` | int 0/1 | mirror legacy | `1` solo cuando Clima=1. Lo leen `LogConsolePanel.cs:260` y `LogUploader.cs:513`. **TODO** migrar consumidores a `Clima` y borrar el mirror. |

### Aplicación del clima

`SceneManager.sceneLoaded` dispara `ApplyClima` que busca con `Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None)` los GO con `name == "LLuvia"` o `"Granizo"` (algunas escenas tienen 2 instancias: BusPasajeros, CamionDCarga, Ambulancia).

| Clima | LLuvia | Granizo | Audio |
|---|---|---|---|
| 0 Sol | OFF | OFF | (ninguno) |
| 1 Lluvia | ON (rate default) | OFF | RainLoop, fade-in 300ms |
| 2 Granizo | ON (rate × 0.3, "lluvia ligera") | ON (size × 4, `size3D=false`) | RainLoop, fade-in 300ms |

### Sorteo

`MenuScreenManager.PickAndSetWeather(int overrideClima)` se llama en los 3 paths de verificación de sesión:
- Demo code con override de clima → `overrideClima ∈ {0,1,2}`.
- QR poll verified → `overrideClima = -1`.
- LookupByCode → `overrideClima = -1`.

Cuando `overrideClima == -1`, sortea ponderado:
```
WEATHER_WEIGHT_SOL    = 0.60f
WEATHER_WEIGHT_RAIN   = 0.40f
WEATHER_WEIGHT_HAIL   = 0.00f   // pendiente validar visualmente antes de subirlo
```

### Demo codes con override de clima

Formato `TTTTXY` (6 dígitos):
- `TTTT` = 4 dígitos repetidos del tipo: `0000` particular, `1111` pasajeros, `2222` moto, `3333` carga, `4444` ambulancia.
- `X∈{0,1,2}` = override de clima (0=sol, 1=lluvia, 2=granizo). Otro valor → sortea random ponderado.
- `Y∈{0..5}` = ubicación (0=random, 1..5=waypoint fijo).

Ejemplos:
- `000000` → particular sol, ubicación random.
- `111121` → pasajeros granizo, ubicación 1.
- `222295` → moto random ponderado, ubicación 5.

## Gotchas (NO repetir)

### `AfterSceneLoad` vs `BeforeSceneLoad`

Tarde 4-5 commits debugeando porque inicialmente usé `BeforeSceneLoad` y el singleton aparentaba funcionar (loggeaba "Skip MainMenu") pero al cargar Sedan `OnSceneLoaded` no disparaba. Ver commit `6d7dcb46` por el fix definitivo.

### `startSize3D=true` con multiplier escalar

El ParticleSystem `Granizo` tiene `startSize3D=1` en YAML. Con eso, `main.startSize` solo escala el eje X, no Y/Z. Resultado al multiplicar por 4: bolas como **discos aplastados** (X=20, Y=Z=5).

**Fix**: `main.startSize3D = false` antes de asignar `startSize`. Eso fuerza modo escalar uniforme y la bola crece igual en los 3 ejes. Ver commit `c63f7672`.

### `rateOverTimeMultiplier` no aplica en runtime

En pruebas, modificar `emission.rateOverTimeMultiplier = 0.3f` (escalar) NO bajaba la densidad de la lluvia. Lo que SÍ funcionó fue reasignar `emission.rateOverTime = new MinMaxCurve(baseRate * 0.3)` directamente. Mismo patrón que con `startSize`. La razón exacta no se verificó — posiblemente Unity reinicializa el módulo `emission` con valores serializados al `SetActive(true)`.

**Caveat**: en pruebas iniciales la lluvia ligera del modo granizo igual no era visible. El granizo solo es aceptable como estado actual; investigar en próximo iter si se quiere mejorar.

### `SetActive` antes de modificar

Aplicar `SetActive(true)` ANTES de tocar `main` o `emission`. Inverso (multipliers primero, después SetActive) → al activar el GO, Unity reinicia el módulo desde el estado serializado y nuestros cambios se pierden.

### `ps.Clear()` rompe la emisión recién activada

Llamar `Clear()` justo después de `SetActive(true)` interrumpe la emisión iniciada por `playOnAwake` y deja el PS sin partículas. Si se quiere defensa contra partículas residuales, hacer `Stop()` + `Play()` en su lugar (o simplemente no llamar `Clear` y aceptar que las primeras gotas pueden estar pre-existentes).

## Audio

`Assets/Resources/Custom/Weather/RainLoop.wav` — 33 MB, 48 kHz stereo, ~3 minutos, looping. Compartido entre clima Lluvia y Granizo. Cargado con `Resources.Load<AudioClip>("Custom/Weather/RainLoop")` al arrancar el efecto. AudioSource hijo del `[WeatherManager]` (DontDestroyOnLoad) con `spatialBlend=0` (2D) y fade-in 300ms vía coroutine. `CleanupAudio` se llama al cargar nueva escena.

`HailLoop.wav` (clip distinto para granizo) se descartó porque el usuario reportó que no sonaba bien — actualmente el clip de Lluvia se usa para ambos.

## Archivos

| Path | Rol |
|---|---|
| `Assets/Custom/WeatherManager.cs` | Singleton orchestrator |
| `Assets/Custom/Menu/MenuScreenManager.cs` | Sorteo `PickAndSetWeather`, parser demo codes `TTTTXY` |
| `Assets/Resources/Custom/Weather/RainLoop.wav` | Audio clip (lluvia y granizo) |
| `Assets/Custom/Weather/` | Folder reservado (sin archivos tras refactor; tiene `.meta` con GUID válido — no borrar) |
| `Assets/pruebas general/CargoLluviaLoader.cs` | Sistema anterior, marcado `[Obsolete]`, no-op transicional. **TODO** borrar tras limpiar componentes huérfanos en las 6 escenas. |

## Pendiente para próximo iter

- **Subir granizo al sorteo random**: cambiar pesos en `MenuScreenManager.PickAndSetWeather` (60/40/0 → 40/30/30) tras validar visualmente que se ve bien en las 6 escenas con `TTTT2Y`.
- **Validar/mejorar lluvia ligera del granizo**: actualmente el código activa el GO LLuvia con rate × 0.3 pero en pruebas no era visible. Posible que en escenas con 2 instancias (BusPasajeros, CamionDCarga, Ambulancia) sí aparezca. Investigar.
- **Limpiar `CargoLluviaLoader`**: abrir las 6 escenas en Unity Editor, eliminar el componente huérfano, después borrar el `.cs`.
- **Migrar logging a `Clima`**: actualizar `LogConsolePanel.cs:260` y `LogUploader.cs:513` para leer `Clima` directo, después borrar el mirror del PlayerPref `Cargolluvia` en `MenuScreenManager.PickAndSetWeather`.
