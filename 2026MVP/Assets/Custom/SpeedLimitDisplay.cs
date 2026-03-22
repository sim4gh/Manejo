using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays the current speed limit from ViolationDetector.
/// Shows a speed limit sign that updates based on the current zone.
/// </summary>
public class SpeedLimitDisplay : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI limitText;
    public Image signBackground;

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color warningColor = new Color(1f, 0.8f, 0f); // Yellow
    public Color dangerColor = new Color(1f, 0.3f, 0.3f); // Red

    [Header("Settings")]
    public bool flashWhenSpeeding = true;
    public float flashSpeed = 2f;

    private ViolationDetector violationDetector;
    private float currentLimit = 40f;
    private float currentSpeed = 0f;

    // RCCP integration
    private GameObject playerVehicle;
    private Component rccpController;
    private System.Type rccpType;
    private System.Reflection.PropertyInfo rccpSpeedProperty;
    private bool useRCCP = false;
    private Rigidbody vehicleRb;

    void Start()
    {
        // Find ViolationDetector
        violationDetector = FindFirstObjectByType<ViolationDetector>();

        if (violationDetector != null)
        {
            // Subscribe to speed limit changes
            violationDetector.OnSpeedLimitChanged += OnSpeedLimitChanged;
            currentLimit = violationDetector.currentSpeedLimit;
        }

        // Find player vehicle for speed
        playerVehicle = GameObject.Find("Player");
        if (playerVehicle != null)
        {
            vehicleRb = playerVehicle.GetComponent<Rigidbody>();
            TryFindRCCP();
        }

        UpdateDisplay();
    }

    void OnDestroy()
    {
        if (violationDetector != null)
        {
            violationDetector.OnSpeedLimitChanged -= OnSpeedLimitChanged;
        }
    }

    void TryFindRCCP()
    {
        if (playerVehicle == null) return;

        foreach (var component in playerVehicle.GetComponents<Component>())
        {
            if (component.GetType().Name.Contains("RCCP_CarController"))
            {
                rccpController = component;
                rccpType = component.GetType();
                rccpSpeedProperty = rccpType.GetProperty("speed");
                useRCCP = rccpSpeedProperty != null;
                break;
            }
        }
    }

    void Update()
    {
        // Get current speed
        currentSpeed = GetSpeed();

        // Update colors based on speed vs limit
        UpdateColors();
    }

    void OnSpeedLimitChanged(float newLimit)
    {
        currentLimit = newLimit;
        UpdateDisplay();
    }

    void UpdateDisplay()
    {
        if (limitText != null)
        {
            limitText.text = currentLimit.ToString("0");
        }
    }

    void UpdateColors()
    {
        if (limitText == null) return;

        float overSpeed = currentSpeed - currentLimit;

        if (overSpeed > 10)
        {
            // Danger - significantly over limit
            if (flashWhenSpeeding)
            {
                float flash = Mathf.PingPong(Time.time * flashSpeed, 1f);
                limitText.color = Color.Lerp(dangerColor, Color.white, flash);
            }
            else
            {
                limitText.color = dangerColor;
            }
        }
        else if (overSpeed > 0)
        {
            // Warning - slightly over limit
            limitText.color = warningColor;
        }
        else
        {
            // Normal - under limit
            limitText.color = normalColor;
        }
    }

    float GetSpeed()
    {
        if (useRCCP && rccpController != null)
        {
            try
            {
                return (float)rccpSpeedProperty.GetValue(rccpController);
            }
            catch { }
        }

        if (vehicleRb != null)
            return vehicleRb.linearVelocity.magnitude * 3.6f;

        return 0f;
    }
}
