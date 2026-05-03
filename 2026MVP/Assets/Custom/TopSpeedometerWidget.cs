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
    // Verde direccional brillante (blink ON cuando direccional activa).
    private static readonly Color ArrowColor = new Color(0.1f, 0.9f, 0.2f, 1f);
    // Verde tenue (blink OFF, mantiene presencia visual al parpadear).
    private static readonly Color ArrowDimColor = new Color(0.1f, 0.9f, 0.2f, 0.25f);
    // Blanco sutil para el estado idle (sin direccional). Hace que las flechas
    // estén siempre visibles como guía, sin distraer del speed.
    private static readonly Color ArrowIdleColor = new Color(1f, 1f, 1f, 0.35f);
    // Cyan consistente con FallbackSpeedHud.
    private static readonly Color SpeedColor = new Color(0.2f, 0.9f, 0.9f, 1f);

    private TextMeshProUGUI leftArrow;
    public TextMeshProUGUI speedText;
    private TextMeshProUGUI rightArrow;
    private TextMeshProUGUI gearStripText;
    private Image bg;

    // Sufijo inline rendereado al lado del número con tamaño chico (rich text).
    // Se concatena en UpdateSpeed sin alocar nada extra que la string del int.
    private const string SPEED_UNIT_SUFFIX = "<size=28> km/h</size>";

    // Pre-construidos: strip horizontal con todas las marchas, la activa
    // resaltada en cyan + bold y las demás atenuadas en gris. Index = gear+1
    // (R=-1→0, N=0→1, 1→2, ..., 6→7). Se generan una sola vez en static ctor;
    // Update solo elige el string ya cacheado y solo cuando el gear cambia.
    private static readonly string[] STRIP_MANUAL_BY_GEAR;
    private static readonly string[] STRIP_AUTO_BY_GEAR;
    private const string STRIP_ACTIVE_OPEN = "<color=#33EEEE><b>";
    private const string STRIP_ACTIVE_CLOSE = "</b></color>";
    private const string STRIP_DIM_OPEN = "<color=#666666>";
    private const string STRIP_DIM_CLOSE = "</color>";
    private const string STRIP_SEP = "  ";

    private int _lastStripGear = int.MinValue;
    private bool _lastStripAuto;

    static TopSpeedometerWidget()
    {
        string[] manualLabels = { "1", "2", "3", "4", "5", "6", "R" };
        STRIP_MANUAL_BY_GEAR = new string[8];
        STRIP_MANUAL_BY_GEAR[0] = BuildStrip(manualLabels, 6); // R activo
        STRIP_MANUAL_BY_GEAR[1] = BuildStrip(manualLabels, -1); // N: ninguno
        for (int g = 1; g <= 6; g++)
            STRIP_MANUAL_BY_GEAR[g + 1] = BuildStrip(manualLabels, g - 1);

        string[] autoLabels = { "D", "R" };
        STRIP_AUTO_BY_GEAR = new string[3];
        STRIP_AUTO_BY_GEAR[0] = BuildStrip(autoLabels, 1); // R activo
        STRIP_AUTO_BY_GEAR[1] = BuildStrip(autoLabels, -1); // N transitorio
        STRIP_AUTO_BY_GEAR[2] = BuildStrip(autoLabels, 0); // D activo (gear>=1)
    }

    private static string BuildStrip(string[] labels, int activeIdx)
    {
        var sb = new System.Text.StringBuilder(160);
        for (int i = 0; i < labels.Length; i++)
        {
            if (i > 0) sb.Append(STRIP_SEP);
            if (i == activeIdx)
                sb.Append(STRIP_ACTIVE_OPEN).Append(labels[i]).Append(STRIP_ACTIVE_CLOSE);
            else
                sb.Append(STRIP_DIM_OPEN).Append(labels[i]).Append(STRIP_DIM_CLOSE);
        }
        return sb.ToString();
    }

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

        leftArrow = CreateArrow("LeftArrow", "◄");
        BuildSpeedBlock();
        rightArrow = CreateArrow("RightArrow", "►");

        FindVehicle();
    }

    TextMeshProUGUI CreateArrow(string name, string symbol)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 56f;
        le.preferredHeight = 130f;
        le.flexibleWidth = 0f;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = symbol;
        // 50% del tamaño original (92→46) — sutiles, no compiten con el "100".
        tmp.fontSize = 46f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = ArrowIdleColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.raycastTarget = false;
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Roboto-Bold SDF");
        if (font != null) tmp.font = font;
        return tmp;
    }

    void BuildSpeedBlock()
    {
        // Contenedor: [Speed grande con km/h inline] arriba + [GearStrip] abajo.
        var block = new GameObject("SpeedBlock");
        block.transform.SetParent(transform, false);
        block.AddComponent<RectTransform>();
        var le = block.AddComponent<LayoutElement>();
        le.preferredWidth = 260f;
        le.preferredHeight = 130f;
        le.flexibleWidth = 0f;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Roboto-Bold SDF");

        // Speed (~70% superior): "100 km/h" con "km/h" inline pequeño via rich text.
        var speedGo = new GameObject("Speed");
        speedGo.transform.SetParent(block.transform, false);
        var speedRt = speedGo.AddComponent<RectTransform>();
        speedRt.anchorMin = new Vector2(0f, 0.32f);
        speedRt.anchorMax = new Vector2(1f, 1f);
        speedRt.offsetMin = Vector2.zero;
        speedRt.offsetMax = Vector2.zero;
        speedText = speedGo.AddComponent<TextMeshProUGUI>();
        speedText.text = "0" + SPEED_UNIT_SUFFIX;
        speedText.fontSize = 104f;
        speedText.fontStyle = FontStyles.Bold;
        speedText.color = SpeedColor;
        speedText.alignment = TextAlignmentOptions.Center;
        speedText.verticalAlignment = VerticalAlignmentOptions.Middle;
        speedText.textWrappingMode = TextWrappingModes.NoWrap;
        speedText.richText = true;
        speedText.raycastTarget = false;
        if (font != null) speedText.font = font;

        // GearStrip (~32% inferior): "1 2 3 4 5 6 R" / "D R", centrado.
        var stripGo = new GameObject("GearStrip");
        stripGo.transform.SetParent(block.transform, false);
        var stripRt = stripGo.AddComponent<RectTransform>();
        stripRt.anchorMin = new Vector2(0f, 0f);
        stripRt.anchorMax = new Vector2(1f, 0.32f);
        stripRt.offsetMin = Vector2.zero;
        stripRt.offsetMax = Vector2.zero;
        gearStripText = stripGo.AddComponent<TextMeshProUGUI>();
        gearStripText.text = STRIP_MANUAL_BY_GEAR[1]; // N inicial
        gearStripText.fontSize = 28f;
        gearStripText.fontStyle = FontStyles.Bold;
        gearStripText.color = SpeedColor;
        gearStripText.alignment = TextAlignmentOptions.Center;
        gearStripText.verticalAlignment = VerticalAlignmentOptions.Middle;
        gearStripText.textWrappingMode = TextWrappingModes.NoWrap;
        gearStripText.richText = true;
        gearStripText.overflowMode = TextOverflowModes.Overflow;
        gearStripText.raycastTarget = false;
        if (font != null) gearStripText.font = font;
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
        UpdateGearStrip();
    }

    void UpdateGearStrip()
    {
        if (gearStripText == null || playerCar == null) return;
        int gear = playerCar.currentGear;
        bool isAuto = playerCar.isAutomaticMode;
        if (gear == _lastStripGear && isAuto == _lastStripAuto) return;
        _lastStripGear = gear;
        _lastStripAuto = isAuto;
        if (isAuto)
        {
            // Auto: gear -1 → R, gear 0 → N (transitorio), gear>=1 → D.
            int autoIdx = gear == -1 ? 0 : (gear == 0 ? 1 : 2);
            gearStripText.text = STRIP_AUTO_BY_GEAR[autoIdx];
        }
        else
        {
            // Manual: index = gear+1, clamp por seguridad.
            int manIdx = Mathf.Clamp(gear + 1, 0, STRIP_MANUAL_BY_GEAR.Length - 1);
            gearStripText.text = STRIP_MANUAL_BY_GEAR[manIdx];
        }
    }

    void UpdateSpeed()
    {
        if (speedText == null) return;
        int kmh = Mathf.RoundToInt(GetSpeed());
        if (kmh < 0) kmh = 0;
        if (kmh != lastDisplayedSpeed)
        {
            lastDisplayedSpeed = kmh;
            speedText.text = kmh.ToString() + SPEED_UNIT_SUFFIX;
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

        // Idle: blanco sutil siempre visible (guía visual). Direccional activa:
        // verde brillante cuando blinkVisible, verde tenue (no apagado) en off
        // — mantiene presencia para que el blink se vea como pulso, no como
        // aparición/desaparición que distrae.
        leftArrow.color = showLeft
            ? (blinkVisible ? ArrowColor : ArrowDimColor)
            : ArrowIdleColor;
        rightArrow.color = showRight
            ? (blinkVisible ? ArrowColor : ArrowDimColor)
            : ArrowIdleColor;
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
