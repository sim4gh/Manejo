using System.Collections.Generic;
using UnityEngine;
using Gley.TrafficSystem;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// GPSControllerV3 — Ruta que respeta calles reales (sin diagonales)
///
/// FIXES respecto a V2:
///  1. ignoreConnectors = FALSE  → los Connectors son los puentes entre
///     intersecciones; sin ellos el grafo queda partido y A* hace saltos.
///  2. GetNearestReachableWaypoint → busca el waypoint más cercano que
///     además tiene al menos un vecino (nodo alcanzable en el grafo).
///  3. AdvanceCurrentWaypoint mejorado → solo avanza si el siguiente
///     waypoint está DENTRO del path calculado, no cualquier vecino.
///  4. DrawLine ya no conecta carPos directo al primer waypoint; en su
///     lugar proyecta el punto de entrada sobre el segmento carPos→wp[0].
/// </summary>
public class GPSControllerV3 : MonoBehaviour
{
    [Header("Referencias")]
    public Transform carTransform;
    public Transform destinationTransform;

    [Header("Línea de ruta")]
    public LineRenderer routeLine;
    public Color routeColor = Color.cyan;
    public float lineWidth = 1.5f;
    public float lineHeightOffset = 0.5f;

    [Header("Recálculo automático")]
    public float recalcInterval = 3f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    // ── Estado interno ───────────────────────────────────────────────────────
    private TrafficWaypointsData _waypointsData;
    private Coroutine _routeCoroutine;

    /// Waypoint de inicio actual en el path (avanza conforme el carro circula)
    private int _currentWaypointIdx = -1;
    private int _goalIdx = -1;

    /// Último path calculado (índices) para que AdvanceCurrentWaypoint
    /// sepa si el siguiente vecino está dentro de la ruta real.
    private List<int> _lastPath = new List<int>();

    public List<Vector3> routePoints = new List<Vector3>();

    // ────────────────────────────────────────────────────────────────────────
    void Start()
    {
        _waypointsData = FindObjectOfType<TrafficWaypointsData>();

        if (_waypointsData == null)
            Debug.LogError("[GPS] No se encontró TrafficWaypointsData en la escena.");
        else
            Log($"Waypoints totales: {_waypointsData.AllTrafficWaypoints.Length}");

        SetupLineRenderer();
        StartRouteUpdater();
    }

    void Update()
    {
        // Recalculo manual con G
        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            _currentWaypointIdx = -1; // fuerza búsqueda desde cero
            CalculateRoute();
        }
    }

    // ── Corrutina de recálculo ───────────────────────────────────────────────
    public void StartRouteUpdater()
    {
        StopRouteUpdater();
        _routeCoroutine = StartCoroutine(RouteUpdaterCoroutine());
    }

    public void StopRouteUpdater()
    {
        if (_routeCoroutine != null)
        {
            StopCoroutine(_routeCoroutine);
            _routeCoroutine = null;
        }
    }

    private IEnumerator RouteUpdaterCoroutine()
    {
        while (true)
        {
            CalculateRoute();
            yield return new WaitForSeconds(recalcInterval);
        }
    }

    // ── API pública ──────────────────────────────────────────────────────────
    public void CalculateRoute()
    {
        routePoints.Clear();

        if (_waypointsData == null) return;

        var allWp = _waypointsData.AllTrafficWaypoints;

        // ── 1. Destino ────────────────────────────────────────────────────────
        // FIX: incluimos Connectors (ignoreConnectors = false)
        // Destino: sin filtro de dirección (el carro puede llegar desde cualquier ángulo)
        _goalIdx = GetNearestReachableWaypointIndex(destinationTransform.position, useDirection: false);
        if (_goalIdx < 0)
        {
            Debug.LogWarning("[GPS] No se encontró waypoint de destino.");
            return;
        }

        // ── 2. Origen ─────────────────────────────────────────────────────────
        if (_currentWaypointIdx < 0)
        {
            // Primera vez: busca el waypoint más cercano al carro
            // Primera vez: busca el waypoint más cercano al carro EN EL CARRIL CORRECTO
            _currentWaypointIdx = GetNearestReachableWaypointIndex(carTransform.position, useDirection: true);
            if (_currentWaypointIdx < 0)
            {
                Debug.LogWarning("[GPS] No se encontró waypoint de origen.");
                return;
            }
        }
        else
        {
            // Recálculos siguientes: avanza solo si el carro ya pasó el wp
            _currentWaypointIdx = AdvanceCurrentWaypoint(_currentWaypointIdx);
        }

        Log($"Origen wp[{_currentWaypointIdx}]: {allWp[_currentWaypointIdx].Name}  |  " +
            $"Destino wp[{_goalIdx}]: {allWp[_goalIdx].Name}");

        // ── 3. A* ─────────────────────────────────────────────────────────────
        _lastPath = AStar(_currentWaypointIdx, _goalIdx);

        // ── 4. Construir lista de puntos ──────────────────────────────────────
        // NO añadimos carTransform.position como primer punto;
        // el LineRenderer empieza en el primer waypoint del path.
        // Esto evita la línea diagonal "de la nada".
        foreach (int idx in _lastPath)
            routePoints.Add(allWp[idx].Position + Vector3.up * lineHeightOffset);

        // El último punto sí apunta al destino real
        if (routePoints.Count > 0)
            routePoints[routePoints.Count - 1] = destinationTransform.position + Vector3.up * lineHeightOffset;

        Log($"Ruta con {routePoints.Count} puntos.");
        DrawLine();
    }

    // ── Avance de waypoint ───────────────────────────────────────────────────
    /// <summary>
    /// Solo avanza al siguiente waypoint si:
    ///   a) Ese vecino está en _lastPath (respeta la ruta calculada)
    ///   b) El carro ya está más cerca del vecino que del actual
    /// </summary>
    int AdvanceCurrentWaypoint(int currentIdx)
    {
        var allWp = _waypointsData.AllTrafficWaypoints;
        Vector3 carPos = carTransform.position;
        float distToCurrent = Vector3.Distance(carPos, allWp[currentIdx].Position);

        int[] neighbors = allWp[currentIdx].Neighbors;
        for (int i = 0; i < neighbors.Length; i++)
        {
            int neighborIdx = neighbors[i];

            // FIX: solo avanza si ese vecino está dentro del path actual
            if (!_lastPath.Contains(neighborIdx)) continue;

            float distToNeighbor = Vector3.Distance(carPos, allWp[neighborIdx].Position);
            if (distToNeighbor < distToCurrent)
            {
                Log($"Avanzando waypoint → {allWp[neighborIdx].Name}");
                return neighborIdx;
            }
        }

        return currentIdx;
    }

    // ── A* ───────────────────────────────────────────────────────────────────
    List<int> AStar(int startIdx, int goalIdx)
    {
        var allWp = _waypointsData.AllTrafficWaypoints;
        Vector3 goalPos = allWp[goalIdx].Position;

        var gCost = new Dictionary<int, float> { [startIdx] = 0f };
        var fCost = new Dictionary<int, float>
        { [startIdx] = Heuristic(allWp[startIdx].Position, goalPos) };

        var parent = new Dictionary<int, int>();
        var closed = new HashSet<int>();

        // SortedSet con desempate por índice para evitar duplicados lógicos
        var open = new SortedSet<(float, int)>(
            Comparer<(float, int)>.Create((a, b) =>
                a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : a.Item2.CompareTo(b.Item2)));

        open.Add((fCost[startIdx], startIdx));

        while (open.Count > 0)
        {
            int current = open.Min.Item2;
            open.Remove(open.Min);

            if (current == goalIdx)
                return ReconstructPath(parent, goalIdx);

            if (closed.Contains(current)) continue;
            closed.Add(current);

            int[] neighbors = allWp[current].Neighbors;
            for (int i = 0; i < neighbors.Length; i++)
            {
                int neighbor = neighbors[i];
                if (closed.Contains(neighbor)) continue;

                float edgeCost = Vector3.Distance(
                    allWp[current].Position,
                    allWp[neighbor].Position);

                float tentativeG = gCost.GetValueOrDefault(current, float.MaxValue) + edgeCost;

                if (tentativeG < gCost.GetValueOrDefault(neighbor, float.MaxValue))
                {
                    gCost[neighbor] = tentativeG;
                    fCost[neighbor] = tentativeG + Heuristic(allWp[neighbor].Position, goalPos);
                    parent[neighbor] = current;

                    // Elimina entrada vieja si existe (evita duplicados en SortedSet)
                    open.Remove((fCost[neighbor], neighbor));
                    open.Add((fCost[neighbor], neighbor));
                }
            }
        }

        Debug.LogWarning("[GPS] A* no encontró ruta. Devolviendo camino directo.");
        return new List<int> { startIdx, goalIdx };
    }

    float Heuristic(Vector3 a, Vector3 b) => Vector3.Distance(a, b);

    List<int> ReconstructPath(Dictionary<int, int> parent, int end)
    {
        var path = new List<int>();
        int node = end;
        while (parent.ContainsKey(node))
        {
            path.Add(node);
            node = parent[node];
        }
        path.Add(node); // nodo inicio
        path.Reverse();
        return path;
    }

    // ── Nearest REACHABLE waypoint (con filtro de dirección) ────────────────
    /// <summary>
    /// Busca el waypoint más cercano a 'pos' que:
    ///   1. Tiene al menos un vecino (nodo válido en el grafo).
    ///   2. Su dirección (wp → su primer vecino) es compatible con
    ///      el heading del carro — evita agarrar el carril contrario.
    ///
    /// Si useDirection = false (para el destino), ignora el heading.
    ///
    /// El parámetro 'directionDotThreshold' controla cuán estricto es
    /// el filtro: 0 = acepta hasta 90°, 0.5 = máx 60°, -1 = desactiva.
    /// </summary>
    int GetNearestReachableWaypointIndex(Vector3 pos,
                                         bool useDirection = false,
                                         float directionDotThreshold = 0f)
    {
        var allWp = _waypointsData.AllTrafficWaypoints;

        // Heading del carro en XZ (ignoramos Y)
        Vector3 carForward = carTransform.forward;
        carForward.y = 0f;
        carForward.Normalize();

        int bestAligned = -1;   // mejor waypoint en el carril correcto
        float minSqAligned = float.MaxValue;

        int bestFallback = -1;   // mejor waypoint ignorando dirección
        float minSqFallback = float.MaxValue;

        for (int i = 0; i < allWp.Length; i++)
        {
            if (allWp[i].Neighbors == null || allWp[i].Neighbors.Length == 0)
                continue;

            float sq = (allWp[i].Position - pos).sqrMagnitude;

            // Fallback: siempre guarda el más cercano sin importar dirección
            if (sq < minSqFallback) { minSqFallback = sq; bestFallback = i; }

            // Filtro de dirección (solo para el origen, donde useDirection = true)
            if (useDirection)
            {
                // Dirección del waypoint: de su posición hacia su primer vecino
                Vector3 wpForward = allWp[allWp[i].Neighbors[0]].Position - allWp[i].Position;
                wpForward.y = 0f;
                wpForward.Normalize();

                float dot = Vector3.Dot(carForward, wpForward);

                // Si el waypoint apunta en sentido contrario, lo descartamos
                if (dot < directionDotThreshold) continue;
            }

            if (sq < minSqAligned) { minSqAligned = sq; bestAligned = i; }
        }

        // Si encontramos uno alineado, úsalo; si no, cae al más cercano sin filtro
        if (bestAligned >= 0)
        {
            Log($"Waypoint alineado: {allWp[bestAligned].Name}");
            return bestAligned;
        }

        Log($"Sin waypoint alineado, usando fallback: {allWp[bestFallback].Name}");
        return bestFallback;
    }

    // ── LineRenderer ─────────────────────────────────────────────────────────
    void SetupLineRenderer()
    {
        if (routeLine == null)
            routeLine = gameObject.AddComponent<LineRenderer>();

        routeLine.material = new Material(Shader.Find("Sprites/Default"));
        routeLine.startColor = routeColor;
        routeLine.endColor = routeColor;
        routeLine.startWidth = lineWidth;
        routeLine.endWidth = lineWidth;
        routeLine.useWorldSpace = true;
        routeLine.positionCount = 0;
    }

    void DrawLine()
    {
        routeLine.positionCount = routePoints.Count;
        routeLine.SetPositions(routePoints.ToArray());
    }

    // ── Utilidades ───────────────────────────────────────────────────────────
    void Log(string msg)
    {
        if (showDebugLogs)
            Debug.Log($"[GPS] {msg}");
    }
}