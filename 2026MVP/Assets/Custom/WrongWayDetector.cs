using UnityEngine;
using Gley.TrafficSystem;

/// <summary>
/// Detects when the player is driving in the wrong direction.
/// Uses Gley Traffic System waypoints automatically - NO MANUAL SETUP NEEDED!
/// Just attach this to the Player vehicle.
/// </summary>
public class WrongWayDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("How far off the correct direction before triggering (-1.0 = completely opposite)")]
    [Range(-1f, 0f)]
    public float wrongWayThreshold = -0.3f;

    [Tooltip("Minimum speed (km/h) to trigger wrong way detection")]
    public float minSpeedToDetect = 10f;

    [Tooltip("Cooldown between violations (seconds)")]
    public float violationCooldown = 5f;

    [Tooltip("How often to check waypoints (seconds)")]
    public float checkInterval = 0.5f;

    [Header("Penalties")]
    public int wrongWayPenalty = 15;

    [Header("References")]
    public ViolationDetector violationDetector;

    [Header("Debug")]
    public bool showDebug = true;

    // State
    private Vector3 correctDirection;
    private bool hasValidDirection = false;
    private float lastViolationTime = -999f;
    private float lastCheckTime = 0f;
    private Rigidbody vehicleRb;

    // RCCP integration
    private Component rccpController;
    private System.Type rccpType;
    private System.Reflection.PropertyInfo rccpSpeedProperty;
    private bool useRCCP = false;

    void Start()
    {
        vehicleRb = GetComponent<Rigidbody>();
        TryFindRCCP();

        // Auto-find ViolationDetector if not assigned
        if (violationDetector == null)
        {
            violationDetector = GetComponent<ViolationDetector>();
        }
    }

    void TryFindRCCP()
    {
        foreach (var component in GetComponents<Component>())
        {
            if (component.GetType().Name.Contains("RCCP_CarController"))
            {
                rccpController = component;
                rccpType = component.GetType();
                rccpSpeedProperty = rccpType.GetProperty("speed");
                useRCCP = rccpSpeedProperty != null;
                break;
            }
        }
    }

    void Update()
    {
        // Check if Gley Traffic System is initialized
        if (!API.IsInitialized()) return;

        float speed = GetSpeed();
        if (speed < minSpeedToDetect) return;

        // Periodically update correct direction from Gley waypoints
        if (Time.time - lastCheckTime > checkInterval)
        {
            lastCheckTime = Time.time;
            UpdateCorrectDirection();
        }

        if (!hasValidDirection) return;

        // Check if enough time has passed since last violation
        if (Time.time - lastViolationTime < violationCooldown) return;

        // Get vehicle's forward direction (horizontal only)
        Vector3 vehicleDirection = transform.forward;
        vehicleDirection.y = 0;
        vehicleDirection.Normalize();

        // Compare with correct road direction
        Vector3 roadDirection = correctDirection;
        roadDirection.y = 0;
        roadDirection.Normalize();

        float dot = Vector3.Dot(vehicleDirection, roadDirection);

        if (showDebug)
        {
            // Blue = vehicle direction, Green = correct direction
            Debug.DrawRay(transform.position + Vector3.up, vehicleDirection * 5f, Color.blue);
            Debug.DrawRay(transform.position + Vector3.up, roadDirection * 5f, Color.green);
        }

        // If dot product is negative enough, player is going wrong way
        if (dot < wrongWayThreshold)
        {
            TriggerWrongWayViolation(dot);
        }
    }

    void UpdateCorrectDirection()
    {
        // Get closest waypoint to player
        TrafficWaypoint waypoint = API.GetClosestWaypoint(transform.position);

        if (waypoint == null)
        {
            hasValidDirection = false;
            return;
        }

        // Get neighbors (next waypoints in the path)
        if (waypoint.Neighbors == null || waypoint.Neighbors.Length == 0)
        {
            hasValidDirection = false;
            return;
        }

        // Get the first neighbor waypoint
        TrafficWaypoint nextWaypoint = API.GetWaypointFromIndex(waypoint.Neighbors[0]);

        if (nextWaypoint == null)
        {
            hasValidDirection = false;
            return;
        }

        // Calculate direction from current waypoint to next waypoint
        correctDirection = (nextWaypoint.Position - waypoint.Position).normalized;
        hasValidDirection = true;

        if (showDebug)
        {
            Debug.DrawLine(waypoint.Position, nextWaypoint.Position, Color.cyan, checkInterval);
        }
    }

    void TriggerWrongWayViolation(float dotProduct)
    {
        lastViolationTime = Time.time;
        float speed = GetSpeed();

        if (showDebug)
        {
            Debug.LogWarning($"[WrongWay] WRONG WAY DETECTED! Dot: {dotProduct:F2}");
        }

        // Deduct from score (usa DeductScore para no bajar de 0)
        if (violationDetector != null)
        {
            violationDetector.DeductScore(wrongWayPenalty);
        }

        // Show notification
        if (NotificationManager.Instance != null)
        {
            NotificationManager.Instance.ShowNotification(
                $"-{wrongWayPenalty} ¡SENTIDO CONTRARIO!",
                Color.red
            );
        }

        // Log telemetry
        if (TelemetryLogger.Instance != null)
        {
            TelemetryLogger.Instance.LogEvent(
                "SENTIDO_CONTRARIO",
                "Conduciendo en sentido contrario",
                -wrongWayPenalty,
                speed
            );
        }
    }

    float GetSpeed()
    {
        if (useRCCP && rccpController != null)
        {
            try
            {
                return (float)rccpSpeedProperty.GetValue(rccpController);
            }
            catch { }
        }

        if (vehicleRb != null)
            return vehicleRb.linearVelocity.magnitude * 3.6f;

        return 0f;
    }
}
