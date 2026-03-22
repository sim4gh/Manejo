using System.Collections;
using UnityEngine;

/// <summary>
/// Animaciones de UI basadas en coroutines. Sin dependencias externas.
/// Replica las animaciones fade-up / fade-in del portal web.
/// </summary>
public static class MenuAnimator
{
    /// <summary>
    /// Transición de opacidad de un CanvasGroup.
    /// </summary>
    public static IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;

        cg.alpha = from;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            cg.alpha = Mathf.Lerp(from, to, EaseOutCubic(t));
            yield return null;
        }

        cg.alpha = to;
        cg.interactable = to > 0.5f;
        cg.blocksRaycasts = to > 0.5f;
    }

    /// <summary>
    /// Entrada desde abajo con fade (replica animate-fade-up del portal).
    /// </summary>
    public static IEnumerator SlideAndFade(RectTransform rt, CanvasGroup cg,
        float slideOffset, float duration, float delay = 0f)
    {
        if (rt == null || cg == null) yield break;

        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        Vector2 startPos = rt.anchoredPosition;
        Vector2 fromPos = new Vector2(startPos.x, startPos.y - slideOffset);
        rt.anchoredPosition = fromPos;
        cg.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutCubic(t);

            rt.anchoredPosition = Vector2.Lerp(fromPos, startPos, eased);
            cg.alpha = Mathf.Lerp(0f, 1f, eased);
            yield return null;
        }

        rt.anchoredPosition = startPos;
        cg.alpha = 1f;
    }

    /// <summary>
    /// Efecto de escala al seleccionar una tarjeta.
    /// </summary>
    public static IEnumerator ScalePunch(RectTransform rt, float targetScale, float duration)
    {
        if (rt == null) yield break;

        Vector3 originalScale = rt.localScale;
        Vector3 punchScale = originalScale * targetScale;
        float halfDuration = duration * 0.5f;

        // Scale up
        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            rt.localScale = Vector3.Lerp(originalScale, punchScale, EaseOutCubic(t));
            yield return null;
        }

        // Scale back
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            rt.localScale = Vector3.Lerp(punchScale, originalScale, EaseInCubic(t));
            yield return null;
        }

        rt.localScale = originalScale;
    }

    /// <summary>
    /// Transición suave de color de una UnityEngine.UI.Image.
    /// </summary>
    public static IEnumerator TintColor(UnityEngine.UI.Image image, Color from, Color to, float duration)
    {
        if (image == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            image.color = Color.Lerp(from, to, EaseOutCubic(t));
            yield return null;
        }

        image.color = to;
    }

    // ── Easing functions ───────────────────────────────────────────────

    public static float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    public static float EaseInCubic(float t)
    {
        return t * t * t;
    }

    public static float EaseInOutCubic(float t)
    {
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }
}
