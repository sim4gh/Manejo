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
            // Single-monitor: explícito por config o porque físicamente solo hay 1
            // display reportado por Unity. Esto evita HUD/cámaras apuntando a
            // displays inexistentes cuando un kiosko configurado para 3 monitores
            // se ejecuta temporalmente con solo uno conectado.
            if (cfg.displayCount == 1) return 0;
            if (Display.displays.Length < 2) return 0;
            int dc = cfg.displayCenter;
            if (dc < 0 || dc >= Display.displays.Length) return 0;
            return dc;
        }
    }

    /// <summary>
    /// Display sobre el que está renderizando la cámara de "vista frontal" del
    /// jugador. Usa la cámara real (siguiendo nombres conocidos y bajando a hijos
    /// cuando hace falta — caso moto: CockpitCamera es un padre vacío con 3
    /// cámaras dentro, una por display). Cae a CenterDisplay si no encuentra.
    /// Esta es la fuente de verdad para colocar HUDs: garantiza que el HUD vaya
    /// al mismo monitor que la vista del jugador, aunque MultiPantallaManager no
    /// haya podido reasignar targetDisplays (porque la cámara serializada vive
    /// en una jerarquía con padres desactivados o con estructura diferente).
    /// </summary>
    public static int CockpitDisplay
    {
        get
        {
            Camera cam = FindCockpitCamera();
            if (cam != null) return cam.targetDisplay;
            return CenterDisplay;
        }
    }

    static readonly string[] CockpitNames = { "Camara_Centro", "CamaraCentro", "CockpitCamera" };

    static Camera FindCockpitCamera()
    {
        int center = CenterDisplay;

        // Camera.main (tagged MainCamera) si existe
        Camera main = Camera.main;
        if (main != null && main.enabled) return main;

        // Por nombre — el GameObject mismo. Si tiene Camera component, listo.
        // Si no (caso moto: CockpitCamera es solo un Transform con 3 cámaras
        // hijas — una por display), buscamos la hija cuyo targetDisplay
        // coincide con el display central configurado. Sin esa preferencia,
        // GetComponentInChildren devuelve la primera hija, que probablemente
        // es la cámara izquierda y nos colocaría el HUD en el monitor equivocado.
        foreach (var n in CockpitNames)
        {
            GameObject go = GameObject.Find(n);
            if (go == null) continue;

            Camera direct = go.GetComponent<Camera>();
            if (direct != null && direct.enabled) return direct;

            Camera[] children = go.GetComponentsInChildren<Camera>(true);
            // Preferir la hija que ya está apuntando al display central.
            foreach (var c in children)
            {
                if (c != null && c.enabled && c.targetDisplay == center) return c;
            }
            // Si ninguna matchea (o sus targets no fueron reasignados todavía),
            // tomar la primera enabled — mejor que nada.
            foreach (var c in children)
            {
                if (c != null && c.enabled) return c;
            }
        }

        // Última opción: cualquier cámara enabled cuyo targetDisplay matchee
        // la central configurada.
        foreach (var c in Camera.allCameras)
        {
            if (c != null && c.enabled && c.targetDisplay == center) return c;
        }
        return null;
    }

    public static void EnsureOnMainDisplay(Canvas canvas, string logPrefix = "[DisplayHelper]")
    {
        if (canvas == null) return;
        Canvas root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
        // Preferimos el display de la cámara cockpit real (sigue al jugador
        // aunque MultiPantallaManager no haya podido reasignar targets).
        int cd = CockpitDisplay;

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
