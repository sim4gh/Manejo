using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple RPM Gauge - displays RPM number in center of gauge image
/// </summary>
public class SimpleRPMGauge : MonoBehaviour
{
    [Header("UI References")]
    public Image gaugeImage;
    public TextMeshProUGUI rpmText;

    [Header("Vehicle")]
    public GameObject vehicle;

    [Header("Display Settings")]
    public float maxRPM = 8000f;
    public float redlineRPM = 6500f;
    public Color normalColor = new Color(0.2f, 0.9f, 0.9f, 1f);  // Cyan
    public Color redlineColor = new Color(1f, 0.3f, 0.3f, 1f);   // Red

    private Rigidbody vehicleRb;
    private Component rccpController;
    private System.Type rccpType;
    private System.Reflection.PropertyInfo rccpRPMProperty;
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

        if (rpmText != null)
            rpmText.color = normalColor;
    }

    void TryFindRCCP()
    {
        foreach (var component in vehicle.GetComponents<Component>())
        {
            if (component.GetType().Name.Contains("RCCP_CarController"))
            {
                rccpController = component;
                rccpType = component.GetType();
                rccpRPMProperty = rccpType.GetProperty("engineRPM");
                useRCCP = rccpRPMProperty != null;
                break;
            }
        }
    }

    void Update()
    {
        if (vehicle == null || rpmText == null) return;

        float rpm = GetRPM();

        // Display RPM divided by 1000 (e.g., 6500 -> 6.5)
        rpmText.text = (rpm / 1000f).ToString("0.0");

        // Change color at redline
        rpmText.color = rpm >= redlineRPM ? redlineColor : normalColor;
    }

    float GetRPM()
    {
        if (useRCCP && rccpController != null)
        {
            try
            {
                return (float)rccpRPMProperty.GetValue(rccpController);
            }
            catch { }
        }

        // Fallback: simulate RPM based on speed
        if (vehicleRb != null)
        {
            float speed = vehicleRb.linearVelocity.magnitude * 3.6f; // km/h
            return Mathf.Lerp(800f, maxRPM, speed / 200f);
        }

        return 800f; // Idle RPM
    }
}
