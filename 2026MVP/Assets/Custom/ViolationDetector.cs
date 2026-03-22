using UnityEngine;
using Gley.TrafficSystem;

/// <summary>
/// Detects traffic violations including speeding with dynamic speed limits from Gley waypoints.
/// Attach to Player vehicle.
/// </summary>
public class ViolationDetector : MonoBehaviour
{
    [Header("Speed Settings")]
    [Tooltip("Fallback speed limit if no waypoint found")]
    public float defaultSpeedLimit = 40f;

    [Tooltip("How often to check for speed limit changes (seconds)")]
    public float speedLimitCheckInterval = 0.5f;

    [Header("Penalties")]
    public int speedingPenalty = 5;
    public int collisionPenalty = 10;
    public int pedestrianPenalty = 25;

    [Header("Current State (Read Only)")]
    public float currentSpeedLimit = 40f;
    public int totalScore = 100;

    [Header("Debug")]
    public bool showDebug = true;

    private Rigidbody rb;
    private bool wasSpeedingLastFrame = false;
    private float lastSpeedLimitCheck = 0f;

    // RCCP integration
    private Component rccpController;
    private System.Type rccpType;
    private System.Reflection.PropertyInfo rccpSpeedProperty;
    private bool useRCCP = false;

    // Event for UI updates
    public System.Action<float> OnSpeedLimitChanged;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        currentSpeedLimit = defaultSpeedLimit;
        TryFindRCCP();
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
        // Update speed limit from Gley waypoints
        if (Time.time - lastSpeedLimitCheck > speedLimitCheckInterval)
        {
            lastSpeedLimitCheck = Time.time;
            UpdateSpeedLimitFromWaypoint();
        }

        // Check for speeding
        float speed = GetSpeed();
        bool isSpeeding = speed > currentSpeedLimit;

        if (isSpeeding && !wasSpeedingLastFrame)
        {
            totalScore -= speedingPenalty;

            NotificationManager.Instance?.ShowNotification(
                $"-{speedingPenalty} ¡EXCESO DE VELOCIDAD! (Límite: {currentSpeedLimit} km/h)",
                Color.yellow);

            TelemetryLogger.Instance?.LogEvent(
                "VELOCIDAD",
                $"Exceso de velocidad (límite: {currentSpeedLimit} km/h)",
                -speedingPenalty,
                speed);

            if (showDebug)
            {
                Debug.LogWarning($"[Speeding] {speed:F0} km/h in {currentSpeedLimit} km/h zone");
            }
        }
        wasSpeedingLastFrame = isSpeeding;
    }

    void UpdateSpeedLimitFromWaypoint()
    {
        if (!API.IsInitialized()) return;

        TrafficWaypoint waypoint = API.GetClosestWaypoint(transform.position);

        if (waypoint != null && waypoint.MaxSpeed > 0)
        {
            float newLimit = waypoint.MaxSpeed;

            if (newLimit != currentSpeedLimit)
            {
                float oldLimit = currentSpeedLimit;
                currentSpeedLimit = newLimit;

                // Notify listeners (for HUD update)
                OnSpeedLimitChanged?.Invoke(currentSpeedLimit);

                if (showDebug)
                {
                    Debug.Log($"[SpeedLimit] Changed: {oldLimit} → {currentSpeedLimit} km/h");
                }
            }
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

        if (rb != null)
            return rb.linearVelocity.magnitude * 3.6f;

        return 0f;
    }

    void OnCollisionEnter(Collision collision)
    {
        float speed = GetSpeed();

        if (collision.gameObject.CompareTag("Pedestrian"))
        {
            totalScore -= pedestrianPenalty;
            NotificationManager.Instance?.ShowNotification(
                $"-{pedestrianPenalty} ¡ATROPELLO!", Color.red);

            TelemetryLogger.Instance?.LogEvent(
                "ATROPELLO",
                "Colisión con peatón",
                -pedestrianPenalty,
                speed);
        }
        else if (collision.gameObject.layer != LayerMask.NameToLayer("Default"))
        {
            totalScore -= collisionPenalty;
            NotificationManager.Instance?.ShowNotification(
                $"-{collisionPenalty} ¡COLISIÓN!", Color.red);

            TelemetryLogger.Instance?.LogEvent(
                "COLISION",
                $"Colisión con {collision.gameObject.name}",
                -collisionPenalty,
                speed);
        }
    }

    public void ExportTelemetry()
    {
        Debug.Log("ExportTelemetry button clicked!");

        if (TelemetryLogger.Instance == null)
        {
            Debug.LogError("TelemetryLogger.Instance is NULL!");
            return;
        }

        Debug.Log("Calling TelemetryLogger.ExportToJSON...");
        TelemetryLogger.Instance.ExportToJSON(totalScore);
    }

    /// <summary>
    /// Get the current speed limit for external use (HUD, etc.)
    /// </summary>
    public float GetCurrentSpeedLimit() => currentSpeedLimit;
}
