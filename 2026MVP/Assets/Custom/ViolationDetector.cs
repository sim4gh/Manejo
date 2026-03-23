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
    public int pedestrianPenalty = 25;
    public int bicycleCollisionPenalty = 15;
    public int vehicleCollisionPenalty = 10;
    public int signCollisionPenalty = 5;
    public int obstacleCollisionPenalty = 5;
    public int dangerousGearChangePenalty = 20;

    [Header("Current State (Read Only)")]
    public float currentSpeedLimit = 40f;
    public int totalScore = 100;

    [Header("Debug")]
    public bool showDebug = true;

    private Rigidbody rb;
    private bool wasSpeedingLastFrame = false;
    private float lastSpeedLimitCheck = 0f;
    private Gley.UrbanSystem.PlayerCar playerCar;
    private int lastGear;

    // Collision cooldown — previene spam de ragdoll y objetos repetidos
    private float lastCollisionTime = -999f;
    private int lastCollisionRootId = -1;
    private const float COLLISION_COOLDOWN = 3f;

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
        playerCar = GetComponent<Gley.UrbanSystem.PlayerCar>();
        if (playerCar != null) lastGear = playerCar.currentGear;
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
            DeductScore(speedingPenalty);

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

        // Cambio D↔R a velocidad
        if (playerCar != null)
        {
            int gear = playerCar.currentGear;
            if (gear != lastGear
                && ((lastGear == 1 && gear == -1) || (lastGear == -1 && gear == 1))
                && speed > 5f)
            {
                DeductScore(dangerousGearChangePenalty);
                NotificationManager.Instance?.ShowNotification(
                    $"-{dangerousGearChangePenalty} \u00a1CAMBIO DE MARCHA PELIGROSO!",
                    Color.red);
                TelemetryLogger.Instance?.LogEvent(
                    "CAMBIO_PELIGROSO",
                    $"Cambio D\u2194R a {speed:F0} km/h",
                    -dangerousGearChangePenalty,
                    speed);
            }
            lastGear = gear;
        }
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

    /// <summary>Deduce puntos sin bajar de 0.</summary>
    public void DeductScore(int penalty)
    {
        totalScore = Mathf.Max(0, totalScore - penalty);
    }

    void OnCollisionEnter(Collision collision)
    {
        // Cooldown global: ignorar colisiones por 3s después de la última penalización
        if (Time.time - lastCollisionTime < COLLISION_COOLDOWN) return;

        // Deduplicar por objeto raíz (ragdoll: Shin.R, Arm.L, Hips → mismo peatón)
        Transform root = collision.transform.root;
        int rootId = root.GetInstanceID();
        if (rootId == lastCollisionRootId && Time.time - lastCollisionTime < COLLISION_COOLDOWN) return;

        float speed = GetSpeed();
        GameObject rootObj = root.gameObject;
        string rootName = rootObj.name;
        int layer = rootObj.layer;

        // Verificar tags/layers en el root (no en el hueso individual)
        bool isPedestrian = rootObj.CompareTag("Pedestrian") || layer == LayerMask.NameToLayer("Peaton");
        bool isBicycle = rootObj.CompareTag("Bicicleta");
        bool isVehicle = rootObj.CompareTag("automovil") || layer == LayerMask.NameToLayer("RCCP_Vehicle");
        bool isSign = rootObj.CompareTag("Senalamiento");

        // También checar el objeto directo (por si el root no tiene el tag)
        GameObject direct = collision.gameObject;
        if (!isPedestrian) isPedestrian = direct.CompareTag("Pedestrian") || direct.layer == LayerMask.NameToLayer("Peaton");
        if (!isBicycle) isBicycle = direct.CompareTag("Bicicleta");
        if (!isVehicle) isVehicle = direct.CompareTag("automovil") || direct.layer == LayerMask.NameToLayer("RCCP_Vehicle");
        if (!isSign) isSign = direct.CompareTag("Senalamiento");

        if (isPedestrian)
        {
            DeductScore(pedestrianPenalty);
            NotificationManager.Instance?.ShowNotification(
                $"-{pedestrianPenalty} ¡ATROPELLO!", Color.red);
            TelemetryLogger.Instance?.LogEvent(
                "ATROPELLO", $"Atropello de peatón ({rootName})",
                -pedestrianPenalty, speed);
        }
        else if (isBicycle)
        {
            DeductScore(bicycleCollisionPenalty);
            NotificationManager.Instance?.ShowNotification(
                $"-{bicycleCollisionPenalty} ¡COLISIÓN CON BICICLETA!", Color.red);
            TelemetryLogger.Instance?.LogEvent(
                "COLISION_BICICLETA", $"Colisión con bicicleta ({rootName})",
                -bicycleCollisionPenalty, speed);
        }
        else if (isVehicle)
        {
            DeductScore(vehicleCollisionPenalty);
            NotificationManager.Instance?.ShowNotification(
                $"-{vehicleCollisionPenalty} ¡COLISIÓN VEHICULAR!", Color.red);
            TelemetryLogger.Instance?.LogEvent(
                "COLISION_VEHICULO", $"Colisión con vehículo ({rootName})",
                -vehicleCollisionPenalty, speed);
        }
        else if (isSign)
        {
            DeductScore(signCollisionPenalty);
            NotificationManager.Instance?.ShowNotification(
                $"-{signCollisionPenalty} ¡COLISIÓN CON SEÑALAMIENTO!", Color.yellow);
            TelemetryLogger.Instance?.LogEvent(
                "COLISION_SENALAMIENTO", $"Colisión con señalamiento ({rootName})",
                -signCollisionPenalty, speed);
        }
        else if (layer != LayerMask.NameToLayer("Default"))
        {
            DeductScore(obstacleCollisionPenalty);
            NotificationManager.Instance?.ShowNotification(
                $"-{obstacleCollisionPenalty} ¡COLISIÓN CON OBSTÁCULO!", Color.yellow);
            TelemetryLogger.Instance?.LogEvent(
                "COLISION_OBSTACULO", $"Colisión con obstáculo ({rootName})",
                -obstacleCollisionPenalty, speed);
        }
        else
        {
            return; // No fue penalizado, no activar cooldown
        }

        // Registrar cooldown
        lastCollisionTime = Time.time;
        lastCollisionRootId = rootId;
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
