using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Overlay fullscreen con resultados del examen.
/// Congela la simulación, muestra score APTO/NO APTO + tabla de infracciones,
/// y regresa automáticamente al menú al expirar el countdown.
/// </summary>
public class ExamResultsScreen : MonoBehaviour
{
    private int score;
    private TextMeshProUGUI countdownText;
    private const float AUTO_RETURN_SECONDS = 45f;

    /// <summary>
    /// Entry point estático — crea el overlay de resultados.
    /// </summary>
    public static void Show(int finalScore)
    {
        GameObject obj = new GameObject("ExamResultsOverlay");
        // CRÍTICO: DontDestroyOnLoad para que el overlay sobreviva el LoadScene("MainMenu")
        // y la coroutine pueda completar + auto-destruirse limpiamente. Sin esto, el
        // GameObject vive en la escena del examen, se destruye al hacer LoadScene, la
        // coroutine se cancela mid-frame, pero el backdrop+UI quedan colgados en cualquier
        // Canvas persistente que hubiera servido de parent → freeze visual con "1s..."
        DontDestroyOnLoad(obj);
        var screen = obj.AddComponent<ExamResultsScreen>();
        screen.score = finalScore;
    }

    private float showTime;
    private bool isReturning;

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
        // Canvas dedicado, child de este GameObject — todo el UI vive aquí.
        // Si el GameObject se destruye, todo el árbol se va con él. Antes el overlay
        // se hacía child de un Canvas externo encontrado con FindObjectsOfType, que
        // si pertenecía a un GameObject persistente entre escenas, dejaba el backdrop
        // colgado tras LoadScene → freeze visual. Mismo patrón que CollisionFeedback.
        var canvasGo = new GameObject("ExamResultsCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000; // arriba de CollisionFeedback (5000) y notificaciones
        canvas.targetDisplay = DisplayHelper.CenterDisplay;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Backdrop oscuro fullscreen
        GameObject backdrop = new GameObject("Backdrop");
        backdrop.transform.SetParent(canvasGo.transform, false);
        RectTransform bdRt = backdrop.AddComponent<RectTransform>();
        bdRt.anchorMin = Vector2.zero;
        bdRt.anchorMax = Vector2.one;
        bdRt.offsetMin = Vector2.zero;
        bdRt.offsetMax = Vector2.zero;
        Image bdImg = backdrop.AddComponent<Image>();
        bdImg.color = new Color(0f, 0f, 0f, 0.85f);
        bdImg.raycastTarget = true;

        // Card central — modal grande para tabla scrollable cómoda en kiosko.
        GameObject card = MenuCardBuilder.CreateCard(backdrop.transform, new Vector2(1500f, 950f));
        RectTransform cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;

        Transform content = card.transform.Find("Content");

        // Filtra eventos con penalización (excluye FIN_EXAMEN y similares con points=0).
        var faults = CollectFaults();
        int totalDeducted = 0;
        for (int i = 0; i < faults.Count; i++) totalDeducted += Mathf.Abs(faults[i].points);

        Color scoreColor = GetScoreColor(score);
        string scoreLabel = GetScoreLabel(score);

        // ── Header: 70-100% del alto de Content ──────────────────────────
        // Título "Resultado del Examen" centrado arriba.
        var titleObj = MenuCardBuilder.CreateText(content, "Title", "Resultado del Examen",
            40f, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Center);
        titleObj.GetComponent<RectTransform>().Set(
            new Vector2(0f, 0.91f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // Score grande (izquierda) + Label APTO/NO APTO debajo.
        var scoreObj = MenuCardBuilder.CreateText(content, "Score", score.ToString(),
            72f, FontStyles.Bold, scoreColor, TextAlignmentOptions.Center);
        scoreObj.GetComponent<RectTransform>().Set(
            new Vector2(0.02f, 0.78f), new Vector2(0.32f, 0.92f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        var labelObj = MenuCardBuilder.CreateText(content, "ScoreLabel", scoreLabel,
            MenuTheme.CardTitleSize, FontStyles.Bold, scoreColor, TextAlignmentOptions.Center);
        var labelTmp = labelObj.GetComponent<TextMeshProUGUI>();
        labelTmp.textWrappingMode = TextWrappingModes.Normal;
        labelObj.GetComponent<RectTransform>().Set(
            new Vector2(0.02f, 0.70f), new Vector2(0.32f, 0.78f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // Resumen (derecha): N infracciones · -X puntos.
        string summary = faults.Count == 0
            ? "Sin infracciones"
            : $"{faults.Count} infracci{(faults.Count == 1 ? "ón" : "ones")} · -{totalDeducted} puntos";
        var summaryObj = MenuCardBuilder.CreateText(content, "Summary", summary,
            MenuTheme.SubtitleSize, FontStyles.Bold, MenuTheme.TextPrimary, TextAlignmentOptions.Left);
        summaryObj.GetComponent<RectTransform>().Set(
            new Vector2(0.36f, 0.80f), new Vector2(0.98f, 0.90f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        var summarySubObj = MenuCardBuilder.CreateText(content, "SummarySub",
            "Detalle de infracciones cometidas durante el examen",
            MenuTheme.CardDescSize, FontStyles.Normal, MenuTheme.TextMuted, TextAlignmentOptions.Left);
        summarySubObj.GetComponent<RectTransform>().Set(
            new Vector2(0.36f, 0.71f), new Vector2(0.98f, 0.79f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // ── Tabla: 9-67% del alto de Content ─────────────────────────────
        BuildFaultsTable(content, faults);

        // ── Footer: 0-7% del alto de Content (countdown) ─────────────────
        var returnObj = MenuCardBuilder.CreateText(content, "ReturnText",
            $"Regresando al menu en {(int)AUTO_RETURN_SECONDS}s... (presiona cualquier tecla)",
            MenuTheme.CardDescSize, FontStyles.Normal, MenuTheme.TextMuted, TextAlignmentOptions.Center);
        returnObj.GetComponent<RectTransform>().Set(
            new Vector2(0f, 0f), new Vector2(1f, 0.07f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        countdownText = returnObj.GetComponent<TextMeshProUGUI>();
    }

    // ── Tabla de infracciones (header fijo + ScrollRect con filas) ──────

    void BuildFaultsTable(Transform parent, List<TelemetryLogger.TelemetryEvent> faults)
    {
        // Header de tabla — fijo, no scrollea.
        GameObject header = new GameObject("TableHeader");
        header.transform.SetParent(parent, false);
        RectTransform headerRt = header.AddComponent<RectTransform>();
        headerRt.Set(new Vector2(0f, 0.62f), new Vector2(1f, 0.68f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        Image headerBg = header.AddComponent<Image>();
        headerBg.sprite = MenuCardBuilder.GetRoundedSprite(MenuTheme.CornerRadiusSmall);
        headerBg.type = Image.Type.Sliced;
        headerBg.color = MenuTheme.InputBackground;
        headerBg.raycastTarget = false;

        BuildTableRow(header.transform, "Minuto", "Infracción", "Puntos",
            isHeader: true, severity: SeverityKind.Minor, points: 0);

        // ScrollRect con la lista — 9-62% del alto de Content.
        GameObject scrollGo = new GameObject("FaultsScroll");
        scrollGo.transform.SetParent(parent, false);
        RectTransform scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.Set(new Vector2(0f, 0.09f), new Vector2(1f, 0.61f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        Image scrollBg = scrollGo.AddComponent<Image>();
        scrollBg.sprite = MenuCardBuilder.GetRoundedSprite(MenuTheme.CornerRadiusSmall);
        scrollBg.type = Image.Type.Sliced;
        scrollBg.color = MenuTheme.CardBackground;
        scrollBg.raycastTarget = true;

        ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 30f;

        // Viewport con mask.
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        RectTransform vpRt = viewport.AddComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = new Vector2(2, 2);
        vpRt.offsetMax = new Vector2(-2, -2);
        Image vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(1f, 1f, 1f, 0.001f); // RectMask2D requiere graphic
        viewport.AddComponent<RectMask2D>();

        // Content: anclado al top del viewport, crece hacia abajo.
        const float rowHeight = 56f;
        const float rowPadding = 4f;
        GameObject scrollContent = new GameObject("Content");
        scrollContent.transform.SetParent(viewport.transform, false);
        RectTransform contentRt = scrollContent.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;

        scroll.viewport = vpRt;
        scroll.content = contentRt;

        if (faults.Count == 0)
        {
            // Mensaje de examen perfecto centrado.
            contentRt.sizeDelta = new Vector2(0f, 120f);
            var emptyObj = MenuCardBuilder.CreateText(scrollContent.transform, "Empty",
                "Sin infracciones registradas — examen perfecto",
                MenuTheme.SubtitleSize, FontStyles.Bold, MenuTheme.SuccessGreen,
                TextAlignmentOptions.Center);
            emptyObj.GetComponent<RectTransform>().Set(
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            return;
        }

        // Una fila por infracción.
        float totalHeight = faults.Count * (rowHeight + rowPadding);
        contentRt.sizeDelta = new Vector2(0f, totalHeight);

        for (int i = 0; i < faults.Count; i++)
        {
            var fault = faults[i];
            GameObject row = new GameObject($"Row_{i}");
            row.transform.SetParent(scrollContent.transform, false);
            RectTransform rowRt = row.AddComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -(i * (rowHeight + rowPadding)));
            rowRt.sizeDelta = new Vector2(0f, rowHeight);

            // Banda alterna para legibilidad.
            if (i % 2 == 1)
            {
                Image rowBg = row.AddComponent<Image>();
                rowBg.color = new Color(1f, 1f, 1f, 0.5f);
                rowBg.raycastTarget = false;
            }

            // Pasivas o eventos con 0 puntos: mostrar "—" en vez de "-0".
            string ptsLabel = fault.points == 0
                ? "—"
                : "-" + Mathf.Abs(fault.points).ToString();
            BuildTableRow(row.transform,
                FormatTimestamp(fault.timestamp),
                fault.description,
                ptsLabel,
                isHeader: false,
                severity: GetSeverity(fault.eventType),
                points: fault.points);
        }
    }

    void BuildTableRow(Transform parent, string min, string desc, string pts,
        bool isHeader, SeverityKind severity, int points)
    {
        // Columnas: Minuto 0-12% · Infracción 13-65% · Severidad 66-83% · Puntos 84-98%
        Color textCol = isHeader ? MenuTheme.TextSecondary : MenuTheme.TextOnCard;
        float fontSize = isHeader ? 18f : 20f;
        FontStyles fs = isHeader ? FontStyles.Bold : FontStyles.Normal;

        var minObj = MenuCardBuilder.CreateText(parent, "ColMin", min,
            fontSize, fs, textCol, TextAlignmentOptions.Center);
        minObj.GetComponent<RectTransform>().Set(
            new Vector2(0f, 0f), new Vector2(0.12f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        var descObj = MenuCardBuilder.CreateText(parent, "ColDesc", desc,
            fontSize, fs, textCol, TextAlignmentOptions.Left);
        var descTmp = descObj.GetComponent<TextMeshProUGUI>();
        descTmp.textWrappingMode = TextWrappingModes.NoWrap;
        descTmp.overflowMode = TextOverflowModes.Ellipsis;
        descObj.GetComponent<RectTransform>().Set(
            new Vector2(0.13f, 0f), new Vector2(0.65f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        if (isHeader)
        {
            var sevObj = MenuCardBuilder.CreateText(parent, "ColSev", "Severidad",
                fontSize, fs, textCol, TextAlignmentOptions.Center);
            sevObj.GetComponent<RectTransform>().Set(
                new Vector2(0.66f, 0f), new Vector2(0.83f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
        }
        else
        {
            BuildSeverityBadge(parent, severity);
        }

        Color ptsCol = isHeader
            ? MenuTheme.TextSecondary
            : (points < 0 ? MenuTheme.TextError : MenuTheme.TextOnCard);
        var ptsObj = MenuCardBuilder.CreateText(parent, "ColPts", pts,
            fontSize, isHeader ? fs : FontStyles.Bold, ptsCol, TextAlignmentOptions.Right);
        ptsObj.GetComponent<RectTransform>().Set(
            new Vector2(0.84f, 0f), new Vector2(0.98f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
    }

    void BuildSeverityBadge(Transform parent, SeverityKind severity)
    {
        Color bg, fg;
        string label;
        switch (severity)
        {
            case SeverityKind.Critical:
                bg = HexColor("#FEE2E2"); fg = HexColor("#991B1B"); label = "Crítica"; break;
            case SeverityKind.Major:
                bg = HexColor("#EDE9FE"); fg = HexColor("#5B21B6"); label = "Grave"; break;
            default:
                bg = HexColor("#FEF3C7"); fg = HexColor("#92400E"); label = "Leve"; break;
        }

        GameObject badge = new GameObject("Badge");
        badge.transform.SetParent(parent, false);
        RectTransform badgeRt = badge.AddComponent<RectTransform>();
        badgeRt.Set(new Vector2(0.67f, 0.18f), new Vector2(0.82f, 0.82f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Image badgeImg = badge.AddComponent<Image>();
        badgeImg.sprite = MenuCardBuilder.GetRoundedSprite(MenuTheme.CornerRadiusSmall);
        badgeImg.type = Image.Type.Sliced;
        badgeImg.color = bg;
        badgeImg.raycastTarget = false;

        var txt = MenuCardBuilder.CreateText(badge.transform, "Text", label,
            16f, FontStyles.Bold, fg, TextAlignmentOptions.Center);
        txt.GetComponent<RectTransform>().Set(
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
    }

    // ── Helpers de datos ────────────────────────────────────────────────

    enum SeverityKind { Minor, Major, Critical }

    static List<TelemetryLogger.TelemetryEvent> CollectFaults()
    {
        var result = new List<TelemetryLogger.TelemetryEvent>();
        if (TelemetryLogger.Instance == null) return result;
        var all = TelemetryLogger.Instance.data.events;
        for (int i = 0; i < all.Count; i++)
        {
            var e = all[i];
            if (e == null) continue;
            // Las pasivas se incluyen aunque tengan points==0 — el alumno
            // sintió el cristal roto/shake/FFB, debe ver la entrada listada
            // para entender que el sistema lo registró pero no lo penalizó.
            if (e.points < 0 || e.eventType == "COLISION_PASIVA") result.Add(e);
        }
        return result;
    }

    // Mapeo idéntico a SimulatorApiClient.MapSeverity para coherencia con backend/portal.
    static SeverityKind GetSeverity(string eventType)
    {
        switch (eventType)
        {
            case "ATROPELLO": return SeverityKind.Critical;
            case "COLISION_BICICLETA":
            case "COLISION_VEHICULO":
            case "SEMAFORO_ROJO":
            case "SENTIDO_CONTRARIO":
            case "CAMBIO_PELIGROSO":
                return SeverityKind.Major;
            // Pasiva: badge Minor (amarillo) — visualmente distinto del rojo
            // de la activa pero sin gritar al alumno por algo que no fue su
            // falta. El símbolo en la columna Puntos será "—" en vez de "-0".
            case "COLISION_PASIVA":
                return SeverityKind.Minor;
            default:
                return SeverityKind.Minor;
        }
    }

    // "45.23s" → "0:45". Mismo formato que el portal admin (m:ss).
    static string FormatTimestamp(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "0:00";
        string trimmed = raw.EndsWith("s") ? raw.Substring(0, raw.Length - 1) : raw;
        if (!float.TryParse(trimmed, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float totalSeconds))
            return "0:00";
        int total = Mathf.FloorToInt(totalSeconds);
        int min = total / 60;
        int sec = total % 60;
        return $"{min}:{sec:D2}";
    }

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
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
        // Deadline absoluto basado en realtime — inmune a timing drift con timeScale=0.
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
        Debug.Log("[ExamResultsScreen] Countdown completo, regresando al menu");
        ReturnToMenu();
    }

    void ReturnToMenu()
    {
        // Guard contra race entre Update (input) y coroutine (timeout) — si ambos
        // disparan ReturnToMenu en frames adyacentes, sin guard tendríamos doble LoadScene.
        if (isReturning) return;
        isReturning = true;
        Debug.Log("[ExamResultsScreen] ReturnToMenu invocado");

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

        // Limpieza explícita: el overlay es DontDestroyOnLoad, sobrevive el LoadScene.
        // Sin este Destroy, quedaría visible permanentemente sobre el MainMenu.
        Destroy(gameObject);
    }
}
