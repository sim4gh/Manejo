using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;

/// <summary>
/// Configuración de scoring descargada del backend.
/// Singleton, persistida localmente como cache para offline.
/// </summary>
public class ScoringConfig : MonoBehaviour
{
    public static ScoringConfig Instance { get; private set; }

    [System.Serializable]
    public class Penalties
    {
        public int speeding = 5;
        public int pedestrianHit = 25;
        public int bicycleCollision = 15;
        public int vehicleCollision = 10;
        // Colisión vehicular pasiva: NPC embistió al alumno por atrás.
        // Default 0 (no penaliza, solo se registra). El examinador puede
        // subirlo desde /admin/scoring sin redeploy si en producción se
        // decide cobrar manejo defensivo pobre.
        public int passiveVehicleCollision = 0;
        public int signCollision = 5;
        public int obstacleCollision = 5;
        public int redLight = 20;
        public int wrongWay = 15;
        public int dangerousGearChange = 20;
        // Rechino: cambio de marcha sin clutch presionado (manual + G923 PS).
        // Default 5 — coherente con velocidad/colisión menor (codex: si no hay
        // cooldown sería alto; con cooldown 3s en VD es defendible).
        public int gearChangeWithoutClutch = 5;
    }

    [System.Serializable]
    public class GradeThresholds
    {
        public int apto = 90;
        public int aptoCondicionado = 80;
        public int aptoReentrenamiento = 70;
    }

    [System.Serializable]
    public class ScoringData
    {
        public Penalties penalties = new Penalties();
        public int passingScore = 70;
        public GradeThresholds gradeThresholds = new GradeThresholds();
        public int examDurationSeconds = 300;
        // Distancia mínima (metros) que el alumno debe recorrer durante el examen
        // para que la sesión sea válida. Si recorre menos, ExamTimer fuerza
        // passed=false sin importar el score (evita que dejar el coche parado
        // los 3-5 min sea aprobar con 100). Configurable desde /admin/scoring.
        public int minValidDistanceMeters = 200;
        public string updatedAt = "";
        public string lastSyncedAt = "";
    }

    public ScoringData data = new ScoringData();
    public bool isLoaded { get; private set; }

    private string CachePath => Path.Combine(Application.persistentDataPath, "scoring_config.json");

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadCache();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Descarga scoring config del backend. Usar con yield return.
    /// </summary>
    public IEnumerator LoadFromBackend()
    {
        string baseUrl = SimulatorConfig.Instance?.data.apiBaseUrl
            ?? "https://d6twaegbhg.execute-api.us-east-1.amazonaws.com";
        string url = $"{baseUrl}/simulator/scoring-config";

        Debug.Log($"[ScoringConfig] GET {url}");

        using (var request = UnityWebRequest.Get(url))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    data = JsonUtility.FromJson<ScoringData>(request.downloadHandler.text);
                    data.lastSyncedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    isLoaded = true;
                    SaveCache();
                    Debug.Log($"[ScoringConfig] Cargado del backend: passingScore={data.passingScore}");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[ScoringConfig] Error parseando respuesta: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[ScoringConfig] Error descargando config: {request.error}");
                // Usar cache si existe
                if (!isLoaded) LoadCache();
            }
        }
    }

    void LoadCache()
    {
        if (File.Exists(CachePath))
        {
            try
            {
                string json = File.ReadAllText(CachePath);
                data = JsonUtility.FromJson<ScoringData>(json);
                isLoaded = true;
                Debug.Log($"[ScoringConfig] Cargado de cache: passingScore={data.passingScore}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ScoringConfig] Error leyendo cache: {e.Message}");
                data = new ScoringData();
            }
        }
        else
        {
            Debug.Log("[ScoringConfig] Sin cache, usando defaults");
            data = new ScoringData();
            isLoaded = true;
        }
    }

    void SaveCache()
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(CachePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ScoringConfig] Error guardando cache: {e.Message}");
        }
    }

    /// <summary>
    /// Aplica la configuración actual a todos los detectores de infracciones.
    /// Llamar después de cargar la escena del examen.
    /// </summary>
    public void ApplyToDetectors()
    {
        // ViolationDetector
        var vd = Object.FindFirstObjectByType<ViolationDetector>();
        if (vd != null)
        {
            vd.speedingPenalty = data.penalties.speeding;
            vd.pedestrianPenalty = data.penalties.pedestrianHit;
            vd.bicycleCollisionPenalty = data.penalties.bicycleCollision;
            vd.vehicleCollisionPenalty = data.penalties.vehicleCollision;
            vd.passiveVehicleCollisionPenalty = data.penalties.passiveVehicleCollision;
            vd.signCollisionPenalty = data.penalties.signCollision;
            vd.obstacleCollisionPenalty = data.penalties.obstacleCollision;
            vd.dangerousGearChangePenalty = data.penalties.dangerousGearChange;
            vd.gearChangeWithoutClutchPenalty = data.penalties.gearChangeWithoutClutch;
            Debug.Log("[ScoringConfig] Aplicado a ViolationDetector");
        }

        // RedLightDetector
        var rld = Object.FindFirstObjectByType<RedLightDetector>();
        if (rld != null)
        {
            rld.redLightPenalty = data.penalties.redLight;
            Debug.Log("[ScoringConfig] Aplicado a RedLightDetector");
        }

        // WrongWayDetector
        var wwd = Object.FindFirstObjectByType<WrongWayDetector>();
        if (wwd != null)
        {
            wwd.wrongWayPenalty = data.penalties.wrongWay;
            Debug.Log("[ScoringConfig] Aplicado a WrongWayDetector");
        }

        // ExamTimer
        var timer = ExamTimer.Instance;
        if (timer != null)
        {
            timer.examDuration = data.examDurationSeconds;
            Debug.Log($"[ScoringConfig] Duración del examen: {data.examDurationSeconds}s");
        }
    }
}
