using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Auto-bootstrap: cuando se carga la escena MainMenu, encuentra el Canvas
/// y le adjunta MenuScreenManager automáticamente. No requiere configuración
/// manual en el editor.
/// </summary>
public static class MenuBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnSceneLoaded()
    {
        SceneManager.sceneLoaded += OnSceneLoadedCallback;

        // También ejecutar para la escena actual (por si MainMenu ya está cargada)
        if (SceneManager.GetActiveScene().name == "MainMenu")
        {
            SetupMenu();
        }
    }

    static void OnSceneLoadedCallback(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu")
        {
            SetupMenu();
        }
    }

    static void SetupMenu()
    {
        // Buscar el Canvas principal en la escena
#pragma warning disable CS0618
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
#pragma warning restore CS0618

        Canvas targetCanvas = null;
        foreach (var canvas in canvases)
        {
            // Usar el primer Canvas root (ScreenSpaceOverlay)
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.transform.parent == null)
            {
                targetCanvas = canvas;
                break;
            }
        }

        if (targetCanvas == null && canvases.Length > 0)
        {
            targetCanvas = canvases[0];
        }

        if (targetCanvas == null)
        {
            Debug.LogWarning("[MenuBootstrap] No se encontró Canvas en MainMenu");
            return;
        }

        // Verificar que no tenga ya MenuScreenManager
        if (targetCanvas.GetComponent<MenuScreenManager>() != null)
        {
            return;
        }

        Debug.Log("[MenuBootstrap] Adjuntando MenuScreenManager al Canvas");
        targetCanvas.gameObject.AddComponent<MenuScreenManager>();
    }
}
