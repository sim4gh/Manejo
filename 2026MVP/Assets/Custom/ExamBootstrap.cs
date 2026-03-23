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
        if (ExamTimer.Instance != null) return;

        Debug.Log("[ExamBootstrap] Inyectando ExamTimer en escena de manejo");
        GameObject obj = new GameObject("ExamTimerManager");
        obj.AddComponent<ExamTimer>();
    }
}
