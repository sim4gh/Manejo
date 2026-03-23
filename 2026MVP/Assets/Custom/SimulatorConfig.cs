using UnityEngine;
using System.IO;

/// <summary>
/// Configuración local del simulador — persistida en simulator_config.json.
/// Cargado al inicio, editable desde el AdminPanel.
/// </summary>
public class SimulatorConfig : MonoBehaviour
{
    public static SimulatorConfig Instance { get; private set; }

    [System.Serializable]
    public class SerialNumbers
    {
        public string frame = "";
        public string seat = "";
        public string computer = "";
        public string dofController = "";
        public string wheel = "";
    }

    [System.Serializable]
    public class ConfigData
    {
        public string stationId = "";
        public string thingName = "sim-pc-unconfigured";
        public string apiBaseUrl = "https://d6twaegbhg.execute-api.us-east-1.amazonaws.com";
        public string adminPin = "202626";
        public SerialNumbers serialNumbers = new SerialNumbers();
        public bool autoUpdate = true;
    }

    public ConfigData data = new ConfigData();

    private string ConfigPath => Path.Combine(Application.persistentDataPath, "simulator_config.json");

    /// <summary>True si el simulador ya fue configurado con un stationId.</summary>
    public bool IsConfigured => !string.IsNullOrEmpty(data.stationId);

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
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
                Debug.Log($"[SimulatorConfig] Cargado: stationId={data.stationId}, thingName={data.thingName}");
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
