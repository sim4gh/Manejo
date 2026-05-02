using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;
using System.Reflection;

namespace ShadedTechnology.WindshieldRainAsset
{
    public class RainController : MonoBehaviour
    {
        [Range(0, 1)] public float m_CurrentRainAmount;
        public bool m_StormEnabled;
        public float m_StormRainAmount = 1.0f;
        public bool m_DisableRainScriptsWhenNotRaining = true;

        public float maxParticlesAmount = 1000;
        public AnimationCurve particleAmountCurve;
        public WindshieldRain[] windshieldRains;
        public Wipers[] wipersScripts;
        public DropletsAcceleration[] dropsAccelerationScripts;
        public SimpleRainManager[] simpleRainManagers;
        public AnimationCurve dropletsSpawnAmountCurve;
        public AnimationCurve dropletsSpawnRateCurve;
        public ParticleSystem rainParticles;
        public Material[] rainMaterials;
        public float materialRainChangeSpeed = 0.1f;
        public float rainStrengthChangeSpeed = 0.1f;

        private float currentMaterialsRainAmount;
        private float currentMaterialsRainStrength;
        private bool isRainEnabled;

        private float lastRainAmountBeforeStorm;
        private float lastRainAmount;
        private bool lastStormEnabled;

        private void EnableDisableRainScripts(bool enable)
        {
            foreach (var windshieldRain in windshieldRains)
            {
                if (windshieldRain == null)
                {
                    continue;
                }
                windshieldRain.enabled = enable;
                if (!enable)
                {
                    windshieldRain.ResetRain();
                }
            }
            foreach (var wipersScript in wipersScripts)
            {
                if (wipersScript == null)
                {
                    continue;
                }
                wipersScript.enabled = enable;
            }
            foreach (var dropsAcceleration in dropsAccelerationScripts)
            {
                if (dropsAcceleration == null)
                {
                    continue;
                }
                dropsAcceleration.enabled = enable;
            }
            foreach (var simpleRainManager in simpleRainManagers)
            {
                if (simpleRainManager == null)
                {
                    continue;
                }
                simpleRainManager.enabled = enable;
            }
        }

        private void UpdateMaterialsRainAmount(float deltaTime)
        {
            float diff = m_CurrentRainAmount - currentMaterialsRainAmount;
            currentMaterialsRainAmount += Mathf.Min(Mathf.Abs(diff), materialRainChangeSpeed * deltaTime) * Mathf.Sign(diff);

            if (m_DisableRainScriptsWhenNotRaining)
            {
                float diffRainStrength = (currentMaterialsRainAmount > 0 ? 1 : 0) - currentMaterialsRainStrength;
                currentMaterialsRainStrength += Mathf.Min(Mathf.Abs(diffRainStrength), rainStrengthChangeSpeed * deltaTime) * Mathf.Sign(diffRainStrength);
            }
            SetMaterialsRainAmount(currentMaterialsRainAmount, m_DisableRainScriptsWhenNotRaining ? currentMaterialsRainStrength : 1);

            if (m_DisableRainScriptsWhenNotRaining)
            {
                if (isRainEnabled != (currentMaterialsRainStrength > 0))
                {
                    isRainEnabled = (currentMaterialsRainStrength > 0);
                    EnableDisableRainScripts(isRainEnabled);
                }
            }
        }

        private void SetMaterialsRainAmount(float rainAmount, float rainStrength)
        {
            foreach (Material rainMaterial in rainMaterials)
            {
                rainMaterial.SetFloat("_RainAmount", rainAmount);
                rainMaterial.SetFloat("_RainStrength", rainStrength);
            }
        }

        public void SetRainAmount(float rainAmount)
        {
            lastRainAmount = m_CurrentRainAmount;
            m_CurrentRainAmount = rainAmount;

            if (rainParticles != null)
            {
                ParticleSystem.EmissionModule emissionModule = rainParticles.emission;
                emissionModule.rateOverTime = particleAmountCurve.Evaluate(rainAmount) * maxParticlesAmount;
            }

            foreach (var windshieldRain in windshieldRains)
            {
                if (windshieldRain == null)
                {
                    continue;
                }

                windshieldRain.m_SpawnAmount = dropletsSpawnAmountCurve.Evaluate(rainAmount);
                windshieldRain.m_SpawnRate = Mathf.Max(0.001f, dropletsSpawnRateCurve.Evaluate(rainAmount));
                windshieldRain.UpdateShaderValues();
            }
        }

        public void ChangeStorm()
        {
            if (m_StormEnabled)
            {
                lastRainAmountBeforeStorm = m_CurrentRainAmount;
                SetRainAmount(1.0f);
                foreach (var windshieldRain in windshieldRains)
                {
                    if (windshieldRain == null)
                    {
                        continue;
                    }
                    windshieldRain.m_SpawnAmount = m_StormRainAmount;
                    windshieldRain.UpdateShaderValues();
                }
            }
            else
            {
                SetRainAmount(lastRainAmountBeforeStorm);
            }
        }

        void Start()
        {
            currentMaterialsRainAmount = m_CurrentRainAmount;
            isRainEnabled = (m_CurrentRainAmount > 0);
            currentMaterialsRainStrength = isRainEnabled ? 1 : 0;
            lastRainAmountBeforeStorm = m_CurrentRainAmount;
            lastRainAmount = m_CurrentRainAmount;
            lastStormEnabled = m_StormEnabled;

            if (m_DisableRainScriptsWhenNotRaining)
            {
                EnableDisableRainScripts(isRainEnabled);
            }
        }

        void Update()
        {
            if (lastStormEnabled != m_StormEnabled)
            {
                lastStormEnabled = m_StormEnabled;
                ChangeStorm();
            }
            if (lastRainAmount != m_CurrentRainAmount)
            {
                SetRainAmount(m_CurrentRainAmount);
            }

            UpdateMaterialsRainAmount(Time.deltaTime);
        }
    }
}
