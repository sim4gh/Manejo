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

        // HUD superior unificado: timer + velocímetro + velocidad máxima.
        // Reemplaza al SimpleSpeedGauge bottom-center y al FallbackSpeedHud bottom-left.
        if (TopHudRow.Instance == null)
        {
            Debug.Log("[ExamBootstrap] Inyectando TopHudRow en escena de manejo");
            GameObject row = new GameObject("TopHudRowManager");
            row.AddComponent<TopHudRow>();
        }

        // Desactivar SpeedGauges legacy si la escena los trae serializados —
        // evita doble velocímetro (bottom-center + top-row).
        DisableLegacySpeedHuds();

        // Aplicar scoring config del backend a todos los detectores
        if (ScoringConfig.Instance != null)
        {
            ScoringConfig.Instance.ApplyToDetectors();
        }
    }

    static void DisableLegacySpeedHuds()
    {
        // SimpleSpeedGauge serializado en escena (Sedan, Camioneta, etc.) — desactivar
        // el GameObject contenedor para que no aparezca duplicado debajo del row.
        foreach (var gauge in Object.FindObjectsByType<SimpleSpeedGauge>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (gauge != null && gauge.gameObject.activeSelf)
            {
                gauge.gameObject.SetActive(false);
            }
        }
        // FallbackSpeedHud (si quedó instanciado de un Setup anterior).
        foreach (var fb in Object.FindObjectsByType<FallbackSpeedHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (fb != null && fb.gameObject.activeSelf)
            {
                Object.Destroy(fb.gameObject);
            }
        }
    }
}
