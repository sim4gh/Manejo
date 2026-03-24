# Cambios del 23 de Marzo 2026

## Resumen

Se implementó el sistema completo de examen cronometrado, integración con el backend AWS, panel administrativo, auto-updater, y sistema de detección de infracciones mejorado.

---

## 1. Timer de 5 minutos + Pantalla de Resultados

### Archivos nuevos
- `Assets/Custom/ExamTimer.cs` — Countdown de 5:00 a 0:00 visible en top-center de la pantalla durante el examen
- `Assets/Custom/ExamResultsScreen.cs` — Overlay fullscreen al terminar: muestra score, APTO/NO APTO, cantidad de infracciones
- `Assets/Custom/ExamBootstrap.cs` — Inyecta automáticamente el timer en todas las escenas de manejo (no en MainMenu)

### Comportamiento
- El timer aparece automáticamente al entrar a cualquier escena de examen
- Feedback visual progresivo: blanco (>60s) → amarillo (≤60s) → rojo (≤30s) → parpadeo (≤10s)
- Al llegar a 0:00: congela la simulación, muestra resultados, regresa al menú en 15s (o cualquier tecla)
- Solo actualiza el texto cuando cambia el segundo (no cada frame) para evitar GC pressure

---

## 2. Integración con Backend AWS

### Archivos nuevos
- `Assets/Custom/SimulatorApiClient.cs` — Cliente HTTP para comunicar con el backend
- `Assets/Custom/SimulatorConfig.cs` — Configuración local persistida en `simulator_config.json`

### Archivos modificados
- `Assets/Custom/GameManager.cs` — Expandido con TramiteId, SessionId, ThingName, ClearSession()
- `Assets/Custom/Menu/MenuScreenManager.cs` — Inicia sesión en backend antes de cargar escena de examen

### Flujo
```
QR/código verificado → tramiteId
    ↓
POST /simulator/sessions {tramiteId, thingName} → sessionId
    ↓
Carga escena de manejo → 5 min de examen
    ↓
Timer 0:00 → POST /simulator/sessions/{sessionId}/results {passed, score, faults[]}
    ↓
Resultados visibles en portal admin y para el ciudadano
```

### Resilencia offline
- Si el POST falla, los resultados se guardan en `pending_results.json`
- Al siguiente inicio de la app, se reintentan automáticamente
- El `PendingCount` está cacheado para evitar file I/O innecesario

### Backend (portal-backend)
- `simulator-start-session.ts` — Ahora acepta `tramiteId` además de `appointmentCode`
- `simulator-register.ts` — **Nuevo**: registra PCs como dispositivos (`POST /simulator/register`)
- `simulator-update-check.ts` — **Nuevo**: verifica versiones de Unity contra S3 (`GET /simulator/update-check`)
- `api-stack.ts` — Nuevas rutas públicas para register y update-check

---

## 3. Panel Administrativo (F10)

### Activación
- **F10 mantenido 1.5 segundos** en el menú principal
- Navega a Screen 3 dentro del mismo flujo de pantallas del menú (mismas transiciones, misma UI)

### Secciones
- **Identidad**: Station ID, API Base URL
- **Números de serie**: Herraje, silla, computadora, DOF controller, volante
- **Actualizaciones**: Versión actual
- **Calificación del servidor**: Penalizaciones, umbrales APTO/NO APTO (sincronizado del backend)
- **Diagnóstico**: Resultados pendientes, estado de red

### Botones
- **Guardar** — Guarda config local + registra PC en backend via `POST /simulator/register`
- **Verificar Red** — Prueba conectividad con el API
- **Volver** — Regresa a la pantalla del QR (Screen 0)

### Archivos
- `Assets/Custom/AdminPanel.cs` — Solo detecta F10 y navega (55 líneas)
- `Assets/Custom/Menu/MenuScreenManager.cs` — Screen 3 con toda la UI admin

---

## 4. Auto-Updater

### Archivo nuevo
- `Assets/Custom/AutoUpdater.cs`

### Comportamiento
1. Al iniciar la app: `GET /simulator/update-check?currentVersion={version}`
2. Si hay versión nueva: descarga ZIP en background
3. Después de las 17:00 (o al siguiente arranque): instala automáticamente via `update.bat`
4. El bat extrae el ZIP, copia los archivos nuevos, y relanza el ejecutable

### Backend
- `GET /simulator/update-check` lee `unity-builds/{env}/latest.json` de S3
- Compara versiones semánticas y devuelve URL de descarga si hay update

---

## 5. Sistema de Detección de Infracciones Mejorado

### Problemas resueltos
| Problema | Antes | Después |
|----------|-------|---------|
| Ragdoll spam | 7 colisiones en 0.24s de 1 peatón (-70 pts) | 1 colisión por peatón (cooldown 3s) |
| Score negativo | -465 | Mínimo 0 (floor con `Mathf.Max`) |
| Colisiones a 0 km/h | Tráfico AI chocaba al jugador estacionado | Ignoradas si velocidad < 3 km/h |
| Velocidad spam | 5 eventos en 4s por oscilar alrededor del límite | Cooldown 5s entre penalizaciones |
| Nombres "Gley" | Todas las colisiones vehiculares decían "Gley" | Nombre descriptivo del vehículo (Honda, Patrulla, etc.) |

### Tipos de colisión diferenciados
| Tipo | Tag/Layer | Penalización | Event Type |
|------|-----------|-------------|------------|
| Peatón | `Pedestrian` / layer `Peaton` | -25 | `ATROPELLO` |
| Bicicleta | `Bicicleta` | -15 | `COLISION_BICICLETA` |
| Vehículo | `automovil` / layer `RCCP_Vehicle` | -10 | `COLISION_VEHICULO` |
| Señalamiento | `Senalamiento` | -5 | `COLISION_SENALAMIENTO` |
| Obstáculo | Cualquier non-Default | -5 | `COLISION_OBSTACULO` |

### Archivos modificados
- `Assets/Custom/ViolationDetector.cs` — Cooldown 3s, deduplicación por root, `DeductScore()`, velocidad mínima 3 km/h
- `Assets/Custom/WrongWayDetector.cs` — Usa `DeductScore()` (score nunca < 0)
- `Assets/Custom/RedLightDetector.cs` — Usa `DeductScore()` (score nunca < 0)

---

## 6. Scoring Config (del otro thread)

### Archivo nuevo
- `Assets/Custom/ScoringConfig.cs` — Singleton que descarga configuración de penalizaciones del backend

### Comportamiento
- Cache local en `scoring_config.json` para modo offline
- Se aplica a todos los detectores al cargar la escena de examen
- Sincronizable desde el panel admin ("Sincronizar Ahora")
- Los umbrales APTO/NO APTO son dinámicos desde el servidor

---

## 7. Fixes de Rendimiento

| Fix | Archivo | Detalle |
|-----|---------|---------|
| Event leak | ExamBootstrap.cs | Flag estático para no acumular `sceneLoaded` listeners |
| PendingCount I/O | SimulatorApiClient.cs | Cache en memoria, invalidado al guardar/limpiar |
| Timer GC | ExamTimer.cs | Solo actualiza texto al cambiar segundo (no cada frame) |
| Scene name cache | AdminPanel.cs | Cache via `sceneLoaded` event, no `GetActiveScene()` cada frame |
| Input delay | ExamResultsScreen.cs | 1s antes de aceptar input (evita skip accidental) |
| TimeScale guard | ExamResultsScreen.cs | `OnDestroy()` restaura `Time.timeScale = 1f` |
| Input System | AdminPanel.cs, ExamResultsScreen.cs | Migrado de legacy `Input.GetKey` a `Keyboard.current` |
| Deprecated API | ScoringConfig.cs, ExamTimer.cs | `FindObjectOfType` → `FindFirstObjectByType` |
