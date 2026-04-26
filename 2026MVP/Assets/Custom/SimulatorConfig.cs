using UnityEngine;
using System.IO;

/// <summary>
/// Configuración local del simulador — persistida en simulator_config.json.
/// Cargado al inicio, editable desde el AdminPanel (F10).
/// </summary>
public class SimulatorConfig : MonoBehaviour
{
    public static SimulatorConfig Instance { get; private set; }

    [System.Serializable]
    public class ConfigData
    {
        public string pcId = "";           // auto-generado (SystemInfo.deviceUniqueIdentifier), inmutable
        public string name = "";           // configurable por el usuario
        public string apiBaseUrl = "https://d6twaegbhg.execute-api.us-east-1.amazonaws.com";
        public string simulatorId = "";    // devuelto por el backend al registrar
        public string simulatorName = "";  // nombre del simulador asignado
        public int displayCount = 3;       // 1 = modo prueba (1 monitor), 3 = producción (3 monitores)
    }

    public ConfigData data = new ConfigData();

    private string ConfigPath => Path.Combine(Application.persistentDataPath, "simulator_config.json");

    /// <summary>True si la PC ya fue configurada con un nombre.</summary>
    public bool IsConfigured => !string.IsNullOrEmpty(data.name);

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();

            // Auto-generar pcId si está vacío (primera ejecución)
            if (string.IsNullOrEmpty(data.pcId))
            {
                data.pcId = SystemInfo.deviceUniqueIdentifier;
                Debug.Log($"[SimulatorConfig] pcId generado: {data.pcId}");
                Save();
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Load()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                string json = File.ReadAllText(ConfigPath);
                data = JsonUtility.FromJson<ConfigData>(json);
                Debug.Log($"[SimulatorConfig] Cargado: pcId={data.pcId}, name={data.name}, simulatorId={data.simulatorId}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SimulatorConfig] Error leyendo config: {e.Message}");
                data = new ConfigData();
            }
        }
        else
        {
            Debug.Log("[SimulatorConfig] No existe config, creando defaults");
            data = new ConfigData();
            Save();
        }
    }

    public void Save()
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(ConfigPath, json);
            Debug.Log($"[SimulatorConfig] Guardado en: {ConfigPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SimulatorConfig] Error guardando config: {e.Message}");
        }
    }
}
