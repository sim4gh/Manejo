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
    // Cambio de marcha sin clutch presionado (rechino de caja). Solo aplica
    // en modo manual con G923 PS (clutch físico). Configurable desde portal
    // admin /admin/scoring → ScoringConfig.gearChangeWithoutClutch.
    public int gearChangeWithoutClutchPenalty = 5;
    [Tooltip("Cooldown (s) solo para la penalización de rechino. El audio NO tiene cooldown — siempre suena. Evita -25 en cascada cuando el examinado practica.")]
    public float gearGrindCooldown = 3f;

    [Header("Current State (Read Only)")]
    public float currentSpeedLimit = 40f;
    public int totalScore = 100;

    [Header("Debug")]
    public bool showDebug = true;

    private Rigidbody rb;
    private float lastSpeedLimitCheck = 0f;
    private Gley.UrbanSystem.PlayerCar playerCar;
    private Gley.UrbanSystem.UIInputNew cachedInputNew;
    private int lastGear;
    private float lastGearGrindPenaltyTime = -999f;

    // Speeding cooldown — 5s para recuperarse antes de otra penalización
    private float lastSpeedingTime = -999f;
    private const float SPEEDING_COOLDOWN = 5f;

    // Collision cooldown — previene spam de ragdoll y objetos repetidos
    private float lastCollisionTime = -999f;
    private int lastCollisionRootId = -1;
    private const float COLLISION_COOLDOWN = 3f;
    private const float MIN_COLLISION_SPEED = 3f;

    // RCCP integration
    private Component rccpController;
    private System.Type rccpType;
    private System.Reflection.PropertyInfo rccpSpeedProperty;
    private bool useRCCP = false;

    // Event for UI updates
    public System.Action<float> OnSpeedLimitChanged;

    /// <summary>Info pasado a CollisionFeedback para overlay/shake/flash/audio/FFB.</summary>
    public struct CollisionImpactInfo
    {
        public Vector3 contactPoint;     // Punto de contacto en mundo (collision.GetContact(0).point)
        public Vector3 impulseWorld;     // collision.impulse
        public float lateralLocal;       // -1 (impacto izquierdo) … +1 (derecho), en espacio del vehículo
        public float magnitude;          // collision.impulse.magnitude
        public string violationType;     // "Pedestrian" | "Bicycle" | "Vehicle" | "Sign" | "Obstacle"
        public float speedKmh;
    }

    /// <summary>Disparado tras dedupe + cooldown del ViolationDetector. CollisionFeedback lo consume.</summary>
    public static event System.Action<CollisionImpactInfo> OnCollisionImpact;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        currentSpeedLimit = defaultSpeedLimit;
        TryFindRCCP();
        playerCar = GetComponent<Gley.UrbanSystem.PlayerCar>();
        if (playerCar != null) lastGear = playerCar.currentGear;
        // UIInputNew NO es singleton — PlayerCar lo agrega al mismo GameObject
        // en su Start(). Cachearlo aquí evita GetComponent por frame.
        if (playerCar != null) cachedInputNew = playerCar.GetComponent<Gley.UrbanSystem.UIInputNew>();
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

        // Check for speeding (cooldown 5s entre penalizaciones)
        float speed = GetSpeed();
        bool isSpeeding = speed > currentSpeedLimit;

        if (isSpeeding && Time.time - lastSpeedingTime >= SPEEDING_COOLDOWN)
        {
            lastSpeedingTime = Time.time;
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

        // Cambio de marcha sin clutch presionado (rechino). Solo evaluamos si hay
        // clutch f\u00edsico \u2014 en G923 Xbox sin clutch, todos los shifts son "sin clutch"
        // y penalizar al examinado por algo que no puede evitar ser\u00eda injusto.
        // El audio se dispara SIEMPRE que detectemos un shift (feedback inmediato);
        // la penalizaci\u00f3n tiene cooldown para no consumir el examen en cascada.
        if (cachedInputNew != null && cachedInputNew.HasPhysicalClutch())
        {
            int n = cachedInputNew.ConsumeGearShiftsWithoutClutch();
            if (n > 0)
            {
                GearGrindingFeedback.Instance?.PlayGrind();
                if (Time.time - lastGearGrindPenaltyTime > gearGrindCooldown)
                {
                    lastGearGrindPenaltyTime = Time.time;
                    DeductScore(gearChangeWithoutClutchPenalty);
                    NotificationManager.Instance?.ShowNotification(
                        $"-{gearChangeWithoutClutchPenalty} \u00a1CAMBIO SIN CLUTCH!",
                        Color.yellow);
                    TelemetryLogger.Instance?.LogEvent(
                        "CAMBIO_SIN_CLUTCH",
                        $"Cambio de marcha sin clutch a {speed:F0} km/h",
                        -gearChangeWithoutClutchPenalty,
                        speed);
                }
            }
        }
    }

    void UpdateSpeedLimitFromWaypoint()
    {
        if (!API.IsInitialized()) return;

        TrafficWaypoint waypoint = API.GetClosestWaypoint(transform.position);

        if (waypoint != null && waypoint.MaxSpeed > 18)
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

    /// <summary>Busca nombre descriptivo subiendo la jerarquía (evita "Gley").</summary>
    string GetCollisionDisplayName(Collision collision)
    {
        GameObject direct = collision.gameObject;
        if (!direct.CompareTag("Untagged")) return direct.name;

        Transform t = direct.transform.parent;
        while (t != null)
        {
            if (!t.CompareTag("Untagged")) return t.gameObject.name;
            t = t.parent;
        }

        return direct.name;
    }

    void OnCollisionEnter(Collision collision)
    {
        // No penalizar si estamos parados (tráfico AI nos chocó)
        float speed = GetSpeed();
        if (speed < MIN_COLLISION_SPEED) return;

        // Cooldown global: ignorar colisiones por 3s después de la última penalización
        if (Time.time - lastCollisionTime < COLLISION_COOLDOWN) return;

        // Deduplicar por objeto raíz (ragdoll: Shin.R, Arm.L, Hips → mismo peatón)
        Transform root = collision.transform.root;
        int rootId = root.GetInstanceID();
        if (rootId == lastCollisionRootId && Time.time - lastCollisionTime < COLLISION_COOLDOWN) return;

        string displayName = GetCollisionDisplayName(collision);
        GameObject rootObj = root.gameObject;
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

        string violationType;
        if (isPedestrian)
        {
            violationType = "Pedestrian";
            DeductScore(pedestrianPenalty);
            NotificationManager.Instance?.ShowNotification(
                $"-{pedestrianPenalty} ¡ATROPELLO!", Color.red);
            TelemetryLogger.Instance?.LogEvent(
                "ATROPELLO", $"Atropello de peatón ({displayName})",
                -pedestrianPenalty, speed);
        }
        else if (isBicycle)
        {
            violationType = "Bicycle";
            DeductScore(bicycleCollisionPenalty);
            NotificationManager.Instance?.ShowNotification(
                $"-{bicycleCollisionPenalty} ¡COLISIÓN CON BICICLETA!", Color.red);
            TelemetryLogger.Instance?.LogEvent(
                "COLISION_BICICLETA", $"Colisión con bicicleta ({displayName})",
                -bicycleCollisionPenalty, speed);
        }
        else if (isVehicle)
        {
            violationType = "Vehicle";
            DeductScore(vehicleCollisionPenalty);
            NotificationManager.Instance?.ShowNotification(
                $"-{vehicleCollisionPenalty} ¡COLISIÓN VEHICULAR!", Color.red);
            TelemetryLogger.Instance?.LogEvent(
                "COLISION_VEHICULO", $"Colisión con vehículo ({displayName})",
                -vehicleCollisionPenalty, speed);
        }
        else if (isSign)
        {
            violationType = "Sign";
            DeductScore(signCollisionPenalty);
            NotificationManager.Instance?.ShowNotification(
                $"-{signCollisionPenalty} ¡COLISIÓN CON SEÑALAMIENTO!", Color.yellow);
            TelemetryLogger.Instance?.LogEvent(
                "COLISION_SENALAMIENTO", $"Colisión con señalamiento ({displayName})",
                -signCollisionPenalty, speed);
        }
        else if (layer != LayerMask.NameToLayer("Default"))
        {
            violationType = "Obstacle";
            DeductScore(obstacleCollisionPenalty);
            NotificationManager.Instance?.ShowNotification(
                $"-{obstacleCollisionPenalty} ¡COLISIÓN CON OBSTÁCULO!", Color.yellow);
            TelemetryLogger.Instance?.LogEvent(
                "COLISION_OBSTACULO", $"Colisión con obstáculo ({displayName})",
                -obstacleCollisionPenalty, speed);
        }
        else
        {
            // Diagnóstico: colisión a velocidad significativa contra layer Default sin tag conocido.
            // Útil para detectar geometría sin clasificar (paredes, edificios) que el examinado
            // choca pero no recibe feedback. Si aparece seguido, agregar tag/layer apropiado.
            Debug.Log($"[ViolationDetector] Colisión sin clasificar (no feedback): obj='{displayName}' tag='{rootObj.tag}' layer={LayerMask.LayerToName(layer)} speed={speed:F1}km/h");
            return; // No fue penalizado, no activar cooldown
        }

        // Registrar cooldown
        lastCollisionTime = Time.time;
        lastCollisionRootId = rootId;

        // Disparar evento para CollisionFeedback (overlay/shake/flash/audio/FFB).
        // Magnitud del impulso vía Unity Physics; lateral en espacio local del vehículo
        // determina dirección del golpe direccional al volante (G923).
        try
        {
            float impulseMag = collision.impulse.magnitude;
            float lateral = 0f;
            if (impulseMag > 0.001f)
            {
                Vector3 impulseLocal = transform.InverseTransformDirection(collision.impulse);
                lateral = Mathf.Clamp(impulseLocal.x / impulseMag, -1f, 1f);
            }
            Vector3 contactPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : transform.position;

            OnCollisionImpact?.Invoke(new CollisionImpactInfo
            {
                contactPoint = contactPoint,
                impulseWorld = collision.impulse,
                lateralLocal = lateral,
                magnitude = impulseMag,
                violationType = violationType,
                speedKmh = speed,
            });
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ViolationDetector] OnCollisionImpact subscriber threw: {e}");
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
