using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controller for the main menu UI.
/// Handles expediente input and navigation to telemetry folder.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField expedienteInput;
    public Button empezarButton;
    public Button abrirFolderButton;
    public TextMeshProUGUI errorText;

    // LEGACY: este flujo del menú fue reemplazado por MenuScreenManager (Pantalla 1
    // tiene su propio toggle Auto/Manual). MainMenuController ya no se inyecta en
    // el bootstrap actual. NO reactivar — generaría doble fuente de verdad sobre
    // PlayerPrefs["TransmisionManual"].
    [Header("Transmission")]
    [Tooltip("Toggle for manual transmission (off = automatic)")]
    public Toggle transmisionManualToggle;

    [Header("Settings")]
    [Tooltip("Name of the game scene to load")]
    public string gameSceneName = "UrbanExample";

    void Start()
    {
        // Setup button listeners
        if (empezarButton != null)
            empezarButton.onClick.AddListener(OnEmpezarClicked);

        if (abrirFolderButton != null)
            abrirFolderButton.onClick.AddListener(OnAbrirFolderClicked);

        // Clear any previous error
        if (errorText != null)
            errorText.text = "";

        // Transmission toggle
        if (transmisionManualToggle != null)
        {
            transmisionManualToggle.isOn = PlayerPrefs.GetInt("TransmisionManual", 0) == 1;
            transmisionManualToggle.onValueChanged.AddListener(OnTransmisionChanged);
        }

        // Ensure GameManager exists
        if (GameManager.Instance == null)
        {
            GameObject go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
        }
    }

    /// <summary>
    /// Called when the "Empezar" button is clicked.
    /// Validates expediente input and loads the game scene.
    /// </summary>
    void OnEmpezarClicked()
    {
        string expediente = expedienteInput != null ? expedienteInput.text.Trim() : "";

        if (string.IsNullOrEmpty(expediente))
        {
            if (errorText != null)
                errorText.text = "Ingrese el número de expediente";
            return;
        }

        // Clear error and store expediente
        if (errorText != null)
            errorText.text = "";

        GameManager.Instance.Expediente = expediente;

        Debug.Log($"Starting game with expediente: {expediente}");
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>
    /// Called when the "Abrir folder de Telemetría" button is clicked.
    /// Opens the telemetry folder in the OS file explorer.
    /// </summary>
    void OnTransmisionChanged(bool isManual)
    {
        PlayerPrefs.SetInt("TransmisionManual", isManual ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"Transmission set to: {(isManual ? "Manual" : "Automatic")}");
    }

    void OnAbrirFolderClicked()
    {
        string path = Application.persistentDataPath;
        Debug.Log($"Opening telemetry folder: {path}");

        // Open folder in file explorer
        // Works on Windows, macOS, and Linux
        Application.OpenURL("file://" + path);
    }
}
