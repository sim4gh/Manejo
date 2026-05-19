using System.Collections.Generic;
using UnityEngine;
using Gley.TrafficSystem;
using Gley.UrbanSystem;
using UnityEngine.InputSystem;

public class GPSController : MonoBehaviour
{
    public Transform carTransform;
    public Transform destinationTransform;
    public List<Vector3> routePoints = new List<Vector3>();

    [Header("Línea de ruta")]
    public LineRenderer routeLine; // Asígnalo en el Inspector

    private WaypointSettings[] _allWaypoints;

    void Start()
    {
        _allWaypoints = FindObjectsOfType<WaypointSettings>();
        Debug.Log($"[GPS] Waypoints cargados: {_allWaypoints.Length}");

        SetupLineRenderer();
    }

    void Update()
    {
        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            CalculateRoute();
        }
    }

    void SetupLineRenderer()
    {
        if (routeLine == null)
        {
            // Si no asignaste uno en el Inspector, lo crea automáticamente
            routeLine = gameObject.AddComponent<LineRenderer>();
        }

        routeLine.material = new Material(Shader.Find("Sprites/Default"));
        routeLine.startColor = Color.cyan;
        routeLine.endColor = Color.cyan;
        routeLine.startWidth = 1.5f;
        routeLine.endWidth = 1.5f;
        routeLine.useWorldSpace = true;
        routeLine.positionCount = 0;
    }

    public void CalculateRoute()
    {
        routePoints.Clear();

        WaypointSettings origin = GetNearestWaypoint(carTransform.position, true);
        WaypointSettings goal = GetNearestWaypoint(destinationTransform.position, true);

        if (origin == null || goal == null)
        {
            Debug.LogWarning("[GPS] No se encontró waypoint cercano.");
            return;
        }

        Debug.Log($"[GPS] Origen: {origin.name} | Destino: {goal.name}");

        List<WaypointSettings> path = BFS(origin, goal);

        // Punto inicial: posición real del carro
        routePoints.Add(carTransform.position + Vector3.up * 0.5f);

        foreach (var wp in path)
            routePoints.Add(wp.transform.position + Vector3.up * 0.5f);

        // Punto final: posición real del destino
        routePoints.Add(destinationTransform.position + Vector3.up * 0.5f);

        Debug.Log($"[GPS] Ruta calculada con {routePoints.Count} puntos.");

        DrawLine();
    }

    void DrawLine()
    {
        routeLine.positionCount = routePoints.Count;
        routeLine.SetPositions(routePoints.ToArray());
    }

    // ── BFS ─────────────────────────────────────────────────────────────────
    List<WaypointSettings> BFS(WaypointSettings start, WaypointSettings end)
    {
        var visited = new HashSet<WaypointSettings> { start };
        var parent = new Dictionary<WaypointSettings, WaypointSettings>();
        parent[start] = null;

        var queue = new Queue<WaypointSettings>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current == end)
                return ReconstructPath(parent, end);

            foreach (WaypointSettingsBase neighborBase in current.neighbors)
            {
                var neighbor = neighborBase as WaypointSettings;
                if (neighbor == null) continue;
                if (visited.Contains(neighbor)) continue;

                visited.Add(neighbor);
                parent[neighbor] = current;
                queue.Enqueue(neighbor);
            }
        }

        Debug.LogWarning($"[GPS] BFS no encontró ruta. Nodos explorados: {visited.Count}");
        return new List<WaypointSettings> { start, end };
    }

    List<WaypointSettings> ReconstructPath(
        Dictionary<WaypointSettings, WaypointSettings> parent,
        WaypointSettings end)
    {
        var path = new List<WaypointSettings>();
        for (var node = end; node != null; node = parent[node])
            path.Add(node);
        path.Reverse();
        return path;
    }

    WaypointSettings GetNearestWaypoint(Vector3 pos, bool ignoreConnectors = false)
    {
        WaypointSettings best = null;
        float minDist = float.MaxValue;

        foreach (var wp in _allWaypoints)
        {
            if (wp == null) continue;
            if (ignoreConnectors && wp.name.Contains("Connector")) continue;

            float d = (wp.transform.position - pos).sqrMagnitude;
            if (d < minDist) { minDist = d; best = wp; }
        }
        return best;
    }
}