using UnityEngine;

/// <summary>
/// Utilidad para mover Canvases UI a la pantalla central configurada en
/// setup de múltiples monitores. Maneja ambos modos de Canvas:
///   - ScreenSpaceOverlay: cambia targetDisplay.
///   - ScreenSpaceCamera: cambia worldCamera a la cámara del display central.
/// </summary>
public static class DisplayHelper
{
    public static int CenterDisplay
    {
        get
        {
            var cfg = SimulatorConfig.Instance?.data;
            if (cfg == null) return 0;
            if (cfg.displayCount == 1) return 0;
            int dc = cfg.displayCenter;
            if (dc < 0 || dc >= Display.displays.Length) return 0;
            return dc;
        }
    }

    public static void EnsureOnMainDisplay(Canvas canvas, string logPrefix = "[DisplayHelper]")
    {
        if (canvas == null) return;
        Canvas root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
        int cd = CenterDisplay;

        if (root.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            if (root.targetDisplay != cd)
            {
                Debug.Log($"{logPrefix} Canvas '{root.name}' Overlay: targetDisplay {root.targetDisplay} → {cd}");
                root.targetDisplay = cd;
            }
        }
        else if (root.renderMode == RenderMode.ScreenSpaceCamera)
        {
            Camera target = FindMainDisplayCamera();
            if (target != null && root.worldCamera != target)
            {
                string oldName = root.worldCamera != null ? root.worldCamera.name : "(null)";
                Debug.Log($"{logPrefix} Canvas '{root.name}' ScreenSpaceCamera: worldCamera '{oldName}' → '{target.name}' (display {cd})");
                root.worldCamera = target;
            }
            else if (target == null)
            {
                Debug.LogWarning($"{logPrefix} Canvas '{root.name}' ScreenSpaceCamera: no se encontró cámara en Display {cd}");
            }
        }
    }

    static Camera FindMainDisplayCamera()
    {
        int cd = CenterDisplay;
        Camera main = Camera.main;
        if (main != null && main.targetDisplay == cd) return main;
        foreach (var cam in Camera.allCameras)
        {
            if (cam != null && cam.targetDisplay == cd) return cam;
        }
        return main;
    }
}
