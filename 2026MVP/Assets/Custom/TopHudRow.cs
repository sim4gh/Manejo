using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Row superior unificado del HUD de manejo: [Timer] | [Velocímetro] | [Velocidad Máxima].
/// Construye Canvas + 3 slots, adopta el container del ExamTimer existente, e instancia
/// los widgets de velocidad y límite. Sin DontDestroyOnLoad — vive y muere con la escena
/// junto al ExamTimer (mismo lifecycle).
/// </summary>
public class TopHudRow : MonoBehaviour
{
    public static TopHudRow Instance;

    private const float SpacingPx = 80f;
    private const float TimerSlotW = 180f;
    private const float TimerSlotH = 70f;
    private const float SpeedSlotW = 340f;
    private const float SpeedSlotH = 100f;
    private const float LimitSlotW = 140f;
    private const float LimitSlotH = 140f;

    private RectTransform row;
    private RectTransform timerSlot;
    private RectTransform speedSlot;
    private RectTransform limitSlot;
    private TopSpeedometerWidget speedometer;
    private SpeedLimitDisplay speedLimit;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        BuildCanvas();
        StartCoroutine(AdoptAndAttach());
    }

    void BuildCanvas()
    {
        var canvasGo = new GameObject("TopHudRowCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1001;
        canvas.targetDisplay = DisplayHelper.CockpitDisplay;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        // Sin GraphicRaycaster — HUD no interactivo.

        // Container del row, top-center, layout horizontal
        var rowGo = new GameObject("TopHudRow");
        rowGo.transform.SetParent(canvasGo.transform, false);
        row = rowGo.AddComponent<RectTransform>();
        row.anchorMin = new Vector2(0.5f, 1f);
        row.anchorMax = new Vector2(0.5f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.anchoredPosition = new Vector2(0f, -20f);
        // sizeDelta lo controla HorizontalLayoutGroup vía LayoutElement de hijos.
        row.sizeDelta = new Vector2(TimerSlotW + SpeedSlotW + LimitSlotW + SpacingPx * 2f,
                                    Mathf.Max(TimerSlotH, SpeedSlotH, LimitSlotH));

        var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = SpacingPx;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        timerSlot = CreateSlot(row, "TimerSlot", TimerSlotW, TimerSlotH);
        speedSlot = CreateSlot(row, "SpeedometerSlot", SpeedSlotW, SpeedSlotH);
        limitSlot = CreateSlot(row, "SpeedLimitSlot", LimitSlotW, LimitSlotH);
    }

    static RectTransform CreateSlot(Transform parent, string name, float w, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = w;
        le.preferredHeight = h;
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;
        return rt;
    }

    System.Collections.IEnumerator AdoptAndAttach()
    {
        // Esperar a que ExamTimer.CreateTimerUI termine (corre dentro de InitAfterFrame
        // que hace yield 1 frame). Esperamos 2 frames para asegurar.
        yield return null;
        yield return null;

        AttachTimer();
        BuildSpeedometer();
        BuildSpeedLimit();

        // Force layout 1× para evitar primer frame con anchors viejos del ExamTimer.
        if (row != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
    }

    void AttachTimer()
    {
        if (ExamTimer.Instance != null && timerSlot != null)
        {
            ExamTimer.Instance.AttachContainerTo(timerSlot);
        }
    }

    void BuildSpeedometer()
    {
        if (speedSlot == null) return;
        var widgetGo = new GameObject("Speedometer");
        widgetGo.transform.SetParent(speedSlot, false);
        var rt = widgetGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        speedometer = widgetGo.AddComponent<TopSpeedometerWidget>();
        speedometer.Build();
    }

    void BuildSpeedLimit()
    {
        if (limitSlot == null) return;
        var go = new GameObject("SpeedLimitSign");
        go.transform.SetParent(limitSlot, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Letrero procedural: círculo rojo borde + interior blanco. El sprite anterior
        // (limite@3x.png) era una captura del letrero 3D del mundo y se veía pixeleado.
        Sprite circle = GetCircleSprite();
        var bg = go.AddComponent<Image>();
        bg.sprite = circle;
        bg.preserveAspect = true;
        bg.raycastTarget = false;

        // Número grande negro centrado en el círculo (anchors en el centro vertical).
        var textObj = new GameObject("LimitText");
        textObj.transform.SetParent(go.transform, false);
        var textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.15f, 0.15f);
        textRt.anchorMax = new Vector2(0.85f, 0.85f);
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "40";
        tmp.fontSize = 56f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.color = Color.black;
        tmp.raycastTarget = false;
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Roboto-Bold SDF");
        if (font != null) tmp.font = font;

        speedLimit = go.AddComponent<SpeedLimitDisplay>();
        speedLimit.limitText = tmp;
        speedLimit.signBackground = bg;
        // El TMP queda sobre fondo blanco → normalColor debe ser negro, no el default white.
        speedLimit.normalColor = Color.black;
        // Warning (overSpeed > 0): naranja oscuro para que se vea contra el blanco.
        speedLimit.warningColor = new Color(0.85f, 0.5f, 0f, 1f);
        // Danger (overSpeed > 10): rojo intenso, parpadea contra blanco.
        speedLimit.dangerColor = new Color(0.85f, 0.1f, 0.1f, 1f);

        Rigidbody rb = FindPlayerRigidbody();
        ViolationDetector vd = Object.FindFirstObjectByType<ViolationDetector>();
        speedLimit.Initialize(rb, vd);
    }

    // Cache del sprite circular procedural (rojo borde + interior blanco) para no
    // regenerar la textura por cada escena cargada.
    private static Sprite cachedCircleSprite;

    static Sprite GetCircleSprite()
    {
        if (cachedCircleSprite != null) return cachedCircleSprite;

        const int Size = 256;
        const float BorderFraction = 0.16f; // 16% del radio para el grosor del anillo
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[Size * Size];
        var center = new Vector2(Size * 0.5f, Size * 0.5f);
        float outerR = Size * 0.5f - 1f;
        float innerR = outerR * (1f - BorderFraction);
        Color red = new Color(0.82f, 0.10f, 0.10f, 1f);
        Color white = Color.white;
        Color clear = new Color(1f, 1f, 1f, 0f);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                Color c;
                if (d <= innerR - 1f) c = white;
                else if (d <= innerR) c = Color.Lerp(white, red, d - (innerR - 1f));
                else if (d <= outerR - 1f) c = red;
                else if (d <= outerR) c = Color.Lerp(red, clear, d - (outerR - 1f));
                else c = clear;
                pixels[y * Size + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(false, false);
        cachedCircleSprite = Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f));
        return cachedCircleSprite;
    }

    static Rigidbody FindPlayerRigidbody()
    {
        // Mismo patrón robusto que SimpleSpeedGauge: por nombre, luego por componente.
        var go = GameObject.Find("Player");
        if (go == null)
        {
            var pc = Object.FindFirstObjectByType<Gley.UrbanSystem.PlayerCar>();
            if (pc != null) go = pc.gameObject;
        }
        return go != null ? go.GetComponent<Rigidbody>() : null;
    }

    void OnDestroy()
    {
        // Reattach defensivo del container del timer si éste sigue vivo (caso raro
        // donde row muere antes que ExamTimer).
        if (ExamTimer.Instance != null)
        {
            ExamTimer.Instance.AttachContainerTo(null);
        }
        if (Instance == this) Instance = null;
    }
}
