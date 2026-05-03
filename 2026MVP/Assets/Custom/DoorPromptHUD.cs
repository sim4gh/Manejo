using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Gley.UrbanSystem;

/// <summary>
/// Widget HUD que muestra "Presiona [BOTÓN] para abrir puerta" cuando el bus
/// está detenido en una zona de parada y la puerta está cerrada. Singleton
/// bootstrapped (mismo patrón que CollisionFeedback). Solo activo en la escena
/// BusPasajeros.
/// </summary>
public class DoorPromptHUD : MonoBehaviour
{
    public static DoorPromptHUD Instance { get; private set; }

    private static readonly HashSet<string> ACTIVE_SCENES = new HashSet<string> { "BusPasajeros" };

    private Canvas canvas;
    private CanvasGroup group;
    private TextMeshProUGUI label;
    private List<ParadaManagerBus> _paradas = new List<ParadaManagerBus>();
    private float _refreshTimer;
    private const float REFRESH_INTERVAL = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[DoorPromptHUD]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<DoorPromptHUD>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildCanvas();
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool active = ACTIVE_SCENES.Contains(scene.name);
        if (canvas != null) canvas.enabled = active;
        if (active) RefreshParadas();
    }

    void RefreshParadas()
    {
        _paradas.Clear();
        var found = Object.FindObjectsByType<ParadaManagerBus>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        _paradas.AddRange(found);
    }

    void Update()
    {
        if (canvas == null || !canvas.enabled) return;

        _refreshTimer += Time.unscaledDeltaTime;
        if (_refreshTimer >= REFRESH_INTERVAL)
        {
            _refreshTimer = 0f;
            // Re-scan por si se cargaron paradas adicionales después del scene load.
            if (_paradas.Count == 0) RefreshParadas();
        }

        bool show = false;
        for (int i = 0; i < _paradas.Count; i++)
        {
            var p = _paradas[i];
            if (p != null && p.IsPromptVisible()) { show = true; break; }
        }

        group.alpha = show ? 1f : 0f;
        if (show) label.text = BuildPromptText();
    }

    string BuildPromptText()
    {
        var ui = Object.FindAnyObjectByType<UIInputNew>();
        string raw = ui != null ? ui.GetDoorBindingPath() : "";
        string hint = FormatBindingPath(raw);
        return $"Presiona <b>{hint}</b> para abrir puerta";
    }

    static string FormatBindingPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "[Configurar en F8]";
        // wheel:buttonN → "Botón W{N}", shifter:buttonN → "Botón S{N}".
        if (path.StartsWith("wheel:button"))   return "Botón W" + path.Substring("wheel:button".Length);
        if (path.StartsWith("shifter:button")) return "Botón S" + path.Substring("shifter:button".Length);
        return path;
    }

    void BuildCanvas()
    {
        var canvasGo = new GameObject("DoorPromptCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4000;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGo.transform, false);
        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0, 80);
        rt.sizeDelta = new Vector2(620, 80);

        group = panel.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        var text = new GameObject("Label");
        text.transform.SetParent(panel.transform, false);
        label = text.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 32;
        label.color = Color.white;
        label.text = "Presiona [BOTÓN] para abrir puerta";
        var trt = text.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(20, 10);
        trt.offsetMax = new Vector2(-20, -10);
    }
}
