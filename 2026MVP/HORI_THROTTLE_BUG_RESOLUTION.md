# HORI Truck Control System (HPC-044U) — Resolución del bug del acelerador

**Fecha:** 2026-05-03
**Tiempo total de debugging:** ~6h (4 builds Unity + 5 plans probados)
**Resultado:** ✅ Throttle responde correctamente vía P/Invoke directo a Windows HID

## TL;DR

El HID parser auto-generado de **Unity 6 Input System** tiene un bug con el descriptor del HORI HPC-044U (declara dos `Slider` con HID Usage `0x36`). Unity hace alias de los dos sliders al mismo byte del input report y deja el byte del **throttle (offset 21-22) huérfano** — ningún `AxisControl` lo lee, y Unity ni siquiera emite `state events` cuando cambia.

**Solución:** abrir el HID device path crudo (`\\?\hid#vid_0f0d&pid_017a&col01`) con `CreateFile`, hacer `ReadFile` en un thread background, leer los bytes 21-22 directo. Bypass total de Unity Input System para ese byte.

---

## 1. El problema reportado

> "tengo un problema en Manejo, no detecta el acelerador del Hori truck volante. Detecta el freno y el clutch pero no el acelerador. Ya pusimos el dump en F7 pero tampoco aparece nada... pero en mi computadora Windows de prueba todo funciona."

Síntomas:
- HORI Truck Control System Wheel (HPC-044U) — wheel oficial para ETS2/ATS.
- Brake y clutch detectados correctamente en Unity.
- Throttle NO detectado en ningún axis (`z`, `rx`, `ry`, `rz`, `slider`, `slider1`, `slider2`).
- Ocurre en 4 kioskos remotos productivos.
- HORI Manager (UI propietaria) sí ve los 3 pedales bien — descarta hardware/firmware.
- (Spoiler: tampoco funcionaba en la PC del usuario, pero al inicio creyó que sí.)

## 2. Hipótesis descartadas (lo que NO era)

### Hipótesis 1: HORI Device Manager activo
**Idea:** el Manager reconfigura el HID descriptor.
**Descartada:** mismo síntoma con/sin Manager instalado. Verificado por el usuario.

### Hipótesis 2: Platform Toggle Switch en posición incorrecta
**Idea:** algunos volantes HORI tienen un switch físico PC/console.
**Descartada:** el HPC-044U es PC-only. El "Platform Toggle Switch" mencionado en una página de HORI venía mezclado con el manual de un modelo distinto (HPC-044E europeo o variante PS4).

### Hipótesis 3: INF residual del HORI Manager corrompiendo enumeración
**Idea:** Manager registra un INF custom; tras desinstalar queda residual.
**Descartada:** el usuario verificó que su PC (que también tenía el problema) **nunca tuvo Manager instalado**.

### Hipótesis 4: Pedal calibration al boot
**Idea:** el wheel auto-calibra solo "L pedal y R pedal" según el manual; el throttle podría quedar sin calibrar.
**Descartada:** HORI Manager ve los 3 pedales con valores válidos → calibración OK.

### Hipótesis 5: USB hub / extensión
**Idea:** el manual prohíbe hubs.
**Descartada parcialmente:** brake+clutch funcionando en Unity descarta esto como causa primaria.

## 3. Setup de diagnóstico

### 3.1 Comunicación SSH con la PC Windows

Para diagnósticos remotos sin viajar a los kioskos, se habilitó OpenSSH en la PC del usuario (Windows 11 Pro 24H2 build 26100):

```powershell
# En PowerShell admin:
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
# (NOTA: estado quedó en "InstallPending" por updates pendientes — necesitó REBOOT)

Start-Service sshd
Set-Service -Name sshd -StartupType 'Automatic'
New-NetFirewallRule -Name sshd -DisplayName 'OpenSSH Server' -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 22
New-Item -Path "HKLM:\SOFTWARE\OpenSSH" -Force | Out-Null
New-ItemProperty -Path "HKLM:\SOFTWARE\OpenSSH" -Name DefaultShell -Value "C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe" -PropertyType String -Force
```

Llave pública SSH (ed25519) en:
```
$env:ProgramData\ssh\administrators_authorized_keys
```

con ACL:
```powershell
icacls $f /inheritance:r /grant "Administrators:F" /grant "SYSTEM:F"
```

Conexión desde Mac:
```bash
ssh simul@10.0.0.235
```

**Truco para evitar quoting hell** con caracteres especiales (`&`, `|`) en comandos PowerShell sobre SSH: pasar como `-EncodedCommand` base64-UTF16:

```bash
CMD='Get-PnpDevice | Where-Object { $_.InstanceId -match "VID_0F0D" }'
ENC=$(python3 -c "import base64,sys; print(base64.b64encode(sys.argv[1].encode('utf-16le')).decode())" "$CMD")
ssh simul@10.0.0.235 "powershell -EncodedCommand $ENC"
```

### 3.2 Scripts de diagnóstico HID (PowerShell + P/Invoke)

Tres scripts construidos durante el debugging, en `/tmp/hori-diag/`:

#### `hid-dump.ps1` — enumera HID interfaces y lista capabilities

Usa P/Invoke a `setupapi.dll` + `hid.dll` (`HidD_GetPreparsedData`, `HidP_GetCaps`, `HidP_GetValueCaps`, `HidP_GetButtonCaps`) para enumerar las interfaces HID del wheel y dumpear:
- VID/PID
- Top-level Usage Page + Usage
- Cantidad de buttons, axes
- Bit size de cada axis, logical/physical min/max

Reveló que el HORI expone **3 HID collections** del mismo USB device:
- `COL01` — HID-compliant game controller (29-30 byte input report)
- `COL02` — vendor-defined (UsagePage 0xFF20, 32 bytes)
- `COL03` — vendor-defined (UsagePage 0xFF21, 64 bytes)

Solo `COL01` es lo que Unity Input System lee (es el Joystick estándar).

#### `hid-poll.ps1` — captura raw input reports en stream

Abre `\\?\hid#vid_0f0d&pid_017a&col01#...` con `CreateFile`, hace `ReadAsync` en loop durante 15s. Guarda timestamps + hex dump de cada report a CSV.

**Procedimiento de captura controlado** (operador del kiosko sigue el cronómetro):
- t=0..3s: nada, pedales en reposo (baseline)
- t=3..6s: pisa **CLUTCH** a fondo y suelta
- t=6..9s: pisa **BRAKE** a fondo y suelta
- t=9..12s: pisa **THROTTLE** a fondo y suelta
- t=12..15s: reposo final

#### `analyze.py` — diff bytes que cambian por phase

Decodifica el CSV, parsea cada report como bytes, decodifica candidate axis bytes como LE16 (little-endian unsigned 16-bit), reporta:
- Span por phase (max - min) de cada par de bytes
- Identifica qué bytes cambian SOLO durante una phase específica → ese byte es ese pedal

**Output del analyze.py para HORI HPC-044U:**

| Phase | bytes 9-10 | bytes 11-18 | **bytes 19-20** | **bytes 21-22** | **bytes 23-24** |
|---|---|---|---|---|---|
| clutch | 32768..32768 (centered) | 32768 (centered) | **0..65535** | 0 (no movement) | 0 (no movement) |
| brake | 32768..32768 | 32768 | 0..65314 | 0..35310 (user touched throttle too) | **0..65346** |
| throttle | 32768..32768 | 32768 | 0 (no movement) | **0..65535** | 0 (no movement) |

**Conclusión empírica:**

| Bytes (LE16) | HID Usage (declarado) | Función física | Range |
|---|---|---|---|
| 9-10 | X (0x30) | stick/wheel | centered (rest 32768) |
| 11-12, 13-14, 15-16, 17-18 | Y, Z, Rx, Ry | sticks/wheel | centered |
| **19-20** | **Slider#1 (0x36)** | **CLUTCH** | unipolar (rest 0, press 65535) |
| **21-22** | **Slider#2 (0x36)** | **THROTTLE** | unipolar |
| **23-24** | **Rz (0x35)** | **BRAKE** | unipolar |

Los **dos sliders Usage 0x36 declarados duplicados** en el descriptor son la pieza clave del bug.

## 4. Identificación del bug en Unity

### Lo que Unity expone (verificado en F7 LogConsolePanel "Dump controls")

```
HORI TRUCK CONTROL SYSTEM WHEEL [HID::HORI CO.,LTD. ...]
  AxisControl  slider    valueType=Single  value=-1  ← unipolar (rest=-1 = HID raw 0)
  AxisControl  slider1   valueType=Single  value=-1  ← unipolar
  AxisControl  rz        valueType=Single  value=-1  ← unipolar
  AxisControl  ry        valueType=Single  value=0   ← centered
  AxisControl  rx        valueType=Single  value=0   ← centered
  AxisControl  z         valueType=Single  value=0   ← centered
  AxisControl  stick/x   valueType=Single  value=0   ← centered
  AxisControl  stick/y   valueType=Single  value=0   ← centered
  ButtonControl ... (varios)
  DpadControl   hat
```

3 axes unipolares (`slider`/`slider1`/`rz`) + 5 centered (`ry`/`rx`/`z`/`stick/x`/`stick/y`) = 8 axes. Match con HID descriptor.

### Comportamiento observado al pisar pedales (en F7 INPUTS EN VIVO):

| Pedal pisado | Unity axes que cambian |
|---|---|
| Clutch | slider/slider1/rz (en lockstep parcial) |
| Brake | slider/slider1/rz (también en lockstep) |
| **Throttle** | **NINGUNO** |

### El bug

`HidP_GetValueCaps()` reporta los 8 axes con `DataIdx` 55..62 secuenciales. Pero por los 2 sliders Usage 0x36 duplicados, el HID parser de Unity:
- Asigna `slider` a un byte específico
- Asigna `slider1` al **MISMO byte** (alias)
- Asigna `rz` a otro byte
- **El tercer byte unipolar (donde vive el throttle) queda sin AxisControl que lo lea — ORPHAN BYTE**

Cuando el byte 21-22 cambia (porque el usuario pisa throttle), Unity:
- Recibe el report HID del OS
- Pasa por `HidP_ParseInputReport`
- No encuentra ningún tracked usage que haya cambiado (slider/slider1 leen otro byte)
- **No emite state event** → ningún sistema upstream se entera

Esto explica todas las observaciones:
- Brake y clutch funcionan (sus bytes están mapeados a `slider`/`slider1`/`rz` cualquiera)
- Throttle no funciona en ningún wheel HORI HPC-044U en Unity (es bug del parser, no del wheel)
- HORI Manager funciona porque NO usa Unity Input System (lee HID raw a su manera)

## 5. Soluciones intentadas

### Plan A (FAILED): Custom Unity HID Layout

**Idea:** registrar un `InputControlLayout` custom para `vendorId=0x0F0D, productId=0x017A` que defina explícitamente `pedalThrottle` en byte offset 21, `pedalBrake` en 23, `pedalClutch` en 19.

```csharp
InputSystem.RegisterLayout(@"
{
    ""name"" : ""HoriTruckPedalsFix"",
    ""extend"" : ""HID"",
    ""controls"" : [
        { ""name"" : ""pedalThrottle"", ""layout"" : ""Axis"", ""format"" : ""USHT"",
          ""offset"" : 21,
          ""parameters"" : ""normalize,normalizeMin=0,normalizeMax=65535,normalizeZero=0"" }
        // + clutch + brake
    ]
}", matches: new InputDeviceMatcher()
    .WithInterface("HID")
    .WithCapability("vendorId", 0x0F0D)
    .WithCapability("productId", 0x017A));
```

**Por qué falló:**
1. **El layout REEMPLAZA el auto-gen layout** del device entero (codex me lo advirtió antes y caí). El nuevo layout solo tenía 3 controls — perdió todos los buttons, hat, sticks, slider/slider1/rz. El wheel quedó "decapitado".
2. El matcher capturó **las 3 HID collections** (COL01 + COL02 + COL03), creando 3 devices "HoriTruckPedalsFix" con solo 3 controls cada uno.
3. Aún en COL01, los offsets 19/21/23 que usé eran probablemente incorrectos — Unity strip-ea el report ID y/o reorganiza el state buffer.
4. Resultado: nada se movía con pedales, y se rompió todo lo demás.

### Plan C (FAILED): InputSystem.onEvent intercept

**Idea:** suscribirse a `InputSystem.onEvent` para interceptar los `StateEvent`/`DeltaStateEvent` que Unity recibe del wheel ANTES de que el parser los procese. Leer bytes 21-22 directo del state buffer.

```csharp
unsafe void OnEvent(InputEventPtr e, InputDevice device) {
    if (!_horiCandidates.Contains(device.deviceId)) return;
    if (e.IsA<StateEvent>()) {
        StateEvent* se = (StateEvent*)e.data;
        byte* state = (byte*)se->state;
        ushort raw = (ushort)(state[21] | (state[22] << 8));
        Value = raw / 65535f;
    }
}
```

**Por qué falló:**

Unity NO emite state events cuando solo cambia un byte huérfano (sin AxisControl que lo lea). Verificado: con el throttle pumping activo durante 30+ segundos, el reader capturó solo eventos donde los axes TRACKED cambiaban (jitter del wheel/sticks centered). Los bytes 21-22 nunca se vieron cambiar en el state buffer entregado a `onEvent`.

Conclusión: el state buffer de Unity (post-parser) **no es un mirror fiel del raw HID report**. Unity solo propaga cambios de usages declarados.

### Plan B (WORKING): P/Invoke directo a Windows HID

**Idea:** abrir el HID device path crudo de Windows con `CreateFile`, hacer `ReadFile` en un thread background. El raw HID report viene tal cual del firmware del wheel — bypass total de Unity Input System.

```csharp
// Enumera HID device paths
var paths = EnumerateHidPaths();  // SetupDi APIs
var col01Path = paths.FirstOrDefault(p =>
    p.ToLower().Contains("vid_0f0d&pid_017a") && p.ToLower().Contains("col01"));

// Abre el device handle (SHARED — coexiste con Unity y HORI Manager)
var handle = CreateFile(col01Path,
    GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
    IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

// Background thread con ReadFile loop
Thread t = new Thread(() => {
    byte[] buffer = new byte[64];
    while (_running) {
        ReadFile(handle, buffer, (uint)buffer.Length, out uint read, IntPtr.Zero);
        if (read >= 23) {
            ushort raw = (ushort)(buffer[21] | (buffer[22] << 8));
            _value = raw / 65535f;  // volatile float
        }
    }
});
t.IsBackground = true;
t.Start();
```

**Por qué funciona:**
- `ReadFile` sobre un HID device handle entrega el input report **completo y crudo** desde el OS. Los 30 bytes con report ID, hat, buttons, axes — todo.
- No depende del parser HID de Unity. El bug del parser es irrelevante.
- Verificado empíricamente: el byte 21-22 cambia 0..65535 al pisar throttle (lo confirmamos primero con `hid-poll.ps1`, luego en producción dentro de Unity).

**Integración con UIInputNew:**

Cross-asmdef issue: `HoriThrottleReader.cs` vive en `Assets/Custom/` (Assembly-CSharp.dll). `UIInputNew.cs` vive en `Assets/Gley/UrbanAssets/` (Gley assembly). Gley NO puede referenciar Assembly-CSharp (ciclo prohibido). Solución: **delegate injection desde Assembly-CSharp → Gley**.

```csharp
// En UIInputNew.cs (Gley):
public static System.Func<float> HoriThrottleProvider;

// En HoriThrottleReader.cs (Custom):
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
static void Bootstrap() {
    // ...
    Gley.UrbanSystem.UIInputNew.HoriThrottleProvider = () =>
        _instance != null ? _instance.Value : 0f;
}
```

`UIInputNew.ReadGasRawValue()` llama el provider cuando el flag `_useHoriRawGas` está set; cae al `_gasCtrl` normal cuando no.

## 6. Archivos del fix

### `Assets/Custom/HoriThrottleReader.cs` (nuevo)

Singleton MonoBehaviour, `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`, Windows-only via `#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN`.

P/Invoke a `setupapi.dll`, `hid.dll`, `kernel32.dll`. Enumera HID paths, filtra `vid_0f0d&pid_017a` + `col01`, `CreateFile`, thread background con `ReadFile` blocking loops, lee bytes 21-22 LE16 / 65535 → `Value` (volatile float).

Hot-plug: `Update()` retry every 2s si el poller muere. `OnDestroy` cierra handle + Join thread. Reset `Value=0` al perder handle (codex review: evita aceleración fantasma).

### `Assets/Gley/UrbanAssets/Runtime/Scripts/UI/UIInputNew.cs`

```diff
+ public const string HORI_RAW_GAS_PATH = "__HORI_RAW_HID_THROTTLE__";
+ public static System.Func<float> HoriThrottleProvider;
+ private bool _useHoriRawGas;

  // En IsHORITruck(device) block de AttachToWheelDevice:
+ PlayerPrefs.SetString("G923_GasAxis", HORI_RAW_GAS_PATH);
+ PlayerPrefs.SetFloat("G923_GasRest", 0f);
+ PlayerPrefs.SetFloat("G923_GasPress", 1f);
+ _useHoriRawGas = true;

  // Después del HORI block, al cargar gasPath de PlayerPrefs:
+ // Guard: si sentinel persiste pero device != HORI, invalida.
+ if (gasPath == HORI_RAW_GAS_PATH && !IsHORITruck(device)) {
+     PlayerPrefs.DeleteKey("G923_GasAxis");
+     gasPath = "z";
+ }
+ _useHoriRawGas = (gasPath == HORI_RAW_GAS_PATH);
+ _gasCtrl = _useHoriRawGas ? null : CacheControl(gasPath);

  // Helper:
+ private float ReadGasRawValue() {
+     if (_useHoriRawGas) {
+         var p = HoriThrottleProvider;
+         return p != null ? p() : _gasRest;
+     }
+     return SafeReadFloatRaw(_gasCtrl, out var v) ? v : _gasRest;
+ }

  // 3 sites de gas read reemplazados:
- float gasRaw = SafeReadFloatRaw(_gasCtrl, out var rg) ? rg : _gasRest;
+ float gasRaw = ReadGasRawValue();
```

### `Assets/Custom/Menu/MenuScreenManager.cs`

**v1.5.12** — Phase 3 (gas) de Pantalla 2 auto-pasaba para HORI con el sentinel
(sin verificar el reader). Discovery sigue normal para steering/brake/clutch.

```csharp
if (!throttleDone) {
    var devForGas = TryAttachToDevice();
    if (devForGas != null && UIInputNew.IsHORITruck(devForGas)) {
        throttleDone = true;
        PlayerPrefs.SetString("G923_GasAxis", UIInputNew.HORI_RAW_GAS_PATH);
        PlayerPrefs.SetFloat("G923_GasRest", 0f);
        PlayerPrefs.SetFloat("G923_GasPress", 1f);
        wheelPrompt.text = "Pisa el FRENO a fondo";
        return;
    }
    // Discovery normal para non-HORI...
}
```

**v1.6.5 (Fase B5)** — el auto-pass se reemplazó con verificación en vivo
del reader. Ver `HORI_CALIBRATION_LESSONS.md` sección "v1.6.5 — Fase B5".
Resumen:

```csharp
if (!throttleDone) {
    var devForGas = TryAttachToDevice();
    if (devForGas != null && UIInputNew.IsHORITruck(devForGas)) {
        if (_horiPhase3StartTime < 0f) _horiPhase3StartTime = Time.unscaledTime;
        var thrReader = HoriThrottleReader.Instance;
        float thrValue = thrReader != null ? thrReader.Value : 0f;
        bool thrHandleOpen = thrReader != null && thrReader.IsHandleOpen;

        // Progress bar en vivo + threshold
        if (thrHandleOpen && thrValue >= 0.7f) {
            throttleDone = true;
            PlayerPrefs.SetString("G923_GasAxis", UIInputNew.HORI_RAW_GAS_PATH);
            PlayerPrefs.SetFloat("G923_GasRest", 0f);
            PlayerPrefs.SetFloat("G923_GasPress", 1f);
            wheelPrompt.text = "Pisa el FRENO a fondo";
            return;
        }
        // Si el handle no abrió tras 8s, advertencia visible. Reader sigue
        // intentando reabrir cada 2s — conectar USB recupera la fase.
        return;
    }
    // Discovery normal para non-HORI...
}
```

`HoriThrottleReader.IsHandleOpen` es nuevo getter (v1.6.5):

```csharp
public bool IsHandleOpen => _running && _hidHandle != null && !_hidHandle.IsInvalid;
```

F7 LogConsolePanel imprime `handle=open|CLOSED` en la sección "CUSTOM HID READERS"
para diagnóstico paralelo (v1.6.5 Fase A).

## 7. Garantía de no romper G923

El fast-path de Logitech G923 en `MenuScreenManager` **no se tocó**. Sigue idéntico:
- Detecta G923 → `EnsureG923PSDefaults` setea `G923_GasAxis = "z"` (PS) o `"stick/y"` (Xbox), rest=1, press=-1.
- Marca todas las fases done, carga escena directo.

En `UIInputNew`, el camino G923:
1. `IsLogitechG923Family(device)` → `EnsureG923PSDefaults` setea `G923_GasAxis`.
2. Load normal: `gasPath = "z"` o `"stick/y"` (no es el sentinel HORI).
3. `_useHoriRawGas = (gasPath == HORI_RAW_GAS_PATH)` → **false**.
4. `_gasCtrl = CacheControl(gasPath)` corre normal.
5. `ReadGasRawValue()` cae al else branch y lee `_gasCtrl` normal vía `SafeReadFloatRaw`.

**Edge case** (usuario cambia de HORI a G923 sin re-discovery): cubierto por el guard en UIInputNew que invalida el sentinel cuando el device actual no es HORI.

## 8. Codex review iterations

Plan se validó con codex en 4 iteraciones a lo largo del debugging:

1. **Hipótesis ranking** (inicial): codex priorizó Platform Toggle Switch → boot calibration → USB hub → física → firmware. La realidad: ninguna de las 5; era bug de Unity HID parser.
2. **Layout JSON validation** (Plan A): codex advirtió que `RegisterLayout` REEMPLAZA en vez de extender, y que necesitaba state struct completo. Caí en el warning. ✅ Decisión correcta de codex.
3. **InputSystem.onEvent design** (Plan C): codex advirtió que podía estar enganchado a la collection equivocada (3 devices) y que faltaba manejar `DeltaStateEvent`. Apliqué ambos. La falla real fue otra: Unity no emite events para bytes huérfanos.
4. **Sleep review final** (Plan B): codex encontró 3 issues concretos:
   - `volatile float` para visibility cross-thread → aplicado
   - Reset `Value=0` en unplug para evitar aceleración fantasma → aplicado
   - Sentinel guard si device cambia de HORI a G923 → aplicado
   - `csc.rsp -unsafe` innecesario tras refactor → removido

## 9. Lecciones aprendidas

1. **Unity HID parser es flaky con descriptors no estándar.** Usages duplicados, vendor collections weird, etc. Rule of thumb: si un wheel funciona en otra app pero no en Unity, sospechar primero del parser.
2. **`InputSystem.onEvent` no es un raw HID intercept.** Solo emite cuando un control declarado cambia. Para raw, P/Invoke es la única salida.
3. **P/Invoke a Windows HID es estable y simple.** ~150 líneas C#, sin dependencias externas. Funciona en standalone Unity Win64.
4. **PowerShell `-EncodedCommand` resuelve el quoting hell** sobre SSH cuando hay `&` o `|` en comandos.
5. **Cross-asmdef en Unity:** Custom → Gley está permitido, Gley → Custom no. Para inyección bidireccional, usar **static delegate** declarado en Gley y asignado desde Custom.
6. **Codex review repetido (3-4 rounds)** atrapó issues que solo hubiera detectado en producción: alias contamination, phantom acceleration, memory ordering. Rentabilísimo.
7. **No confiar en el primer F7 dump del usuario.** El usuario reportó que su PC funcionaba; en realidad solo brake+clutch funcionaban, throttle nunca. La verificación empírica con raw HID definió el problema.

## 10. Tools de diagnóstico — referencia rápida

| Tool | Path | Usado para |
|---|---|---|
| `hid-dump.ps1` | `/tmp/hori-diag/` | Enumerar HID interfaces + capabilities (P/Invoke a HidD/HidP APIs) |
| `hid-poll.ps1` | `/tmp/hori-diag/` | Capturar raw HID input reports a CSV |
| `analyze.py` | `/tmp/hori-diag/` | Diff bytes que cambian por phase de pedales |
| `hori-diagnose.ps1` | `Manejo/2026MVP/scripts/` | Dump genérico PnP/HID/INF para diff entre PCs |

## 11. Pendiente y future-proofing

- **Si Unity actualiza Input System** y arregla el bug del parser HID con duplicate Slider Usages, el reader puede retirarse. Verificar:
  ```csharp
  // Si en F7 LogConsolePanel post-update aparece pedalThrottle moviéndose,
  // o si slider/slider1 dejan de hacer alias, es seguro remover el bypass.
  ```
- **Otros wheels con 3 pedales y Slider duplicado** podrían tener el mismo bug. Si aparecen, la misma solución (P/Invoke + sentinel) generaliza fácil.
- **FFB del HORI** está fuera del alcance de este fix. El HPC-044U sí tiene motors FFB pero el bypass solo cubre input, no output.

## 12. Referencias

- [HORI USA HPC-044U product page](https://stores.horiusa.com/HPC-044U)
- [HORI HPC-044U manual (manuals.plus)](https://manuals.plus/hori/hpc-044u-force-feedback-truck-control-system-manual)
- [Unity Input System 1.7 HID Support](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.7/manual/HID.html)
- [Microsoft HID drivers reference](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/)
- [Understanding HID Report Descriptors (who-t)](http://who-t.blogspot.com/2018/12/understanding-hid-report-descriptors.html)
- PR de la solución: [General-Kain/Manejo#127](https://github.com/General-Kain/Manejo/pull/127)
