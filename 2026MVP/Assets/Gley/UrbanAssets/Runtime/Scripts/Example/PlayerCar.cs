using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Gley.UrbanSystem
{
    /// <summary>
    /// This class is for testing purpose only
    /// It is the car controller provided by Unity:
    /// https://docs.unity3d.com/Manual/WheelColliderTutorial.html
    /// </summary>
    [System.Serializable]
    public class AxleInfo
    {
        public WheelCollider leftWheel;
        public WheelCollider rightWheel;
        public bool motor;
        public bool steering;
    }


    public class PlayerCar : MonoBehaviour
    {
        public List<AxleInfo> axleInfos;
        public Transform centerOfMass;
        public float maxMotorTorque;
        public float maxSteeringAngle;
        IVehicleLightsComponent lightsComponent;
        bool mainLights;
        bool brake;
        bool reverse;
        [HideInInspector] public bool blinkLeft;
        [HideInInspector] public bool blinkRight;
        [HideInInspector] public bool hazardActive;
        float realtimeSinceStartup;
        Rigidbody rb;

        IUIInput inputScript;

        [Header("Rotacion Volante")]
        public Transform steeringWheel;
        public float steeringWheelMaxRotation = 450f;
        public float steeringWheelSmoothTime = 0.08f;

        private float currentSteeringInput;
        private float steeringWheelVelocity;
        private float steeringWheelCurrentAngle;
        private Quaternion steeringWheelInitialRotation;

        // Direccionales: flanco para toggle
        private int lastIndicatorInput;

        // Debug frenado
        private bool isBraking;
        private Vector3 brakeStartPos;
        private float brakeStartSpeed;


        [Header("Sonido Motor")]
        public AudioSource motor;

        [Tooltip("Pitch cuando el carro está en ralentí (parado)")]
        public float idlePitch = 0.8f;

        [Tooltip("Pitch máximo en avance a velocidad tope")]
        public float maxPitch = 1.8f;

        [Tooltip("Pitch máximo en reversa (más contenido)")]
        public float maxPitchReverse = 1.2f;

        [Tooltip("Volumen en ralentí (nunca debe ser 0)")]
        [Range(0f, 1f)] public float idleVolume = 0.3f;

        [Tooltip("Volumen a velocidad máxima")]
        [Range(0f, 1f)] public float maxVolume = 0.7f;

        [Tooltip("Velocidad (km/h) a la que el pitch llega al máximo en avance")]
        public float maxSpeedForSound = 120f;

        [Tooltip("Velocidad (km/h) a la que el pitch llega al máximo en reversa")]
        public float maxSpeedForSoundReverse = 40f;

        [Tooltip("Pitch extra al pisar acelerador (motor responde al gas)")]
        public float accelBoost = 0.15f;

        [Tooltip("Cuánto baja el pitch al frenar")]
        public float brakeReduction = 0.1f;

        [Tooltip("Tiempo de suavizado del pitch (más bajo = más reactivo)")]
        public float pitchSmoothTime = 0.3f;

        [Tooltip("Tiempo de suavizado del volumen")]
        public float volumeSmoothTime = 0.2f;

        // Internas para SmoothDamp
        private float currentPitch;
        private float currentVolume;
        private float pitchVelocity;
        private float volumeVelocity;


        private void Start()
        {
            if (motor == null)
                motor = GetComponent<AudioSource>();

            motor.loop = true;          // El loop de motor SIEMPRE debe estar en loop
            motor.playOnAwake = false;
            motor.pitch = idlePitch;
            motor.volume = idleVolume;
            currentPitch = idlePitch;
            currentVolume = idleVolume;

            if (!motor.isPlaying)
                motor.Play();


            GetComponent<Rigidbody>().centerOfMass = centerOfMass.localPosition;
#if ENABLE_LEGACY_INPUT_MANAGER
            inputScript = gameObject.AddComponent<UIInputOld>().Initialize();
            isAutomaticMode = PlayerPrefs.GetInt("TransmisionManual", 0) == 0;
#else
            var uiInputNew = gameObject.AddComponent<UIInputNew>().Initialize();
            inputScript = uiInputNew;
            isAutomaticMode = uiInputNew.IsAutomaticMode();
#endif
            if (isAutomaticMode) currentGear = 1;
            lightsComponent = gameObject.GetComponent<VehicleLightsComponent>();
            lightsComponent.Initialize();
            rb = GetComponent<Rigidbody>();


            steeringWheelInitialRotation = steeringWheel.localRotation;

            // Muchas escenas tienen steeringWheelSmoothTime=1.5 (configurado para
            // teclado). Con volante analógico causa 1.5s de lag visual en el cockpit.
            if (steeringWheelSmoothTime > 0.1f)
                steeringWheelSmoothTime = 0.05f;
        }

        // finds the corresponding visual wheel
        // correctly applies the transform
        public void ApplyLocalPositionToVisuals(WheelCollider collider)
        {
            if (collider.transform.childCount == 0)
            {
                return;
            }

            Transform visualWheel = collider.transform.GetChild(0);

            Vector3 position;
            Quaternion rotation;
            collider.GetWorldPose(out position, out rotation);

            visualWheel.transform.position = position;
            visualWheel.transform.rotation = rotation;
        }

        [Header("Freno")]
        public float maxBrakeTorque = 3000f;

        [Header("Transmisión Manual")]
        // Histéresis del clutch: por debajo del engage threshold se considera
        // acoplado, por encima del disengage está desacoplado. La banda 0.40..0.65
        // evita chatter por ruido del sensor cerca del umbral. Los valores son
        // tuneables por escena en el inspector.
        [Range(0.05f, 0.9f)] public float clutchEngageThreshold = 0.40f;
        // ⚠️ IMPORTANTE: este valor (0.65) está acoplado con
        // UIInputNew.CLUTCH_ENGAGE_THRESHOLD para que el bloqueo del cambio
        // mecánico de marchas en UIInputNew y el corte de motorTorque aquí
        // sucedan en el mismo punto. Si bajas este threshold, en la banda
        // [nuevo, 0.65) UIInputNew aún bloqueará el cambio pero el motor ya
        // estará desacoplado — quedaría una banda donde el conductor no
        // puede avanzar ni cambiar.
        [Range(0.10f, 0.95f)] public float clutchDisengageThreshold = 0.65f;
        // Estado interno del clutch (sticky). Se evalúa solo en modo manual;
        // en automático queda forzado en false (no hay clutch).
        private bool _clutchDisengaged = false;

        // Gear ratios (solo modo manual). Index 0=N (no usado), 1-6 = marchas.
        // Cada marcha tiene rango [min, max]:
        //   - Bajo min  => lugging (torque cae cuadráticamente). 1ra tiene min=0.
        //   - Sobre max => rev limit (torque cae a 0 en último 15%).
        // Sin estos, las marchas serían decorativas (todas darían el mismo torque).
        // Nota: marchas altas (5-6) deben quedar cerca de 1.0; reducir el multiplicador
        // emula "gear ratio" mecánico, pero el WheelCollider no compensa con RPM mayor
        // como un coche real. Si bajas mucho 5-6, drag > torque y el coche topea muy
        // por debajo de su gearMaxSpeedKmh (en feat anterior 6ta no pasaba de 110 km/h).
        [Tooltip("Multiplicador de torque por marcha. Index 0=N (no usado), 1-6. Mantener 5-6 cerca de 1.0 para que puedan alcanzar gearMaxSpeedKmh.")]
        public float[] gearTorqueMultiplier = { 0f, 1.5f, 1.3f, 1.15f, 1.05f, 1.0f, 1.0f };

        [Tooltip("Velocidad mínima útil por marcha (km/h). Index 0=N, 1-6. Bajo este valor el motor 'lugger': torque cae cuadráticamente. 1ra debe ser 0 para arrancar.")]
        public float[] gearMinSpeedKmh = { 0f, 0f, 15f, 30f, 50f, 75f, 100f };

        [Tooltip("Velocidad máxima alcanzable por marcha (km/h). Index 0=N, 1-6. 6ta=180 da headroom sobre el límite legal de 110 km/h en autopista para que el examinado pueda excederse y disparar penalización.")]
        public float[] gearMaxSpeedKmh = { 0f, 25f, 45f, 70f, 100f, 130f, 180f };

        // Gear actual: 0=N, 1-6, -1=R — leido por SimpleSpeedGauge
        [HideInInspector] public int currentGear = 0;
        [HideInInspector] public bool isAutomaticMode;

        public void FixedUpdate()
        {
            float gasInput = Mathf.Clamp01(inputScript.GetVerticalInput());
            float brakeInputValue = Mathf.Clamp01(inputScript.GetBrakeInput());
            currentSteeringInput = inputScript.GetHorizontalInput();
            float steering = maxSteeringAngle * currentSteeringInput;
            currentGear = inputScript.GetCurrentGear();

            // Clutch con histéresis: solo afecta motorTorque en modo manual.
            // En automático, _clutchDisengaged queda fijo en false (no hay
            // pedal/concept de clutch que aplique).
            float clutchValue = inputScript.GetClutchInput();
            if (!isAutomaticMode)
            {
                if (_clutchDisengaged && clutchValue < clutchEngageThreshold) _clutchDisengaged = false;
                else if (!_clutchDisengaged && clutchValue > clutchDisengageThreshold) _clutchDisengaged = true;
            }
            else
            {
                _clutchDisengaged = false;
            }

#if UNITY_6000_0_OR_NEWER
            var velocity = rb.linearVelocity;
#else
            var velocity = rb.velocity;
#endif
            float localVelocity = transform.InverseTransformDirection(velocity).z;

            // Motor segun gear
            float motorTorque;
            if (currentGear == -1) // Reversa
                motorTorque = -maxMotorTorque * gasInput;
            else if (currentGear == 0) // Neutral
            {
                // Codex fix: NO aplicar ghost-torque en Neutral plain. La asistencia
                // pertenece al shift transit (clutch press), no a Neutral sostenido
                // — donde el operador deliberadamente desengaged y deberíamos respetar
                // el coast natural.
                motorTorque = 0f;
            }
            else // 1-6
            {
                // En manual aplicamos gear ratios: torque y velocidad tope
                // dependen de la marcha. Sin esto el shifter es decorativo:
                // cualquier marcha 1-6 daba el mismo torque.
                // En automático currentGear queda forzado a 1 en Start(); si
                // aplicáramos el ratio de 1ra ahí, el coche topearía a 25 km/h.
                if (!isAutomaticMode && currentGear >= 1
                    && currentGear < gearTorqueMultiplier.Length
                    && currentGear < gearMaxSpeedKmh.Length
                    && currentGear < gearMinSpeedKmh.Length)
                {
                    float speedKmh = velocity.magnitude * 3.6f;
                    float minSpeed = gearMinSpeedKmh[currentGear];
                    float maxSpeed = gearMaxSpeedKmh[currentGear];
                    float torqueMul = gearTorqueMultiplier[currentGear];

                    // Soft rev-limit: el torque cae a 0 al acercarse a maxSpeed (último 15%).
                    float revFactor = 1f - Mathf.Clamp01((speedKmh - maxSpeed * 0.85f) / (maxSpeed * 0.15f));

                    // Lugging cuadrático: bajo minSpeed el torque cae a 0 sin piso.
                    // Arrancar en 2-6 (speed=0) da lugFactor=0 → coche clavado.
                    // 1ra tiene minSpeed=0 → lugFactor=1 siempre (arranca libre).
                    float lugFactor = (minSpeed > 0.1f && speedKmh < minSpeed)
                        ? Mathf.Pow(speedKmh / minSpeed, 2f)
                        : 1f;

                    motorTorque = maxMotorTorque * gasInput * torqueMul * revFactor * lugFactor;
                }
                else
                {
                    motorTorque = maxMotorTorque * gasInput;
                }
            }

            // Manual con clutch desacoplado: cortar transmisión de torque al eje
            // motriz. El motor sigue revolucionando (ver UpdateEngineSound) pero
            // el coche no avanza.
            // v1.7.0 HORI-only: durante clutch-press del shift, aplicar ghost-torque
            // para preservar inercia.
            //
            // Guards (codex fix):
            //   - !brake: si el operador frena durante shift, respetar (hill stop intencional)
            //   - sign from _lastNonNeutralGear (intent), no velocity (evita asistir rollback)
            //   - speedKmh > 1: solo si ya tenía inercia
            //
            // G923/Moto NO entran — physics estándar para ellos.
            if (_clutchDisengaged)
            {
                motorTorque = 0f;
                var uiNewClutch = inputScript as UIInputNew;
                // DIAG r13: snapshot ANTES del ghost-torque para entender por qué
                // el operador HORI pierde 60 km/h en 2s durante shift transit.
                // Logueamos cada 15 frames (~0.25s a 60fps) — suficiente granularidad
                // para reconstruir la curva de deceleración sin spam.
                if (Time.frameCount % 15 == 0)
                {
                    bool isHoriDiag = uiNewClutch != null && uiNewClutch.IsHORITruckActive();
                    float speedKmhDiag = Mathf.Abs(localVelocity) * 3.6f;
                    int lastNNDiag = uiNewClutch != null ? uiNewClutch.LastNonNeutralGear : 0;
                    Debug.Log($"[PlayerCar/SHIFT_DIAG] hori={isHoriDiag} speedKmh={speedKmhDiag:F1} "
                        + $"vel.z={velocity.z:F2} vel.mag={velocity.magnitude:F2} "
                        + $"gas={gasInput:F2} brake={brakeInputValue:F2} clutch={clutchValue:F2} "
                        + $"_clutchDisengaged={_clutchDisengaged} currentGear={currentGear} "
                        + $"lastNonNeutral={lastNNDiag} "
                        + $"rb.drag={rb.linearDamping:F3} rb.angularDrag={rb.angularDamping:F3} "
                        + $"mass={rb.mass:F0} motorTorqueBeforeGhost={motorTorque:F0}");
                }

                if (uiNewClutch != null && uiNewClutch.IsHORITruckActive()
                    && brakeInputValue < 0.3f)
                {
                    float speedKmhClutch = Mathf.Abs(localVelocity) * 3.6f;
                    int intentGear = uiNewClutch.LastNonNeutralGear;
                    if (speedKmhClutch > 1f && intentGear != 0)
                    {
                        float signClutch = Mathf.Sign(intentGear);
                        motorTorque = signClutch * maxMotorTorque * 0.08f * Mathf.Clamp01(speedKmhClutch / 30f);
                    }
                }
            }

            // Freno: usa brakeTorque real del WheelCollider
            float brakeTorque = maxBrakeTorque * brakeInputValue;

            // DIAG r13: snapshot DESPUÉS de calcular motorTorque + brakeTorque finales,
            // incluyendo wheel RPM (para detectar wheel slip vs chassis decel).
            if (_clutchDisengaged && Time.frameCount % 15 == 0)
            {
                float wheelRpmAvg = 0f; int wheelsMotor = 0;
                foreach (AxleInfo axDiag in axleInfos)
                {
                    if (axDiag.motor)
                    {
                        wheelRpmAvg += axDiag.leftWheel.rpm + axDiag.rightWheel.rpm;
                        wheelsMotor += 2;
                    }
                }
                if (wheelsMotor > 0) wheelRpmAvg /= wheelsMotor;
                Debug.Log($"[PlayerCar/SHIFT_APPLY] motorTorqueFinal={motorTorque:F0} "
                    + $"brakeTorqueApplied={brakeTorque:F0} "
                    + $"wheelRpmAvg={wheelRpmAvg:F0} "
                    + $"maxMotorTorque={maxMotorTorque:F0} maxBrakeTorque={maxBrakeTorque:F0}");
            }

            // Luces
            reverse = (currentGear == -1);
            brake = brakeInputValue > 0.05f;

            foreach (AxleInfo axleInfo in axleInfos)
            {
                if (axleInfo.steering)
                {
                    axleInfo.leftWheel.steerAngle = steering;
                    axleInfo.rightWheel.steerAngle = steering;
                }
                if (axleInfo.motor)
                {
                    axleInfo.leftWheel.motorTorque = motorTorque;
                    axleInfo.rightWheel.motorTorque = motorTorque;
                    axleInfo.leftWheel.brakeTorque = brakeTorque;
                    axleInfo.rightWheel.brakeTorque = brakeTorque;
                }
                ApplyLocalPositionToVisuals(axleInfo.leftWheel);
                ApplyLocalPositionToVisuals(axleInfo.rightWheel);
            }
        }

        private void Update()
        {
            realtimeSinceStartup += Time.deltaTime;

            // Luces principales (teclado: Space)
            if (GetKeyDownSpace())
            {
                mainLights = !mainLights;
                lightsComponent.SetMainLights(mainLights);
            }

            // Direccionales: D-pad del volante (toggle por flanco) + teclado Q/E
            int indicatorInput = inputScript.GetIndicatorInput();
            if (indicatorInput != lastIndicatorInput && indicatorInput != 0)
            {
                if (indicatorInput == -1)
                {
                    // Toggle Left (si ya estaba Left → Off; cancela Right o Hazard)
                    SetIndicatorState((blinkLeft && !hazardActive) ? IndicatorState.Off : IndicatorState.Left);
                }
                else if (indicatorInput == 1)
                {
                    SetIndicatorState((blinkRight && !hazardActive) ? IndicatorState.Off : IndicatorState.Right);
                }
                else if (indicatorInput == 2)
                {
                    SetIndicatorState(hazardActive ? IndicatorState.Off : IndicatorState.Hazard);
                }
            }
            lastIndicatorInput = indicatorInput;

            // Teclado fallback: Q/U=izq, E/I=der, O=hazards
            if (GetKeyDownQ() || GetKeyDownU())
            {
                SetIndicatorState((blinkLeft && !hazardActive) ? IndicatorState.Off : IndicatorState.Left);
            }
            if (GetKeyDownE() || GetKeyDownI())
            {
                SetIndicatorState((blinkRight && !hazardActive) ? IndicatorState.Off : IndicatorState.Right);
            }
            if (GetKeyDownO())
            {
                SetIndicatorState(hazardActive ? IndicatorState.Off : IndicatorState.Hazard);
            }

            lightsComponent.SetBrakeLights(brake);
            lightsComponent.SetReverseLights(reverse);
            lightsComponent.UpdateLights(realtimeSinceStartup);

            UpdateSteeringWheel();
            UpdateBrakeDebug();
            UpdateEngineSound();
        }

        void UpdateBrakeDebug()
        {
#if UNITY_6000_0_OR_NEWER
            float speed = rb.linearVelocity.magnitude * 3.6f;
#else
            float speed = rb.velocity.magnitude * 3.6f;
#endif

            if (brake && !isBraking && speed > 5f)
            {
                // Inicio de frenado
                isBraking = true;
                brakeStartPos = transform.position;
                brakeStartSpeed = speed;
            }
            else if (isBraking && speed < 1f)
            {
                // Fin de frenado
                float distance = Vector3.Distance(brakeStartPos, transform.position);
                Debug.Log($"[FRENO] De {brakeStartSpeed:F0} km/h a 0 en {distance:F1}m");
                isBraking = false;
            }
            else if (!brake)
            {
                isBraking = false;
            }
        }

        void UpdateSteeringWheel()
        {
            if (steeringWheel == null)
                return;

            float targetAngle = currentSteeringInput * steeringWheelMaxRotation;

            steeringWheelCurrentAngle = Mathf.SmoothDamp(
                steeringWheelCurrentAngle,
                targetAngle,
                ref steeringWheelVelocity,
                steeringWheelSmoothTime
            );

            steeringWheel.localRotation = steeringWheelInitialRotation *
                Quaternion.Euler(0f, 0f, -steeringWheelCurrentAngle);
        }

        void UpdateEngineSound()
        {
            if (motor == null) return;

#if UNITY_6000_0_OR_NEWER
            float speedKmh = rb.linearVelocity.magnitude * 3.6f;
#else
            float speedKmh = rb.velocity.magnitude * 3.6f;
#endif

            float gasInput = Mathf.Clamp01(inputScript.GetVerticalInput());
            float brakeInput = Mathf.Clamp01(inputScript.GetBrakeInput());

            // Elegir techo de pitch y velocidad según dirección
            float pitchCeiling;
            float speedCeiling;
            if (currentGear == -1)
            {
                pitchCeiling = maxPitchReverse;
                speedCeiling = maxSpeedForSoundReverse;
            }
            else
            {
                pitchCeiling = maxPitch;
                speedCeiling = maxSpeedForSound;
            }

            // Velocidad normalizada 0..1 (clamp para que no se pase del techo)
            float speedNormalized = Mathf.Clamp01(speedKmh / speedCeiling);

            // Pitch base por velocidad
            float targetPitch = Mathf.Lerp(idlePitch, pitchCeiling, speedNormalized);

            // Modificadores por input
            targetPitch += accelBoost * gasInput;
            targetPitch -= brakeReduction * brakeInput;

            // Manual con clutch desacoplado y gas pisado: el motor "ruge" libre
            // sin que el coche avance. Usamos Mathf.Max para preservar el modelo
            // basado en velocidad — solo eleva el pitch cuando aplica, no lo reemplaza.
            if (_clutchDisengaged && gasInput > 0.05f)
            {
                float revPitch = Mathf.Lerp(idlePitch, pitchCeiling, gasInput);
                targetPitch = Mathf.Max(targetPitch, revPitch);
            }

            // Clamp final para que nunca baje del idle ni se pase del techo + un margen
            targetPitch = Mathf.Clamp(targetPitch, idlePitch, pitchCeiling + accelBoost);

            // Volumen base por velocidad, con un pequeño boost al pisar gas
            float targetVolume = Mathf.Lerp(idleVolume, maxVolume, speedNormalized);
            targetVolume += 0.1f * gasInput;
            targetVolume = Mathf.Clamp(targetVolume, idleVolume, maxVolume);

            // Suavizado (mismo patrón que el volante)
            currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVelocity, pitchSmoothTime);
            currentVolume = Mathf.SmoothDamp(currentVolume, targetVolume, ref volumeVelocity, volumeSmoothTime);

            motor.pitch = currentPitch;
            motor.volume = currentVolume;
        }


        private bool GetKeyDownSpace()
        {
#if !ENABLE_LEGACY_INPUT_MANAGER
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }
        private bool GetKeyDownQ()
        {
#if !ENABLE_LEGACY_INPUT_MANAGER
            return Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
#else
    return Input.GetKeyDown(KeyCode.Q);
#endif
        }

        private bool GetKeyDownE()
        {
#if !ENABLE_LEGACY_INPUT_MANAGER
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
    return Input.GetKeyDown(KeyCode.E);
#endif
        }

        private bool GetKeyDownU()
        {
#if !ENABLE_LEGACY_INPUT_MANAGER
            return Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame;
#else
    return Input.GetKeyDown(KeyCode.U);
#endif
        }

        private bool GetKeyDownI()
        {
#if !ENABLE_LEGACY_INPUT_MANAGER
            return Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame;
#else
    return Input.GetKeyDown(KeyCode.I);
#endif
        }

        private bool GetKeyDownO()
        {
#if !ENABLE_LEGACY_INPUT_MANAGER
            return Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame;
#else
    return Input.GetKeyDown(KeyCode.O);
#endif
        }

        public enum IndicatorState { Off, Left, Right, Hazard }

        // VehicleLightsComponentV2.SetBlinker ignora cambios mientras siga en
        // StartHazard salvo que se llame StopHazard. Por eso al salir de hazard
        // hay que pasar primero por StopHazard, sino las luces quedan atascadas
        // y el HUD/luces físicas divergen.
        void SetIndicatorState(IndicatorState newState)
        {
            if (hazardActive && newState != IndicatorState.Hazard)
            {
                lightsComponent.SetBlinker(BlinkType.StopHazard);
                hazardActive = false;
            }

            switch (newState)
            {
                case IndicatorState.Off:
                    blinkLeft = false;
                    blinkRight = false;
                    lightsComponent.SetBlinker(BlinkType.Stop);
                    Debug.Log("[DIRECCIONAL] Off");
                    break;
                case IndicatorState.Left:
                    blinkLeft = true;
                    blinkRight = false;
                    lightsComponent.SetBlinker(BlinkType.Left);
                    Debug.Log("[DIRECCIONAL] Izquierda ON");
                    break;
                case IndicatorState.Right:
                    blinkLeft = false;
                    blinkRight = true;
                    lightsComponent.SetBlinker(BlinkType.Right);
                    Debug.Log("[DIRECCIONAL] Derecha ON");
                    break;
                case IndicatorState.Hazard:
                    if (!hazardActive)
                    {
                        blinkLeft = false;
                        blinkRight = false;
                        hazardActive = true;
                        lightsComponent.SetBlinker(BlinkType.StartHazard);
                        Debug.Log("[DIRECCIONAL] Hazard ON");
                    }
                    break;
            }
        }
    }
}