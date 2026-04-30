using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TMPro;
using System.Collections;

/// <summary>
/// Overlay fullscreen con resultados del examen.
/// Congela la simulación, muestra score APTO/NO APTO,
/// y regresa automáticamente al menú en 15 segundos.
/// </summary>
public class ExamResultsScreen : MonoBehaviour
{
    private int score;
    private TextMeshProUGUI countdownText;
    private const float AUTO_RETURN_SECONDS = 15f;

    /// <summary>
    /// Entry point estático — crea el overlay de resultados.
    /// </summary>
    public static void Show(int finalScore)
    {
        GameObject obj = new GameObject("ExamResultsOverlay");
        var screen = obj.AddComponent<ExamResultsScreen>();
        screen.score = finalScore;
    }

    private float showTime;

    void Start()
    {
        Time.timeScale = 0f;
        showTime = Time.realtimeSinceStartup;
        BuildUI();
        StartCoroutine(AutoReturnCoroutine());
    }

    void Update()
    {
        // Ignorar input el primer segundo para evitar skip accidental
        if (Time.realtimeSinceStartup - showTime < 1f) return;

        // Cualquier tecla para saltar el countdown
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            ReturnToMenu();
            return;
        }

        // Cualquier botón del volante/joystick (en kiosko sin teclado)
        if (Joystick.current != null)
        {
            foreach (var ctrl in Joystick.current.allControls)
            {
                if (ctrl is ButtonControl btn && btn.wasPressedThisFrame)
                {
                    ReturnToMenu();
                    return;
                }
            }
        }
    }

    void OnDestroy()
    {
        // Safeguard: restaurar timeScale si el objeto se destruye inesperadamente
        Time.timeScale = 1f;
    }

    void BuildUI()
    {
        // Buscar Canvas root — priorizar el que renderiza en el display principal
#pragma warning disable CS0618
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
#pragma warning restore CS0618

        Canvas targetCanvas = null;
        // Primera pasada: Canvas overlay root en display principal (targetDisplay == 0)
        foreach (var canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay
                && canvas.transform.parent == null
                && canvas.targetDisplay == DisplayHelper.CenterDisplay)
            {
                targetCanvas = canvas;
                break;
            }
        }
        // Fallback: cualquier overlay root
        if (targetCanvas == null)
        {
            foreach (var canvas in canvases)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.transform.parent == null)
                {
                    targetCanvas = canvas;
                    break;
                }
            }
        }
        if (targetCanvas == null && canvases.Length > 0)
            targetCanvas = canvases[0];

        if (targetCanvas == null)
        {
            Debug.LogWarning("[ExamResultsScreen] No se encontró Canvas");
            ReturnToMenu();
            return;
        }

        // Garantizar pantalla principal (cubre caso ScreenSpaceCamera en display lateral)
        DisplayHelper.EnsureOnMainDisplay(targetCanvas, "[ExamResultsScreen]");

        // Backdrop oscuro fullscreen
        GameObject backdrop = new GameObject("Backdrop");
        backdrop.transform.SetParent(targetCanvas.transform, false);
        RectTransform bdRt = backdrop.AddComponent<RectTransform>();
        bdRt.anchorMin = Vector2.zero;
        bdRt.anchorMax = Vector2.one;
        bdRt.offsetMin = Vector2.zero;
        bdRt.offsetMax = Vector2.zero;
        Image bdImg = backdrop.AddComponent<Image>();
        bdImg.color = new Color(0f, 0f, 0f, 0.85f);
        bdImg.raycastTarget = true;

        // Card central con MenuCardBuilder
        GameObject card = MenuCardBuilder.CreateCard(backdrop.transform, new Vector2(700f, 500f));
        RectTransform cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;

        Transform content = card.transform.Find("Content");

        // Título: "Resultado del Examen"
        var titleObj = MenuCardBuilder.CreateText(content, "Title", "Resultado del Examen",
            40f, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Center);
        titleObj.GetComponent<RectTransform>().Set(
            new Vector2(0f, 0.78f), new Vector2(1f, 0.95f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // Score grande
        string scoreLabel = GetScoreLabel(score);
        Color scoreColor = GetScoreColor(score);

        var scoreObj = MenuCardBuilder.CreateText(content, "Score", score.ToString(),
            72f, FontStyles.Bold, scoreColor, TextAlignmentOptions.Center);
        scoreObj.GetComponent<RectTransform>().Set(
            new Vector2(0f, 0.52f), new Vector2(1f, 0.78f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // Label APTO/NO APTO
        var labelObj = MenuCardBuilder.CreateText(content, "ScoreLabel", scoreLabel,
            MenuTheme.CardTitleSize, FontStyles.Bold, scoreColor, TextAlignmentOptions.Center);
        var labelTmp = labelObj.GetComponent<TextMeshProUGUI>();
        labelTmp.textWrappingMode = TextWrappingModes.Normal;
        labelObj.GetComponent<RectTransform>().Set(
            new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.52f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // Cantidad de infracciones
        int eventCount = TelemetryLogger.Instance != null ? TelemetryLogger.Instance.data.events.Count : 0;
        var infraObj = MenuCardBuilder.CreateText(content, "Infracciones",
            $"Infracciones registradas: {eventCount}",
            MenuTheme.SubtitleSize, FontStyles.Normal, MenuTheme.TextSecondary, TextAlignmentOptions.Center);
        infraObj.GetComponent<RectTransform>().Set(
            new Vector2(0f, 0.22f), new Vector2(1f, 0.35f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // Texto de auto-return
        var returnObj = MenuCardBuilder.CreateText(content, "ReturnText",
            $"Regresando al menu en {(int)AUTO_RETURN_SECONDS}s...",
            MenuTheme.CardDescSize, FontStyles.Normal, MenuTheme.TextMuted, TextAlignmentOptions.Center);
        returnObj.GetComponent<RectTransform>().Set(
            new Vector2(0f, 0.05f), new Vector2(1f, 0.18f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        countdownText = returnObj.GetComponent<TextMeshProUGUI>();
    }

    string GetScoreLabel(int s)
    {
        var t = ScoringConfig.Instance?.data.gradeThresholds;
        int apto = t?.apto ?? 90;
        int condicionado = t?.aptoCondicionado ?? 80;
        int reentrenamiento = t?.aptoReentrenamiento ?? 70;

        if (s >= apto) return "APTO";
        if (s >= condicionado) return "APTO CONDICIONADO";
        if (s >= reentrenamiento) return "APTO CONDICIONADO\nRequiere reentrenamiento";
        return "NO APTO";
    }

    Color GetScoreColor(int s)
    {
        var t = ScoringConfig.Instance?.data.gradeThresholds;
        int apto = t?.apto ?? 90;
        int reentrenamiento = t?.aptoReentrenamiento ?? 70;

        if (s >= apto) return MenuTheme.SuccessGreen;
        if (s >= reentrenamiento) return MenuTheme.Gold;
        return MenuTheme.TextError;
    }

    IEnumerator AutoReturnCoroutine()
    {
        // Deadline absoluto basado en realtime — inmune a timing drift de
        // WaitForSecondsRealtime con timeScale=0 (que en Unity 6 puede colgarse
        // en el último segundo, dejando "Regresando al menu en 1s..." stuck).
        float deadline = Time.realtimeSinceStartup + AUTO_RETURN_SECONDS;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (countdownText != null)
            {
                int remaining = Mathf.CeilToInt(deadline - Time.realtimeSinceStartup);
                if (remaining < 1) remaining = 1;
                countdownText.text = $"Regresando al menu en {remaining}s...";
            }
            yield return null;
        }
        ReturnToMenu();
    }

    void ReturnToMenu()
    {
        try
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ExamResultsScreen] ReturnToMenu falló: {e}. Forzando reload.");
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
