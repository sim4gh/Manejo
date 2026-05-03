using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Gley.UrbanSystem;
using TrafficAPI = Gley.TrafficSystem.API;
using TrafficWaypoint = Gley.TrafficSystem.TrafficWaypoint;

/// <summary>
/// Teleporta el vehículo del jugador a uno de 5 waypoints predefinidos al cargar
/// una escena de manejo, según GameManager.LocationId (1..5; 0 = aleatorio).
///
/// Reusa la red de waypoints de Gley TrafficSystem ya cargada en cada escena —
/// no requiere editar el .unity ni colocar GameObjects manualmente.
/// </summary>
public static class SpawnLocationManager
{
    static bool subscribed;

    // Índices distribuidos en la red de waypoints de Gley UrbanExample.
    // Para refinar un slot: maneja a la zona deseada y presiona [K] — log
    // en F7 imprime el índice del waypoint más cercano al Player. Cambia
    // el entero correspondiente y rebuildea.
    //   slot 1 = 11111 (legacy)                  slot 2 = 11112 (zona 2)
    //   slot 3 = 11113 (antes de glorieta)       slot 4 = 11114
    //   slot 5 = 11115
    // -1 = sin asignar (placeholder). El runner deja el spawn original cuando
    // el slot vale -1 — así un código demo con sufijo TBD no manda al jugador
    // a un waypoint arbitrario (especialmente el índice 0, que es válido en
    // Gley pero rara vez es la zona deseada).
    const int UNASSIGNED = -1;
    static readonly int[] DEFAULT_WAYPOINTS = { 4588, 4588, 3170, 6765, 3016 };

    // Override por escena solo si el mapa Gley difiere (ej. moto). Si no aparece
    // aquí, se usa DEFAULT_WAYPOINTS.
    static readonly Dictionary<string, int[]> WAYPOINT_OVERRIDES = new Dictionary<string, int[]>
    {
        // { "Motocicleta", new[] { 5, 30, 60, 90, 120 } },
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (subscribed) return;
        SceneManager.sceneLoaded += OnSceneLoaded;
        subscribed = true;

        string current = SceneManager.GetActiveScene().name;
        if (IsDrivingScene(current))
        {
            EnsureWaypointDebugger();
            Schedule(current);
        }
    }

    // El debugger y el runner viven SOLO en la escena de manejo. Cuando la
    // escena se descarga (volver al MainMenu), Unity los destruye junto con
    // el resto de la escena — no usamos DontDestroyOnLoad porque entonces
    // sobrevivirían al unload de Gley y crashearían al tocar TrafficAPI.
    static void EnsureWaypointDebugger()
    {
        if (Object.FindFirstObjectByType<WaypointDebugger>() != null) return;
        var go = new GameObject("WaypointDebugger");
        go.AddComponent<WaypointDebugger>();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsDrivingScene(scene.name))
        {
            EnsureWaypointDebugger();
            Schedule(scene.name);
        }
    }

    // Whitelist explícita de escenas donde inyectamos el spawn manager + debugger.
    // Mejor que excluir MainMenu/SampleScene: si mañana alguien agrega una escena
    // de loading/calibración/resultados, no dispara accidentalmente.
    static readonly HashSet<string> DRIVING_SCENES = new HashSet<string>
    {
        "Sedan", "Camioneta", "Motocicleta",
        "BusPasajeros", "CamionDCarga", "Ambulancia",
    };

    internal static bool IsDrivingScene(string sceneName)
    {
        return DRIVING_SCENES.Contains(sceneName);
    }

    static void Schedule(string sceneName)
    {
        // Guard: evitar doble runner si por alguna razón sceneLoaded dispara dos
        // veces para la misma escena (additive load, recompilación caliente, etc.).
        if (Object.FindFirstObjectByType<SpawnLocationRunner>() != null) return;

        // Vive con la escena actual. Si volvemos al MainMenu antes de que
        // termine la corutina, Unity destruye el GameObject y la corutina
        // se cancela limpiamente — no toca Gley ya descargado.
        var runner = new GameObject("SpawnLocationRunner").AddComponent<SpawnLocationRunner>();
        runner.StartCoroutine(runner.ApplySpawn(sceneName));
    }

    internal static int[] GetWaypoints(string sceneName)
    {
        return WAYPOINT_OVERRIDES.TryGetValue(sceneName, out var ov) ? ov : DEFAULT_WAYPOINTS;
    }
}

internal class SpawnLocationRunner : MonoBehaviour
{
    public IEnumerator ApplySpawn(string sceneName)
    {
        int locationId = GameManager.Instance != null ? GameManager.Instance.LocationId : 1;
        int slot = locationId == 0 ? Random.Range(1, 6) : Mathf.Clamp(locationId, 1, 5);

        int[] waypoints = SpawnLocationManager.GetWaypoints(sceneName);
        int waypointIndex = waypoints[slot - 1];

        // Slot sin asignar (placeholder TBD): no toques al jugador.
        if (waypointIndex < 0)
        {
            Debug.Log($"[SpawnLocationManager] Slot {slot} sin asignar — spawn original. scene={sceneName} locationId={locationId}");
            Destroy(gameObject);
            yield break;
        }

        // Espera a que Gley TrafficSystem inicialice (timeout 5s).
        float gleyTimeout = Time.unscaledTime + 5f;
        while (Time.unscaledTime < gleyTimeout)
        {
            if (TrafficAPI.IsInitialized()) break;
            yield return null;
        }

        if (!TrafficAPI.IsInitialized())
        {
            Debug.LogWarning($"[SpawnLocationManager] Gley no inicializó en 5s — spawn original. scene={sceneName} locationId={locationId}");
            Destroy(gameObject);
            yield break;
        }

        TrafficWaypoint waypoint = TrafficAPI.GetWaypointFromIndex(waypointIndex);
        if (waypoint == null)
        {
            Debug.LogWarning($"[SpawnLocationManager] Waypoint {waypointIndex} inválido — spawn original. scene={sceneName} locationId={locationId}");
            Destroy(gameObject);
            yield break;
        }

        // El PlayerCar puede instanciarse uno o dos frames después de Gley.
        // Reintentamos durante 2s antes de rendirnos.
        Transform target = FindPlayerVehicle();
        float playerTimeout = Time.unscaledTime + 2f;
        while (target == null && Time.unscaledTime < playerTimeout)
        {
            yield return null;
            target = FindPlayerVehicle();
        }
        if (target == null)
        {
            Debug.LogWarning($"[SpawnLocationManager] No se encontró Player tras 2s — spawn original. scene={sceneName}");
            Destroy(gameObject);
            yield break;
        }

        Vector3 pos = waypoint.Position;
        Quaternion rot = target.rotation;
        if (waypoint.Neighbors != null && waypoint.Neighbors.Length > 0)
        {
            var next = TrafficAPI.GetWaypointFromIndex(waypoint.Neighbors[0]);
            if (next != null)
            {
                Vector3 forward = (next.Position - pos);
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.001f)
                {
                    rot = Quaternion.LookRotation(forward.normalized, Vector3.up);
                }
            }
        }

        target.SetPositionAndRotation(pos, rot);
        var rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log($"[SpawnLocationManager] scene={sceneName} locationId={locationId} slot={slot} waypoint={waypointIndex} pos={pos}");
        Destroy(gameObject);
    }

    static Transform FindPlayerVehicle()
    {
        var pc = Object.FindFirstObjectByType<PlayerCar>();
        if (pc != null) return pc.transform;

        var tagged = GameObject.FindWithTag("Player");
        if (tagged != null) return tagged.transform.root;

        var named = GameObject.Find("Player");
        if (named != null) return named.transform.root;

        return null;
    }
}

/// <summary>
/// Helper de debug: presiona [K] para loggear el índice del waypoint Gley más
/// cercano al Player. Útil para descubrir qué número poner en
/// SpawnLocationManager.DEFAULT_WAYPOINTS para una zona específica (glorieta,
/// escolar, hospital, etc.) sin tener que abrir el editor de Unity.
/// </summary>
internal class WaypointDebugger : MonoBehaviour
{
    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.kKey.wasPressedThisFrame) return;

        // Defensivo: si se quedó vivo durante un cambio de escena, no toques
        // Gley a menos que la escena actual sea de manejo (whitelist).
        if (!SpawnLocationManager.IsDrivingScene(SceneManager.GetActiveScene().name)) return;

        if (!TrafficAPI.IsInitialized())
        {
            Debug.Log("[WaypointDebug] Gley TrafficSystem no inicializado todavía.");
            return;
        }

        Transform t = null;
        var pc = Object.FindFirstObjectByType<PlayerCar>();
        if (pc != null) t = pc.transform;
        else { var named = GameObject.Find("Player"); if (named != null) t = named.transform.root; }
        if (t == null) { Debug.Log("[WaypointDebug] No se encontró Player."); return; }

        var wp = TrafficAPI.GetClosestWaypoint(t.position);
        if (wp == null) { Debug.Log($"[WaypointDebug] No hay waypoint cercano a {t.position}."); return; }

        Debug.Log($"[WaypointDebug] Player @ {t.position} → nearest waypoint idx={wp.ListIndex} pos={wp.Position}  (úsalo en DEFAULT_WAYPOINTS)");
    }
}
