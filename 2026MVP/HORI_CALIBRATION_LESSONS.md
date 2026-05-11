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

## Estado actual tras v1.6.5

| Pieza | Estado |
|---|---|
| HORI throttle visible en F7 | ✅ Fase A — sección "CUSTOM HID READERS" |
| HORI throttle visible en gameplay | ✅ HoriThrottleReader P/Invoke (v1.5.x+) |
| HORI throttle verificado antes de gameplay | ✅ Fase B5 — Phase 3 valida `IsHandleOpen + Value≥0.7` |
| HORI pedales hardware-fijos (rest/press) | ✅ v1.6.4 hardcodea, v1.6.5 deja de escribir basura del Discovery |
| HORI brake/clutch path detection | ✅ Phase 4/6 Discovery + sanity hardware-aware (Fase B3) |
| G923 sin cambios | ✅ todas las modificaciones gateadas por `IsHORITruck` |
| Moto sin cambios | ✅ AttachAsMotoSimulator branch intacta |
| Calibración persistente entre sesiones | ⚠️ **persiste, pero se re-valida cada arranque** en Pantalla 2 |
| Recalibración silenciosa por sanity check destructivo | ✅ v1.5.12+ fix; v1.6.5 hardware-aware no toca rest/press HORI |

## v1.7.0 — la cosa más importante que viene (calibración immutable + Pantalla 2 muerta)

**Diagnóstico del usuario (2026-05-11)**: *"la pantalla donde se visualizan los
sliders no es realmente necesaria. O se reconocen todos los controles para lo
que se escogió y se carga la escena, o en caso de que algo no se reconozca,
se debe de poner 'necesita calibración'."*

Hoy Pantalla 2 hace DOS cosas mezcladas:
1. **Calibración**: descubre paths de axes y los persiste en PlayerPrefs.
2. **Verificación**: chequea que los controles respondan antes de cargar
   la escena (Fase B5 v1.6.5 añadió esto para HORI throttle).

Cada arranque corre ambas, mostrando los sliders incluso cuando todo ya
está calibrado. Esto:
- Confunde al operador con UI innecesaria
- Re-corre Discovery cuando algo está "calibrado pero no perfecto" (ej. fingerprint match parcial)
- Permite que un splash destructivo wipe-ee la calibración válida (caso v1.5.10 sanity check)

### Visión v1.7.0

**Separar definitivamente** calibración de verificación:

1. **Calibración** se hace UNA SOLA VEZ por kiosko, en una **ventana dedicada
   F8/F9** (sostén 1.5s). Operador. No usuario. Escribe a un archivo immutable:
   ```
   <persistentDataPath>/control_mapping.json
   ```
   con el mapping completo (axes paths + canónicos + bindings + tuning).
   Read-only en runtime; nadie escribe excepto F8 explícito.

2. **Verificación al arranque** (donde hoy vive Pantalla 2):
   - Lee `control_mapping.json` y resuelve cada control en el device actual.
   - Si TODOS responden a su prompt esperado → carga escena. **Sin UI de sliders.**
   - Si ALGUNO no responde → modal *"Control X no responde, F8 sostén 1.5s para recalibrar (operador)."*
   - Verificación semántica por control (codex review v3): pedir "Pisa el FRENO" y validar SOLO que la lectura del brake mapping suba >0.7, no que "algo se movió" genérico.

3. **Migración v1.6.x → v1.7.0** atómica (codex review v3):
   - Boot detecta JSON ausente + `Cal_DeviceFingerprint` en PlayerPrefs → migra a JSON.
   - Detección canon-aware: para HORI, si `BrakeAxis="z"` (canon es `rz`) → flag `unverified=["brake"]`. No esperar al splash a descubrirlo.
   - Write atómico: `.tmp` → fsync → rename + flag `Mig_v17_Done=1`.

4. **Profiles como spec providers** (codex review v3): los profiles `G923PsProfile`, `G923XboxProfile`, `HoriTruckProfile`, `MotoProfile` aíslan canonicals/detección. **NO ejecutan el runtime** — `UIInputNew` sigue siendo host (no refactor de runtime mientras G923/Moto funcionen perfectamente).

5. **Path degradado sin F8 obligatorio post-OTA**: para G923/Moto/HORI-Auto se genera JSON con defaults canónicos sin marcar nada → no exige F8 al primer arranque post-v1.7.0. Solo HORI-Manual sin clutch viable exige F8.

### Por qué v1.7.0 mata la clase completa de bug

| Bug pasado | Por qué no puede volver en v1.7.0 |
|---|---|
| `BrakeAxis="z"` heredado de G923 Xbox sembrado en HORI | Profile fija canónicos al instalar JSON; nadie sobrescribe en runtime |
| `BrakeRest=1, BrakePress=0` capturados con pedal pisado | Calibración explícita F8 (no automática silenciosa), prompts claros + verificación inmediata |
| Sanity check destructivo wipea calibración válida | Splash solo VERIFICA — no escribe a JSON jamás |
| Auto-pass HORI con throttle reader muerto | Verificación semántica por control (B5 ya prefigura esto, v1.7.0 lo generaliza) |
| Discovery captura el "pedal equivocado" cuando el operador pisa otro | UI deja de pedir Discovery; el JSON tiene los paths canónicos |

### Pasos concretos para v1.7.0 (próximo sprint, 2-3 días)

- `Assets/Custom/Input/ControlMapping.cs` — clase de datos JSON + load/save atómico
- `Assets/Custom/Input/Profiles/{IDeviceProfile, G923Ps, G923Xbox, HoriTruck, Moto, Unknown}.cs` — spec providers
- `Assets/Custom/CalibrationPanel.cs` — reemplaza/extiende F8 con flujo paso-a-paso para escribir JSON
- `MenuScreenManager.cs` — Pantalla 2 reescrita a "verify-only" o eliminada (cargar escena directo si JSON válido)
- `UIInputNew.cs` — leer de `ControlMapping.Active` en lugar de PlayerPrefs (~68 reads identificados)
- Migración: detectar PlayerPrefs existente → migrar → marcar `unverified`/`needsRecalibrate` con canon-aware
- Tests EditMode para ControlMapping (load/save round-trip, migración, schema validation)
- Regression tests en sedan + motocicleta antes de merge (verificar input idéntico bit-a-bit)

Ver plan completo en `/Users/sim4r4/.claude/plans/necesito-de-todo-tu-woolly-lightning.md`.

## v1.6.6 — Fase B6: bug 3ª gear PULSE intermitente

**Reportado por Aramis 2026-05-11**: tras OTA exitoso a v1.6.5, el operador en
HORI metía 1ra → bien, 2da → bien, 3ra → a veces funciona, a veces se queda
"clavado a 20-30 km/h" sin avanzar.

### Causa raíz

`UIInputNew.cs:336` declaraba `HoriShifterStateProvider` para futura integración
runtime (v1.6.0 plan), con comentario literal en el código:

> "Asignado por HoriShifterReader.Bootstrap. UIInputNew NO lo consume todavía"

Por tanto la lógica de gear en `Update()` (línea 1995-2003) seguía iterando
sobre `_gearControls[i].IsPressed(...)` via Unity InputSystem. En HPC-044U:
- `shifter:trigger` (1ra) — HOLD (persistente mientras lever en gear)
- `shifter:button2` (2da) — HOLD aparentemente
- `shifter:button3..6` (3ra-6ta) — **PULSE** 1-2 frames al cruzar la posición
- `shifter:button7` (R) — PULSE (ya conocido v1.5.8+)

Para 3ra: Unity ve `button3` presionado 1-2 frames → `desiredGear=3` → en el
siguiente frame `IsPressed(button3)=false` → for-loop encuentra ningún
gear pressed → `desiredGear=0=Neutral` → vehículo en Neutral pierde tracción
del motor → coasts a la velocidad que tenía (~20-30 km/h después de 2da).

**Intermitencia explicada**: si el operador presiona button3 y otro frame
Unity tiene encolado el event → puede tomarse hasta 2-3 frames detectarlo
→ a veces el polling de gear lo agarra "dentro" del pulse, a veces "después".

### Fix v1.6.6

Conectar `HoriShifterStateProvider` al cálculo de `desiredGear`. El reader
ya lee `byte[1] bits 0-5+6` directo del HID con calibración HPC-044U
hardcoded por default (ver `HoriShifterReader.ApplyHPC044UDefaults`), retorna
`-1=R, 0=N, 1-6=gear, int.MinValue=not connected`.

`UIInputNew.cs` (Update Manual block):

```csharp
bool isHoriShift = _wheelDevice != null && IsHORITruck(_wheelDevice);
int readerGear = (isHoriShift && HoriShifterStateProvider != null)
    ? HoriShifterStateProvider() : int.MinValue;
if (isHoriShift && readerGear != int.MinValue)
{
    desiredGear = readerGear;  // reader vivo: directo
}
else if (_gearControls != null)
{
    // fallback pulse-based (G923, o HORI con reader dead)
    for (int i = 0; i < _gearControls.Length; i++) { ... }
}
```

### Defensivo

- Si `HoriShifterReader` no inicializó (USB hot-plug, handle CLOSED,
  uncalibrated), `HoriShifterStateProvider()` retorna `int.MinValue`
  → cae al pulse-based legacy. Comportamiento igual a v1.6.5.
- El sticky latch v1.5.9 de reverse (`_manualReverseLatched`) queda dormant
  cuando reader está vivo (gearActive=true cancela el latch al detectar
  gear válido). NO se removió por seguridad — sigue siendo fallback si el
  reader muere mid-gameplay.
- G923 y Moto NO cambian — `isHoriShift` gatea todo el cambio.

### Validación

Aramis tiene HoriShifterReader vivo en v1.6.5 (logs confirmaron
`Started poller on \\?\hid#vid_0f0d&pid_0186&col01...`). En v1.6.6
después del deploy, meter 3ra debe quedar engranada **persistente**.
Confirmar con F7 sostén 1.5s — `HoriShifterReader gear=3 conn=True`
mientras la palanca esté en posición.

## v1.6.7 — Fase B7: Neutral debounce HORI (bug "atorado a 20 km/h")

**Reportado por Aramis 2026-05-11 post-deploy v1.6.6**: operador HORI en
Manual va 40 km/h en 2da, mueve lever a 3ra, velocidad baja a 20 km/h
y se atora. v1.6.6 (Fase B6) acababa de conectar HoriShifterStateProvider
al cálculo de desiredGear.

### Causa raíz

`HoriShifterReader` reporta `byte[1]=0` brevemente durante el cruce
mecánico del lever entre gates (~50-200ms). Sin debounce:

1. Op en 2da, `_currentGear=2`, `desiredGear=2`. Estable.
2. Op mueve lever sin clutch. Reader transitoriamente reporta 0.
3. `desiredGear=0`. **Línea 2142**: `toNeutral=true` → condición
   `clutchPressed || toNeutral || sameGear` se cumple → `_currentGear=0`.
4. Lever termina en 3ra. Reader reporta 3. `desiredGear=3`.
5. `clutchInput=0`, ni `toNeutral` ni `sameGear` → BLOQUEADO forever.
6. `_currentGear=0` → `PlayerCar` aplica `motorTorque=0` → coast a 20 km/h.

Antes de v1.6.6 (pulse-based gear detection), el bug existía latente:
`_currentGear` se quedaba en última marcha conocida (2) y el vehículo
bajaba un poco por rev-limit, pero NO caía a 0. El reader continuo del
v1.6.6 expuso la trampa.

### Fix v1.6.7

**Suprimir transitorios Neutral del reader durante cruces de gate.**
Solo aplicar Neutral si el lever sostiene la posición por
>NEUTRAL_HOLD_SECONDS (300ms).

`Assets/Gley/UrbanAssets/Runtime/Scripts/UI/UIInputNew.cs` después del
asignamiento `desiredGear = readerGear`:

```csharp
const float NEUTRAL_HOLD_SECONDS = 0.30f;
if (isHoriShift && desiredGear == 0 && _currentGear != 0)
{
    if (_horiNeutralPendingSince < 0f)
        _horiNeutralPendingSince = Time.unscaledTime;
    if (Time.unscaledTime - _horiNeutralPendingSince < NEUTRAL_HOLD_SECONDS)
    {
        // Transitorio: enmascarar como sameGear → no-op en apply logic
        desiredGear = _currentGear;
    }
    // elapsed >= threshold → dejar desiredGear=0 (Neutral intencional)
}
else
{
    _horiNeutralPendingSince = -1f;
}
```

Reset defensivo en `AttachToWheelDevice` junto a `_manualReverseLatched=false`.

### Por qué preserva la pedagogía

Cuando el operador mueve lever sin clutch:
- v1.6.7: debounce mantiene `_currentGear=2` durante el cruce; al llegar
  a 3ra sin clutch → BLOQUEADO con rechino (`_pendingGearShiftWithoutClutchCount++`
  línea 2176-2186). Vehículo sigue en 2da, rev-limit pero NO se atora.
- ViolationDetector consume el contador: -5 pts + audio rechino.
- Op escucha rechino, entiende: "necesito clutch". Lección preservada.

Cuando el operador mueve lever CON clutch:
- Reader reporta 0 transitorio brevemente (debounce activo).
- `clutchInput>0.65` → `clutchPressed=true`.
- Al llegar a 3ra: `desiredGear=3`, condición se cumple por `clutchPressed`
  → `_currentGear=3`. Cambio limpio.

Cuando el operador deliberadamente quiere Neutral:
- Mueve lever a posición Neutral del gate y lo deja.
- Tras 300ms, debounce expira → `desiredGear=0` propaga → `toNeutral=true`
  → `_currentGear=0`. Comportamiento correcto.

### Edge cases verificados

- 2→3 con clutch: ✓ (transitorio enmascarado, cambio limpio al llegar)
- 2→3 sin clutch: ✓ (rechino, vehículo en 2da, no atorado)
- 2→N intencional (>300ms): ✓ (debounce expira, Neutral aplica)
- N→1 con clutch: ✓ (`_currentGear==0` → condición debounce no aplica)
- Hot-plug shifter: ✓ (AttachToWheelDevice resetea el field)
- HORI → G923 switch: ✓ (`isHoriShift=false` → toda la rama no aplica)

### Limitaciones conocidas (codex review v5)

- **D↔R direct detection (`ViolationDetector.cs:195`) ahora más fácil de
  enmascarar**: el paso por Neutral (debounce activo) lo oculta. Sigue
  siendo bug pre-existente, no introducido por v1.6.7. Pendiente revisar
  post-fix.
- **300ms es arbitrario**: balance permitir disengage rápido vs enmascarar
  tránsitos largos. Si hardware varía mucho entre kioskos, ajustar via
  PlayerPref futuro.

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

## v1.7.0 — Calibración immutable + verify-only Pantalla 2 (2026-05-11)

Shipped: ver `docs/superpowers/specs/2026-05-11-hori-v170-immutable-calibration-design.md` para el design completo y `docs/superpowers/plans/2026-05-11-hori-v170-immutable-calibration.md` para el plan de implementación.

### Cambios clave
- **JSON file** `<persistentDataPath>/hori_mapping.json` reemplaza PlayerPrefs para HORI.
- **F8 panel** `HoriCalibrationPanel` (`Assets/Custom/HoriCalibrationPanel.cs`) es el ÚNICO writer del JSON.
- **Pantalla 2** para HORI ahora es verify-only — si JSON existe y preflight pasa, carga escena directo (sin sliders, sin Phase 4/6 Discovery).
- **Modal "necesita calibración"** lista controles faltantes y guía al F8 sostén 1.5s.
- **Heartbeat sync** `controlMapping` field — portal admin muestra el mapping en `/admin/simuladores/pcs/[pcId]/calibracion`.
- **Folder consolidation**: install path canónico `C:\Tlax2026-RC\` (script `scripts/consolidate-install-path.ps1`).

### Archivos nuevos (Manejo/)
- `Assets/Custom/HoriCalibration/TlaxSim.HoriCalibration.asmdef`
- `Assets/Custom/HoriCalibration/HoriMapping.cs` — struct serializable
- `Assets/Custom/HoriCalibration/HoriControlMapping.cs` — singleton load/save atómico (.tmp→rename)
- `Assets/Custom/HoriCalibration/HoriMappingMigration.cs` — PlayerPrefs→JSON con validación estricta
- `Assets/Custom/HoriCalibration/HoriPreflightCheck.cs` — Validate(mapping, resolver, manual)
- `Assets/Custom/HoriCalibrationPanel.cs` — F8 UI (header + rows + Detect flows + footer + bootstrap)
- `Assets/Tests/EditMode/HoriControlMappingTests.cs` — 5 tests
- `Assets/Tests/EditMode/HoriMappingMigrationTests.cs` — 6 tests
- `Assets/Tests/EditMode/HoriPreflightCheckTests.cs` — 7 tests
- `scripts/consolidate-install-path.ps1` — one-time per kiosko folder rename

### Archivos modificados (Manejo/)
- `Assets/Gley/UrbanAssets/Runtime/Scripts/UI/UIInputNew.cs` — HORI branch lee rest/press y binds desde `HoriControlMapping.Active`; agregados helpers `IsHORITruckWheel/Shifter`; throttle bypass preservado; `ForceHoriBind` deprecated (comentado).
- `Assets/Custom/Menu/MenuScreenManager.cs` — `PrepareWheelScreen` HORI rama verify-only + `ShowHoriPreflightModal` + `RuntimeHoriResolver`; Phase 4/6 skipped for HORI; sanity check guarded.
- `Assets/Custom/BindingsPanel.cs` — F8 hold gateado por HORI detection.
- `Assets/Custom/SimulatorApiClient.cs` — heartbeat agrega `controlMapping` JSON string blob.
- `scripts/bootstrap-install.ps1` — default `InstallDir` es ahora `C:\Tlax2026-RC`.

### Cross-repo
- `portal-backend/lib/lambdas/simulator-api/simulator-heartbeat.ts` — whitelist `controlMapping` + escribe a DynamoDB.
- `portal/src/lib/simulator-api.ts` — types `controlMapping`, `controlMappingUpdatedAt`, `HoriMappingV1`.
- `portal/src/components/admin/HoriMappingTable.tsx` — display component.
- `portal/src/app/admin/simuladores/pcs/[pcId]/calibracion/page.tsx` — integración.

### Bugs cerrados
- "presiono acelerador, los 3 son reconocidos" — Discovery HORI removida.
- "no persiste entre sesiones" — JSON atómico + único writer.
- "sanity check borra calibración" — sanity check HORI eliminado.
- "Pantalla 2 cada arranque" — verify-only.

### Scope
HORI Truck only. G923 PS/Xbox y Moto sin cambios.

### Path canónico del claxon (verified Phase 0 SSH Aramis)
`wheel:button7` (verificado por hex decode del registry HKCU\Software\Tlaxcala\Tlax2026-RC, valor `776865656C3A627574746F6E37` = `wheel:button7`).

### Productivos
NO se desplegó OTA a productivos en v1.7.0. Aramis only. Productivos pendientes en sesión futura con autorización explícita.

## v1.7.0 — Lecciones del smoke testing (post-PR #162 merge)

El shipping del v1.7.0 tardó **16 builds iterativos** (r1→r15c) en Aramis antes de que todo funcionara. Cada build descubrió bugs nuevos. Lecciones clave:

### 1. Bug catastrófico: brake fantasma durante clutch-press (r14)

**Síntoma**: operator en 3ra a 60 km/h, pisa clutch, sostén 2 seg → pierde 60 km/h. Más rápido que el frenado normal.

**Hipótesis inicial (mala)**: drag/wheel friction natural cuando `motorTorque=0`. Esto explicaba sólo ~1.7 m/s² (drag) — faltaba ~6.6 m/s² inexplicado.

**Root cause** (encontrada con logs SHIFT_DIAG agregados en r13): PlayerPrefs `G923_BrakeAxis` y `G923_ClutchAxis` AMBOS sembrados con `rz` por Discovery antiguo. Mi Phase 6 v1.7.0 wire-in solo override `rest/press` desde JSON pero **NO override los axis paths**. Resultado:
- `_brakeCtrl = CacheControl("rz")`
- `_clutchCtrl = CacheControl("rz")`  ← mismo control que brake

Cuando operador pisa clutch → axis `rz` se mueve → tanto `clutchInput` como `brakeInput` reportan ~0.7 → `brakeTorque = 3000 * 0.7 = 2100 Nm`/wheel → ~5 m/s² de freno fantasma + 1.7 drag = ~7 m/s² total.

**Fix**: en `AttachToWheelDevice`, cuando es HORI, override `brakePath` y `clutchPath` desde `HoriControlMapping.Active.axes.brake.path` y `.clutch.path` ANTES de `CacheControl()`.

### 2. Audit de phantoms post-bug (r15)

Después del bug catastrófico, codex y yo auditamos TODAS las lecturas de PlayerPrefs `G923_*` y `Bind_*` en `UIInputNew.cs`. Encontramos 2 phantoms más donde para HORI debíamos leer del JSON pero seguíamos leyendo PlayerPrefs:

| Phantom | Línea | Síntoma posible |
|---------|-------|-----------------|
| Steer center/leftMax/rightMax | `_steerCenter = PlayerPrefs.GetFloat("G923_SteerCenter")` etc. | Rangos viejos sobreviven aunque F8 guarde nuevos en JSON |
| Manual block gate | `if (string.IsNullOrEmpty(PlayerPrefs.GetString(PREF_G923_CLUTCH_AXIS)))` | Bloqueo manual decide por PlayerPrefs no JSON; si limpian un kiosko sin tocar JSON, falsea bloqueo |

**Principio cementado v1.7.0**: para HORI, **NINGUNA lectura de PlayerPrefs debe sobrevivir**. Toda calibración HORI viene del JSON `HoriControlMapping.Active`. PlayerPrefs `G923_*` y `Bind_*` siguen vivos para G923/Moto (gated por `IsHORITruck(device)` en cada read).

### 3. Diagnostic-first cuando el root cause no es obvio

Tras 4 builds (r9-r12) intentando "subir el ghost-torque para compensar el drag", el bug seguía. La frustración me llevó a meter logs exhaustivos (`SHIFT_DIAG` + `SHIFT_APPLY`) cada 15 frames con TODOS los valores de input + state. **Los logs revelaron en <30 segundos** que `brake = clutch = 0.65` valor por valor — imposible si fueran axes distintos. Sin logs hubiera seguido fixing síntomas.

**Lección**: cuando llevo >2 iteraciones sin pegar al root cause, **bajar al data-mining mode**. Logs cada 15 frames con state completo > más builds con hipótesis débiles.

### 4. Unity batch build trampa: asmdef matters

Cuando verifiqué que mi código estaba en el DLL desplegado, hashing `Assembly-CSharp.dll` me dio resultados "idénticos a antes" → casi me llevó a creer que Unity ignoraba mis edits. **Pero `UIInputNew.cs` vive en asmdef `Gley.UrbanSystem` → compila a `Gley.UrbanSystem.dll`, NO a `Assembly-CSharp.dll`**.

**Lección**: para verificar que un cambio C# está baked en un build, primero identificar el `.asmdef` del archivo modificado y hashear el DLL que corresponde a ese asmdef.

### 5. SSH directo > LogUploader S3 para iteración rápida en LAN

Durante el bug investigation, esperar 5 min al flush de LogUploader era inviable. Conexión SSH directa a Aramis vía LAN bajaba `Player.log` en <5 segundos:

```bash
ssh simul@10.0.0.235 'Get-Content "$env:USERPROFILE\AppData\LocalLow\Tlaxcala\Tlax2026-RC\Player.log" | Select-String -Pattern "SHIFT_DIAG"' > /tmp/aramis-shift-logs.txt
```

LogUploader queda activo para diagnóstico remoto de productivos (donde no hay SSH).

### 6. Codex como reviewer crítico continuo

Codex reviewó la rama 4 veces durante el desarrollo (v1.7.0 design → UX r8 → reset combo → phantom audit). Cada review encontró ≥1 HIGH/MED issue:

| Review | HIGH | MED | LOW |
|--------|------|-----|-----|
| 1 (design) | 0 | 0 | 0 (aprobó) |
| 2 (UX + initial fixes r8) | 1 (reset combo latch) | 3 (DetectButton snapshot, SwitchPage, EventSystem dedup) | 0 |
| 3 (r12 fixes) | 1 (ghost-torque en N sostenido) | 3 (intent vs velocity sign, hill-start, diag log) | 1 (reader health) |
| 4 (r15 phantom audit) | 0 | 0 | 0 (aprobó) |

**Costo total Codex**: ~$? (no medido). Valor: detectó issues que hubiera costado más rounds de testing en kiosko remoto. Vale en projects con loop deploy/test caro.

## Pendiente / mejora long-term

- Productivos HORI (pasajeros1/2, carga, ambulancia) — OTA push pendiente con autorización
- Detección operativa de phantom future PlayerPrefs reads (un test EditMode que grep el code base por `PlayerPrefs.Get*` no-gated por HORI)
- v1.7.1: push/restore/clone del mapping desde portal admin
