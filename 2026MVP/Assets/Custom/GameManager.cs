using UnityEngine;

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
        // Cargar thingName desde config local
        if (SimulatorConfig.Instance != null)
            ThingName = SimulatorConfig.Instance.data.thingName;
        else
            ThingName = "sim-pc-unconfigured";

        // Reintentar resultados pendientes al iniciar
        StartCoroutine(SimulatorApiClient.RetryPendingResults());
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
    }

    /// <summary>Limpia datos de sesión para el siguiente ciudadano.</summary>
    public void ClearSession()
    {
        Expediente = null;
        TramiteId = null;
        CitizenName = null;
        LicenseType = null;
        SessionId = null;
    }
}
