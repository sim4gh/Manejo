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
                motorTorque = 0f;
            else // 1-6
                motorTorque = maxMotorTorque * gasInput;

            // Freno: usa brakeTorque real del WheelCollider
            float brakeTorque = maxBrakeTorque * brakeInputValue;

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

            // Teclado fallback: Q=izq, E=der
            if (GetKeyDownQ())
            {
                SetIndicatorState((blinkLeft && !hazardActive) ? IndicatorState.Off : IndicatorState.Left);
            }
            if (GetKeyDownE())
            {
                SetIndicatorState((blinkRight && !hazardActive) ? IndicatorState.Off : IndicatorState.Right);
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