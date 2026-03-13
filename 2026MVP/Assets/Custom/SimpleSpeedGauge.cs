using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple Speed Gauge - just displays speed number in center of gauge image
/// </summary>
public class SimpleSpeedGauge : MonoBehaviour
{
    [Header("UI References")]
    public Image gaugeImage;
    public TextMeshProUGUI speedText;
    public string velocidadActual;

    [Header("Vehicle")]
    public GameObject vehicle;

    [Header("Display Settings")]
    public string speedFormat = "0";  // "0" for whole numbers, "0.0" for decimal
    public Color textColor = new Color(0.2f, 0.9f, 0.9f, 1f);  // Cyan to match gauge

    private Rigidbody vehicleRb;
    private Component rccpController;
    private System.Type rccpType;
    private bool useRCCP = false;

    void Start()
    {
        if (vehicle == null)
            vehicle = GameObject.Find("Player");

        if (vehicle != null)
        {
            vehicleRb = vehicle.GetComponent<Rigidbody>();
            TryFindRCCP();
        }

        if (speedText != null)
            speedText.color = textColor;
    }

    void TryFindRCCP()
    {
        foreach (var component in vehicle.GetComponents<Component>())
        {
            if (component.GetType().Name.Contains("RCCP_CarController"))
            {
                rccpController = component;
                rccpType = component.GetType();
                useRCCP = true;
                break;
            }
        }
    }

    void Update()
    {
        if (vehicle == null || speedText == null) return;

        float speed = GetSpeed();
        speedText.text = speed.ToString(speedFormat);


        velocidadActual = speedText.text;
    }

    float GetSpeed()
    {
        if (useRCCP && rccpController != null)
        {
            try
            {
                return (float)rccpType.GetProperty("speed").GetValue(rccpController);
            }
            catch { }
        }

        if (vehicleRb != null)
            return vehicleRb.linearVelocity.magnitude * 3.6f;

        return 0f;
    }
}
