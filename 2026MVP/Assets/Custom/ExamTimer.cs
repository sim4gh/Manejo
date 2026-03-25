using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Countdown de 5 minutos para el examen de manejo.
/// Crea su propia UI proceduralmente en el Canvas.
/// Al llegar a 0:00, exporta telemetría y muestra resultados.
/// </summary>
public class ExamTimer : MonoBehaviour
{
    public static ExamTimer Instance;

    [Header("Configuración")]
    [Tooltip("Duración del examen en segundos (300 = 5 min)")]
    public float examDuration = 300f;

    /// <summary>Evento disparado cuando el examen termina.</summary>
    public System.Action OnExamFinished;

    private float startTime;
    private bool examFinished;
    private TextMeshProUGUI timerText;
    private Image bgPanel;

    private int lastDisplayedSecond = -1;
    private int lastKnownScore = 100;

    // Colores por fase
    private static readonly Color colorNormal  = Color.white;
    private static readonly Color colorWarning = Color.yellow;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        startTime = Time.time;
        CreateTimerUI();
    }

    void CreateTimerUI()
    {
        // Buscar Canvas root (ScreenSpaceOverlay) — mismo patrón que MenuBootstrap
#pragma warning disable CS0618
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
#pragma warning restore CS0618

        Canvas targetCanvas = null;
        foreach (var canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.transform.parent == null)
            {
                targetCanvas = canvas;
                break;
            }
        }

        if (targetCanvas == null && canvases.Length > 0)
            targetCanvas = canvases[0];

        if (targetCanvas == null)
        {
            Debug.LogWarning("[ExamTimer] No se encontró Canvas en la escena");
            return;
        }

        // Contenedor del timer
        GameObject container = new GameObject("ExamTimerHUD");
        container.transform.SetParent(targetCanvas.transform, false);
        RectTransform containerRt = container.AddComponent<RectTransform>();
        // Top-center
        containerRt.anchorMin = new Vector2(0.5f, 1f);
        containerRt.anchorMax = new Vector2(0.5f, 1f);
        containerRt.pivot = new Vector2(0.5f, 1f);
        containerRt.anchoredPosition = new Vector2(0f, -15f);
        containerRt.sizeDelta = new Vector2(180f, 55f);

        // Fondo semi-transparente oscuro con esquinas redondeadas
        bgPanel = container.AddComponent<Image>();
        bgPanel.sprite = MenuCardBuilder.GetRoundedSprite(MenuTheme.CornerRadiusSmall);
        bgPanel.type = Image.Type.Sliced;
        bgPanel.color = new Color(0f, 0f, 0f, 0.6f);
        bgPanel.raycastTarget = false;

        // Texto del timer
        GameObject textObj = new GameObject("TimerText");
        textObj.transform.SetParent(container.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        timerText = textObj.AddComponent<TextMeshProUGUI>();
        timerText.text = FormatTime(examDuration);
        timerText.fontSize = 42f;
        timerText.fontStyle = FontStyles.Bold;
        timerText.color = colorNormal;
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.verticalAlignment = VerticalAlignmentOptions.Middle;
        timerText.raycastTarget = false;

        // Intentar usar Roboto Bold (misma fuente del menú)
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Roboto-Bold SDF");
        if (font != null) timerText.font = font;
    }

    void Update()
    {
        if (examFinished || timerText == null) return;

        float remainingTime = examDuration - (Time.time - startTime);

        if (remainingTime <= 0f)
        {
            EndExam();
            return;
        }

        // Actualizar display solo cuando cambia el segundo (evita GC cada frame)
        int totalSec = Mathf.CeilToInt(remainingTime);
        if (totalSec != lastDisplayedSecond)
        {
            lastDisplayedSecond = totalSec;
            timerText.text = FormatTime(remainingTime);
        }

        // Feedback visual por fase
        if (remainingTime <= 10f)
        {
            // Rojo + parpadeo
            timerText.color = MenuTheme.TextError;
            float alpha = Mathf.Lerp(0.3f, 1f, Mathf.PingPong(Time.time * 3f, 1f));
            timerText.alpha = alpha;
        }
        else if (remainingTime <= 30f)
        {
            // Rojo fijo
            timerText.color = MenuTheme.TextError;
            timerText.alpha = 1f;
        }
        else if (remainingTime <= 60f)
        {
            // Amarillo
            timerText.color = colorWarning;
            timerText.alpha = 1f;
        }
        else
        {
            // Blanco normal
            timerText.color = colorNormal;
            timerText.alpha = 1f;
        }

        // Cachear score para interrupción (OnDestroy/OnApplicationQuit)
        ViolationDetector det = Object.FindFirstObjectByType<ViolationDetector>();
        if (det != null) lastKnownScore = det.totalScore;
    }

    void EndExam()
    {
        examFinished = true;
        timerText.text = "0:00";
        timerText.color = MenuTheme.TextError;
        timerText.alpha = 1f;

        // Log evento de fin
        TelemetryLogger.Instance?.LogEvent("FIN_EXAMEN", "Tiempo agotado", 0, 0f);

        // Exportar telemetría
        ViolationDetector detector = Object.FindFirstObjectByType<ViolationDetector>();
        int finalScore = 100;
        if (detector != null)
        {
            finalScore = detector.totalScore;
            detector.ExportTelemetry();
        }

        // Disparar evento
        OnExamFinished?.Invoke();

        // Enviar resultados al backend
        SendResultsToApi(finalScore);

        // Mostrar pantalla de resultados
        ExamResultsScreen.Show(finalScore);
    }

    void SendResultsToApi(int finalScore)
    {
        string sessionId = GameManager.Instance?.SessionId;
        if (string.IsNullOrEmpty(sessionId))
        {
            Debug.LogWarning("[ExamTimer] No hay sessionId — resultados solo guardados localmente");
            return;
        }

        bool passed = finalScore >= 70;
        var faults = SimulatorApiClient.BuildFaultsFromTelemetry();

        StartCoroutine(SimulatorApiClient.EndSession(sessionId, passed, finalScore, faults, (success) =>
        {
            if (!success)
            {
                Debug.LogWarning("[ExamTimer] Fallo al enviar resultados — guardando para retry");
                SimulatorApiClient.SavePendingResult(sessionId, passed, finalScore, faults);
            }
        }));
    }

    // ── Interrupción: guardar resultados parciales si el examen no terminó ──

    void OnApplicationQuit()
    {
        SavePartialIfNeeded();
    }

    void OnDestroy()
    {
        if (!Application.isPlaying) return;
        SavePartialIfNeeded();
    }

    void SavePartialIfNeeded()
    {
        if (examFinished) return;

        string sessionId = GameManager.Instance?.SessionId;
        if (string.IsNullOrEmpty(sessionId)) return;

        bool passed = lastKnownScore >= 70;
        var faults = SimulatorApiClient.BuildFaultsFromTelemetry();
        SimulatorApiClient.SavePendingResult(sessionId, passed, lastKnownScore, faults);
        Debug.Log($"[ExamTimer] Examen interrumpido — resultados parciales guardados (score={lastKnownScore})");

        // Marcar como finished para evitar doble guardado (OnDestroy + OnApplicationQuit)
        examFinished = true;
    }

    static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int min = (int)(seconds / 60f);
        int sec = (int)(seconds % 60f);
        return $"{min}:{sec:D2}";
    }
}
