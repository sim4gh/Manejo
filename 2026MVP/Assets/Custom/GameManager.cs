using System;
using UnityEngine;
using System.Collections;

/// <summary>
/// Singleton GameManager that persists across scenes.
/// Stores the expediente number and other session data.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>
    /// The expediente number entered by the user in the main menu.
    /// Kept for backwards compatibility — same as TramiteId.
    /// </summary>
    public string Expediente { get; set; }

    // ── Datos de sesión (poblados por MenuScreenManager) ─────────────
    public string TramiteId { get; set; }
    public string CitizenName { get; set; }
    public string LicenseType { get; set; }

    /// <summary>ID de sesión del backend (de POST /simulator/sessions).</summary>
    public string SessionId { get; set; }

    /// <summary>Identidad del PC (de SimulatorConfig).</summary>
    public string ThingName { get; set; }

    /// <summary>ID del simulador asignado (de backend).</summary>
    public string SimulatorId { get; set; }

    /// <summary>0 = aleatorio entre 1..5; 1..5 = waypoint fijo. Default 1 (legacy).</summary>
    public int LocationId { get; set; } = 1;

    // ── Modo Práctica (no cuenta como examen real) ───────────────────
    public bool IsPracticeMode { get; set; }
    public string PracticeId { get; set; }
    public string PracticeVehicleType { get; set; }       // "Sedan", "Camioneta", "BusPasajeros", "CamionDCarga", "Motocicleta", "Ambulancia"
    public string PracticeTransmission { get; set; }      // "Manual"|"Automatica"|null
    public string PracticeWeather { get; set; }           // "Sol"|"Lluvia"|"Granizo"
    public string PracticeSpawnLocation { get; set; }     // "1".."5"|"random"
    public DateTime PracticeStartedAt { get; set; }       // Seteado por ExamTimer.Start() (no por el menú) para no inflar el tiempo

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureSingletons();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Cargar identidad desde config local
        if (SimulatorConfig.Instance != null)
        {
            ThingName = SimulatorConfig.Instance.data.pcId;
            SimulatorId = SimulatorConfig.Instance.data.simulatorId;
        }
        else
        {
            ThingName = "sim-pc-unconfigured";
        }

        // Reintentar resultados pendientes al iniciar
        StartCoroutine(SimulatorApiClient.RetryPendingResults());

        // Heartbeat cada 3 minutos
        StartCoroutine(HeartbeatLoop());
    }

    IEnumerator HeartbeatLoop()
    {
        // Boot register: crea el record en backend si no existe (con name
        // default derivado del pcId) y sincroniza el name canónico desde el
        // backend. Sin esto, una PC nueva nunca aparecería en el portal hasta
        // que un operador abriera F10. NO manda name — el backend usa
        // if_not_exists para no pisar renames hechos desde el portal admin.
        yield return StartCoroutine(SimulatorApiClient.SendBootRegister());

        // Primer heartbeat inmediato
        yield return StartCoroutine(SimulatorApiClient.SendHeartbeat());

        while (true)
        {
            yield return new WaitForSecondsRealtime(180f); // 3 minutos
            yield return StartCoroutine(SimulatorApiClient.SendHeartbeat());
        }
    }

    /// <summary>Asegura que SimulatorConfig y AdminPanel existan.</summary>
    void EnsureSingletons()
    {
        if (SimulatorConfig.Instance == null)
        {
            var configObj = new GameObject("SimulatorConfig");
            configObj.AddComponent<SimulatorConfig>();
        }
        if (AdminPanel.Instance == null)
        {
            var adminObj = new GameObject("AdminPanel");
            adminObj.AddComponent<AdminPanel>();
        }
        if (AutoUpdater.Instance == null)
        {
            var updaterObj = new GameObject("AutoUpdater");
            updaterObj.AddComponent<AutoUpdater>();
        }
        if (ScoringConfig.Instance == null)
        {
            var scoringObj = new GameObject("ScoringConfig");
            scoringObj.AddComponent<ScoringConfig>();
        }
    }

    /// <summary>
    /// True si la sesión actual es un examen de vehículo de emergencia (ambulancia).
    /// Por la Ley de Movilidad y Seguridad Vial del Estado de Tlaxcala (2024), los
    /// vehículos de emergencia en servicio están exentos de penalizaciones por
    /// exceso de velocidad, cruce de semáforo en rojo y conducción en sentido
    /// contrario. Las colisiones (peatón, vehículo, etc.) y errores de control
    /// vehicular (cambio de marcha peligroso, sin clutch) SIGUEN aplicando — la
    /// exención no excusa negligencia.
    /// Cubre tanto el flujo de trámite (LicenseType=="emergencia") como el de
    /// práctica (PracticeVehicleType=="Ambulancia").
    /// </summary>
    public static bool IsEmergencyExam()
    {
        if (Instance == null) return false;
        var lic = (Instance.LicenseType ?? "").Trim();
        if (string.Equals(lic, "emergencia", StringComparison.OrdinalIgnoreCase))
            return true;
        var practice = (Instance.PracticeVehicleType ?? "").Trim();
        if (string.Equals(practice, "Ambulancia", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    /// <summary>Limpia datos de sesión para el siguiente ciudadano.</summary>
    public void ClearSession()
    {
        Expediente = null;
        TramiteId = null;
        CitizenName = null;
        LicenseType = null;
        SessionId = null;
        LocationId = 1;

        IsPracticeMode = false;
        PracticeId = null;
        PracticeVehicleType = null;
        PracticeTransmission = null;
        PracticeWeather = null;
        PracticeSpawnLocation = null;
        PracticeStartedAt = default;
    }
}
