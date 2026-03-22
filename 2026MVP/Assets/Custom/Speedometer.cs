using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Custom Speedometer with Speed, RPM, and Gear indicator
/// Works with RCCP or any Rigidbody-based vehicle
/// </summary>
public class Speedometer : MonoBehaviour
{
    [Header("Vehicle Reference")]
    [Tooltip("Leave empty to auto-find 'Player' object")]
    public GameObject vehicle;

    [Header("Speed Gauge")]
    public RectTransform speedNeedle;
    public TextMeshProUGUI speedText;
    [Range(0, 360)] public float speedMinAngle = 135f;    // Needle angle at 0 km/h
    [Range(0, 360)] public float speedMaxAngle = -135f;   // Needle angle at max speed
    public float maxSpeed = 200f;                          // Max speed on dial (km/h)

    [Header("RPM Gauge")]
    public RectTransform rpmNeedle;
    public TextMeshProUGUI rpmText;
    public Image rpmBar;                                   // Alternative: fill bar instead of needle
    [Range(0, 360)] public float rpmMinAngle = 135f;
    [Range(0, 360)] public float rpmMaxAngle = -135f;
    public float maxRPM = 8000f;
    public float redlineRPM = 6500f;
    public Color normalRPMColor = Color.white;
    public Color redlineColor = Color.red;

    [Header("Gear Indicator")]
    public TextMeshProUGUI gearText;
    public float gearTextSize = 72f;

    [Header("Smoothing")]
    [Range(1f, 20f)] public float needleSmoothSpeed = 10f;

    // Internal
    private Rigidbody vehicleRb;
    private float currentSpeed;
    private float currentRPM;
    private int currentGear;
    private float displaySpeed;
    private float displayRPM;

    // RCCP Reference (optional - uses reflection to avoid hard dependency)
    private Component rccpController;
    private System.Type rccpType;
    private System.Reflection.PropertyInfo rccpSpeedProperty;
    private System.Reflection.PropertyInfo rccpRPMProperty;
    private System.Reflection.PropertyInfo rccpGearProperty;
    private bool useRCCP = false;
    private Gley.UrbanSystem.PlayerCar playerCar;

    void Start()
    {
        // Find vehicle if not assigned
        if (vehicle == null)
        {
            vehicle = GameObject.Find("Player");
        }

        if (vehicle != null)
        {
            vehicleRb = vehicle.GetComponent<Rigidbody>();
            playerCar = vehicle.GetComponent<Gley.UrbanSystem.PlayerCar>();

            // Try to find RCCP controller
            TryFindRCCP();
        }

        if (vehicle == null)
        {
            Debug.LogWarning("[Speedometer] No vehicle found! Assign vehicle or name it 'Player'");
        }

        // Initialize gear text size
        if (gearText != null)
        {
            gearText.fontSize = gearTextSize;
        }
    }

    void TryFindRCCP()
    {
        // Try to find RCCP_CarController without hard reference
        foreach (var component in vehicle.GetComponents<Component>())
        {
            if (component.GetType().Name.Contains("RCCP_CarController"))
            {
                rccpController = component;
                rccpType = component.GetType();
                rccpSpeedProperty = rccpType.GetProperty("speed");
                rccpRPMProperty = rccpType.GetProperty("engineRPM");
                rccpGearProperty = rccpType.GetProperty("currentGear");
                useRCCP = rccpSpeedProperty != null;
                Debug.Log("[Speedometer] Found RCCP controller");
                break;
            }
        }
    }

    void Update()
    {
        if (vehicle == null) return;

        // Get vehicle data
        GetVehicleData();

        // Smooth the display values
        displaySpeed = Mathf.Lerp(displaySpeed, currentSpeed, Time.deltaTime * needleSmoothSpeed);
        displayRPM = Mathf.Lerp(displayRPM, currentRPM, Time.deltaTime * needleSmoothSpeed);

        // Update displays
        UpdateSpeedGauge();
        UpdateRPMGauge();
        UpdateGearIndicator();
    }

    void GetVehicleData()
    {
        if (useRCCP && rccpController != null)
        {
            // Get data from RCCP using reflection
            try
            {
                currentSpeed = (float)rccpSpeedProperty.GetValue(rccpController);
                currentRPM = (float)rccpRPMProperty.GetValue(rccpController);
                currentGear = (int)rccpGearProperty.GetValue(rccpController);
            }
            catch
            {
                // Fallback to rigidbody
                GetDataFromRigidbody();
            }
        }
        else
        {
            GetDataFromRigidbody();
        }
    }

    void GetDataFromRigidbody()
    {
        if (vehicleRb != null)
        {
            // Speed from rigidbody (m/s to km/h)
            currentSpeed = vehicleRb.linearVelocity.magnitude * 3.6f;

            // Simulate RPM based on speed (simple approximation)
            currentRPM = Mathf.Lerp(800f, maxRPM, currentSpeed / maxSpeed);

            // Simulate gear based on speed
            currentGear = Mathf.Clamp(Mathf.FloorToInt(currentSpeed / 30f) + 1, 1, 6);
        }
    }

    void UpdateSpeedGauge()
    {
        // Update needle rotation
        if (speedNeedle != null)
        {
            float speedPercent = Mathf.Clamp01(displaySpeed / maxSpeed);
            float angle = Mathf.Lerp(speedMinAngle, speedMaxAngle, speedPercent);
            speedNeedle.localRotation = Quaternion.Euler(0, 0, angle);
        }

        // Update text
        if (speedText != null)
        {
            speedText.text = $"{Mathf.RoundToInt(displaySpeed)}";
        }
    }

    void UpdateRPMGauge()
    {
        float rpmPercent = Mathf.Clamp01(displayRPM / maxRPM);
        bool isRedline = displayRPM >= redlineRPM;

        // Update needle rotation
        if (rpmNeedle != null)
        {
            float angle = Mathf.Lerp(rpmMinAngle, rpmMaxAngle, rpmPercent);
            rpmNeedle.localRotation = Quaternion.Euler(0, 0, angle);
        }

        // Update bar (alternative to needle)
        if (rpmBar != null)
        {
            rpmBar.fillAmount = rpmPercent;
            rpmBar.color = isRedline ? redlineColor : normalRPMColor;
        }

        // Update text
        if (rpmText != null)
        {
            rpmText.text = $"{Mathf.RoundToInt(displayRPM)}";
            rpmText.color = isRedline ? redlineColor : normalRPMColor;
        }
    }

    void UpdateGearIndicator()
    {
        if (gearText == null) return;

        // Automático: mostrar "A" (Drive) o "R" (Reversa)
        if (playerCar != null && playerCar.isAutomaticMode)
        {
            if (playerCar.currentGear == -1)
            {
                gearText.text = "R";
                gearText.color = Color.red;
            }
            else
            {
                gearText.text = "A";
                gearText.color = Color.cyan;
            }
            return;
        }

        // Manual: comportamiento existente
        switch (currentGear)
        {
            case -1:
                gearText.text = "R";
                gearText.color = Color.red;
                break;
            case 0:
                gearText.text = "N";
                gearText.color = Color.green;
                break;
            default:
                gearText.text = currentGear.ToString();
                gearText.color = Color.white;
                break;
        }
    }

    // Public methods for external access
    public float GetSpeed() => currentSpeed;
    public float GetRPM() => currentRPM;
    public int GetGear() => currentGear;
}
