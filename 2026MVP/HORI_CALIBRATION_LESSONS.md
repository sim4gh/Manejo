# HORI Truck Calibration — Lecciones aprendidas (sesión 2026-05-08)

Esfuerzo de 4 horas, 4 builds (v1.6.1 → v1.6.4), 2 kioskos (Pasajeros 1 + 2),
3 enfoques (whitelist, migración, hardcode constants). El bug de "carro no
avanza ni en Auto ni Manual" tuvo varias capas. Documentando todo aquí para
que la siguiente sesión no repita los mismos errores.

## Síntoma original

Operador: *"Pasajeros 2 no funciona el automático. No acelera ni reversa."*
HUD: 0 km/h con gear=1 visible (controles llegan al motor pero el carro no
se mueve). Confirmado en Auto y Manual.

## Causa raíz final (después de 3 hipótesis fallidas)

`UIInputNew.NormalizePedal(raw, rest, press)` aplicado al freno con valores
corruptos en PlayerPrefs:

```csharp
private float NormalizePedal(float raw, float rest, float press) {
    float span = press - rest;
    if (Mathf.Abs(span) < 0.05f) return 0f;        // guard de divide-por-cero
    return Mathf.Clamp01((raw - rest) / span);
}
```

Pasajeros 2 tenía persistido en `HKCU\Software\Tlaxcala\Tlax2026-RC`:
- `G923_BrakeAxis = "rz"` ✓ (axis correcto del HORI)
- `G923_BrakeRest = 1.0` ❌ (HORI sano sería -1.0)
- `G923_BrakePress = 0.0` ❌ (HORI sano sería +1.0)

Con esos valores: `NormalizePedal(raw=-1, 1.0, 0.0) = (-1-1)/(0-1) = 2 → clamp01 = 1.0`.
**El freno reportaba 100% pisado siempre, sin importar la posición física del pedal.**
En Auto: brake=1.0 cancela throttle → carro inmóvil. En Manual: igual + sin clutch
threshold.

## Por qué los rest/press se corrompieron

HORI Truck pedales son hardware-fijos (HID LE16 0x0000..0xFFFF → Unity normalized
-1..+1) per `CLAUDE.md`. El valor correcto siempre es `rest=-1, press=+1`. Pero
`MenuScreenManager.cs` Phase 4 (brake Discovery) los CAPTURA dinámicamente cuando
el operador pisa el freno:

```csharp
// MenuScreenManager.cs:2489-2499 (Phase 4 brake)
float rest = pedalCandidateRests[bestIdx];                   // valor en SnapshotPedalRests
float press = rest + pedalCandidateMaxDeltas[bestIdx];       // delta máximo durante press window
PlayerPrefs.SetFloat("G923_BrakeRest", rest);
PlayerPrefs.SetFloat("G923_BrakePress", press);
```

**Problema fundamental:** si el operador tiene un pie apoyado en el pedal durante
`SnapshotPedalRests` ("suelta los pedales"), el snapshot captura `rest = +1.0`
(pedal pisado, no la posición física de reposo). Cuando suelta, el delta es
negativo, queda `press = 0.0`. Resultado: calibración guardada con polaridad
invertida y rango parcial.

## Las 3 hipótesis que NO resolvieron

### Hipótesis 1 (v1.6.2): "Discovery elige axis fantasma"

**Plan:** whitelist HORI a `rz/slider/slider1` en `CachePedalCandidates` para que
ruido HID en axes no-pedal (`z`, `stick/y`, etc.) no gane el max-delta.

**Por qué falló:** el axis SÍ era correcto (`rz`). El bug estaba en los valores
rest/press, no en el axis path. Whitelist no toca rest/press.

### Hipótesis 2 (v1.6.3): "Calibración corrupta persiste, hay que invalidar"

**Plan:** detectar en `PrepareWheelScreen` si `|press - rest| < 1.5` (HORI sano
es ≈2.0) y borrar las prefs para forzar re-Discovery con whitelist v1.6.2.

**Por qué falló:** la migración SÍ disparaba (`Discovery limitada a rz,slider,slider1`
aparecía en log post-OTA), borraba las prefs, Pantalla 2 entraba en Discovery,
operador pisaba… y Discovery capturaba **otra vez** con el mismo bug (foot
position issue). Ciclo infinito de invalidación → captura mala → invalidación.

### Hipótesis 3 (write directo al registry vía SSH)

**Plan:** matar el juego, `reg add /v G923_BrakeRest /t REG_DWORD /d 0xBF800000`
para escribir -1.0/+1.0 directo, ignorar Discovery por completo.

**Por qué falló:** al boot, Unity carga las prefs (-1/+1) ✓, pero el sanity
check de v1.5.12 (líneas 3380-3424) compara el reading actual del axis vs el
rest guardado. Si delta > 0.5 tras 1 retry, **DELETE de las prefs** (lines
3411-3414) y re-Pantalla 2 → Discovery → captura mala. Mismo ciclo.

## Solución final (v1.6.4)

**HORI pedales son hardware-fijos. No leer ni escribir PlayerPrefs `*Rest/*Press`
para HORI; usar constantes en código.**

### Cambio 1 — `UIInputNew.AttachToWheelDevice` líneas 1448-1471

```csharp
if (IsHORITruck(device)) {
    // HORI Truck pedales: hardware-fijos a rest=-1, press=+1.
    // No leer PlayerPrefs G923_*Rest/Press (puede tener basura de Discovery
    // mal capturado). Throttle bypassa via HoriThrottleReader (HID byte 21-22).
    _gasRest = 0f; _gasPress = 1f;
    _brakeRest = -1f; _brakePress = 1f;
    _clutchRest = -1f; _clutchPress = 1f;
} else {
    // G923 / otros: cargar de PlayerPrefs (variantes PS/Xbox tienen polaridades
    // distintas y SÍ deben respetar Discovery).
    _gasRest    = PlayerPrefs.GetFloat("G923_GasRest",  1f);
    _gasPress   = PlayerPrefs.GetFloat("G923_GasPress", -1f);
    _brakeRest  = PlayerPrefs.GetFloat("G923_BrakeRest",  1f);
    _brakePress = PlayerPrefs.GetFloat("G923_BrakePress", -1f);
    _clutchRest  = PlayerPrefs.GetFloat(PREF_G923_CLUTCH_REST,  -1f);
    _clutchPress = PlayerPrefs.GetFloat(PREF_G923_CLUTCH_PRESS,  1f);
}
```

### Cambio 2 — `MenuScreenManager.SanityCheckThenLoad` línea ~3434

```csharp
bool isHori = dev != null && UIInputNew.IsHORITruck(dev);
float gasRest    = isHori ? 0f  : PlayerPrefs.GetFloat("G923_GasRest", 0f);
float brakeRest  = isHori ? -1f : PlayerPrefs.GetFloat("G923_BrakeRest", 0f);
float clutchRest = isHori ? -1f : PlayerPrefs.GetFloat(UIInputNew.PREF_G923_CLUTCH_REST, -1f);
```

Esto hace que el sanity check use los hardcoded constants para HORI, ignorando
basura del registry. Sin esto, el sanity check seguía fallando y borrando prefs.

### Cambio 3 — eliminada migración v1.6.3

Ya no hace falta detectar prefs corruptas y borrarlas: UIInputNew las ignora
para HORI. Menos código, menos paths de error.

## Lecciones tácticas

### 1. Las PlayerPrefs `G923_*` son storage compartido para todos los wheels

El nombre legacy despista. Cuando hay HORI conectado, el código lee y escribe a
las mismas keys `G923_BrakeAxis`/`G923_BrakeRest`/`G923_BrakePress`. No hay keys
`HORI_*` separadas. Para evitar confusión, en código futuro: comentar
explícitamente que estas keys son wheel-agnostic.

### 2. Discovery no es robusto a posiciones iniciales del pedal

El `SnapshotPedalRests` asume que el operador realmente suelta los pedales en
los primeros 100ms del Phase 4. En la práctica:
- Operador con pie casual sobre el pedal → captura mal el rest
- Pedal con resorte débil → no vuelve a -1 fast enough → captura intermedio
- Reconnect del USB durante el snapshot → control stale → captura garbage

Para hardware con pedales conocidos hardware-fijos (HORI), no hay razón para
"capturar" rest/press. Hardcodearlos. Discovery solo debería identificar el
**axis path** (qué canal HID es brake vs clutch).

### 3. Sanity check destructivo + Discovery frágil = loop infinito

Cuando una calibración mala dispara sanity check, sanity borra las prefs,
Pantalla 2 vuelve a Discovery, operador captura mal otra vez, sanity falla
otra vez al siguiente boot, ciclo infinito. La solución más limpia es
**no permitir que sanity check toque hardware-fixed values** (bypass
para HORI), como hicimos en v1.6.4.

### 4. Reg-write directo no resuelve nada si el código sobrescribe en boot

Escribí `reg add /v G923_BrakeRest /d 0xBF800000` directamente al registry de
Pasajeros 2. El verify confirmó los valores. Pero al siguiente boot del juego,
el sanity check los detectó "anómalos" (vs el reading actual) y los borró.
El registry volvió a basura. **Lección:** cualquier fix manual en el registry
es transitorio si hay código de Unity que valida y borra esos valores.

### 5. Tailscale device names ≠ portal admin device names

El user me dio la IP `100.66.150.50` con un chat sobre "Pasajeros 2". Esa IP
en Tailscale es `pasajeros1`. En el portal admin, "Pasajeros 1" es PC del
"Simulador Urbano 1" (SIM-005), y "Pasajeros 2" es PC del "Simulador Urbano 2"
(SIM-006). **Verificar siempre** el `pcId` (40 hex) directamente en el portal
antes de deployar a un kiosko productivo.

### 6. Auto-update OTA no afecta kioskos powered-off

El endpoint `/admin/unity-builds/deploy` con `targetPcIds` ESCRIBE el
`pendingUpdate` en DynamoDB. El kiosko ejecuta el update en el siguiente
heartbeat (~3 min). Si está apagado, espera hasta que prenda. **Nunca asumir
que el deploy se aplicó hasta ver `appVersion` actualizado en el portal.**

### 7. PlayerPrefs en Windows: `_h<hash>` suffix

Las keys en registry tienen sufijo de hash CRC32: `G923_BrakeRest_h1582774858`.
El sufijo es estable por instalación pero específico del key string y la
versión de Unity. Nunca asumir el hash; hacer `reg query` y reusar el sufijo
existente.

REG_DWORD para floats: Unity escribe el float como `Int32` con el bit pattern.
- `0xBF800000` = float `-1.0`
- `0x3F800000` = float `+1.0`
- `0x00000000` = float `0.0`

### 8. HID polling con FILE_FLAG_OVERLAPPED requiere marshalling cuidadoso

Mi primer intento del script `hori-poll.ps1` con overlapped I/O recibía 0
reports en 30s. El issue: `[ref]$ovl` en PowerShell no marshalea bien. Cambié
a synchronous `ReadFile` (mismo patrón que `HoriThrottleReader.cs`) y funcionó.

### 9. ReadFile sincrónico bloquea hasta el próximo report HID

Si el dispositivo no manda reports (operator quieto), ReadFile bloquea
indefinidamente. Para test interactivo, decirle al operador que **mueva
algo continuamente** durante el polling. O cambiar a OVERLAPPED + WaitForSingleObject
con timeout.

### 10. SetupAPI vs registry para enumerar HID devices

Mi primer enfoque P/Invoke de SetupDi recibía `cbSize=8` para x64 pero el
DevicePath salía garbled (truncado a 1 char). El issue: alignment del struct.
**Mucho más fácil:** enumerar paths via registry en
`HKLM\SYSTEM\CurrentControlSet\Control\DeviceClasses\{4d1e55b2-f16f-11cf-88cb-001111000030}`.
Decode: subkey `##?#HID#VID_...` → path `\\?\HID#VID_...`.

### 11. Detección de HORI por strings es preexistente y es un punto débil

`UIInputNew.IsHORITruck(device)` retorna `displayName.Contains("HORI") ||
product.Contains("HORI")`. Si Windows enumera el HORI como genérico (rare pero
posible), `IsHORITruck` retorna `false` → cae en path G923 → defaults
incorrectos. El fix v1.6.4 hereda este riesgo. Mejora futura: detectar también
por VID/PID (`0x0F0D` / `0x017A`) directo.

### 12. Codex como reviewer crítico atrapó cosas

Antes del build de v1.6.2 y v1.6.4, le pasé los planes a `mcp__codex__codex` con
"sé crítico, dilo si hay agujeros". Detectó:
- v1.6.2 plan: "el modal 'Continuar con guardada' no rescata estado sano,
  preserva el malo" → drop esa idea, hicimos solo el whitelist.
- v1.6.4 plan: confirmó OK pero recordó el riesgo de detección por strings.

Vale el costo de la consulta para fixes que tocan paths de input.

### 13. SSH a kiosko productivo: leer es OK, escribir requiere autorización

El classifier bloquea operaciones destructivas / escrituras en producción.
Para `reg add`, `taskkill`, etc., necesité autorización explícita del user.
**Buen sistema** — previene accidentes. Para reads (`reg query`,
`Get-Content Player.log`, `tasklist`) pasa sin fricción.

### 14. `aws s3 cp` para uploads está PROHIBIDO desde el mac

Memory `feedback_no_aws_s3_for_uploads.md`: XFinity bloquea S3 PUT desde la red
del user. Para subir release notes md y artefactos, **siempre vía API de
Mexicalabs** (`/admin/unity-builds/part-proxy` → `complete-upload`). Recordatorio
útil — fallé varias veces hasta que volví a leer la memory.

### 15. Logs de Unity tienen 2 fuentes y rotación

- `Player.log` — Unity nativo. Se REINICIA cada launch (overwrite). Vive en
  `%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\Player.log`.
- `current.log` — `LogUploader.cs` (custom) buffer persistente. Cada 5 min sube
  a S3 (`s3://simtabasco-simulator-logs-{env}/logs/{pcId}/...`) y trunca el
  archivo local. Vive en `<persistentDataPath>/logs/current.log`.

Para diagnostic en vivo: `Player.log` (current session, frágil). Para histórico:
S3 vía portal admin.

## Pendientes / mejora long-term

- [ ] **Eliminar `_h<hash>` sufijo dependence**: si Unity bumps a una versión
  con CRC32 diferente, los hashes existentes se vuelven huérfanos. Actualmente
  no es problema pero sí frágil.
- [x] **Detección HORI por VID/PID en lugar de displayName** — resuelto en
  v1.6.5 (Fase B1). `IsHORITruck` usa `HIDDeviceDescriptor.FromJson` con
  match exacto VID=0x0F0D + PID=0x017A (wheel) o 0x0186 (shifter). Fallback
  a strings preserva v1.6.4.
- [x] **Discovery solo identifica axis para HORI** — resuelto en v1.6.5
  (Fase B2). Phase 4 (brake) y Phase 6 (clutch) checan `IsHORITruck(dev)`
  antes de escribir `G923_*Rest/Press`. Gates `pedalsCal` y `clutchPathPersisted`
  en `PrepareWheelScreen` hacen requirement axis-only para HORI. Sin ese
  ajuste de gates, fast-path se rompe (HORI re-Discovery cada boot).
- [x] **Sanity check más inteligente** — resuelto en v1.6.5 (Fase B3).
  `SanityCheckThenLoad` valida que axis raw HORI brake/clutch caiga en
  `[-1.05, +1.05]` durante 3 frames consecutivos. Fuera de rango → invalida
  SOLO el path para forzar Phase 4/6 dirigida (no wipe global). Window 3
  frames evita falsos positivos por jitter post-attach.
- [x] **Test unitario de `NormalizePedal`** — resuelto en v1.6.5 (Fase B4).
  Función pura extraída a `Assets/Custom/InputMath/InputMath.cs` con asmdef
  propio (`TlaxSim.Input`). UIInputNew delega via static `Func<>`, default
  inline preserva runtime si la inyección no corre. 10 tests EditMode
  cubren divide-by-zero, span boundary, polaridad invertida, rangos
  out-of-calibración, y el regression test del bug v1.6.3.

## v1.6.5 — Fase B5: verificación de throttle en Pantalla 2

Hueco real detectado tras v1.6.4: si `HoriThrottleReader` falla al inicializar
(USB hot-plug, driver conflict, otra app reservó el handle), v1.5.12 auto-pasaba
la Phase 3 de Pantalla 2 sin verificación → operador llegaba a la escena con
throttle muerto **silentemente**, sin manera de detectarlo en la calibración.

### Cambio

`MenuScreenManager.cs` Phase 3, branch HORI:

```csharp
if (devForGas != null && UIInputNew.IsHORITruck(devForGas))
{
    if (_horiPhase3StartTime < 0f) _horiPhase3StartTime = Time.unscaledTime;
    float horiPhase3Elapsed = Time.unscaledTime - _horiPhase3StartTime;

    var thrReader = HoriThrottleReader.Instance;
    float thrValue = thrReader != null ? thrReader.Value : 0f;
    bool thrHandleOpen = thrReader != null && thrReader.IsHandleOpen;

    // Progress bar en vivo basada en valor del reader (no candidates discovery).
    // Progresa visualmente igual que G923 — operador ve que algo está pasando.
    float gasProgress = Mathf.Clamp01(thrValue);
    if (Mathf.Abs(gasProgress - gasFillRT.anchorMax.x) > 0.005f) {
        gasFillRT.anchorMax = new Vector2(gasProgress, 1);
        gasFill.color = Color.Lerp(MenuTheme.SecondaryCrimson, MenuTheme.IndicatorDone, gasProgress);
    }

    const float HORI_THROTTLE_VERIFY_THRESHOLD = 0.7f;
    const float HORI_READER_GRACE_SECONDS = 8f;

    if (thrHandleOpen && thrValue >= HORI_THROTTLE_VERIFY_THRESHOLD)
    {
        // Pasa: handle abierto + valor superó threshold → la cadena P/Invoke
        // (CreateFile → ReadFile → byte 21-22 LE16) funciona end-to-end.
        throttleDone = true;
        // ... (escribe sentinel + rest=0 + press=1 a PlayerPrefs)
    }

    if (!thrHandleOpen && horiPhase3Elapsed > HORI_READER_GRACE_SECONDS) {
        wheelPrompt.text = "HoriThrottleReader sin handle - verifica USB del HORI Truck";
        // No bloqueamos — el reader sigue intentando reabrir cada 2s
        // (HoriThrottleReader.Update retry loop), conectar USB recupera.
    }
    return;
}
```

### Soporte: `HoriThrottleReader.IsHandleOpen`

Nuevo getter público en `HoriThrottleReader.cs`:

```csharp
public bool IsHandleOpen => _running && _hidHandle != null && !_hidHandle.IsInvalid;
```

Permite distinguir "reader vivo y leyendo" vs "reader muerto, retry loop
intentando". El F7 LogConsolePanel también imprime `handle=open|CLOSED` en la
sección "CUSTOM HID READERS" (Fase A) para diagnóstico paralelo sin entrar a
Pantalla 2.

### Comportamiento esperado

| Estado | Pantalla 2 |
|---|---|
| HORI conectado, reader handle abierto, no se pisa pedal | "Pisa el ACELERADOR a fondo", barra al 0% |
| HORI conectado, reader handle abierto, operador pisa | Barra crece hasta 100%, marca Phase 3 done al ≥0.7 |
| HORI conectado pero reader nunca abrió (USB unplug) | Tras 8s: prompt cambia a "HoriThrottleReader sin handle - verifica USB del HORI Truck". Sigue chequeando — al reconectar, retry loop del reader recupera y la fase pasa al pisar |
| Cambio de HORI a G923 a mid-flow | Reset del timer (`_horiPhase3StartTime=-1f`), discovery normal G923 corre |

### Por qué 0.7 (no 0.85)

`PEDAL_DELTA_THRESHOLD` para discovery via `SamplePedalCandidates` es 0.85 (delta
máximo durante una ventana). Para verificación con reader directo (sin captura
de baseline), usamos un umbral más bajo (0.7) porque:

- El reader retorna 0..1 normalizado, no un delta vs baseline
- 0.7 ≈ 70% pisado: lo suficientemente alto para no disparar con jitter,
  lo suficientemente bajo para que el operador no tenga que poner el pedal
  a fondo total

### Por qué grace 8s (no 2s o 30s)

`HoriThrottleReader.Update` reintenta abrir el handle cada 2s post-bootstrap
(USB hot-plug). 8s = 4 intentos: cubre casos comunes (driver loading lento,
USB enumerando) sin ser tan corto que falle por jitter de inicialización.

Codex review v3 sugirió 6-8s; elegí 8s por margen.

## Lecciones operativas adicionales (v1.6.5)

### 16. Auto-pass silencioso esconde fallos de inicialización

El auto-pass de v1.5.12 era una decisión defensiva ("si el operador no puede
verificar el throttle en Unity, no le pidamos hacerlo"), pero el efecto neto
era ocultar fallos del reader. La verificación con `IsHandleOpen + Value > 0.7`
saca esos fallos a la luz **antes** de la escena, donde el operador puede
actuar (verificar USB, F8 recalibrar).

**Regla general:** validar antes en lugar de degradar silenciosamente. Un
operador frustrado en Pantalla 2 es mejor que un operador frustrado en gameplay.

### 17. F7 + Pantalla 2 son canales paralelos de diagnóstico

F7 (Fase A) muestra `HoriThrottleReader v=X.XXX handle=open|CLOSED` durante
TODO el ciclo de vida del juego — útil después de la calibración, en gameplay
o post-mortem. Pantalla 2 (Fase B5) verifica EN VIVO antes de avanzar a la
escena. Los dos paths se complementan: si F7 muestra `handle=CLOSED` después
de gameplay, hubo un disconnect mid-examen.

## Archivos tocados en v1.6.4

- `2026MVP/Assets/Gley/UrbanAssets/Runtime/Scripts/UI/UIInputNew.cs:1448-1471`
- `2026MVP/Assets/Custom/Menu/MenuScreenManager.cs:3434-3442` (sanity check)
- `2026MVP/Assets/Custom/Menu/MenuScreenManager.cs:2107-2110` (migración v1.6.3 removida)
- `2026MVP/ProjectSettings/ProjectSettings.asset` (bundleVersion)

## Scripts útiles que quedaron en `/tmp` (Mac) y `Desktop` (Pasajeros 2)

- `hori-poll.ps1` — abre HORI HID col01 directo, imprime byte changes. Útil
  para mapear bytes ↔ pedales/botones sin dependencia de Unity.
- `run-pedales.bat`, `run-cajavel.bat`, `run-utilidades.bat` — wrappers con
  instrucciones para el operador. Cada uno corre el .ps1 con args distintos
  y guarda output a archivo separado.
- `calibrate-hori-pedals.ps1` — helper de read/write de PlayerPrefs G923_*
  con phases (read latest axes, kill game, write float, list prefs). No
  necesario tras v1.6.4 pero útil para diagnóstico futuro.
