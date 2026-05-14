using UnityEngine;
using Gley.TrafficSystem;
using System.Collections.Generic;
using System.Collections;
/// <summary>
/// Detects traffic violations including speeding with dynamic speed limits from Gley waypoints.
/// Attach to Player vehicle.
/// </summary>
public class ViolationDetector : MonoBehaviour
{
    [Header("Speed Settings")]
    [Tooltip("Fallback speed limit if no waypoint found")]
    public float defaultSpeedLimit = 40f;
    public bool yapuedo;
    [Tooltip("How often to check for speed limit changes (seconds)")]
    public float speedLimitCheckInterval = 0.5f;

    [Header("Penalties")]
    public int speedingPenalty = 5;
    public int pedestrianPenalty = 25;
    public int bicycleCollisionPenalty = 15;
    public int vehicleCollisionPenalty = 10;
    [Tooltip("Colisión vehicular pasiva (NPC embistió al alumno por atrás). Default 0: no penaliza, solo se registra para que el examinador la vea.")]
    public int passiveVehicleCollisionPenalty = 0;
    public int signCollisionPenalty = 5;
    public int obstacleCollisionPenalty = 5;
    public int dangerousGearChangePenalty = 20;
    // Cambio de marcha sin clutch presionado (rechino de caja). Solo aplica
    // en modo manual con G923 PS (clutch físico). Configurable desde portal
    // admin /admin/scoring → ScoringConfig.gearChangeWithoutClutch.
    public int gearChangeWithoutClutchPenalty = 5;
    [Tooltip("Cooldown (s) solo para la penalización de rechino. El audio NO tiene cooldown — siempre suena. Evita -25 en cascada cuando el examinado practica.")]
    public float gearGrindCooldown = 3f;

    [Header("Vehicle geometry")]
    [Tooltip("Medio largo del vehículo en metros (espacio local). Sedán ~2.0, SUV ~2.3, ambulancia ~2.7, camión ~4.5, bus ~5.0, moto ~1.0. Configurable por prefab — Collider.bounds está en world space y no sirve para clasificar impacto trasero cuando el carro gira.")]
    public float vehicleHalfLength = 2.0f;

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

    // Collision cooldown — previene spam de ragdoll y objetos repetidos.
    // Separamos activa/pasiva para que una activa real no se trague justo
    // después de una pasiva (cadena: te embisten y rebotas contra el de
    // adelante, ambos eventos deben registrarse).
    private float lastActiveCollisionTime = -999f;
    private float lastPassiveCollisionTime = -999f;
    private int lastCollisionRootId = -1;
    private const float COLLISION_COOLDOWN_SAME = 3f;
    private const float COLLISION_COOLDOWN_CROSS = 1f;
    private const float MIN_COLLISION_SPEED = 3f;
    // Threshold sensorial: gatea OnCollisionImpact (overlay/shake/audio/FFB)
    // basado en Collision.relativeVelocity (velocidad relativa pre-solver,
    // robusta contra deflexión post-impulso de linearVelocity). 1 km/h cubre
    // parking reverse legítimo (1.5–3 km/h) y filtra roces estacionarios.
    private const float MIN_FEEDBACK_SPEED_KMH = 1f;

    /// <summary>Resultado de clasificar una colisión vehicular.</summary>
    public enum VehicleCollisionKind { Active, Passive }

    /// <summary>
    /// Heurística pura: ¿el alumno chocó (Active) o lo embistieron (Passive)?
    /// Aislada como método estático para testear sin escena Unity.
    ///
    /// Pasiva = todas estas condiciones a la vez:
    ///   1) El golpe entró por la defensa trasera (z local &lt; -halfLen·0.6).
    ///   2) El otro se acercaba al alumno por atrás (relativeVelocity en
    ///      forward local positiva &gt; closingThreshold).
    ///   3) El otro venía con velocidad real (descarta empujones a 0 km/h).
    ///   4) El otro iba más rápido que el alumno (cierre dominado por el NPC).
    ///
    /// `relLocal.z` se calcula con `transform.InverseTransformDirection(
    /// collision.relativeVelocity)`. El signo se valida empíricamente en el
    /// primer test manual — si Unity invierte el convenio, basta cambiar
    /// `closingThreshold` por su negativo en el caller.
    /// </summary>
    public static VehicleCollisionKind ClassifyVehicleCollision(
        Vector3 localPoint,
        Vector3 relLocal,
        float halfLen,
        float playerSpeedKmh,
        float npcSpeedKmh,
        float closingThreshold = 3f)
    {
        bool rearImpact = localPoint.z < -halfLen * 0.6f;
        bool closingFromBehind = relLocal.z > closingThreshold;
        bool isPassive = rearImpact
                      && closingFromBehind
                      && npcSpeedKmh > 5f
                      && npcSpeedKmh > playerSpeedKmh + 3f;
        return isPassive ? VehicleCollisionKind.Passive : VehicleCollisionKind.Active;
    }

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
        StartCoroutine(Espera());
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

        // Vehículos de emergencia (ambulancia) exentos de exceso de velocidad por
        // la Ley de Movilidad y Seguridad Vial de Tlaxcala. Guard en la condición
        // del if para que ni el cooldown se actualice — no afecta cambios de
        // marcha ni colisiones (esos siguen aplicando).
        if (isSpeeding && Time.time - lastSpeedingTime >= SPEEDING_COOLDOWN
            && !GameManager.IsEmergencyExam())
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
                    //Debug.Log($"[SpeedLimit] Changed: {oldLimit} → {currentSpeedLimit} km/h");
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
    public IEnumerator Espera()
    {
        yield return new WaitForSeconds(2);
        yapuedo = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (yapuedo == false)
            return;
        // Velocidades de ambos lados para clasificación passive/active aguas
        // abajo. NO se usan para el guard sensorial: leer linearVelocity dentro
        // de OnCollisionEnter devuelve el valor post-impulso del solver, que
        // puede caer ~0 para mass-mismatch o ángulos rasantes aunque la
        // pre-colisión fuera 30 km/h.
        float speed = GetSpeed();
        Rigidbody otherRb = collision.rigidbody;  // ya retorna attachedRigidbody (compound colliders OK)
        float npcSpeedKmh = otherRb != null ? otherRb.linearVelocity.magnitude * 3.6f : 0f;

        // Guard sensorial basado en velocidad relativa pre-solver. Es la
        // métrica canónica de Unity para "¿hubo un golpe real?": independiente
        // del estado deflectado de cada Rigidbody, captura tanto rear-end al
        // alumno parado como reversa a parking-speed.
        float relativeSpeedKmh = collision.relativeVelocity.magnitude * 3.6f;
        if (relativeSpeedKmh < MIN_FEEDBACK_SPEED_KMH) return;

        // Deduplicar por objeto raíz (ragdoll: Shin.R, Arm.L, Hips → mismo peatón)
        Transform root = collision.transform.root;
        int rootId = root.GetInstanceID();

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
        if (!isVehicle) isVehicle = direct.CompareTag("automovil");
        if (!isSign) isSign = direct.CompareTag("Senalamiento");

        // Clasificar activa/pasiva SOLO para colisiones vehiculares. Las demás
        // (peatón, bici, señal, obstáculo) son siempre culpa del alumno.
        bool isPassive = false;
        if (isVehicle)
        {
            // Punto de contacto MÁS TRASERO en espacio local del player.
            // GetContact(0) en colisiones multipunto puede agarrar un lateral
            // aunque la energía dominante venga de atrás — iteramos todos.
            Vector3 localPoint = Vector3.zero;
            float minZ = float.PositiveInfinity;
            int contactCount = collision.contactCount;
            for (int i = 0; i < contactCount; i++)
            {
                Vector3 p = transform.InverseTransformPoint(collision.GetContact(i).point);
                if (p.z < minZ) { minZ = p.z; localPoint = p; }
            }
            Vector3 relLocal = transform.InverseTransformDirection(collision.relativeVelocity);
            isPassive = ClassifyVehicleCollision(localPoint, relLocal, vehicleHalfLength, speed, npcSpeedKmh)
                        == VehicleCollisionKind.Passive;
        }

        // Cooldown adaptativo: 3s mismo tipo, 1s cruzado.
        // Una pasiva no debe tragarse una activa real que venga inmediatamente
        // después (cadena de colisiones por inercia).
        float now = Time.time;
        float lastSame = isPassive ? lastPassiveCollisionTime : lastActiveCollisionTime;
        float lastOther = isPassive ? lastActiveCollisionTime : lastPassiveCollisionTime;
        if (now - lastSame < COLLISION_COOLDOWN_SAME) return;
        if (now - lastOther < COLLISION_COOLDOWN_CROSS) return;
        // Dedupe por objeto raíz dentro del mismo tipo
        if (rootId == lastCollisionRootId && now - lastSame < COLLISION_COOLDOWN_SAME) return;

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
            
            if (isPassive)
            {
                violationType = "VehiclePassive";
                if (passiveVehicleCollisionPenalty > 0) DeductScore(passiveVehicleCollisionPenalty);
                NotificationManager.Instance?.ShowNotification(
                    $"Te impactó otro vehículo ({displayName})",
                    new Color(0.4f, 0.7f, 1f));  // azul informativo
                TelemetryLogger.Instance?.LogEvent(
                    "COLISION_PASIVA",
                    $"Otro vehículo impactó al alumno por atrás ({displayName})",
                    -passiveVehicleCollisionPenalty, speed);
            }
            else
            {
                violationType = "Vehicle";
                DeductScore(vehicleCollisionPenalty);
                NotificationManager.Instance?.ShowNotification(
                    $"-{vehicleCollisionPenalty} ¡COLISIÓN VEHICULAR!", Color.red);
                TelemetryLogger.Instance?.LogEvent(
                    "COLISION_VEHICULO", $"Colisión con vehículo ({displayName})",
                    -vehicleCollisionPenalty, speed);
            }
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
            // Geometría no clasificada (pared/edificio/banqueta sin tag). No se
            // penaliza al alumno por algo que puede ser falso positivo de la
            // escena, pero SÍ debe sentir el impacto — el feedback sensorial
            // es esencial para el examen. Log se conserva para ayudar a
            // detectar geometría que merezca tag/layer apropiado.
            if (layer != LayerMask.NameToLayer("Suelo")&&totalScore!=100)
            {
                violationType = "Obstacle";
                Debug.Log($"[ViolationDetector] Colisión sin clasificar (feedback sin penalty): obj='{displayName}' tag='{rootObj.tag}' layer={LayerMask.LayerToName(layer)} speed={speed:F1}km/h");
            }
            violationType = null;
        }

        // Registrar cooldown del tipo apropiado
        if (isPassive) lastPassiveCollisionTime = now;
        else lastActiveCollisionTime = now;
        lastCollisionRootId = rootId;

        // Disparar evento para CollisionFeedback (overlay/shake/flash/audio/FFB).
        // Magnitud del impulso vía Unity Physics; lateral en espacio local del vehículo
        // determina dirección del golpe direccional al volante (G923).
        // Las pasivas también disparan feedback — el alumno DEBE sentir el golpe
        // aunque no haya descuento (es la queja sensorial que motivó la feature).
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
