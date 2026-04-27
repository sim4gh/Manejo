using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Auto-bootstrap: cuando se carga una escena de manejo (cualquier escena
/// que NO sea MainMenu), inyecta ExamTimer automáticamente.
/// Sigue el mismo patrón que MenuBootstrap.cs.
/// </summary>
public static class ExamBootstrap
{
    static bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnSceneLoaded()
    {
        if (!subscribed)
        {
            SceneManager.sceneLoaded += OnSceneLoadedCallback;
            subscribed = true;
        }

        // Verificar escena actual por si ya es una escena de manejo
        string current = SceneManager.GetActiveScene().name;
        if (IsDrivingScene(current))
        {
            Setup();
        }
    }

    static void OnSceneLoadedCallback(Scene scene, LoadSceneMode mode)
    {
        if (IsDrivingScene(scene.name))
        {
            Setup();
        }
    }

    static bool IsDrivingScene(string sceneName)
    {
        return sceneName != "MainMenu" && sceneName != "SampleScene";
    }

    static void Setup()
    {
        if (ExamTimer.Instance == null)
        {
            Debug.Log("[ExamBootstrap] Inyectando ExamTimer en escena de manejo");
            GameObject obj = new GameObject("ExamTimerManager");
            obj.AddComponent<ExamTimer>();
        }

        // Si la escena no tiene un velocímetro activo (caso Motocicleta — el
        // SpeedGauge serializado vive bajo un GameObject "Player" desactivado),
        // spawneamos un HUD procedural mínimo para que igual se muestre la velocidad.
        EnsureSpeedHud();

        // Aplicar scoring config del backend a todos los detectores
        if (ScoringConfig.Instance != null)
        {
            ScoringConfig.Instance.ApplyToDetectors();
        }
    }

    static void EnsureSpeedHud()
    {
        // Solo cuenta una instancia activa: FindFirstObjectByType ya filtra inactivos.
        var active = Object.FindFirstObjectByType<SimpleSpeedGauge>();
        if (active != null) return;
        if (Object.FindFirstObjectByType<FallbackSpeedHud>() != null) return;

        Debug.Log("[ExamBootstrap] Sin SpeedGauge activo — spawning FallbackSpeedHud.");
        GameObject hud = new GameObject("FallbackSpeedHudManager");
        hud.AddComponent<FallbackSpeedHud>();
    }
}
