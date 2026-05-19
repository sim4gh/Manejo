using System.Collections.Generic;
using UnityEngine;
using Gley.TrafficSystem;
using Gley.UrbanSystem;
using UnityEngine.InputSystem;
using System.Collections;

public class GPSController : MonoBehaviour
{
    public Transform carTransform;
    public Transform destinationTransform;
    public List<Vector3> routePoints = new List<Vector3>();

    [Header("Línea de ruta")]
    public LineRenderer routeLine;

    [Header("Borrado de puntos")]
    public float waypointReachedDistance = 8f;

    private WaypointSettings[] _allWaypoints;

    void Start()
    {
        _allWaypoints = FindObjectsOfType<WaypointSettings>();
        Debug.Log($"[GPS] Waypoints cargados: {_allWaypoints.Length}");
        SetupLineRenderer();
        StartCoroutine(comenzarPuntos());
    }

    public IEnumerator comenzarPuntos()
    {
        yield return new WaitForSeconds(4);
        CalculateRoute();
        StartCoroutine(RouteUpdaterCoroutine());
    }

    void Update()
    {
        if (Keyboard.current.gKey.wasPressedThisFrame)
            CalculateRoute();
    }

    void SetupLineRenderer()
    {
        if (routeLine == null)
            routeLine = gameObject.AddComponent<LineRenderer>();

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

        List<WaypointSettings> path = BFS(origin, goal);

        if (path.Count == 2 && path[0] == origin && path[1] == goal)
        {
            Debug.LogWarning("[GPS] BFS falló, reintentando con connectors...");
            WaypointSettings originC = GetNearestWaypoint(carTransform.position, false);
            WaypointSettings goalC = GetNearestWaypoint(destinationTransform.position, false);
            path = BFS(originC, goalC);
            Debug.Log($"[GPS] Reintento — Origen: {originC.name} | Destino: {goalC.name}");
        }
        else
        {
            Debug.Log($"[GPS] Origen: {origin.name} | Destino: {goal.name}");
        }

        routePoints.Add(carTransform.position + Vector3.up * 0.5f);
        foreach (var wp in path)
            routePoints.Add(wp.transform.position + Vector3.up * 0.5f);
        routePoints.Add(destinationTransform.position + Vector3.up * 0.5f);

        Debug.Log($"[GPS] Ruta calculada con {routePoints.Count} puntos.");
        DrawLine();
    }

    void DrawLine()
    {
        routeLine.positionCount = routePoints.Count;
        routeLine.SetPositions(routePoints.ToArray());
    }

    private IEnumerator RouteUpdaterCoroutine()
    {
        while (true)
        {
            if (routePoints.Count <= 1)
            {
                routePoints.Clear();
                DrawLine();
                Debug.Log("[GPS] Llegaste al destino.");
                yield break;
            }

            int closestIndex = 0;
            float closestDist = float.MaxValue;

            for (int i = 0; i < routePoints.Count; i++)
            {
                float dist = Vector3.Distance(carTransform.position, routePoints[i]);
                if (dist < closestDist) { closestDist = dist; closestIndex = i; }
            }

            if (closestIndex > 0)
            {
                routePoints.RemoveRange(0, closestIndex);
                DrawLine();
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

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

            foreach (WaypointSettingsBase nb in current.neighbors)
            {
                var neighbor = nb as WaypointSettings;
                if (neighbor == null || visited.Contains(neighbor)) continue;
                visited.Add(neighbor);
                parent[neighbor] = current;
                queue.Enqueue(neighbor);
            }
        }

        Debug.LogWarning($"[GPS] BFS dirigido falló ({visited.Count} nodos), reintentando sin dirección...");
        return BFSUndirected(start, end);
    }

    List<WaypointSettings> BFSUndirected(WaypointSettings start, WaypointSettings end)
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
            {
                Debug.Log("[GPS] Ruta encontrada en modo no dirigido.");
                return ReconstructPath(parent, end);
            }

            foreach (WaypointSettingsBase nb in current.neighbors)
            {
                var n = nb as WaypointSettings;
                if (n == null || visited.Contains(n)) continue;
                visited.Add(n);
                parent[n] = current;
                queue.Enqueue(n);
            }

            foreach (WaypointSettingsBase pb in current.prev)
            {
                var p = pb as WaypointSettings;
                if (p == null || visited.Contains(p)) continue;
                visited.Add(p);
                parent[p] = current;
                queue.Enqueue(p);
            }
        }

        Debug.LogWarning("[GPS] Sin ruta posible.");
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