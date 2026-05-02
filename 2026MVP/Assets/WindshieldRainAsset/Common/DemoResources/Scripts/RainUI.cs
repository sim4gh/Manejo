using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ShadedTechnology.WindshieldRainAsset.Demo
{
    public class RainUI : MonoBehaviour
    {
        public UnityEngine.UI.Slider m_Slider;
        public Component m_TextField;
        public Toggle m_Toggle;
        public RainController m_RainController;

        public void ChangeRainAmount(float rainAmount)
        {
            m_RainController.SetRainAmount(rainAmount);

            SetText($"Rain Amount: {(float)rainAmount}");
        }
        public void ChangeStorm(bool value)
        {
            m_RainController.m_StormEnabled = value;
        }

        private void SetText(string text)
        {
            if (m_TextField == null) return;

            var type = m_TextField.GetType();
            if (!type.FullName.Contains("TMPro.TextMeshProUGUI")) return;

            var textProperty = type.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);

            if (textProperty != null)
            {
                // Set the value using reflection
                textProperty.SetValue(m_TextField, text);
            }
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            ChangeRainAmount(m_Slider.value);
            m_Slider.onValueChanged.AddListener(ChangeRainAmount);
            m_Toggle.onValueChanged.AddListener(ChangeStorm);
        }
    }

}
