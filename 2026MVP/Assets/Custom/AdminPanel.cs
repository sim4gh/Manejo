using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// Detecta F10 mantenido 1.5s para navegar a la pantalla admin en MenuScreenManager.
/// DontDestroyOnLoad — solo activo en MainMenu.
/// </summary>
public class AdminPanel : MonoBehaviour
{
    public static AdminPanel Instance { get; private set; }

    private const float HOLD_DURATION = 1.5f;
    private float holdTimer;
    private static string cachedSceneName = "";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += (scene, _) => cachedSceneName = scene.name;
            cachedSceneName = SceneManager.GetActiveScene().name;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        if (cachedSceneName != "MainMenu") return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.f10Key.isPressed)
        {
            holdTimer += Time.unscaledDeltaTime;
            if (holdTimer >= HOLD_DURATION)
            {
                holdTimer = 0f;
                var msm = Object.FindFirstObjectByType<MenuScreenManager>();
                if (msm != null)
                {
                    msm.NavigateToAdmin();
                    Debug.Log("[AdminPanel] F10 → navegando a pantalla admin");
                }
            }
        }
        else
        {
            holdTimer = 0f;
        }
    }
}
