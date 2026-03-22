using UnityEngine;

/// <summary>
/// Constantes de diseño del menú — flat/clean, legible.
/// Paleta del portal web (tailwind.config.ts).
/// </summary>
public static class MenuTheme
{
    // ── Paleta base ────────────────────────────────────────────────────
    public static readonly Color PrimaryPurple    = HexColor("#3D1A50");  // Más oscuro para accesibilidad
    public static readonly Color PrimaryAccent    = HexColor("#2E1440");
    public static readonly Color SecondaryCrimson = HexColor("#A2185D");
    public static readonly Color Gold             = HexColor("#CDB786");
    public static readonly Color GoldDark         = HexColor("#8B7355");
    public static readonly Color GoldLight        = HexColor("#F5F0E4");

    // ── Fondo ──────────────────────────────────────────────────────────
    public static readonly Color PageBackground   = Color.white;

    // ── Cards ──────────────────────────────────────────────────────────
    public static readonly Color CardBackground     = HexColor("#F3F4F6");  // gray-100
    public static readonly Color CardSelected       = HexColor("#EDE7F1");  // light purple
    public static readonly Color CardBorder         = HexColor("#E5E7EB");  // gray-200
    public static readonly Color CardBorderGold     = HexColor("#3D1A50");  // Púrpura oscuro para selección

    // ── Texto (oscuro sobre fondo claro — máxima legibilidad) ──────────
    public static readonly Color TextPrimary      = HexColor("#3D1A50");  // Primary purple — gubernamental
    public static readonly Color TextSecondary    = HexColor("#4B5563");  // gray-600
    public static readonly Color TextMuted        = HexColor("#6B7280");  // gray-500 (WCAG AA 4.6:1)

    // ── Texto sobre cards ──────────────────────────────────────────────
    public static readonly Color TextOnCard       = HexColor("#111827");
    public static readonly Color TextOnCardMuted  = HexColor("#6B7280");  // gray-500

    // ── Estados ────────────────────────────────────────────────────────
    public static readonly Color TextError        = HexColor("#DC2626");  // red-600
    public static readonly Color TextSuccess      = HexColor("#16A34A");  // green-600
    public static readonly Color SuccessGreen     = HexColor("#16A34A");

    // ── Botones ────────────────────────────────────────────────────────
    public static readonly Color ButtonPrimary      = PrimaryPurple;      // Púrpura oscuro
    public static readonly Color ButtonPrimaryText  = Color.white;
    public static readonly Color ButtonSecondary    = HexColor("#F3F4F6");
    public static readonly Color ButtonSecondaryText = HexColor("#374151");
    public static readonly Color ButtonGhost        = Color.clear;
    public static readonly Color ButtonDisabled     = HexColor("#E5E7EB");
    public static readonly Color ButtonDisabledText = HexColor("#6B7280");

    // ── Inputs ─────────────────────────────────────────────────────────
    public static readonly Color InputBackground  = HexColor("#F3F4F6");  // gray-100
    public static readonly Color InputBorder      = HexColor("#D1D5DB");  // gray-300
    public static readonly Color InputPlaceholder = HexColor("#4B5563");  // gray-600 (WCAG AA 7:1 sobre gray-100)
    public static readonly Color InputText        = HexColor("#111827");

    // ── Placeholders de ícono ──────────────────────────────────────────
    public static readonly Color CircleBg         = HexColor("#EDE7F1");
    public static readonly Color CircleBgSelected = HexColor("#3D1A50");
    public static readonly Color CircleText       = HexColor("#3D1A50");
    public static readonly Color CircleTextSelected = Color.white;

    // ── Verificación de volante ────────────────────────────────────────
    public static readonly Color IndicatorPending = HexColor("#E5E7EB");
    public static readonly Color IndicatorActive  = HexColor("#D1D5DB");
    public static readonly Color IndicatorDone    = HexColor("#16A34A");

    // ── QR ──────────────────────────────────────────────────────────────
    public static readonly Color QRBackground     = HexColor("#F3F4F6");
    public static readonly Color DividerColor     = HexColor("#D1D5DB");

    // ── Corner radius (MUY sutil) ─────────────────────────────────────
    public const int CornerRadius        = 5;
    public const int CornerRadiusSmall   = 4;
    public const int CardCornerRadius    = 6;

    // ── Texto (GRANDE — usuario a 1m de pantalla) ──────────────────────
    public const float TitleSize         = 52f;
    public const float SubtitleSize      = 26f;
    public const float CardTitleSize     = 28f;
    public const float CardDescSize      = 18f;
    public const float ButtonTextSize    = 24f;
    public const float LabelSize         = 20f;
    public const float InputTextSize     = 24f;
    public const float HeaderTitleSize   = 72f;
    public const float HeaderSubSize     = 16f;

    public const float HeaderHeight      = 0f; // Sin navbar, solo título flotante

    // ── Animación ──────────────────────────────────────────────────────
    public const float ScreenFadeDuration = 0.4f;
    public const float CardStaggerDelay   = 0.08f;
    public const float CardSlideDuration  = 0.5f;
    public const float CardSlideOffset    = 30f;
    public const float CardPunchScale     = 1.04f;
    public const float CardPunchDuration  = 0.2f;

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color color);
        return color;
    }
}
