using UnityEngine;

/// <summary>
/// Utilidad para mover Canvases UI a la pantalla principal (Display 0) en
/// setup de múltiples monitores. Maneja ambos modos de Canvas:
///   - ScreenSpaceOverlay: cambia targetDisplay.
///   - ScreenSpaceCamera: cambia worldCamera a una cámara en Display 0.
/// </summary>
public static class DisplayHelper
{
    public static void EnsureOnMainDisplay(Canvas canvas, string logPrefix = "[DisplayHelper]")
    {
        if (canvas == null) return;
        Canvas root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;

        if (root.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            if (root.targetDisplay != 0)
            {
                Debug.Log($"{logPrefix} Canvas '{root.name}' Overlay: targetDisplay {root.targetDisplay} → 0");
                root.targetDisplay = 0;
            }
        }
        else if (root.renderMode == RenderMode.ScreenSpaceCamera)
        {
            Camera target = FindMainDisplayCamera();
            if (target != null && root.worldCamera != target)
            {
                string oldName = root.worldCamera != null ? root.worldCamera.name : "(null)";
                Debug.Log($"{logPrefix} Canvas '{root.name}' ScreenSpaceCamera: worldCamera '{oldName}' → '{target.name}' (display 0)");
                root.worldCamera = target;
            }
            else if (target == null)
            {
                Debug.LogWarning($"{logPrefix} Canvas '{root.name}' ScreenSpaceCamera: no se encontró cámara en Display 0");
            }
        }
    }

    static Camera FindMainDisplayCamera()
    {
        Camera main = Camera.main;
        if (main != null && main.targetDisplay == 0) return main;
        foreach (var cam in Camera.allCameras)
        {
            if (cam != null && cam.targetDisplay == 0) return cam;
        }
        return main; // último fallback
    }
}
