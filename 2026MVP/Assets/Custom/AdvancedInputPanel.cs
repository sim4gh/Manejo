using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Panel avanzado para tunear parámetros de sensibilidad del volante y freno en runtime.
/// Se auto-instancia y persiste entre escenas. Abrir manteniendo F9 por 1.5s.
/// Cambios se guardan en PlayerPrefs y se aplican en vivo (ReloadTuning).
/// </summary>
public class AdvancedInputPanel : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoCreate()
    {
        GameObject go = new GameObject("[AdvancedInputPanel]");
        go.AddComponent<AdvancedInputPanel>();
        DontDestroyOnLoad(go);
    }

    const float HOLD_TIME = 1.5f;

    // Rangos de los sliders
    const float STEER_MIN = 0.3f, STEER_MAX = 1.0f;
    const float STEER_DZ_MIN = 0.0f, STEER_DZ_MAX = 0.15f;
    const float BRAKE_END_MIN = 0.5f, BRAKE_END_MAX = 0.95f;
    const float BRAKE_OUT_MIN = 0.1f, BRAKE_OUT_MAX = 0.6f;
    const float GAS_CURVE_MIN = 0.5f, GAS_CURVE_MAX = 1.8f;

    float holdTimer = 0f;
    GameObject panelRoot;
    float prevTimeScale = 1f;

    Slider sliderSteer, sliderSteerDz, sliderBrakeEnd, sliderBrakeOut, sliderGasCurve;
    TextMeshProUGUI valueSteer, valueSteerDz, valueBrakeEnd, valueBrakeOut, valueGasCurve;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        bool panelOpen = panelRoot != null;

        // Hold F9 para abrir (solo si no está abierto)
        if (!panelOpen)
        {
            if (kb.f9Key.isPressed)
            {
                holdTimer += Time.unscaledDeltaTime;
                if (holdTimer >= HOLD_TIME)
                {
                    OpenPanel();
                    holdTimer = 0f;
                }
            }
            else
            {
                holdTimer = 0f;
            }
        }
        // Si está abierto: Escape o F9 pulsado (no held) cierra
        else
        {
            if (kb.escapeKey.wasPressedThisFrame) ClosePanel();
            else if (kb.f9Key.wasPressedThisFrame) ClosePanel();
        }
    }

    void OpenPanel()
    {
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        BuildUI();
    }

    void ClosePanel()
    {
        if (panelRoot != null) Destroy(panelRoot);
        panelRoot = null;
        Time.timeScale = prevTimeScale;
    }

    void BuildUI()
    {
        panelRoot = new GameObject("AdvancedInputPanelCanvas");
        DontDestroyOnLoad(panelRoot);

        Canvas canvas = panelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.targetDisplay = DisplayHelper.CenterDisplay;
        canvas.sortingOrder = 32000;

        CanvasScaler scaler = panelRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        panelRoot.AddComponent<GraphicRaycaster>();

        // Backdrop que bloquea input debajo
        GameObject backdrop = new GameObject("Backdrop");
        backdrop.transform.SetParent(panelRoot.transform, false);
        RectTransform bdRt = backdrop.AddComponent<RectTransform>();
        bdRt.anchorMin = Vector2.zero; bdRt.anchorMax = Vector2.one;
        bdRt.offsetMin = Vector2.zero; bdRt.offsetMax = Vector2.zero;
        Image bdImg = backdrop.AddComponent<Image>();
        bdImg.color = new Color(0, 0, 0, 0.75f);
        bdImg.raycastTarget = true;

        // Card
        GameObject card = new GameObject("Card");
        card.transform.SetParent(panelRoot.transform, false);
        RectTransform cardRt = card.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(760, 760);
        Image cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(0.1f, 0.12f, 0.16f, 0.98f);

        float y = 335f;

        CreateText(card.transform, "Configuración avanzada de input", 26f, FontStyles.Bold, Color.white, y);
        y -= 42f;
        CreateText(card.transform, "Cambios en vivo · Esc o F9 para cerrar", 12f, FontStyles.Italic, new Color(0.7f, 0.7f, 0.7f), y);
        y -= 38f;

        // Sección Volante
        CreateText(card.transform, "VOLANTE", 17f, FontStyles.Bold, new Color(1f, 0.85f, 0.2f), y);
        y -= 28f;
        sliderSteer = CreateSliderRow(card.transform, "Curva respuesta", STEER_MIN, STEER_MAX,
            PlayerPrefs.GetFloat(UIInputNew_PREF_STEER_CURVE_A, UIInputNew_DEFAULT_STEER_CURVE_A),
            y, OnSteerChanged, out valueSteer);
        y -= 36f;
        CreateText(card.transform, "1.00 = lineal · menor = más sensible en pequeños giros", 11f, FontStyles.Italic, new Color(0.6f, 0.7f, 0.7f), y);
        y -= 28f;
        sliderSteerDz = CreateSliderRow(card.transform, "Deadzone", STEER_DZ_MIN, STEER_DZ_MAX,
            PlayerPrefs.GetFloat(UIInputNew_PREF_STEER_DEADZONE, UIInputNew_DEFAULT_STEER_DEADZONE),
            y, OnSteerDzChanged, out valueSteerDz);
        y -= 36f;
        CreateText(card.transform, "Ignora micro-giros cerca del centro (estabilidad recta)", 11f, FontStyles.Italic, new Color(0.6f, 0.7f, 0.7f), y);
        y -= 34f;

        // Sección Freno
        CreateText(card.transform, "FRENO", 17f, FontStyles.Bold, new Color(1f, 0.85f, 0.2f), y);
        y -= 28f;
        sliderBrakeEnd = CreateSliderRow(card.transform, "Punto de quiebre", BRAKE_END_MIN, BRAKE_END_MAX,
            PlayerPrefs.GetFloat(UIInputNew_PREF_BRAKE_SOFT_END, UIInputNew_DEFAULT_BRAKE_SOFT_END),
            y, OnBrakeEndChanged, out valueBrakeEnd);
        y -= 36f;
        sliderBrakeOut = CreateSliderRow(card.transform, "Freno en quiebre", BRAKE_OUT_MIN, BRAKE_OUT_MAX,
            PlayerPrefs.GetFloat(UIInputNew_PREF_BRAKE_SOFT_MAX_OUTPUT, UIInputNew_DEFAULT_BRAKE_SOFT_MAX_OUTPUT),
            y, OnBrakeOutChanged, out valueBrakeOut);
        y -= 36f;
        CreateText(card.transform, "Ej: 0.80/0.30 → pedal al 80% = freno al 30%, luego sube a 100%", 11f, FontStyles.Italic, new Color(0.6f, 0.7f, 0.7f), y);
        y -= 34f;

        // Sección Acelerador
        CreateText(card.transform, "ACELERADOR", 17f, FontStyles.Bold, new Color(1f, 0.85f, 0.2f), y);
        y -= 28f;
        sliderGasCurve = CreateSliderRow(card.transform, "Curva (pow N)", GAS_CURVE_MIN, GAS_CURVE_MAX,
            PlayerPrefs.GetFloat(UIInputNew_PREF_GAS_CURVE_N, UIInputNew_DEFAULT_GAS_CURVE_N),
            y, OnGasCurveChanged, out valueGasCurve);
        y -= 36f;
        CreateText(card.transform, "1.00 = lineal · <1 arranque más vivo · >1 control fino", 11f, FontStyles.Italic, new Color(0.6f, 0.7f, 0.7f), y);
        y -= 48f;

        // Botones
        CreateButton(card.transform, "Restaurar defaults", -180f, y, new Color(0.5f, 0.35f, 0.1f), OnRestoreDefaults);
        CreateButton(card.transform, "Cerrar", 180f, y, new Color(0.12f, 0.4f, 0.6f), ClosePanel);
    }

    // Wrappers porque la clase UIInputNew está en namespace Gley.UrbanSystem
    // y C# no permite const imports en scope de clase.
    const string UIInputNew_PREF_STEER_CURVE_A        = "Adv_SteerCurveA";
    const string UIInputNew_PREF_STEER_DEADZONE       = "Adv_SteerDeadzone";
    const string UIInputNew_PREF_BRAKE_SOFT_END       = "Adv_BrakeSoftEnd";
    const string UIInputNew_PREF_BRAKE_SOFT_MAX_OUTPUT = "Adv_BrakeSoftMaxOutput";
    const string UIInputNew_PREF_GAS_CURVE_N          = "Adv_GasCurveN";
    const float  UIInputNew_DEFAULT_STEER_CURVE_A        = 1.0f;
    const float  UIInputNew_DEFAULT_STEER_DEADZONE       = 0.02f;
    const float  UIInputNew_DEFAULT_BRAKE_SOFT_END       = 0.8f;
    const float  UIInputNew_DEFAULT_BRAKE_SOFT_MAX_OUTPUT = 0.3f;
    const float  UIInputNew_DEFAULT_GAS_CURVE_N          = 1.0f;

    TextMeshProUGUI CreateText(Transform parent, string text, float size, FontStyles style, Color color, float yOffset)
    {
        GameObject obj = new GameObject("Text");
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(1, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, yOffset);
        rt.sizeDelta = new Vector2(-40, 30);
        TextMeshProUGUI t = obj.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    Slider CreateSliderRow(Transform parent, string labelText, float min, float max, float value,
        float yOffset, UnityEngine.Events.UnityAction<float> onChanged, out TextMeshProUGUI valueLabel)
    {
        GameObject row = new GameObject("Row_" + labelText);
        row.transform.SetParent(parent, false);
        RectTransform rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0, 0.5f);
        rowRt.anchorMax = new Vector2(1, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.anchoredPosition = new Vector2(0, yOffset);
        rowRt.sizeDelta = new Vector2(-60, 34);

        // Label a la izquierda
        GameObject lblObj = new GameObject("Label");
        lblObj.transform.SetParent(row.transform, false);
        RectTransform lblRt = lblObj.AddComponent<RectTransform>();
        lblRt.anchorMin = new Vector2(0, 0);
        lblRt.anchorMax = new Vector2(0.38f, 1);
        lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
        TextMeshProUGUI lblT = lblObj.AddComponent<TextMeshProUGUI>();
        lblT.text = labelText;
        lblT.fontSize = 16;
        lblT.color = Color.white;
        lblT.alignment = TextAlignmentOptions.MidlineLeft;
        lblT.raycastTarget = false;

        // Slider (centro)
        GameObject sObj = new GameObject("Slider");
        sObj.transform.SetParent(row.transform, false);
        RectTransform sRt = sObj.AddComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.38f, 0.25f);
        sRt.anchorMax = new Vector2(0.85f, 0.75f);
        sRt.offsetMin = Vector2.zero; sRt.offsetMax = Vector2.zero;
        Slider slider = sObj.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = Mathf.Clamp(value, min, max);

        // Background
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(sObj.transform, false);
        RectTransform bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.06f, 0.08f);

        // Fill Area + Fill
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sObj.transform, false);
        RectTransform faRt = fillArea.AddComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero; faRt.anchorMax = Vector2.one;
        faRt.offsetMin = new Vector2(4, 4); faRt.offsetMax = new Vector2(-4, -4);
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fRt = fill.AddComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one;
        fRt.offsetMin = Vector2.zero; fRt.offsetMax = Vector2.zero;
        Image fImg = fill.AddComponent<Image>();
        fImg.color = new Color(0.12f, 0.94f, 0.96f);
        slider.fillRect = fRt;

        // Handle Area + Handle
        GameObject hArea = new GameObject("Handle Slide Area");
        hArea.transform.SetParent(sObj.transform, false);
        RectTransform haRt = hArea.AddComponent<RectTransform>();
        haRt.anchorMin = Vector2.zero; haRt.anchorMax = Vector2.one;
        haRt.offsetMin = new Vector2(10, 0); haRt.offsetMax = new Vector2(-10, 0);
        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(hArea.transform, false);
        RectTransform hRt = handle.AddComponent<RectTransform>();
        hRt.sizeDelta = new Vector2(22, 22);
        Image hImg = handle.AddComponent<Image>();
        hImg.color = Color.white;
        slider.handleRect = hRt;
        slider.targetGraphic = hImg;

        // Valor (derecha)
        GameObject vObj = new GameObject("Value");
        vObj.transform.SetParent(row.transform, false);
        RectTransform vRt = vObj.AddComponent<RectTransform>();
        vRt.anchorMin = new Vector2(0.87f, 0);
        vRt.anchorMax = new Vector2(1, 1);
        vRt.offsetMin = Vector2.zero; vRt.offsetMax = Vector2.zero;
        TextMeshProUGUI vT = vObj.AddComponent<TextMeshProUGUI>();
        vT.text = slider.value.ToString("F2");
        vT.fontSize = 18;
        vT.fontStyle = FontStyles.Bold;
        vT.color = new Color(0.12f, 0.94f, 0.96f);
        vT.alignment = TextAlignmentOptions.Center;
        vT.raycastTarget = false;
        valueLabel = vT;

        slider.onValueChanged.AddListener(v =>
        {
            vT.text = v.ToString("F2");
            onChanged?.Invoke(v);
        });
        return slider;
    }

    void CreateButton(Transform parent, string text, float xOffset, float yOffset, Color bgColor, System.Action onClick)
    {
        GameObject btnObj = new GameObject("Btn_" + text);
        btnObj.transform.SetParent(parent, false);
        RectTransform btnRt = btnObj.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0.5f);
        btnRt.anchorMax = new Vector2(0.5f, 0.5f);
        btnRt.pivot = new Vector2(0.5f, 0.5f);
        btnRt.anchoredPosition = new Vector2(xOffset, yOffset);
        btnRt.sizeDelta = new Vector2(260, 50);
        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;
        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        GameObject tObj = new GameObject("Text");
        tObj.transform.SetParent(btnObj.transform, false);
        RectTransform tRt = tObj.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
        TextMeshProUGUI t = tObj.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = 18;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
    }

    // ── Callbacks ───────────────────────────────────────────────────────

    void OnSteerChanged(float v)
    {
        PlayerPrefs.SetFloat(UIInputNew_PREF_STEER_CURVE_A, v);
        PlayerPrefs.Save();
        NotifyInputReload();
    }

    void OnSteerDzChanged(float v)
    {
        PlayerPrefs.SetFloat(UIInputNew_PREF_STEER_DEADZONE, v);
        PlayerPrefs.Save();
        NotifyInputReload();
    }

    void OnBrakeEndChanged(float v)
    {
        PlayerPrefs.SetFloat(UIInputNew_PREF_BRAKE_SOFT_END, v);
        PlayerPrefs.Save();
        NotifyInputReload();
    }

    void OnBrakeOutChanged(float v)
    {
        PlayerPrefs.SetFloat(UIInputNew_PREF_BRAKE_SOFT_MAX_OUTPUT, v);
        PlayerPrefs.Save();
        NotifyInputReload();
    }

    void OnGasCurveChanged(float v)
    {
        PlayerPrefs.SetFloat(UIInputNew_PREF_GAS_CURVE_N, v);
        PlayerPrefs.Save();
        NotifyInputReload();
    }

    void OnRestoreDefaults()
    {
        Debug.Log("[AdvancedInputPanel] Restaurando defaults");
        // Setear sliders + llamar handlers explícitamente: si el valor ya era
        // el default, Slider.value = x no dispara onValueChanged y PlayerPrefs
        // quedaría con el valor anterior.
        if (sliderSteer != null)
        {
            sliderSteer.value = UIInputNew_DEFAULT_STEER_CURVE_A;
            if (valueSteer != null) valueSteer.text = UIInputNew_DEFAULT_STEER_CURVE_A.ToString("F2");
            OnSteerChanged(UIInputNew_DEFAULT_STEER_CURVE_A);
        }
        if (sliderSteerDz != null)
        {
            sliderSteerDz.value = UIInputNew_DEFAULT_STEER_DEADZONE;
            if (valueSteerDz != null) valueSteerDz.text = UIInputNew_DEFAULT_STEER_DEADZONE.ToString("F2");
            OnSteerDzChanged(UIInputNew_DEFAULT_STEER_DEADZONE);
        }
        if (sliderBrakeEnd != null)
        {
            sliderBrakeEnd.value = UIInputNew_DEFAULT_BRAKE_SOFT_END;
            if (valueBrakeEnd != null) valueBrakeEnd.text = UIInputNew_DEFAULT_BRAKE_SOFT_END.ToString("F2");
            OnBrakeEndChanged(UIInputNew_DEFAULT_BRAKE_SOFT_END);
        }
        if (sliderBrakeOut != null)
        {
            sliderBrakeOut.value = UIInputNew_DEFAULT_BRAKE_SOFT_MAX_OUTPUT;
            if (valueBrakeOut != null) valueBrakeOut.text = UIInputNew_DEFAULT_BRAKE_SOFT_MAX_OUTPUT.ToString("F2");
            OnBrakeOutChanged(UIInputNew_DEFAULT_BRAKE_SOFT_MAX_OUTPUT);
        }
        if (sliderGasCurve != null)
        {
            sliderGasCurve.value = UIInputNew_DEFAULT_GAS_CURVE_N;
            if (valueGasCurve != null) valueGasCurve.text = UIInputNew_DEFAULT_GAS_CURVE_N.ToString("F2");
            OnGasCurveChanged(UIInputNew_DEFAULT_GAS_CURVE_N);
        }
    }

    void NotifyInputReload()
    {
#pragma warning disable CS0618
        var input = Object.FindObjectOfType<Gley.UrbanSystem.UIInputNew>();
#pragma warning restore CS0618
        if (input != null) input.ReloadTuning();
    }
}
