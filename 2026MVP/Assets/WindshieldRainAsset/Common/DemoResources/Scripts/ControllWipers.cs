using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadedTechnology.WindshieldRainAsset.Demo
{

    [System.Serializable]
    public struct WipingPreset
    {
        public KeyCode keyCode;
        public float delay;
        public float animSpeed;
    }

    public class ControllWipers : MonoBehaviour
    {
        public Animator m_WipersAnimator;

        public AudioSource m_AudioSource;
        public float m_AnimationSpeed = 1;
        public KeyCode m_SingleWipeKey = KeyCode.E;
        public KeyCode m_TurnOffWipersKey = KeyCode.Alpha0;

        public WipingPreset[] m_WipingPresets;

        private int _wiperMode = 0;
        private float _lastWipedTime = 0;
        private bool _goingToWipe = false;

        private readonly int wipeTriggerHash = Animator.StringToHash("Wipe");
        private readonly int wipingSpeedHash = Animator.StringToHash("WipingSpeed");

        public void PlayWipersAudio()
        {
            m_AudioSource.Play();
            _lastWipedTime = Time.time;
            _goingToWipe = false;
        }

#if ENABLE_INPUT_SYSTEM

        public void OnSingleWipeKey(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                m_WipersAnimator.SetFloat(wipingSpeedHash, 1.0f);
                m_WipersAnimator.SetTrigger(wipeTriggerHash);
            }
        }

        public void OnTurnOffKey(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _wiperMode = 0;
            }
        }

        public void OnWipingModeKey(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                if (context.control is UnityEngine.InputSystem.Controls.KeyControl keyControl)
                {
                    for (int i = 0; i < m_WipingPresets.Length; ++i)
                    {
                        string keyCodeString = m_WipingPresets[i].keyCode.ToString();
                        keyCodeString = keyCodeString.Replace("Alpha", "");
                        if (keyControl.name == keyCodeString)
                        {
                            _wiperMode = i + 1;
                        }
                    }
                }
            }
        }
#endif

        // Custom (Tlax2026): permite que WiperAutoController.cs maneje el modo
        // sin pasar por el InputSystem. Si el prefab tiene menos presets de los
        // pedidos, degrada al máximo disponible en vez de quedar fuera de rango.
        public void SetMode(int mode)
        {
            int max = m_WipingPresets != null ? m_WipingPresets.Length : 0;
            if (mode < 0) mode = 0;
            if (mode > max) mode = max;
            _wiperMode = mode;
        }

        // Update is called once per frame
        void Update()
        {
            if (_wiperMode > 0 && _wiperMode <= m_WipingPresets.Length && !_goingToWipe)
            {
                WipingPreset preset = m_WipingPresets[_wiperMode - 1];
                if ((Time.time - _lastWipedTime) > preset.delay)
                {
                    m_WipersAnimator.SetFloat(wipingSpeedHash, preset.animSpeed);
                    m_WipersAnimator.SetTrigger(wipeTriggerHash);
                    _goingToWipe = true;
                }
            }


            m_WipersAnimator.speed = m_AnimationSpeed;
#if !ENABLE_INPUT_SYSTEM
            if (Input.GetKeyDown(m_SingleWipeKey))
            {
                m_WipersAnimator.SetFloat(wipingSpeedHash, 1.0f);
                m_WipersAnimator.SetTrigger(wipeTriggerHash);
            }
            if (Input.GetKeyDown(m_TurnOffWipersKey))
            {
                _wiperMode = 0;
            }
            for (int i = 0; i < m_WipingPresets.Length; ++i)
            {
                if (Input.GetKeyDown(m_WipingPresets[i].keyCode))
                {
                    _wiperMode = i + 1;
                }
            }
#endif
        }
    }
}
