using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD mínimo de velocidad/marcha procedural. Se usa cuando la escena no
/// tiene un SimpleSpeedGauge activo (caso Motocicleta: el SpeedCanvas vive
/// bajo un GameObject "Player" desactivado).
///
/// Sin imagen de gauge — solo texto de velocidad + marcha en la esquina
/// inferior izquierda. Lee del Gley.UrbanSystem.PlayerCar o del Rigidbody.
/// </summary>
public class FallbackSpeedHud : MonoBehaviour
{
    private GameObject vehicle;
    private Rigidbody vehicleRb;
    private Gley.UrbanSystem.PlayerCar playerCar;

    private TextMeshProUGUI speedText;
    private TextMeshProUGUI gearText;

    private static readonly string[] GearStrings = { "R", "N", "1", "2", "3", "4", "5", "6" };

    void Start()
    {
        FindVehicle();
        BuildUI();
    }

    void FindVehicle()
    {
        vehicle = GameObject.Find("Player");
        if (vehicle == null)
        {
            var pc = Object.FindFirstObjectByType<Gley.UrbanSystem.PlayerCar>();
            if (pc != null) vehicle = pc.gameObject;
        }
        if (vehicle != null)
        {
            vehicleRb = vehicle.GetComponent<Rigidbody>();
            playerCar = vehicle.GetComponent<Gley.UrbanSystem.PlayerCar>();
        }
    }

    void BuildUI()
    {
        Canvas canvas = ExamTimer.EnsureFallbackHudCanvas();
        if (canvas == null) return;

        GameObject container = new GameObject("FallbackSpeedHud");
        container.transform.SetParent(canvas.transform, false);
        RectTransform rt = container.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(30f, 30f);
        rt.sizeDelta = new Vector2(220f, 130f);

        Image bg = container.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Roboto-Bold SDF");
        Color cyan = new Color(0.2f, 0.9f, 0.9f, 1f);

        // Velocidad (grande, centrado)
        GameObject speedObj = new GameObject("Speed");
        speedObj.transform.SetParent(container.transform, false);
        RectTransform speedRt = speedObj.AddComponent<RectTransform>();
        speedRt.anchorMin = new Vector2(0f, 0.35f);
        speedRt.anchorMax = new Vector2(1f, 1f);
        speedRt.offsetMin = Vector2.zero;
        speedRt.offsetMax = Vector2.zero;
        speedText = speedObj.AddComponent<TextMeshProUGUI>();
        speedText.text = "0";
        speedText.fontSize = 64f;
        speedText.fontStyle = FontStyles.Bold;
        speedText.color = cyan;
        speedText.alignment = TextAlignmentOptions.Center;
        speedText.raycastTarget = false;
        if (font != null) speedText.font = font;

        // Unidad (km/h, debajo del número, pequeño)
        GameObject unitObj = new GameObject("Unit");
        unitObj.transform.SetParent(container.transform, false);
        RectTransform unitRt = unitObj.AddComponent<RectTransform>();
        unitRt.anchorMin = new Vector2(0f, 0.05f);
        unitRt.anchorMax = new Vector2(0.6f, 0.35f);
        unitRt.offsetMin = Vector2.zero;
        unitRt.offsetMax = Vector2.zero;
        var unit = unitObj.AddComponent<TextMeshProUGUI>();
        unit.text = "km/h";
        unit.fontSize = 22f;
        unit.color = new Color(1f, 1f, 1f, 0.75f);
        unit.alignment = TextAlignmentOptions.Center;
        unit.raycastTarget = false;
        if (font != null) unit.font = font;

        // Gear (a la derecha del km/h)
        GameObject gearObj = new GameObject("Gear");
        gearObj.transform.SetParent(container.transform, false);
        RectTransform gearRt = gearObj.AddComponent<RectTransform>();
        gearRt.anchorMin = new Vector2(0.6f, 0.05f);
        gearRt.anchorMax = new Vector2(1f, 0.35f);
        gearRt.offsetMin = Vector2.zero;
        gearRt.offsetMax = Vector2.zero;
        gearText = gearObj.AddComponent<TextMeshProUGUI>();
        gearText.text = "N";
        gearText.fontSize = 26f;
        gearText.fontStyle = FontStyles.Bold;
        gearText.color = cyan;
        gearText.alignment = TextAlignmentOptions.Center;
        gearText.raycastTarget = false;
        if (font != null) gearText.font = font;
    }

    void Update()
    {
        if (speedText == null) return;

        // Re-buscar tarde si el vehículo se instancia después de Start()
        if (vehicle == null) FindVehicle();

        speedText.text = GetSpeed().ToString("0");

        if (gearText != null && playerCar != null)
        {
            if (playerCar.isAutomaticMode)
            {
                gearText.text = playerCar.currentGear == -1 ? "R" : "A";
            }
            else
            {
                int idx = playerCar.currentGear + 1;
                if (idx >= 0 && idx < GearStrings.Length)
                    gearText.text = GearStrings[idx];
            }
        }
    }

    float GetSpeed()
    {
        if (vehicleRb != null) return vehicleRb.linearVelocity.magnitude * 3.6f;
        return 0f;
    }
}
