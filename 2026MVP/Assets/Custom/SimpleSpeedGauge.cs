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
    private System.Reflection.PropertyInfo rccpSpeedProperty;
    private bool useRCCP = false;
    private Gley.UrbanSystem.PlayerCar playerCar;

    // Gear strings cacheados (evita ToString() allocation cada frame)
    private static readonly string[] GearStrings = { "R", "N", "1", "2", "3", "4", "5", "6" };

    // Direccionales HUD
    private TextMeshProUGUI leftArrow;
    private TextMeshProUGUI rightArrow;
    private float blinkTimer;
    private bool blinkVisible;
    private const float BLINK_INTERVAL = 0.4f;

    void Start()
    {
        // Mover el Canvas del velocímetro a la pantalla principal.
        // Maneja ambos modos: Overlay (targetDisplay) y ScreenSpaceCamera (worldCamera).
        DisplayHelper.EnsureOnMainDisplay(GetComponentInParent<Canvas>(), "[SimpleSpeedGauge]");

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
            gearText.fontStyle = TMPro.FontStyles.Bold;
            gearText.color = textColor;
            gearText.alignment = TextAlignmentOptions.Center;
            // Posicionar debajo del texto de velocidad
            RectTransform rt = gearText.GetComponent<RectTransform>();
            RectTransform speedRt = speedText.GetComponent<RectTransform>();
            rt.anchorMin = speedRt.anchorMin;
            rt.anchorMax = speedRt.anchorMax;
            rt.anchoredPosition = speedRt.anchoredPosition + new Vector2(0, -speedRt.rect.height * 1.0f);
            rt.sizeDelta = new Vector2(speedRt.sizeDelta.x, speedRt.sizeDelta.y * 0.4f);
        }

        // Crear flechas de direccionales
        if (speedText != null)
        {
            leftArrow = CreateArrow("LeftArrow", "\u25C4", -1); // ◄
            rightArrow = CreateArrow("RightArrow", "\u25BA", 1); // ►
            leftArrow.gameObject.SetActive(false);
            rightArrow.gameObject.SetActive(false);
        }
    }

    TextMeshProUGUI CreateArrow(string name, string symbol, int side)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(speedText.transform.parent, false);
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = symbol;
        tmp.font = speedText.font;
        tmp.fontSize = speedText.fontSize * 0.5f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.1f, 0.9f, 0.2f, 1f); // verde direccional
        tmp.alignment = TextAlignmentOptions.Center;
        RectTransform rt = tmp.GetComponent<RectTransform>();
        RectTransform speedRt = speedText.GetComponent<RectTransform>();
        rt.anchorMin = speedRt.anchorMin;
        rt.anchorMax = speedRt.anchorMax;
        rt.anchoredPosition = speedRt.anchoredPosition + new Vector2(side * speedRt.sizeDelta.x * 0.78f, 0);
        rt.sizeDelta = new Vector2(speedRt.sizeDelta.y * 0.5f, speedRt.sizeDelta.y * 0.5f);
        return tmp;
    }

    void TryFindRCCP()
    {
        foreach (var component in vehicle.GetComponents<Component>())
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
        if (vehicle == null || speedText == null) return;

        float speed = GetSpeed();
        speedText.text = speed.ToString(speedFormat);
        velocidadActual = speedText.text;

        // Mostrar gear (sin allocation: strings cacheados)
        // GearStrings: [0]=R, [1]=N, [2]=1, [3]=2, ... [7]=6
        // Direccionales parpadeantes
        if (playerCar != null && leftArrow != null && rightArrow != null)
        {
            bool anyBlink = playerCar.blinkLeft || playerCar.blinkRight;
            if (anyBlink)
            {
                blinkTimer += Time.deltaTime;
                if (blinkTimer >= BLINK_INTERVAL)
                {
                    blinkTimer = 0f;
                    blinkVisible = !blinkVisible;
                }
                leftArrow.gameObject.SetActive(playerCar.blinkLeft && blinkVisible);
                rightArrow.gameObject.SetActive(playerCar.blinkRight && blinkVisible);
            }
            else
            {
                leftArrow.gameObject.SetActive(false);
                rightArrow.gameObject.SetActive(false);
                blinkTimer = 0f;
                blinkVisible = false;
            }
        }

        if (gearText != null && playerCar != null)
        {
            if (playerCar.isAutomaticMode)
            {
                gearText.text = playerCar.currentGear == -1 ? "R" : "A";
            }
            else
            {
                int gear = playerCar.currentGear;
                int idx = gear + 1; // -1→0(R), 0→1(N), 1→2("1"), ...6→7("6")
                if (idx >= 0 && idx < GearStrings.Length)
                    gearText.text = GearStrings[idx];
            }
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
