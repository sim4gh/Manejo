using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadedTechnology.WindshieldRainAsset
{
    [AddComponentMenu("Windshield Rain Asset/DropletsAcceleration")]
    [RequireComponent(typeof(WindshieldRain))]
    public class DropletsAcceleration : MonoBehaviour
    {
        [HideInInspector] public WindshieldRain m_RainScript;
        public float m_AccelerationScale = 1;
        public float m_AddVelocityFactor = 0.5f;
        public Vector3 m_GravityForce = new Vector3(0, -1, 0);

        private Vector3 m_LastPosition;
        private Vector3 m_LastVelocity = Vector3.zero;
        private const float _accelerationScaleMultiplier = 0.01f;

        private void Start()
        {
            m_LastPosition = transform.position;
        }

        void FixedUpdate()
        {
            Vector3 velocity = (transform.position - m_LastPosition) / Time.fixedDeltaTime;
            m_LastPosition = transform.position;
            Vector3 acceleration = -((velocity - m_LastVelocity) / Time.fixedDeltaTime);

            acceleration = (m_GravityForce + acceleration + (m_AddVelocityFactor * -m_LastVelocity)) * m_AccelerationScale * _accelerationScaleMultiplier;

            m_LastVelocity = velocity;
            float vertical = Vector3.Dot(acceleration, transform.up);
            float horizontal = Vector3.Dot(acceleration, transform.right);

            m_RainScript.Movement = new Vector2(horizontal, vertical);
        }
    }
}
