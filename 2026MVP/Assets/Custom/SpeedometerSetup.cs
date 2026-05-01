#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

/// <summary>
/// LEGACY editor-only utility para construir Speed/RPM Gauges y SpeedLimitSign
/// proceduralmente vía menú GameObject > UI. La auto-instanciación en runtime
/// pasó a TopHudRow + ExamBootstrap (Apr 2026); estos MenuItem permanecen como
/// herramienta de Editor para escenas demo fuera del flujo de examen.
/// </summary>
public class SpeedometerSetup : MonoBehaviour
{
    // Colors matching the gauge design
    private static readonly Color CyanColor = new Color(0.2f, 0.9f, 0.9f, 1f);
    private static readonly Color LightBlueColor = new Color(0.4f, 0.6f, 0.8f, 1f);
    private static readonly float GaugeSize = 200f;
    private static readonly float GaugeSpacing = 20f;

    [MenuItem("GameObject/UI/Create Dashboard (Speed + RPM)", false, 10)]
    static void CreateDashboard()
    {
        // Find or create Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("DashboardCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Create dashboard container (bottom center)
        GameObject dashboard = new GameObject("Dashboard");
        dashboard.transform.SetParent(canvas.transform, false);
        RectTransform dashRect = dashboard.AddComponent<RectTransform>();
        dashRect.anchorMin = new Vector2(0.5f, 0);
        dashRect.anchorMax = new Vector2(0.5f, 0);
        dashRect.pivot = new Vector2(0.5f, 0);
        dashRect.anchoredPosition = new Vector2(0, 20);
        dashRect.sizeDelta = new Vector2(GaugeSize * 2 + GaugeSpacing, GaugeSize);

        // Load sprite once
        Sprite gaugeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/speed@2x.png");
        if (gaugeSprite == null)
        {
            Debug.LogWarning("[Dashboard] speed@2x.png not found at Assets/Images/ - set sprites manually");
        }

        // Create Speed Gauge (LEFT)
        GameObject speedGauge = CreateGauge(dashboard.transform, "SpeedGauge",
            new Vector2(-GaugeSize/2 - GaugeSpacing/2, 0), gaugeSprite);
        CreateGaugeText(speedGauge.transform, "SpeedText", "0", 72, new Vector2(0, 10));
        CreateGaugeText(speedGauge.transform, "UnitText", "km/h", 22, new Vector2(0, -35));

        SimpleSpeedGauge speedScript = speedGauge.AddComponent<SimpleSpeedGauge>();
        speedScript.gaugeImage = speedGauge.GetComponent<Image>();
        speedScript.speedText = speedGauge.transform.Find("SpeedText").GetComponent<TextMeshProUGUI>();
        speedScript.textColor = CyanColor;

        // Create RPM Gauge (RIGHT)
        GameObject rpmGauge = CreateGauge(dashboard.transform, "RPMGauge",
            new Vector2(GaugeSize/2 + GaugeSpacing/2, 0), gaugeSprite);
        CreateGaugeText(rpmGauge.transform, "RPMText", "0.0", 72, new Vector2(0, 10));
        CreateGaugeText(rpmGauge.transform, "UnitText", "x1000", 22, new Vector2(0, -35));

        SimpleRPMGauge rpmScript = rpmGauge.AddComponent<SimpleRPMGauge>();
        rpmScript.gaugeImage = rpmGauge.GetComponent<Image>();
        rpmScript.rpmText = rpmGauge.transform.Find("RPMText").GetComponent<TextMeshProUGUI>();
        rpmScript.normalColor = CyanColor;

        Selection.activeGameObject = dashboard;
        Debug.Log("[Dashboard] Created Speed + RPM gauges! Make sure speed@2x.png is set to Sprite (2D and UI) with Single mode");
    }

    [MenuItem("GameObject/UI/Create Speed Limit Sign", false, 13)]
    static void CreateSpeedLimitSign()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("UICanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Load the speed limit sign sprite
        Sprite signSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/limite@3x.png");
        if (signSprite == null)
        {
            Debug.LogWarning("[SpeedLimitSign] limite@3x.png not found at Assets/Images/ - using placeholder");
        }

        // Container for speed limit sign (TOP RIGHT)
        GameObject signObj = new GameObject("SpeedLimitSign");
        signObj.transform.SetParent(canvas.transform, false);

        RectTransform signRect = signObj.AddComponent<RectTransform>();
        signRect.anchorMin = new Vector2(1f, 1f);  // Top right
        signRect.anchorMax = new Vector2(1f, 1f);
        signRect.pivot = new Vector2(1f, 1f);
        signRect.anchoredPosition = new Vector2(-20, -20);
        signRect.sizeDelta = new Vector2(100, 130);  // Taller for MAXIMA text

        // Sign background image
        Image signBg = signObj.AddComponent<Image>();
        if (signSprite != null)
        {
            signBg.sprite = signSprite;
            signBg.preserveAspect = true;
        }
        else
        {
            signBg.color = Color.white;
        }
        signBg.raycastTarget = false;

        // Speed limit number (positioned ABOVE the "Km/h" text in the circle)
        GameObject limitTextObj = new GameObject("LimitText");
        limitTextObj.transform.SetParent(signObj.transform, false);
        RectTransform textRect = limitTextObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0.52f);
        textRect.anchorMax = new Vector2(1, 0.80f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI limitTmp = limitTextObj.AddComponent<TextMeshProUGUI>();
        limitTmp.text = "40";
        limitTmp.fontSize = 32;
        limitTmp.fontStyle = FontStyles.Bold;
        limitTmp.alignment = TextAlignmentOptions.Center;
        limitTmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        limitTmp.color = Color.black;
        limitTmp.raycastTarget = false;

        // Add the display script
        SpeedLimitDisplay displayScript = signObj.AddComponent<SpeedLimitDisplay>();
        displayScript.limitText = limitTmp;
        displayScript.signBackground = signBg;

        Selection.activeGameObject = signObj;
        Debug.Log("[SpeedLimitSign] Created in top-right corner! Make sure limite@3x.png is set to Sprite (2D and UI)");
    }

    [MenuItem("GameObject/UI/Create Speed Gauge Only", false, 11)]
    static void CreateSpeedGaugeOnly()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("GaugeCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        Sprite gaugeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/speed@2x.png");

        GameObject speedGauge = CreateGauge(canvas.transform, "SpeedGauge", Vector2.zero, gaugeSprite);
        RectTransform rect = speedGauge.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(0, 0);
        rect.pivot = new Vector2(0, 0);
        rect.anchoredPosition = new Vector2(30, 30);

        CreateGaugeText(speedGauge.transform, "SpeedText", "0", 72, new Vector2(0, 10));
        CreateGaugeText(speedGauge.transform, "UnitText", "km/h", 22, new Vector2(0, -35));

        SimpleSpeedGauge script = speedGauge.AddComponent<SimpleSpeedGauge>();
        script.gaugeImage = speedGauge.GetComponent<Image>();
        script.speedText = speedGauge.transform.Find("SpeedText").GetComponent<TextMeshProUGUI>();
        script.textColor = CyanColor;

        Selection.activeGameObject = speedGauge;
    }

    [MenuItem("GameObject/UI/Create RPM Gauge Only", false, 12)]
    static void CreateRPMGaugeOnly()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("GaugeCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        Sprite gaugeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/speed@2x.png");

        GameObject rpmGauge = CreateGauge(canvas.transform, "RPMGauge", Vector2.zero, gaugeSprite);
        RectTransform rect = rpmGauge.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-30, 30);

        CreateGaugeText(rpmGauge.transform, "RPMText", "0.0", 72, new Vector2(0, 10));
        CreateGaugeText(rpmGauge.transform, "UnitText", "x1000", 22, new Vector2(0, -35));

        SimpleRPMGauge script = rpmGauge.AddComponent<SimpleRPMGauge>();
        script.gaugeImage = rpmGauge.GetComponent<Image>();
        script.rpmText = rpmGauge.transform.Find("RPMText").GetComponent<TextMeshProUGUI>();
        script.normalColor = CyanColor;

        Selection.activeGameObject = rpmGauge;
    }

    static GameObject CreateGauge(Transform parent, string name, Vector2 position, Sprite sprite)
    {
        GameObject gauge = new GameObject(name);
        gauge.transform.SetParent(parent, false);

        RectTransform rect = gauge.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(GaugeSize, GaugeSize);

        Image img = gauge.AddComponent<Image>();
        if (sprite != null)
        {
            img.sprite = sprite;
            img.preserveAspect = true;
        }
        img.raycastTarget = false;

        return gauge;
    }

    static void CreateGaugeText(Transform parent, string name, string defaultText, float fontSize, Vector2 position)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);

        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(150, 80);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.fontSize = fontSize;
        tmp.fontStyle = fontSize > 50 ? FontStyles.Bold : FontStyles.Normal;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = fontSize > 50 ? CyanColor : LightBlueColor;
        tmp.raycastTarget = false;
    }
}
#endif
