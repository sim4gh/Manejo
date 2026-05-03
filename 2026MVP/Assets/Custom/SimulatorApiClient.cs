using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Cliente HTTP para comunicar el simulador Unity con el backend AWS.
/// Endpoints: /simulator/sessions (start) y /simulator/sessions/{id}/results (end).
/// </summary>
public static class SimulatorApiClient
{
    private static string BaseUrl => SimulatorConfig.Instance?.data.apiBaseUrl
        ?? "https://d6twaegbhg.execute-api.us-east-1.amazonaws.com";

    private const int TIMEOUT_SECONDS = 10;

    // ── Modelos JSON ─────────────────────────────────────────────────

    [System.Serializable]
    public class StartSessionRequest
    {
        public string pcId;
        public string tramiteId;
    }

    [System.Serializable]
    public class StartSessionResponse
    {
        public string sessionId;
        public string tramiteId;
        public string citizenName;
        public string licenseType;
    }

    [System.Serializable]
    public class SessionFault
    {
        public string type;
        public string description;
        public int secondsFromStart;
        public string severity;
        public int deduction;
    }

    [System.Serializable]
    public class EndSessionRequest
    {
        public bool passed;
        public int score;
        // Distancia recorrida en metros durante el examen. Backend la usa para
        // detectar exámenes inválidos por inactividad (alumno dejó el coche
        // parado y al expirar el timer salía con score 100). 0 si build viejo.
        public int distanceMeters;
        public SessionFault[] faults;
        public bool interrupted;
    }

    [System.Serializable]
    public class EndSessionResponse
    {
        public bool success;
        public string sessionId;
    }

    // ── Modo Práctica ───────────────────────────────────────────────

    [System.Serializable]
    public class EndPracticeRequest
    {
        public string practiceId;
        public string pcId;
        public string vehicleType;
        public string transmission;     // nullable — "" en JSON cuando no aplica
        public string weather;
        public string spawnLocation;
        public string startedAt;        // ISO 8601 UTC
        public string completedAt;      // ISO 8601 UTC
        public int durationSeconds;
        public int score;
        public int distanceMeters;
        public SessionFault[] faults;
        public bool completed;
    }

    // ── Resultado pendiente (para retry offline) ─────────────────────

    [System.Serializable]
    public class PendingResult
    {
        // Discriminador: "exam" (default — sesión real con sessionId) o "practice".
        // Antiguos archivos sin kind se cargan como "exam" gracias a JsonUtility.
        public string kind;
        // Comunes a ambos
        public int score;
        public int distanceMeters;
        public SessionFault[] faults;
        public string savedAt;
        // Sólo kind=="exam"
        public string sessionId;
        public bool passed;
        public bool interrupted;
        // Sólo kind=="practice"
        public string practiceId;
        public string pcId;
        public string vehicleType;
        public string transmission;
        public string weather;
        public string spawnLocation;
        public string startedAt;
        public string completedAt;
        public int durationSeconds;
        public bool completed;
    }

    [System.Serializable]
    public class PendingResultsList
    {
        public List<PendingResult> results = new List<PendingResult>();
    }

    // ── API: Iniciar sesión ──────────────────────────────────────────

    /// <summary>
    /// POST /simulator/sessions — crea sesión en el backend.
    /// onSessionId recibe el sessionId o null si falla.
    /// </summary>
    public static IEnumerator StartSession(string tramiteId,
        System.Action<string> onSessionId)
    {
        string url = $"{BaseUrl}/simulator/sessions";

        var config = SimulatorConfig.Instance?.data;
        var requestBody = new StartSessionRequest
        {
            pcId = config?.pcId ?? "",
            tramiteId = tramiteId,
        };

        string json = JsonUtility.ToJson(requestBody);
        Debug.Log($"[SimulatorAPI] POST {url} body={json}");

        using (var request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = TIMEOUT_SECONDS;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<StartSessionResponse>(request.downloadHandler.text);
                    Debug.Log($"[SimulatorAPI] Sesión creada: {response.sessionId}");
                    onSessionId?.Invoke(response.sessionId);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[SimulatorAPI] Error parseando respuesta: {e.Message}");
                    onSessionId?.Invoke(null);
                }
            }
            else
            {
                Debug.LogWarning($"[SimulatorAPI] Error iniciando sesión: {request.error} ({request.responseCode})");
                onSessionId?.Invoke(null);
            }
        }
    }

    // ── API: Enviar resultados ───────────────────────────────────────

    /// <summary>
    /// POST /simulator/sessions/{sessionId}/results — envía resultados al backend.
    /// onComplete recibe true si fue exitoso, false si falló.
    /// </summary>
    public static IEnumerator EndSession(string sessionId, bool passed, int score,
        int distanceMeters, SessionFault[] faults, bool interrupted, System.Action<bool> onComplete)
    {
        string url = $"{BaseUrl}/simulator/sessions/{sessionId}/results";

        var requestBody = new EndSessionRequest
        {
            passed = passed,
            score = score,
            distanceMeters = distanceMeters,
            faults = faults,
            interrupted = interrupted
        };

        string json = JsonUtility.ToJson(requestBody);
        Debug.Log($"[SimulatorAPI] POST {url} score={score} passed={passed} distance={distanceMeters}m faults={faults?.Length ?? 0}");

        using (var request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = TIMEOUT_SECONDS;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[SimulatorAPI] Resultados enviados exitosamente para sesión {sessionId}");
                onComplete?.Invoke(true);
            }
            else
            {
                Debug.LogWarning($"[SimulatorAPI] Error enviando resultados: {request.error} ({request.responseCode})");
                onComplete?.Invoke(false);
            }
        }
    }

    // ── API: Enviar resultados de Práctica ───────────────────────────

    /// <summary>
    /// POST /simulator/practice-results — envía resultados de modo práctica.
    /// Lee identidad y configuración de GameManager.Instance.
    /// completed=true cuando los 3 min terminaron normales; false si interrumpido.
    /// onComplete recibe true si fue exitoso, false si falló.
    /// </summary>
    public static IEnumerator EndPracticeSession(int finalScore, int distanceMeters,
        SessionFault[] faults, bool completed, System.Action<bool> onComplete)
    {
        string url = $"{BaseUrl}/simulator/practice-results";
        var gm = GameManager.Instance;
        var config = SimulatorConfig.Instance?.data;

        int duration = completed ? 180 : 0;
        if (gm != null && gm.PracticeStartedAt != default(System.DateTime))
        {
            duration = Mathf.Max(0,
                Mathf.RoundToInt((float)(System.DateTime.UtcNow - gm.PracticeStartedAt).TotalSeconds));
        }

        var requestBody = new EndPracticeRequest
        {
            practiceId = gm?.PracticeId ?? "",
            pcId = config?.pcId ?? "",
            vehicleType = gm?.PracticeVehicleType ?? "",
            transmission = gm?.PracticeTransmission ?? "",
            weather = gm?.PracticeWeather ?? "Sol",
            spawnLocation = gm?.PracticeSpawnLocation ?? "random",
            startedAt = (gm?.PracticeStartedAt ?? System.DateTime.UtcNow).ToString("o"),
            completedAt = System.DateTime.UtcNow.ToString("o"),
            durationSeconds = completed ? 180 : duration,
            score = finalScore,
            distanceMeters = distanceMeters,
            faults = faults,
            completed = completed,
        };

        string json = JsonUtility.ToJson(requestBody);
        Debug.Log($"[SimulatorAPI] POST {url} score={finalScore} distance={distanceMeters}m vehicle={requestBody.vehicleType} completed={completed}");

        using (var request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = TIMEOUT_SECONDS;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[SimulatorAPI] Práctica enviada exitosamente ({requestBody.practiceId})");
                onComplete?.Invoke(true);
            }
            else
            {
                Debug.LogWarning($"[SimulatorAPI] Error enviando práctica: {request.error} ({request.responseCode})");
                onComplete?.Invoke(false);
            }
        }
    }

    // ── Mapeo de telemetría a faults ─────────────────────────────────

    /// <summary>
    /// Convierte eventos del TelemetryLogger al formato faults[] del backend.
    /// </summary>
    public static SessionFault[] BuildFaultsFromTelemetry()
    {
        if (TelemetryLogger.Instance == null) return new SessionFault[0];

        var events = TelemetryLogger.Instance.data.events;
        var faults = new SessionFault[events.Count];

        for (int i = 0; i < events.Count; i++)
        {
            var evt = events[i];
            faults[i] = new SessionFault
            {
                type = MapEventType(evt.eventType),
                description = evt.description,
                secondsFromStart = ParseSeconds(evt.timestamp),
                severity = MapSeverity(evt.eventType),
                deduction = Mathf.Abs(evt.points)
            };
        }

        return faults;
    }

    static string MapEventType(string eventType)
    {
        switch (eventType)
        {
            case "VELOCIDAD": return "speeding";
            case "ATROPELLO": return "pedestrian-hit";
            case "COLISION_BICICLETA": return "bicycle-collision";
            case "COLISION_VEHICULO": return "vehicle-collision";
            // NO renombrar vehicle-collision → active-vehicle-collision: trámites
            // históricos en DynamoDB ya tienen "vehicle-collision" como activa.
            case "COLISION_PASIVA": return "passive-vehicle-collision";
            case "COLISION_SENALAMIENTO": return "sign-collision";
            case "COLISION_OBSTACULO": return "obstacle-collision";
            case "COLISION": return "collision";
            case "SEMAFORO_ROJO": return "red-light";
            case "SENTIDO_CONTRARIO": return "wrong-way";
            case "CAMBIO_PELIGROSO": return "dangerous-gear-change";
            case "CAMBIO_SIN_CLUTCH": return "gear-change-without-clutch";
            case "FIN_EXAMEN": return "exam-end";
            // Razón principal del fail cuando el alumno se queda parado:
            // aparece en el feedback del trámite y en la pantalla de resultados.
            case "INVALIDO_INACTIVIDAD": return "inactivity-invalid";
            default: return eventType.ToLower();
        }
    }

    static string MapSeverity(string eventType)
    {
        switch (eventType)
        {
            case "ATROPELLO":
            // Inactividad reprueba directo aunque no descuente puntos —
            // tratamos como crítica para que destaque arriba del feedback.
            case "INVALIDO_INACTIVIDAD": return "critical";
            case "COLISION_BICICLETA":
            case "COLISION_VEHICULO":
            case "COLISION":
            case "SEMAFORO_ROJO":
            case "SENTIDO_CONTRARIO": return "major";
            // Pasiva: el alumno no la causó. La marcamos "info" para que el
            // portal admin la pinte distinto (badge azul, no rojo).
            case "COLISION_PASIVA": return "info";
            case "COLISION_SENALAMIENTO":
            case "COLISION_OBSTACULO":
            case "VELOCIDAD":
            case "CAMBIO_PELIGROSO":
            case "CAMBIO_SIN_CLUTCH": return "minor";
            default: return "minor";
        }
    }

    /// <summary>Parsea "45.23s" → 45</summary>
    static int ParseSeconds(string timestamp)
    {
        if (string.IsNullOrEmpty(timestamp)) return 0;
        string clean = timestamp.TrimEnd('s');
        if (float.TryParse(clean, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float seconds))
            return Mathf.RoundToInt(seconds);
        return 0;
    }

    // ── Resultados pendientes (offline resilience) ───────────────────

    private static string PendingPath =>
        Path.Combine(Application.persistentDataPath, "pending_results.json");

    /// <summary>Guarda un resultado fallido para retry posterior.</summary>
    public static void SavePendingResult(string sessionId, bool passed, int score,
        int distanceMeters, SessionFault[] faults, bool interrupted = false)
    {
        var list = LoadPendingResults();
        list.results.Add(new PendingResult
        {
            kind = "exam",
            sessionId = sessionId,
            passed = passed,
            score = score,
            distanceMeters = distanceMeters,
            faults = faults,
            savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            interrupted = interrupted
        });

        string json = JsonUtility.ToJson(list, true);
        File.WriteAllText(PendingPath, json);
        cachedPendingCount = list.results.Count;
        Debug.Log($"[SimulatorAPI] Resultado pendiente guardado ({cachedPendingCount} total)");
    }

    /// <summary>Guarda una práctica fallida para retry posterior.</summary>
    public static void SavePendingPracticeResult(int score, int distanceMeters,
        SessionFault[] faults, bool completed)
    {
        var gm = GameManager.Instance;
        var config = SimulatorConfig.Instance?.data;
        int duration = completed ? 180 : 0;
        if (gm != null && gm.PracticeStartedAt != default(System.DateTime))
        {
            duration = Mathf.Max(0,
                Mathf.RoundToInt((float)(System.DateTime.UtcNow - gm.PracticeStartedAt).TotalSeconds));
        }

        var list = LoadPendingResults();
        list.results.Add(new PendingResult
        {
            kind = "practice",
            score = score,
            distanceMeters = distanceMeters,
            faults = faults,
            savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            practiceId = gm?.PracticeId ?? System.Guid.NewGuid().ToString(),
            pcId = config?.pcId ?? "",
            vehicleType = gm?.PracticeVehicleType ?? "",
            transmission = gm?.PracticeTransmission ?? "",
            weather = gm?.PracticeWeather ?? "Sol",
            spawnLocation = gm?.PracticeSpawnLocation ?? "random",
            startedAt = (gm?.PracticeStartedAt ?? System.DateTime.UtcNow).ToString("o"),
            completedAt = System.DateTime.UtcNow.ToString("o"),
            durationSeconds = completed ? 180 : duration,
            completed = completed,
        });

        string json = JsonUtility.ToJson(list, true);
        File.WriteAllText(PendingPath, json);
        cachedPendingCount = list.results.Count;
        Debug.Log($"[SimulatorAPI] Práctica pendiente guardada ({cachedPendingCount} total)");
    }

    public static PendingResultsList LoadPendingResults()
    {
        if (!File.Exists(PendingPath)) return new PendingResultsList();
        try
        {
            string json = File.ReadAllText(PendingPath);
            return JsonUtility.FromJson<PendingResultsList>(json) ?? new PendingResultsList();
        }
        catch
        {
            return new PendingResultsList();
        }
    }

    public static void ClearPendingResults()
    {
        if (File.Exists(PendingPath)) File.Delete(PendingPath);
        cachedPendingCount = 0;
    }

    /// <summary>
    /// Reintenta enviar todos los resultados pendientes.
    /// Llamar desde MainMenu al cargar.
    /// </summary>
    public static IEnumerator RetryPendingResults()
    {
        var list = LoadPendingResults();
        if (list.results.Count == 0) yield break;

        Debug.Log($"[SimulatorAPI] Reintentando {list.results.Count} resultados pendientes...");

        var remaining = new List<PendingResult>();

        foreach (var pending in list.results)
        {
            bool success = false;
            // Discriminador kind: "exam" (default cuando vacío, archivos antiguos) o "practice".
            if (pending.kind == "practice")
            {
                yield return EndPracticeSessionFromPending(pending, (ok) => success = ok);
                if (success) Debug.Log($"[SimulatorAPI] Práctica pendiente {pending.practiceId} enviada");
            }
            else
            {
                yield return EndSession(pending.sessionId, pending.passed, pending.score,
                    pending.distanceMeters, pending.faults, pending.interrupted, (ok) => success = ok);
                if (success) Debug.Log($"[SimulatorAPI] Resultado pendiente {pending.sessionId} enviado");
            }

            if (!success) remaining.Add(pending);
        }

        if (remaining.Count == 0)
        {
            ClearPendingResults();
            Debug.Log("[SimulatorAPI] Todos los resultados pendientes enviados");
        }
        else
        {
            var newList = new PendingResultsList { results = remaining };
            File.WriteAllText(PendingPath, JsonUtility.ToJson(newList, true));
            cachedPendingCount = remaining.Count;
            Debug.Log($"[SimulatorAPI] Quedan {remaining.Count} resultados pendientes");
        }
    }

    /// <summary>
    /// Reintenta una práctica pendiente leyendo todos los datos del PendingResult
    /// (no depende de GameManager.Instance, que en este punto ya se reseteó).
    /// </summary>
    private static IEnumerator EndPracticeSessionFromPending(PendingResult p, System.Action<bool> onComplete)
    {
        string url = $"{BaseUrl}/simulator/practice-results";
        var requestBody = new EndPracticeRequest
        {
            practiceId = p.practiceId,
            pcId = p.pcId,
            vehicleType = p.vehicleType,
            transmission = p.transmission ?? "",
            weather = p.weather,
            spawnLocation = p.spawnLocation,
            startedAt = p.startedAt,
            completedAt = p.completedAt,
            durationSeconds = p.durationSeconds,
            score = p.score,
            distanceMeters = p.distanceMeters,
            faults = p.faults,
            completed = p.completed,
        };
        string json = JsonUtility.ToJson(requestBody);

        using (var request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = TIMEOUT_SECONDS;
            yield return request.SendWebRequest();
            onComplete?.Invoke(request.result == UnityWebRequest.Result.Success);
        }
    }

    /// <summary>Cantidad de resultados sin enviar (cacheado, sin file I/O).</summary>
    private static int cachedPendingCount = -1;
    public static int PendingCount
    {
        get
        {
            if (cachedPendingCount < 0)
                cachedPendingCount = LoadPendingResults().results.Count;
            return cachedPendingCount;
        }
    }

    // ── Heartbeat (cada 3 minutos) ─────────────────────────────────

    [System.Serializable]
    private class HeartbeatRequest
    {
        public string pcId;
        // appVersion es Application.version del binario corriendo. El backend lo
        // usa como evidencia LIVE para auto-confirmar INSTALLED cuando matchea
        // pendingUpdate.version (=> install exitoso, limpia pendingUpdate). Sin
        // este campo el backend no tiene forma honesta de detectar éxito de un
        // install OTA porque el cliente nunca llama explícitamente
        // ReportUpdateStatus("INSTALLED").
        public string appVersion;
        public CalibrationPayload calibration;
    }

    // Subset de PlayerPrefs que el portal admin necesita para visualizar la
    // calibración del volante/pedales. Sólo se rellenan campos con valor real;
    // backend hace whitelist y descarta lo demás.
    [System.Serializable]
    public class CalibrationPayload
    {
        public string deviceFingerprint;
        public bool reverseDone;
        // Bindings (BindingsPanel F8 + Pantalla 2)
        public string bindSteerAxis;
        public string bindReverse;
        public string bindDrive;
        public string bindPaddleLeft;
        public string bindPaddleRight;
        // Volante (Pantalla 2)
        public float steerCenter;
        public float steerMax;
        public float steerMin;
        // Pedales (Pantalla 2)
        public string gasAxis;
        public float gasRest;
        public float gasPress;
        public string brakeAxis;
        public float brakeRest;
        public float brakePress;
        // Tuning runtime (AdvancedInputPanel F9)
        public float advSteerCurveA;
        public float advSteerDeadzone;
        public float advBrakeSoftEnd;
        public float advBrakeSoftMaxOutput;
        public float advGasCurveN;
        // Misc
        public bool transmisionManual;
    }

    private static CalibrationPayload BuildCalibrationPayload()
    {
        return new CalibrationPayload
        {
            deviceFingerprint    = PlayerPrefs.GetString("Cal_DeviceFingerprint", ""),
            reverseDone          = PlayerPrefs.GetInt("Cal_ReverseDone", 0) == 1,
            bindSteerAxis        = PlayerPrefs.GetString("Bind_steerAxis", ""),
            bindReverse          = PlayerPrefs.GetString("Bind_reverse", ""),
            bindDrive            = PlayerPrefs.GetString("Bind_drive", ""),
            bindPaddleLeft       = PlayerPrefs.GetString("Bind_paddleLeft", ""),
            bindPaddleRight      = PlayerPrefs.GetString("Bind_paddleRight", ""),
            steerCenter          = PlayerPrefs.GetFloat("G923_SteerCenter", 0f),
            steerMax             = PlayerPrefs.GetFloat("G923_SteerMax", 0f),
            steerMin             = PlayerPrefs.GetFloat("G923_SteerMin", 0f),
            gasAxis              = PlayerPrefs.GetString("G923_GasAxis", ""),
            gasRest              = PlayerPrefs.GetFloat("G923_GasRest", 0f),
            gasPress             = PlayerPrefs.GetFloat("G923_GasPress", 0f),
            brakeAxis            = PlayerPrefs.GetString("G923_BrakeAxis", ""),
            brakeRest            = PlayerPrefs.GetFloat("G923_BrakeRest", 0f),
            brakePress           = PlayerPrefs.GetFloat("G923_BrakePress", 0f),
            advSteerCurveA       = PlayerPrefs.GetFloat("Adv_SteerCurveA", 1f),
            advSteerDeadzone     = PlayerPrefs.GetFloat("Adv_SteerDeadzone", 0.02f),
            advBrakeSoftEnd      = PlayerPrefs.GetFloat("Adv_BrakeSoftEnd", 0.8f),
            advBrakeSoftMaxOutput = PlayerPrefs.GetFloat("Adv_BrakeSoftMaxOutput", 0.3f),
            advGasCurveN         = PlayerPrefs.GetFloat("Adv_GasCurveN", 1f),
            transmisionManual    = PlayerPrefs.GetInt("TransmisionManual", 0) == 1,
        };
    }

    [System.Serializable]
    private class HeartbeatResponse
    {
        public bool ok;
        public string name;
        public PendingConfig pendingConfig;
        public PendingUpdateData pendingUpdate;
    }

    [System.Serializable]
    private class RegisterResponse
    {
        public bool success;
        public string pcId;
        public string name;
        public string simulatorId;
        public string simulatorName;
        public bool created;
    }

    [System.Serializable]
    private class PendingConfig
    {
        public string apiBaseUrl;
        public string environment;
    }

    [System.Serializable]
    public class PendingUpdateData
    {
        public string version;
        public string sha256;
        public long size;
        public string releaseNotes;
        public bool mandatory;
        public string scheduledAfter;
        public string status;
    }

    [System.Serializable]
    public class UpdateUrlResponse
    {
        public string downloadUrl;
        public string sha256;
        public long size;
        public string version;
    }

    /// <summary>
    /// POST /simulator/heartbeat — actualiza lastSeen y recibe config remota.
    /// Llamado periódicamente por GameManager.
    /// </summary>
    public static IEnumerator SendHeartbeat()
    {
        var config = SimulatorConfig.Instance?.data;
        if (config == null || string.IsNullOrEmpty(config.pcId)) yield break;

        string url = $"{BaseUrl}/simulator/heartbeat";
        string json = JsonUtility.ToJson(new HeartbeatRequest
        {
            pcId = config.pcId,
            appVersion = UnityEngine.Application.version,
            calibration = BuildCalibrationPayload(),
        });

        using (var request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[SimulatorAPI] Heartbeat failed: {request.error}");
                yield break;
            }

            // Check for pending config (environment promotion)
            string newRegUrl = null;
            string newRegJson = null;
            string newEnv = null;

            try
            {
                var response = JsonUtility.FromJson<HeartbeatResponse>(request.downloadHandler.text);

                // Sincronizar name canónico desde backend (ver register/PATCH admin).
                // Backend es source-of-truth — Unity sólo refleja.
                if (response != null && !string.IsNullOrEmpty(response.name) && response.name != config.name)
                {
                    Debug.Log($"[SimulatorAPI] Nombre PC sincronizado: {config.name} → {response.name}");
                    config.name = response.name;
                    SimulatorConfig.Instance.Save();
                }

                // Procesar pending update si existe
                if (response?.pendingUpdate != null && !string.IsNullOrEmpty(response.pendingUpdate.version))
                {
                    if (AutoUpdater.Instance != null)
                        AutoUpdater.Instance.ProcessPendingUpdate(response.pendingUpdate);
                }

                if (response?.pendingConfig != null && !string.IsNullOrEmpty(response.pendingConfig.apiBaseUrl))
                {
                    Debug.Log($"[SimulatorAPI] Ambiente cambiado a {response.pendingConfig.environment}: {response.pendingConfig.apiBaseUrl}");
                    config.apiBaseUrl = response.pendingConfig.apiBaseUrl;
                    SimulatorConfig.Instance.Save();

                    newEnv = response.pendingConfig.environment;
                    newRegUrl = $"{config.apiBaseUrl}/simulator/register";
                    // Re-register tras cambio de ambiente: NO mandar name. Backend
                    // usa if_not_exists para preservar el name actual (que en el
                    // ambiente nuevo puede no existir todavía → seed con default).
                    newRegJson = JsonUtility.ToJson(new RegisterRequest
                    {
                        pcId = config.pcId,
                        name = "",
                        appVersion = UnityEngine.Application.version,
                        platform = "windows"
                    });
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SimulatorAPI] Error parseando heartbeat: {e.Message}");
            }

            // Re-register fuera del try-catch (yield no permitido dentro de try-catch)
            if (newRegUrl != null)
            {
                using (var regReq = new UnityWebRequest(newRegUrl, "POST"))
                {
                    regReq.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(newRegJson));
                    regReq.downloadHandler = new DownloadHandlerBuffer();
                    regReq.SetRequestHeader("Content-Type", "application/json");
                    regReq.timeout = 10;
                    yield return regReq.SendWebRequest();

                    if (regReq.result == UnityWebRequest.Result.Success)
                        Debug.Log($"[SimulatorAPI] Registrado en {newEnv}");
                    else
                        Debug.LogWarning($"[SimulatorAPI] Error registrando en nuevo ambiente: {regReq.error}");
                }
            }
        }
    }

    [System.Serializable]
    private class RegisterRequest
    {
        public string pcId;
        public string name;
        public string appVersion;
        public string platform;
    }

    /// <summary>
    /// POST /simulator/register al arranque, SIN mandar name. El backend crea
    /// el record si no existe (con un name default derivado del pcId) y
    /// preserva el actual si ya existe (no pisa renames hechos desde el portal
    /// admin). El name canónico viene en la response y se sincroniza local.
    /// </summary>
    public static IEnumerator SendBootRegister()
    {
        var config = SimulatorConfig.Instance?.data;
        if (config == null || string.IsNullOrEmpty(config.pcId)) yield break;

        string url = $"{BaseUrl}/simulator/register";
        string json = JsonUtility.ToJson(new RegisterRequest
        {
            pcId = config.pcId,
            // name vacío → backend usa if_not_exists. NO autoridad sobre el
            // name desde un boot — sólo F10 explícito y PATCH admin lo cambian.
            name = "",
            appVersion = UnityEngine.Application.version,
            platform = "windows",
        });

        using (var request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[SimulatorAPI] Boot register failed: {request.error}");
                yield break;
            }

            try
            {
                var response = JsonUtility.FromJson<RegisterResponse>(request.downloadHandler.text);
                if (response != null)
                {
                    bool changed = false;
                    if (!string.IsNullOrEmpty(response.name) && response.name != config.name)
                    {
                        Debug.Log($"[SimulatorAPI] Nombre PC sincronizado al boot: {config.name} → {response.name}");
                        config.name = response.name;
                        changed = true;
                    }
                    if (!string.IsNullOrEmpty(response.simulatorId) && response.simulatorId != config.simulatorId)
                    {
                        config.simulatorId = response.simulatorId;
                        config.simulatorName = response.simulatorName ?? "";
                        changed = true;
                    }
                    if (changed) SimulatorConfig.Instance.Save();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SimulatorAPI] Error parseando boot register response: {e.Message}");
            }
        }
    }

    // ── API: Solicitar URL de descarga de update ──────────────────────

    [System.Serializable]
    private class RequestUpdateRequest
    {
        public string pcId;
        public string version;
    }

    /// <summary>
    /// POST /simulator/request-update — solicita presigned URL para descargar el build.
    /// </summary>
    public static IEnumerator RequestUpdateUrl(string version, System.Action<UpdateUrlResponse> onResult)
    {
        var config = SimulatorConfig.Instance?.data;
        if (config == null || string.IsNullOrEmpty(config.pcId))
        {
            onResult?.Invoke(null);
            yield break;
        }

        string url = $"{BaseUrl}/simulator/request-update";
        string json = JsonUtility.ToJson(new RequestUpdateRequest
        {
            pcId = config.pcId,
            version = version,
        });

        Debug.Log($"[SimulatorAPI] POST {url} version={version}");

        using (var request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 15;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<UpdateUrlResponse>(request.downloadHandler.text);
                    Debug.Log($"[SimulatorAPI] URL de descarga recibida para v{response.version}");
                    onResult?.Invoke(response);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[SimulatorAPI] Error parseando request-update: {e.Message}");
                    onResult?.Invoke(null);
                }
            }
            else
            {
                Debug.LogWarning($"[SimulatorAPI] Error request-update: {request.error} ({request.responseCode})");
                onResult?.Invoke(null);
            }
        }
    }

    // ── API: Reportar status de update ────────────────────────────────

    [System.Serializable]
    private class UpdateStatusRequest
    {
        public string pcId;
        public string status;
        public string error;
        public string version;
    }

    /// <summary>
    /// POST /simulator/update-status — reporta progreso de la actualización.
    /// </summary>
    public static IEnumerator ReportUpdateStatus(string status, string error, string version, System.Action<bool> onComplete)
    {
        var config = SimulatorConfig.Instance?.data;
        if (config == null || string.IsNullOrEmpty(config.pcId)) yield break;

        string url = $"{BaseUrl}/simulator/update-status";
        string json = JsonUtility.ToJson(new UpdateStatusRequest
        {
            pcId = config.pcId,
            status = status,
            error = error ?? "",
            version = version ?? "",
        });

        Debug.Log($"[SimulatorAPI] POST {url} status={status}");

        using (var request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            yield return request.SendWebRequest();

            bool success = request.result == UnityWebRequest.Result.Success;
            if (!success)
            {
                Debug.LogWarning($"[SimulatorAPI] Error update-status: {request.error}");
            }
            onComplete?.Invoke(success);
        }
    }
}
