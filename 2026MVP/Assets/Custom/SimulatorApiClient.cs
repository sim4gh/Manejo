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
        public string tramiteId;
        public string thingName;
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
        public SessionFault[] faults;
    }

    [System.Serializable]
    public class EndSessionResponse
    {
        public bool success;
        public string sessionId;
    }

    // ── Resultado pendiente (para retry offline) ─────────────────────

    [System.Serializable]
    public class PendingResult
    {
        public string sessionId;
        public bool passed;
        public int score;
        public SessionFault[] faults;
        public string savedAt;
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
    public static IEnumerator StartSession(string tramiteId, string thingName,
        System.Action<string> onSessionId)
    {
        string url = $"{BaseUrl}/simulator/sessions";

        var requestBody = new StartSessionRequest
        {
            tramiteId = tramiteId,
            thingName = thingName
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
        SessionFault[] faults, System.Action<bool> onComplete)
    {
        string url = $"{BaseUrl}/simulator/sessions/{sessionId}/results";

        var requestBody = new EndSessionRequest
        {
            passed = passed,
            score = score,
            faults = faults
        };

        string json = JsonUtility.ToJson(requestBody);
        Debug.Log($"[SimulatorAPI] POST {url} score={score} passed={passed} faults={faults?.Length ?? 0}");

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
            case "COLISION": return "collision";
            case "ATROPELLO": return "pedestrian-hit";
            case "SEMAFORO_ROJO": return "red-light";
            case "SENTIDO_CONTRARIO": return "wrong-way";
            case "FIN_EXAMEN": return "exam-end";
            default: return eventType.ToLower();
        }
    }

    static string MapSeverity(string eventType)
    {
        switch (eventType)
        {
            case "ATROPELLO": return "critical";
            case "SEMAFORO_ROJO":
            case "SENTIDO_CONTRARIO":
            case "COLISION": return "major";
            case "VELOCIDAD": return "minor";
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
    public static void SavePendingResult(string sessionId, bool passed, int score, SessionFault[] faults)
    {
        var list = LoadPendingResults();
        list.results.Add(new PendingResult
        {
            sessionId = sessionId,
            passed = passed,
            score = score,
            faults = faults,
            savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });

        string json = JsonUtility.ToJson(list, true);
        File.WriteAllText(PendingPath, json);
        cachedPendingCount = list.results.Count;
        Debug.Log($"[SimulatorAPI] Resultado pendiente guardado ({cachedPendingCount} total)");
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
            yield return EndSession(pending.sessionId, pending.passed, pending.score,
                pending.faults, (ok) => success = ok);

            if (!success)
                remaining.Add(pending);
            else
                Debug.Log($"[SimulatorAPI] Resultado pendiente {pending.sessionId} enviado");
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
}
