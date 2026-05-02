using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadedTechnology.WindshieldRainAsset.Demo
{
    public class SimpleCarController : MonoBehaviour
    {
        private float horizontalInput, verticalInput;
        private float currentSteerAngle, currentBreakForce;
        private bool isBreaking;

        // Settings
        [SerializeField] private float motorForce, breakForce, maxSteerAngle, idleBreakingForce = 100f, maxRPM = 100f;

        // Wheel Colliders
        [SerializeField] private WheelCollider frontLeftWheelCollider, frontRightWheelCollider;
        [SerializeField] private WheelCollider rearLeftWheelCollider, rearRightWheelCollider;

        // Wheels
        [SerializeField] private Transform frontLeftWheelTransform, frontRightWheelTransform;
        [SerializeField] private Transform rearLeftWheelTransform, rearRightWheelTransform;
        [SerializeField] private float steeringWheelRotation = 270;
        [SerializeField] private Transform steeringWheelTransform;

        [SerializeField] private Rigidbody carRigidbody;
        [SerializeField] private Transform centerOfMass;
        [SerializeField] private AnimationCurve torqueCurve;

#if ENABLE_INPUT_SYSTEM
        [SerializeField] private float maxSteeringStep = 0.1f;
#endif

        private void Start()
        {
            if (centerOfMass && carRigidbody)
            {
                carRigidbody.centerOfMass = centerOfMass.position - carRigidbody.transform.position;
            }
        }

        private void FixedUpdate()
        {
#if !ENABLE_INPUT_SYSTEM
            GetInput();
#endif
#if ENABLE_INPUT_SYSTEM
            UpdateInput();
#endif
            HandleMotor();
            HandleSteering();
            UpdateWheels();
        }


#if ENABLE_INPUT_SYSTEM

        private Vector2 _movementVec;

        void UpdateInput()
        {
            float steeringStep = Mathf.Min(maxSteeringStep, Mathf.Abs(horizontalInput - _movementVec.x));
            horizontalInput = horizontalInput + (horizontalInput > _movementVec.x ? -steeringStep : steeringStep);
            horizontalInput = Mathf.Clamp(horizontalInput, -1, 1);
            verticalInput = -_movementVec.y;
        }

        public void OnMoveInput(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            _movementVec = context.ReadValue<Vector2>();
        }

        public void OnBreakInput(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            isBreaking = context.ReadValueAsButton();
        }

#endif

#if !ENABLE_INPUT_SYSTEM
        private void GetInput()
        {
            // Steering Input
            horizontalInput = Input.GetAxis("Horizontal");

            // Acceleration Input
            verticalInput = -Input.GetAxis("Vertical");

            // Breaking Input
            isBreaking = Input.GetKey(KeyCode.Space);
        }
#endif

        private void HandleMotor()
        {
            //Acceleration on front wheels
            float rightRPM = Mathf.Max(0, (verticalInput > 0 ? frontRightWheelCollider.rpm : -frontRightWheelCollider.rpm));
            float leftRPM = Mathf.Max(0, (verticalInput > 0 ? frontLeftWheelCollider.rpm : -frontLeftWheelCollider.rpm));
            frontRightWheelCollider.motorTorque = verticalInput * motorForce * torqueCurve.Evaluate(Mathf.Clamp01(rightRPM / maxRPM));
            frontLeftWheelCollider.motorTorque = verticalInput * motorForce * torqueCurve.Evaluate(Mathf.Clamp01(leftRPM / maxRPM));

            if (isBreaking)
            {
                currentBreakForce = breakForce;
            }
            else
            {
                currentBreakForce = (Mathf.Abs(verticalInput) < 0.1f) ? idleBreakingForce : 0f;
            }
            ApplyBreaking();
        }

        private void ApplyBreaking()
        {
            frontRightWheelCollider.brakeTorque = currentBreakForce;
            frontLeftWheelCollider.brakeTorque = currentBreakForce;
            rearLeftWheelCollider.brakeTorque = currentBreakForce;
            rearRightWheelCollider.brakeTorque = currentBreakForce;
        }

        private void HandleSteering()
        {
            currentSteerAngle = maxSteerAngle * horizontalInput;
            frontLeftWheelCollider.steerAngle = currentSteerAngle;
            frontRightWheelCollider.steerAngle = currentSteerAngle;

            if (steeringWheelTransform)
            {
                steeringWheelTransform.localRotation = Quaternion.AngleAxis(horizontalInput * steeringWheelRotation, Vector3.forward);
            }
        }

        private void UpdateWheels()
        {
            UpdateSingleWheel(frontLeftWheelCollider, frontLeftWheelTransform);
            UpdateSingleWheel(frontRightWheelCollider, frontRightWheelTransform);
            UpdateSingleWheel(rearRightWheelCollider, rearRightWheelTransform);
            UpdateSingleWheel(rearLeftWheelCollider, rearLeftWheelTransform);
        }

        private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform)
        {
            Vector3 pos;
            Quaternion rot;
            wheelCollider.GetWorldPose(out pos, out rot);
            wheelTransform.rotation = rot;
            wheelTransform.position = pos;
        }
    }
}