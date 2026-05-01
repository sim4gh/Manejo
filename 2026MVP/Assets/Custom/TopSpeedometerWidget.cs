using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Widget central del TopHudRow: [◄ 50 km/h ►].
/// Las flechas se prenden parpadeando con la intermitente correspondiente; ambas
/// con hazards (lectura coherente desde PlayerCar.blinkLeft/Right + hazardActive).
/// Usa Time.deltaTime para sincronizar con el reloj del PlayerCar (que blinkea
/// sus luces físicas con tiempo escalado).
/// </summary>
public class TopSpeedometerWidget : MonoBehaviour
{
    private const float BLINK_INTERVAL = 0.5f;
    // Verde direccional consistente con SimpleSpeedGauge.
    private static readonly Color ArrowColor = new Color(0.1f, 0.9f, 0.2f, 1f);
    // Cyan consistente con FallbackSpeedHud.
    private static readonly Color SpeedColor = new Color(0.2f, 0.9f, 0.9f, 1f);

    private TextMeshProUGUI leftArrow;
    private TextMeshProUGUI speedText;
    private TextMeshProUGUI unitText;
    private TextMeshProUGUI rightArrow;
    private Image bg;

    private GameObject vehicle;
    private Rigidbody vehicleRb;
    private Gley.UrbanSystem.PlayerCar playerCar;
    private Component rccpController;
    private System.Reflection.PropertyInfo rccpSpeedProperty;
    private bool useRCCP;

    private float blinkTimer;
    private bool blinkVisible;
    private int lastDisplayedSpeed = -1;

    /// <summary>Llamado por TopHudRow tras instanciar este componente; construye la UI hija.</summary>
    public void Build()
    {
        // Fondo translúcido coherente con el timer.
        bg = gameObject.AddComponent<Image>();
        bg.sprite = MenuCardBuilder.GetRoundedSprite(MenuTheme.CornerRadiusSmall);
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;

        // HorizontalLayoutGroup interno para [◄] [Speed km/h] [►] con anchos fijos
        var hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.padding = new RectOffset(16, 16, 0, 0);

        leftArrow = CreateArrow("LeftArrow", "◄");      // ◄
        BuildSpeedBlock();
        rightArrow = CreateArrow("RightArrow", "►");    // ►

        // Apagar flechas inicialmente (Image.enabled vía color.a para no marcar layout dirty).
        leftArrow.enabled = false;
        rightArrow.enabled = false;

        FindVehicle();
    }

    TextMeshProUGUI CreateArrow(string name, string symbol)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 64f;
        le.preferredHeight = 130f;
        le.flexibleWidth = 0f;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = symbol;
        tmp.fontSize = 92f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = ArrowColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.raycastTarget = false;
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Roboto-Bold SDF");
        if (font != null) tmp.font = font;
        return tmp;
    }

    void BuildSpeedBlock()
    {
        // Contenedor para [Speed grande] + [km/h pequeño debajo]
        var block = new GameObject("SpeedBlock");
        block.transform.SetParent(transform, false);
        block.AddComponent<RectTransform>();
        var le = block.AddComponent<LayoutElement>();
        le.preferredWidth = 260f;
        le.preferredHeight = 130f;
        le.flexibleWidth = 0f;

        // Speed (top)
        var speedGo = new GameObject("Speed");
        speedGo.transform.SetParent(block.transform, false);
        var speedRt = speedGo.AddComponent<RectTransform>();
        speedRt.anchorMin = new Vector2(0f, 0.26f);
        speedRt.anchorMax = new Vector2(1f, 1f);
        speedRt.offsetMin = Vector2.zero;
        speedRt.offsetMax = Vector2.zero;
        speedText = speedGo.AddComponent<TextMeshProUGUI>();
        speedText.text = "0";
        speedText.fontSize = 104f;
        speedText.fontStyle = FontStyles.Bold;
        speedText.color = SpeedColor;
        speedText.alignment = TextAlignmentOptions.Center;
        speedText.verticalAlignment = VerticalAlignmentOptions.Middle;
        speedText.textWrappingMode = TextWrappingModes.NoWrap;
        speedText.raycastTarget = false;
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Roboto-Bold SDF");
        if (font != null) speedText.font = font;

        // km/h (bottom small)
        var unitGo = new GameObject("Unit");
        unitGo.transform.SetParent(block.transform, false);
        var unitRt = unitGo.AddComponent<RectTransform>();
        unitRt.anchorMin = new Vector2(0f, 0f);
        unitRt.anchorMax = new Vector2(1f, 0.26f);
        unitRt.offsetMin = Vector2.zero;
        unitRt.offsetMax = Vector2.zero;
        unitText = unitGo.AddComponent<TextMeshProUGUI>();
        unitText.text = "km/h";
        unitText.fontSize = 28f;
        unitText.color = new Color(1f, 1f, 1f, 0.75f);
        unitText.alignment = TextAlignmentOptions.Center;
        unitText.verticalAlignment = VerticalAlignmentOptions.Middle;
        unitText.raycastTarget = false;
        if (font != null) unitText.font = font;
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
            TryFindRCCP();
        }
    }

    void TryFindRCCP()
    {
        if (vehicle == null) return;
        foreach (var c in vehicle.GetComponents<Component>())
        {
            if (c.GetType().Name.Contains("RCCP_CarController"))
            {
                rccpController = c;
                rccpSpeedProperty = c.GetType().GetProperty("speed");
                useRCCP = rccpSpeedProperty != null;
                break;
            }
        }
    }

    void Update()
    {
        // Re-buscar tarde si el vehículo se instancia después de Start().
        if (vehicle == null) FindVehicle();

        UpdateSpeed();
        UpdateBlinkers();
    }

    void UpdateSpeed()
    {
        if (speedText == null) return;
        int kmh = Mathf.RoundToInt(GetSpeed());
        if (kmh < 0) kmh = 0;
        if (kmh != lastDisplayedSpeed)
        {
            lastDisplayedSpeed = kmh;
            speedText.text = kmh.ToString();
        }
    }

    void UpdateBlinkers()
    {
        if (leftArrow == null || rightArrow == null) return;

        bool showLeft = false, showRight = false;
        if (playerCar != null)
        {
            showLeft = playerCar.blinkLeft || playerCar.hazardActive;
            showRight = playerCar.blinkRight || playerCar.hazardActive;
        }

        if (showLeft || showRight)
        {
            blinkTimer += Time.deltaTime;
            if (blinkTimer >= BLINK_INTERVAL)
            {
                blinkTimer = 0f;
                blinkVisible = !blinkVisible;
            }
        }
        else
        {
            blinkTimer = 0f;
            blinkVisible = false;
        }

        // Toggle vía Image.enabled, no SetActive — no marca layout dirty.
        leftArrow.enabled = showLeft && blinkVisible;
        rightArrow.enabled = showRight && blinkVisible;
    }

    float GetSpeed()
    {
        if (useRCCP && rccpController != null && rccpSpeedProperty != null)
        {
            try { return (float)rccpSpeedProperty.GetValue(rccpController); }
            catch { }
        }
        if (vehicleRb != null) return vehicleRb.linearVelocity.magnitude * 3.6f;
        return 0f;
    }
}
