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
    private bool initializedExternally;

    /// <summary>
    /// Inyección desde TopHudRow para evitar GameObject.Find("Player") (que falla en
    /// Motocicleta donde el GO se llama "Player_Moto_Completo") y para que el orden
    /// con Awake de Gley no importe.
    /// </summary>
    public void Initialize(Rigidbody rb, ViolationDetector vd)
    {
        initializedExternally = true;
        vehicleRb = rb;
        SubscribeTo(vd);
    }

    void SubscribeTo(ViolationDetector vd)
    {
        if (violationDetector != null)
        {
            violationDetector.OnSpeedLimitChanged -= OnSpeedLimitChanged;
        }
        violationDetector = vd;
        if (violationDetector != null)
        {
            violationDetector.OnSpeedLimitChanged += OnSpeedLimitChanged;
            currentLimit = violationDetector.currentSpeedLimit;
            UpdateDisplay();
        }
    }

    void Start()
    {
        if (!initializedExternally)
        {
            // Path autosuficiente: solo se ejerce cuando se monta vía MenuItem editor
            // (legacy). En runtime, TopHudRow llama Initialize() y este path no corre.
            playerVehicle = GameObject.Find("Player");
            if (playerVehicle != null)
            {
                vehicleRb = playerVehicle.GetComponent<Rigidbody>();
                TryFindRCCP();
            }
            SubscribeTo(FindFirstObjectByType<ViolationDetector>());
        }

        if (violationDetector == null)
        {
            // Race con Awake de Gley: ViolationDetector aún no instanciado. Reintentar.
            StartCoroutine(RetryLocateDetector());
        }

        UpdateDisplay();
    }

    System.Collections.IEnumerator RetryLocateDetector()
    {
        for (int i = 0; i < 3 && violationDetector == null; i++)
        {
            yield return new WaitForSeconds(0.5f);
            SubscribeTo(FindFirstObjectByType<ViolationDetector>());
        }
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
        currentSpeed = GetSpeed();
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
