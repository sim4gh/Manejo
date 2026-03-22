# Tlax2026MVP - Simulador de Examen de Manejo

## Resumen

Simulador de examen de manejo para evaluacion de conductores en Tlaxcala, Mexico (2026). Proyecto en Unity 2022.3 LTS con deteccion automatica de infracciones, telemetria en tiempo real, sistema de pasajeros/peatones, y soporte multi-monitor para kiosko.

## Stack Tecnico

- **Engine:** Unity 2022.3 LTS
- **Render Pipeline:** URP (Universal Render Pipeline) 17.3.0
- **Input:** Unity Input System 1.17.0
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
| 3 | Jetta | Jetta | Examen con vehiculo Jetta |
| 4 | Camioneta | Camioneta | Examen con camioneta |
| 5 | Bus Pasajeros | Bus | Examen con autobus de pasajeros |
| 6 | Camion D Carga | Camion | Examen con camion de carga |
| 7 | Motocicleta | Moto | Examen con motocicleta |

## Flujo del Juego

1. **Inicio:** QR kiosko o MainMenu con expediente
2. **Carga:** Se carga "UrbanExample" con trafico y peatones Gley
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
