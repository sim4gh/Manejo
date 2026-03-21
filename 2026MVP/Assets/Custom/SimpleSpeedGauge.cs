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

    [Header("Gear Display")]
    public TextMeshProUGUI gearText;

    [Header("Vehicle")]
    public GameObject vehicle;

    [Header("Display Settings")]
    public string speedFormat = "0";  // "0" for whole numbers, "0.0" for decimal
    public Color textColor = new Color(0.2f, 0.9f, 0.9f, 1f);  // Cyan to match gauge

    private Rigidbody vehicleRb;
    private Component rccpController;
    private System.Type rccpType;
    private bool useRCCP = false;
    private Gley.UrbanSystem.PlayerCar playerCar;

    void Start()
    {
        if (vehicle == null)
            vehicle = GameObject.Find("Player");

        if (vehicle != null)
        {
            vehicleRb = vehicle.GetComponent<Rigidbody>();
            playerCar = vehicle.GetComponent<Gley.UrbanSystem.PlayerCar>();
            TryFindRCCP();
        }

        if (speedText != null)
            speedText.color = textColor;

        // Crear gear text automaticamente si no esta asignado
        if (gearText == null && speedText != null)
        {
            GameObject gearObj = new GameObject("GearText");
            gearObj.transform.SetParent(speedText.transform.parent, false);
            gearText = gearObj.AddComponent<TextMeshProUGUI>();
            gearText.font = speedText.font;
            gearText.fontSize = speedText.fontSize * 0.35f;
            gearText.color = textColor;
            gearText.alignment = TextAlignmentOptions.Center;
            // Posicionar debajo del texto de velocidad
            RectTransform rt = gearText.GetComponent<RectTransform>();
            RectTransform speedRt = speedText.GetComponent<RectTransform>();
            rt.anchorMin = speedRt.anchorMin;
            rt.anchorMax = speedRt.anchorMax;
            rt.anchoredPosition = speedRt.anchoredPosition + new Vector2(0, -speedRt.rect.height * 0.7f);
            rt.sizeDelta = new Vector2(speedRt.sizeDelta.x, speedRt.sizeDelta.y * 0.4f);
        }
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

        // Mostrar gear
        if (gearText != null && playerCar != null)
        {
            int gear = playerCar.currentGear;
            if (gear == -1) gearText.text = "R";
            else if (gear == 0) gearText.text = "N";
            else gearText.text = gear.ToString();
        }
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
