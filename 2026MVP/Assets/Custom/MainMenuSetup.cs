using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Automatically sets up the MainMenu UI layout with rounded corners.
/// Attach this to the Canvas GameObject.
/// </summary>
public class MainMenuSetup : MonoBehaviour
{
    [Header("Colors")]
    public Color backgroundColor = new Color(0.12f, 0.12f, 0.16f, 1f);
    public Color panelColor = new Color(0.18f, 0.18f, 0.22f, 1f);
    public Color buttonColor = new Color(0.25f, 0.52f, 0.85f, 1f);
    public Color secondaryButtonColor = new Color(0.35f, 0.35f, 0.4f, 1f);
    public Color inputFieldColor = new Color(0.22f, 0.22f, 0.28f, 1f);
    public Color textColor = Color.white;
    public Color errorColor = new Color(1f, 0.4f, 0.4f, 1f);
    public Color placeholderColor = new Color(0.5f, 0.5f, 0.55f, 1f);

    [Header("Design")]
    public int cornerRadius = 12;

    [Header("Scene Settings")]
    public string gameSceneName = "UrbanExample";

    private Sprite roundedSprite;

    void Start()
    {
        roundedSprite = CreateRoundedSprite(128, 128, cornerRadius * 2);
        SetupCanvas();
        ClearExistingChildren();
        CreateUI();
    }

    Sprite CreateRoundedSprite(int width, int height, int radius)
    {
        Texture2D texture = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                pixels[y * width + x] = IsInsideRoundedRect(x, y, width, height, radius)
                    ? Color.white
                    : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        int border = radius;
        return Sprite.Create(texture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
    }

    bool IsInsideRoundedRect(int x, int y, int width, int height, int radius)
    {
        if (x < radius && y < radius)
            return IsInsideCircle(x, y, radius, radius, radius);
        if (x >= width - radius && y < radius)
            return IsInsideCircle(x, y, width - radius - 1, radius, radius);
        if (x < radius && y >= height - radius)
            return IsInsideCircle(x, y, radius, height - radius - 1, radius);
        if (x >= width - radius && y >= height - radius)
            return IsInsideCircle(x, y, width - radius - 1, height - radius - 1, radius);
        return true;
    }

    bool IsInsideCircle(int x, int y, int cx, int cy, int radius)
    {
        float dx = x - cx;
        float dy = y - cy;
        return (dx * dx + dy * dy) <= (radius * radius);
    }

    void SetupCanvas()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
    }

    void ClearExistingChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }

    void CreateUI()
    {
        // Background - full screen
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(transform, false);
        RectTransform bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImage = bg.AddComponent<Image>();
        bgImage.color = backgroundColor;

        // Card - centered, fixed size
        GameObject card = new GameObject("Card");
        card.transform.SetParent(bg.transform, false);
        RectTransform cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(450, 550);
        cardRect.anchoredPosition = Vector2.zero;

        Image cardImage = card.AddComponent<Image>();
        cardImage.color = panelColor;
        if (roundedSprite != null)
        {
            cardImage.sprite = roundedSprite;
            cardImage.type = Image.Type.Sliced;
        }

        // Content area inside card with padding
        GameObject content = new GameObject("Content");
        content.transform.SetParent(card.transform, false);
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(40, 40);
        contentRect.offsetMax = new Vector2(-40, -40);

        // Calculate positions manually (top to bottom)
        float currentY = 0;

        // Title at top
        GameObject title = CreateTextElement("Title", content.transform, "Simulador de Manejo", 38, FontStyles.Bold);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, currentY);
        titleRect.sizeDelta = new Vector2(0, 50);
        currentY -= 55;

        // Subtitle
        GameObject subtitle = CreateTextElement("Subtitle", content.transform, "Tlaxcala 2026", 20, FontStyles.Normal);
        subtitle.GetComponent<TextMeshProUGUI>().color = placeholderColor;
        RectTransform subtitleRect = subtitle.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0, 1);
        subtitleRect.anchorMax = new Vector2(1, 1);
        subtitleRect.pivot = new Vector2(0.5f, 1);
        subtitleRect.anchoredPosition = new Vector2(0, currentY);
        subtitleRect.sizeDelta = new Vector2(0, 30);
        currentY -= 60;

        // Input Label
        GameObject inputLabel = CreateTextElement("InputLabel", content.transform, "Numero de Expediente", 14, FontStyles.Normal);
        inputLabel.GetComponent<TextMeshProUGUI>().color = placeholderColor;
        inputLabel.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        RectTransform labelRect = inputLabel.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 1);
        labelRect.anchorMax = new Vector2(1, 1);
        labelRect.pivot = new Vector2(0.5f, 1);
        labelRect.anchoredPosition = new Vector2(0, currentY);
        labelRect.sizeDelta = new Vector2(0, 25);
        currentY -= 30;

        // Input Field
        GameObject inputField = CreateInputField("ExpedienteInput", content.transform, "Ej: 12345");
        RectTransform inputRect = inputField.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0, 1);
        inputRect.anchorMax = new Vector2(1, 1);
        inputRect.pivot = new Vector2(0.5f, 1);
        inputRect.anchoredPosition = new Vector2(0, currentY);
        inputRect.sizeDelta = new Vector2(0, 50);
        currentY -= 55;

        // Error Text
        GameObject errorText = CreateTextElement("ErrorText", content.transform, "", 13, FontStyles.Normal);
        errorText.GetComponent<TextMeshProUGUI>().color = errorColor;
        errorText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        RectTransform errorRect = errorText.GetComponent<RectTransform>();
        errorRect.anchorMin = new Vector2(0, 1);
        errorRect.anchorMax = new Vector2(1, 1);
        errorRect.pivot = new Vector2(0.5f, 1);
        errorRect.anchoredPosition = new Vector2(0, currentY);
        errorRect.sizeDelta = new Vector2(0, 25);
        currentY -= 45;

        // Empezar Button
        GameObject empezarBtn = CreateButton("EmpezarButton", content.transform, "Empezar", buttonColor);
        RectTransform empezarRect = empezarBtn.GetComponent<RectTransform>();
        empezarRect.anchorMin = new Vector2(0, 1);
        empezarRect.anchorMax = new Vector2(1, 1);
        empezarRect.pivot = new Vector2(0.5f, 1);
        empezarRect.anchoredPosition = new Vector2(0, currentY);
        empezarRect.sizeDelta = new Vector2(0, 55);
        currentY -= 65;

        // Abrir Folder Button
        GameObject abrirBtn = CreateButton("AbrirFolderButton", content.transform, "Abrir Carpeta Telemetria", secondaryButtonColor);
        abrirBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 16;
        RectTransform abrirRect = abrirBtn.GetComponent<RectTransform>();
        abrirRect.anchorMin = new Vector2(0, 1);
        abrirRect.anchorMax = new Vector2(1, 1);
        abrirRect.pivot = new Vector2(0.5f, 1);
        abrirRect.anchoredPosition = new Vector2(0, currentY);
        abrirRect.sizeDelta = new Vector2(0, 45);

        // Wire up buttons
        SetupButtonActions(inputField, empezarBtn, abrirBtn, errorText);
    }

    GameObject CreateTextElement(string name, Transform parent, string text, int fontSize, FontStyles style)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        textObj.AddComponent<RectTransform>();
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        return textObj;
    }

    GameObject CreateInputField(string name, Transform parent, string placeholder)
    {
        GameObject inputObj = new GameObject(name);
        inputObj.transform.SetParent(parent, false);
        inputObj.AddComponent<RectTransform>();

        Image image = inputObj.AddComponent<Image>();
        image.color = inputFieldColor;
        if (roundedSprite != null)
        {
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
        }

        TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();

        // Text Area
        GameObject textArea = new GameObject("Text Area");
        textArea.transform.SetParent(inputObj.transform, false);
        RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(15, 5);
        textAreaRect.offsetMax = new Vector2(-15, -5);
        textArea.AddComponent<RectMask2D>();

        // Placeholder
        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(textArea.transform, false);
        RectTransform placeholderRect = placeholderObj.AddComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;
        TextMeshProUGUI placeholderTMP = placeholderObj.AddComponent<TextMeshProUGUI>();
        placeholderTMP.text = placeholder;
        placeholderTMP.fontSize = 18;
        placeholderTMP.color = placeholderColor;
        placeholderTMP.alignment = TextAlignmentOptions.Left;
        placeholderTMP.verticalAlignment = VerticalAlignmentOptions.Middle;

        // Input Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(textArea.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI textTMP = textObj.AddComponent<TextMeshProUGUI>();
        textTMP.fontSize = 18;
        textTMP.color = textColor;
        textTMP.alignment = TextAlignmentOptions.Left;
        textTMP.verticalAlignment = VerticalAlignmentOptions.Middle;

        inputField.textViewport = textAreaRect;
        inputField.textComponent = textTMP;
        inputField.placeholder = placeholderTMP;

        return inputObj;
    }

    GameObject CreateButton(string name, Transform parent, string text, Color color)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent, false);
        buttonObj.AddComponent<RectTransform>();

        Image image = buttonObj.AddComponent<Image>();
        image.color = color;
        if (roundedSprite != null)
        {
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
        }

        Button button = buttonObj.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.fadeDuration = 0.1f;
        button.colors = colors;

        // Button Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 20;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;

        return buttonObj;
    }

    void SetupButtonActions(GameObject inputFieldObj, GameObject empezarBtn, GameObject abrirBtn, GameObject errorTextObj)
    {
        TMP_InputField inputField = inputFieldObj.GetComponent<TMP_InputField>();
        TextMeshProUGUI errorText = errorTextObj.GetComponent<TextMeshProUGUI>();
        Button empezarButton = empezarBtn.GetComponent<Button>();
        Button abrirFolderButton = abrirBtn.GetComponent<Button>();

        // Ensure GameManager exists
        if (GameManager.Instance == null)
        {
            GameObject gmObj = new GameObject("GameManager");
            gmObj.AddComponent<GameManager>();
        }

        empezarButton.onClick.AddListener(() => {
            string expediente = inputField != null ? inputField.text.Trim() : "";

            if (string.IsNullOrEmpty(expediente))
            {
                if (errorText != null)
                    errorText.text = "Por favor ingrese el numero de expediente";
                return;
            }

            if (errorText != null)
                errorText.text = "";

            GameManager.Instance.Expediente = expediente;
            Debug.Log($"Starting game with expediente: {expediente}");
            SceneManager.LoadScene(gameSceneName);
        });

        abrirFolderButton.onClick.AddListener(() => {
            string path = Application.persistentDataPath;
            Debug.Log($"Opening telemetry folder: {path}");
            Application.OpenURL("file://" + path);
        });

        Debug.Log("MainMenu buttons configured successfully");
    }
}
