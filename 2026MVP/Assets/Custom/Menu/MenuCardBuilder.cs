using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Fábrica de elementos UI — flat/clean, sin glassmorphismo.
/// Background pre-renderizado, cards sólidos, anti-aliased sprites.
/// </summary>
public static class MenuCardBuilder
{
    private static Dictionary<int, Sprite> _roundedCache = new Dictionary<int, Sprite>();
    private static Sprite _circleSprite;

    // ── Sprites ────────────────────────────────────────────────────────

    public static Sprite GetRoundedSprite(int radius = MenuTheme.CornerRadius)
    {
        if (_roundedCache.TryGetValue(radius, out Sprite cached))
            return cached;

        int texSize = 256;
        var sprite = CreateRoundedSprite(texSize, texSize, radius * 4);
        _roundedCache[radius] = sprite;
        return sprite;
    }

    public static Sprite GetCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;
        _circleSprite = CreateCircleSprite(128);
        return _circleSprite;
    }

    static Sprite CreateRoundedSprite(int width, int height, int radius)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float dist = SDF_RoundedRect(x, y, width, height, radius);
                pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - dist / 1.5f));
            }

        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;

        int border = Mathf.Max(radius + 2, width / 4);
        return Sprite.Create(texture, new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
    }

    static Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        float center = size * 0.5f, radius = center - 2f;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((radius - dist) / 1.5f));
            }

        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    static float SDF_RoundedRect(float px, float py, float width, float height, float radius)
    {
        float halfW = width * 0.5f, halfH = height * 0.5f;
        float dx = Mathf.Abs(px - halfW) - (halfW - radius);
        float dy = Mathf.Abs(py - halfH) - (halfH - radius);
        return Mathf.Sqrt(Mathf.Max(dx, 0) * Mathf.Max(dx, 0) + Mathf.Max(dy, 0) * Mathf.Max(dy, 0))
             + Mathf.Min(Mathf.Max(dx, dy), 0) - radius;
    }

    // ── Background ─────────────────────────────────────────────────────

    /// <summary>
    /// Background fullscreen — blanco sólido o textura.
    /// </summary>
    public static GameObject CreateBackground(Transform parent, Texture2D bgTexture = null)
    {
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(parent, false);
        RectTransform rt = bg.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        if (bgTexture != null)
        {
            RawImage rawImg = bg.AddComponent<RawImage>();
            rawImg.texture = bgTexture;
            rawImg.raycastTarget = false;
        }
        else
        {
            Image img = bg.AddComponent<Image>();
            img.color = MenuTheme.PageBackground;
            img.raycastTarget = false;
        }
        return bg;
    }

    // ── Card sólido ────────────────────────────────────────────────────

    public static GameObject CreateCard(Transform parent, Vector2 size, int cornerRadius = -1)
    {
        if (cornerRadius < 0) cornerRadius = MenuTheme.CardCornerRadius;
        Sprite rounded = GetRoundedSprite(cornerRadius);

        GameObject card = new GameObject("Card");
        card.transform.SetParent(parent, false);
        card.AddComponent<RectTransform>().sizeDelta = size;

        // Border (detrás)
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(card.transform, false);
        RectTransform borderRt = borderObj.AddComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = new Vector2(-2, -2);
        borderRt.offsetMax = new Vector2(2, 2);
        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.sprite = rounded;
        borderImg.type = Image.Type.Sliced;
        borderImg.color = MenuTheme.CardBorder;
        borderImg.raycastTarget = false;

        // Fondo sólido
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(card.transform, false);
        RectTransform bgRt = bgObj.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = rounded;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = MenuTheme.CardBackground;
        bgImg.raycastTarget = true;

        // Contenido
        GameObject content = new GameObject("Content");
        content.transform.SetParent(card.transform, false);
        RectTransform contentRt = content.AddComponent<RectTransform>();
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.offsetMin = new Vector2(20, 20);
        contentRt.offsetMax = new Vector2(-20, -20);

        return card;
    }

    // ── Card con ícono/letra ───────────────────────────────────────────

    public static GameObject CreateIconCard(Transform parent, Sprite icon,
        string title, string description, Vector2 size, string iconLetter = null)
    {
        GameObject card = CreateCard(parent, size);
        Transform content = card.transform.Find("Content");

        // Usar ANCHORS proporcionales — funciona con cualquier tamaño de card
        // Círculo: centro-top (60-100% vertical)
        if (icon != null)
        {
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(content, false);
            iconObj.AddComponent<RectTransform>().Set(
                new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(70, 70));
            Image img = iconObj.AddComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            img.color = MenuTheme.TextOnCard;
            img.raycastTarget = false;
        }
        else if (!string.IsNullOrEmpty(iconLetter))
        {
            GameObject circleObj = new GameObject("IconCircle");
            circleObj.transform.SetParent(content, false);
            circleObj.AddComponent<RectTransform>().Set(
                new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(70, 70));
            Image ci = circleObj.AddComponent<Image>();
            ci.sprite = GetCircleSprite();
            ci.color = MenuTheme.CircleBg;
            ci.raycastTarget = false;

            GameObject letterObj = CreateText(circleObj.transform, "Letter", iconLetter,
                30f, FontStyles.Bold, MenuTheme.CircleText, TextAlignmentOptions.Center);
            letterObj.GetComponent<RectTransform>().Set(
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
        }

        // Título: centro (35-55%)
        CreateText(content, "Title", title,
            MenuTheme.CardTitleSize, FontStyles.Bold, MenuTheme.TextOnCard,
            TextAlignmentOptions.Center).GetComponent<RectTransform>().Set(
            new Vector2(0, 0.35f), new Vector2(1, 0.55f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // Descripción: abajo (15-35%)
        if (!string.IsNullOrEmpty(description))
        {
            var descObj = CreateText(content, "Description", description,
                MenuTheme.CardDescSize, FontStyles.Normal, MenuTheme.TextOnCardMuted,
                TextAlignmentOptions.Center);
            descObj.GetComponent<TextMeshProUGUI>().textWrappingMode = TextWrappingModes.Normal;
            descObj.GetComponent<RectTransform>().Set(
                new Vector2(0, 0.10f), new Vector2(1, 0.35f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
        }

        return card;
    }

    // ── QR Display ─────────────────────────────────────────────────────

    public static GameObject CreateQRDisplay(Transform parent, Vector2 size)
    {
        Sprite rounded = GetRoundedSprite(MenuTheme.CornerRadiusSmall);

        GameObject container = new GameObject("QRDisplay");
        container.transform.SetParent(parent, false);
        RectTransform rt = container.AddComponent<RectTransform>();
        rt.sizeDelta = size;

        // Fondo blanco para el QR
        Image bg = container.AddComponent<Image>();
        bg.sprite = rounded;
        bg.type = Image.Type.Sliced;
        bg.color = MenuTheme.QRBackground;
        bg.raycastTarget = false;

        // RawImage para el QR generado
        GameObject qrObj = new GameObject("QRImage");
        qrObj.transform.SetParent(container.transform, false);
        RectTransform qrRt = qrObj.AddComponent<RectTransform>();
        qrRt.anchorMin = Vector2.zero;
        qrRt.anchorMax = Vector2.one;
        qrRt.offsetMin = new Vector2(10, 10);
        qrRt.offsetMax = new Vector2(-10, -10);
        qrObj.AddComponent<RawImage>().raycastTarget = false;

        return container;
    }

    // ── Divider con texto ──────────────────────────────────────────────

    public static GameObject CreateDivider(Transform parent, string text, float width)
    {
        GameObject container = new GameObject("Divider");
        container.transform.SetParent(parent, false);
        RectTransform rt = container.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, 30);

        // Línea izquierda
        GameObject lineL = new GameObject("LineLeft");
        lineL.transform.SetParent(container.transform, false);
        RectTransform llRt = lineL.AddComponent<RectTransform>();
        llRt.anchorMin = new Vector2(0, 0.5f);
        llRt.anchorMax = new Vector2(0.42f, 0.5f);
        llRt.sizeDelta = new Vector2(0, 1);
        llRt.offsetMin = Vector2.zero;
        llRt.offsetMax = Vector2.zero;
        lineL.AddComponent<Image>().color = MenuTheme.DividerColor;

        // Texto
        GameObject textObj = CreateText(container.transform, "Text", text,
            16f, FontStyles.Normal, MenuTheme.TextMuted, TextAlignmentOptions.Center);
        textObj.GetComponent<RectTransform>().Set(
            new Vector2(0.42f, 0), new Vector2(0.58f, 1), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        // Línea derecha
        GameObject lineR = new GameObject("LineRight");
        lineR.transform.SetParent(container.transform, false);
        RectTransform lrRt = lineR.AddComponent<RectTransform>();
        lrRt.anchorMin = new Vector2(0.58f, 0.5f);
        lrRt.anchorMax = new Vector2(1f, 0.5f);
        lrRt.sizeDelta = new Vector2(0, 1);
        lrRt.offsetMin = Vector2.zero;
        lrRt.offsetMax = Vector2.zero;
        lineR.AddComponent<Image>().color = MenuTheme.DividerColor;

        return container;
    }

    // ── Botón ──────────────────────────────────────────────────────────

    public static GameObject CreateButton(Transform parent, string text, string style,
        Vector2 size, System.Action onClick = null)
    {
        Color bgColor, textColor;
        switch (style)
        {
            case "primary": bgColor = MenuTheme.ButtonPrimary; textColor = MenuTheme.ButtonPrimaryText; break;
            case "secondary": bgColor = MenuTheme.ButtonSecondary; textColor = MenuTheme.ButtonSecondaryText; break;
            case "ghost": bgColor = MenuTheme.ButtonGhost; textColor = MenuTheme.TextSecondary; break;
            case "disabled": bgColor = MenuTheme.ButtonDisabled; textColor = MenuTheme.ButtonDisabledText; break;
            default: bgColor = MenuTheme.ButtonPrimary; textColor = MenuTheme.ButtonPrimaryText; break;
        }

        Sprite rounded = GetRoundedSprite(MenuTheme.CornerRadiusSmall);
        GameObject btnObj = new GameObject("Button_" + text.Replace(" ", ""));
        btnObj.transform.SetParent(parent, false);
        btnObj.AddComponent<RectTransform>().sizeDelta = size;

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.sprite = rounded;
        btnImg.type = Image.Type.Sliced;
        btnImg.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.1f;
        btn.colors = colors;

        if (onClick != null) btn.onClick.AddListener(() => onClick());

        var textObj = CreateText(btnObj.transform, "Text", text,
            MenuTheme.ButtonTextSize, FontStyles.Bold, textColor, TextAlignmentOptions.Center);
        textObj.GetComponent<RectTransform>().Set(
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        return btnObj;
    }

    // ── Input field ────────────────────────────────────────────────────

    public static GameObject CreateInputField(Transform parent, string label,
        string placeholder, Vector2 size, TMP_InputField.ContentType contentType = TMP_InputField.ContentType.Standard)
    {
        Sprite rounded = GetRoundedSprite(MenuTheme.CornerRadiusSmall);

        GameObject container = new GameObject("Input_" + label.Replace(" ", ""));
        container.transform.SetParent(parent, false);
        container.AddComponent<RectTransform>().sizeDelta = new Vector2(size.x, size.y + 25);

        if (!string.IsNullOrEmpty(label))
        {
            var lbl = CreateText(container.transform, "Label", label,
                MenuTheme.LabelSize, FontStyles.Normal, MenuTheme.TextSecondary, TextAlignmentOptions.Left);
            lbl.GetComponent<RectTransform>().Set(
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                Vector2.zero, new Vector2(0, 22));
        }

        GameObject inputObj = new GameObject("InputField");
        inputObj.transform.SetParent(container.transform, false);
        RectTransform inputRt = inputObj.AddComponent<RectTransform>();
        inputRt.Set(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, string.IsNullOrEmpty(label) ? 0 : -25), new Vector2(0, size.y));

        Image inputImg = inputObj.AddComponent<Image>();
        inputImg.sprite = rounded;
        inputImg.type = Image.Type.Sliced;
        inputImg.color = MenuTheme.InputBackground;

        TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
        inputField.contentType = contentType;

        GameObject textArea = new GameObject("Text Area");
        textArea.transform.SetParent(inputObj.transform, false);
        RectTransform taRt = textArea.AddComponent<RectTransform>();
        taRt.anchorMin = Vector2.zero; taRt.anchorMax = Vector2.one;
        taRt.offsetMin = new Vector2(16, 5); taRt.offsetMax = new Vector2(-16, -5);
        textArea.AddComponent<RectMask2D>();

        GameObject phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(textArea.transform, false);
        RectTransform phRt = phObj.AddComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
        phRt.offsetMin = Vector2.zero; phRt.offsetMax = Vector2.zero;
        TextMeshProUGUI phTmp = phObj.AddComponent<TextMeshProUGUI>();
        phTmp.text = placeholder;
        phTmp.fontSize = MenuTheme.InputTextSize;
        phTmp.color = MenuTheme.InputPlaceholder;
        phTmp.alignment = TextAlignmentOptions.Left;
        phTmp.verticalAlignment = VerticalAlignmentOptions.Middle;

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(textArea.transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;
        TextMeshProUGUI txtTmp = txtObj.AddComponent<TextMeshProUGUI>();
        txtTmp.fontSize = MenuTheme.InputTextSize;
        txtTmp.color = MenuTheme.TextPrimary;
        txtTmp.alignment = TextAlignmentOptions.Left;
        txtTmp.verticalAlignment = VerticalAlignmentOptions.Middle;

        inputField.textViewport = taRt;
        inputField.textComponent = txtTmp;
        inputField.placeholder = phTmp;

        return container;
    }

    // ── PIN Input (cajas individuales estilo verificación) ─────────────

    public static GameObject CreatePinInput(Transform parent, int digitCount, float boxSize, float spacing)
    {
        Sprite rounded = GetRoundedSprite(MenuTheme.CornerRadiusSmall);

        float totalWidth = digitCount * boxSize + (digitCount - 1) * spacing;
        float boxHeight = boxSize * 1.15f;

        GameObject container = new GameObject("PinInput");
        container.transform.SetParent(parent, false);
        container.AddComponent<RectTransform>().sizeDelta = new Vector2(totalWidth, boxHeight);

        // Contenedor de cajas visibles
        GameObject boxRow = new GameObject("BoxRow");
        boxRow.transform.SetParent(container.transform, false);
        RectTransform boxRowRt = boxRow.AddComponent<RectTransform>();
        boxRowRt.anchorMin = Vector2.zero;
        boxRowRt.anchorMax = Vector2.one;
        boxRowRt.offsetMin = Vector2.zero;
        boxRowRt.offsetMax = Vector2.zero;

        // Crear cajas visuales
        TextMeshProUGUI[] digitTexts = new TextMeshProUGUI[digitCount];
        Image[] boxBorders = new Image[digitCount];

        for (int i = 0; i < digitCount; i++)
        {
            float xPos = i * (boxSize + spacing);

            // Caja con borde
            GameObject border = new GameObject("Border_" + i);
            border.transform.SetParent(boxRow.transform, false);
            RectTransform borderRt = border.AddComponent<RectTransform>();
            borderRt.anchorMin = new Vector2(0, 0);
            borderRt.anchorMax = new Vector2(0, 1);
            borderRt.pivot = new Vector2(0, 0.5f);
            borderRt.anchoredPosition = new Vector2(xPos, 0);
            borderRt.sizeDelta = new Vector2(boxSize, 0);
            Image borderImg = border.AddComponent<Image>();
            borderImg.sprite = rounded;
            borderImg.type = Image.Type.Sliced;
            borderImg.color = MenuTheme.InputBorder;
            boxBorders[i] = borderImg;

            // Fondo interior
            GameObject bg = new GameObject("Bg_" + i);
            bg.transform.SetParent(border.transform, false);
            RectTransform bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = new Vector2(3, 3);
            bgRt.offsetMax = new Vector2(-3, -3);
            Image bgImg = bg.AddComponent<Image>();
            bgImg.sprite = rounded;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = MenuTheme.InputBackground;

            // Texto del dígito
            GameObject digitObj = new GameObject("Digit_" + i);
            digitObj.transform.SetParent(border.transform, false);
            RectTransform digitRt = digitObj.AddComponent<RectTransform>();
            digitRt.anchorMin = Vector2.zero;
            digitRt.anchorMax = Vector2.one;
            digitRt.offsetMin = Vector2.zero;
            digitRt.offsetMax = Vector2.zero;
            TextMeshProUGUI digitTmp = digitObj.AddComponent<TextMeshProUGUI>();
            digitTmp.text = "";
            digitTmp.fontSize = 36f;
            digitTmp.color = MenuTheme.InputText;
            digitTmp.alignment = TextAlignmentOptions.Center;
            digitTmp.verticalAlignment = VerticalAlignmentOptions.Middle;
            digitTexts[i] = digitTmp;
        }

        // InputField oculto que captura el teclado
        GameObject hiddenInput = new GameObject("HiddenInputField");
        hiddenInput.transform.SetParent(container.transform, false);
        RectTransform hiddenRt = hiddenInput.AddComponent<RectTransform>();
        hiddenRt.anchorMin = Vector2.zero;
        hiddenRt.anchorMax = Vector2.one;
        hiddenRt.offsetMin = Vector2.zero;
        hiddenRt.offsetMax = Vector2.zero;

        // Imagen transparente para recibir clicks/touch
        Image hiddenBg = hiddenInput.AddComponent<Image>();
        hiddenBg.color = Color.clear;

        TMP_InputField inputField = hiddenInput.AddComponent<TMP_InputField>();
        inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        inputField.characterLimit = digitCount;

        // Text area invisible
        GameObject textArea = new GameObject("Text Area");
        textArea.transform.SetParent(hiddenInput.transform, false);
        RectTransform taRt = textArea.AddComponent<RectTransform>();
        taRt.anchorMin = Vector2.zero;
        taRt.anchorMax = Vector2.one;
        taRt.offsetMin = Vector2.zero;
        taRt.offsetMax = Vector2.zero;
        textArea.AddComponent<RectMask2D>();

        // Placeholder invisible
        GameObject phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(textArea.transform, false);
        RectTransform phRt = phObj.AddComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = Vector2.zero;
        phRt.offsetMax = Vector2.zero;
        TextMeshProUGUI phTmp = phObj.AddComponent<TextMeshProUGUI>();
        phTmp.text = "";
        phTmp.fontSize = 1f;
        phTmp.color = Color.clear;

        // Texto invisible (requerido por TMP_InputField)
        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(textArea.transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;
        TextMeshProUGUI txtTmp = txtObj.AddComponent<TextMeshProUGUI>();
        txtTmp.fontSize = 1f;
        txtTmp.color = Color.clear;

        inputField.textViewport = taRt;
        inputField.textComponent = txtTmp;
        inputField.placeholder = phTmp;

        // Sincronizar dígitos visibles con el input oculto
        int dc = digitCount;
        inputField.onValueChanged.AddListener((string val) =>
        {
            for (int j = 0; j < dc; j++)
            {
                digitTexts[j].text = j < val.Length ? val[j].ToString() : "";
                // Estilo: llena = borde púrpura, vacía = borde gris, activa = borde púrpura
                if (j < val.Length)
                    boxBorders[j].color = MenuTheme.PrimaryPurple;
                else if (j == val.Length)
                    boxBorders[j].color = MenuTheme.PrimaryPurple;
                else
                    boxBorders[j].color = MenuTheme.InputBorder;
            }
        });

        // Marcar la primera caja como activa al inicio
        if (boxBorders.Length > 0)
            boxBorders[0].color = MenuTheme.PrimaryPurple;

        return container;
    }

    // ── Toggle ─────────────────────────────────────────────────────────

    public static GameObject CreateToggle(Transform parent, string label, bool defaultValue = false)
    {
        Sprite rounded = GetRoundedSprite(MenuTheme.CornerRadiusSmall);

        GameObject container = new GameObject("Toggle_" + label.Replace(" ", ""));
        container.transform.SetParent(parent, false);
        container.AddComponent<RectTransform>().sizeDelta = new Vector2(400, 40);

        CreateText(container.transform, "Label", label,
            MenuTheme.InputTextSize, FontStyles.Normal, MenuTheme.TextPrimary, TextAlignmentOptions.Left)
            .GetComponent<RectTransform>().Set(
                new Vector2(0, 0), new Vector2(0.75f, 1), new Vector2(0, 0.5f),
                Vector2.zero, Vector2.zero);

        GameObject toggleBg = new GameObject("ToggleBg");
        toggleBg.transform.SetParent(container.transform, false);
        RectTransform tbRt = toggleBg.AddComponent<RectTransform>();
        tbRt.anchorMin = new Vector2(1, 0.5f); tbRt.anchorMax = new Vector2(1, 0.5f);
        tbRt.pivot = new Vector2(1, 0.5f);
        tbRt.sizeDelta = new Vector2(56, 30);
        Image tbImg = toggleBg.AddComponent<Image>();
        tbImg.sprite = rounded; tbImg.type = Image.Type.Sliced;
        tbImg.color = defaultValue ? MenuTheme.Gold : MenuTheme.InputBackground;

        GameObject knob = new GameObject("Knob");
        knob.transform.SetParent(toggleBg.transform, false);
        RectTransform kRt = knob.AddComponent<RectTransform>();
        kRt.sizeDelta = new Vector2(22, 22);
        kRt.anchorMin = new Vector2(0, 0.5f); kRt.anchorMax = new Vector2(0, 0.5f);
        kRt.pivot = new Vector2(0, 0.5f);
        kRt.anchoredPosition = new Vector2(defaultValue ? 30f : 4f, 0);
        Image kImg = knob.AddComponent<Image>();
        kImg.sprite = GetCircleSprite();
        kImg.color = Color.white;

        Toggle toggle = container.AddComponent<Toggle>();
        toggle.isOn = defaultValue;
        toggle.targetGraphic = tbImg;
        toggle.graphic = kImg;
        toggle.onValueChanged.AddListener((bool isOn) =>
        {
            tbImg.color = isOn ? MenuTheme.Gold : MenuTheme.InputBackground;
            kRt.anchoredPosition = new Vector2(isOn ? 30f : 4f, 0);
        });

        return container;
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static TMP_FontAsset _robotoFont;
    private static bool _fontLoaded;

    static TMP_FontAsset GetFont()
    {
        if (!_fontLoaded)
        {
            _fontLoaded = true;
            _robotoFont = Resources.Load<TMP_FontAsset>("Roboto-Bold SDF");
        }
        return _robotoFont;
    }

    public static GameObject CreateText(Transform parent, string name, string text,
        float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();

        // Roboto Bold — tipografía sólida para kiosco gubernamental
        TMP_FontAsset font = GetFont();
        if (font != null) tmp.font = font;

        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Truncate;
        return obj;
    }

    public static GameObject CreateScreenContainer(Transform parent, string name, bool visible = false)
    {
        GameObject screen = new GameObject(name);
        screen.transform.SetParent(parent, false);
        RectTransform rt = screen.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(0, 0);
        rt.offsetMax = new Vector2(0, -MenuTheme.HeaderHeight);

        CanvasGroup cg = screen.AddComponent<CanvasGroup>();
        cg.alpha = visible ? 1f : 0f;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;

        return screen;
    }
}

/// <summary>
/// Extension para configurar RectTransforms de forma compacta.
/// </summary>
public static class RectTransformExt
{
    public static void Set(this RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
    }
}
