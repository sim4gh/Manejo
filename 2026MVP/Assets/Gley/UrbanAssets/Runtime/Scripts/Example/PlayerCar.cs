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
        bool blinkLeft;
        bool blinkRifgt;
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



        private void Start()
        {
            GetComponent<Rigidbody>().centerOfMass = centerOfMass.localPosition;
#if ENABLE_LEGACY_INPUT_MANAGER
            inputScript = gameObject.AddComponent<UIInputOld>().Initialize();
#else
            inputScript = gameObject.AddComponent<UIInputNew>().Initialize();
#endif
            lightsComponent = gameObject.GetComponent<VehicleLightsComponent>();
            lightsComponent.Initialize();
            rb = GetComponent<Rigidbody>();


            steeringWheelInitialRotation = steeringWheel.localRotation;
          
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
            float motor;
            if (currentGear == -1) // Reversa
                motor = -maxMotorTorque * gasInput;
            else if (currentGear == 0) // Neutral
                motor = 0f;
            else // 1-6
                motor = maxMotorTorque * gasInput;

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
                    axleInfo.leftWheel.motorTorque = motor;
                    axleInfo.rightWheel.motorTorque = motor;
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
            if (GetKeyDownSpace())
            {
                mainLights = !mainLights;
                lightsComponent.SetMainLights(mainLights);
            }

            if (GetKeyDownQ())
            {
                blinkLeft = !blinkLeft;
                if (blinkLeft == true)
                {
                    blinkRifgt = false;
                    lightsComponent.SetBlinker(BlinkType.Left);
                }
                else
                {
                    lightsComponent.SetBlinker(BlinkType.Stop);
                }
            }

            if (GetKeyDownE())
            {
                blinkRifgt = !blinkRifgt;
                if (blinkRifgt == true)
                {
                    blinkLeft = false;
                    lightsComponent.SetBlinker(BlinkType.Right);
                }
                else
                {
                    lightsComponent.SetBlinker(BlinkType.Stop);
                }
            }

            lightsComponent.SetBrakeLights(brake);
            lightsComponent.SetReverseLights(reverse);
            lightsComponent.UpdateLights(realtimeSinceStartup);

            UpdateSteeringWheel();

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
    }
}