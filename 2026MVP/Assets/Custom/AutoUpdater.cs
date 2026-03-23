using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;

/// <summary>
/// Auto-updater para el simulador Unity.
/// - Al iniciar: verifica si hay nueva versión disponible.
/// - Si hay: descarga ZIP en background.
/// - Al final del día (o en siguiente arranque): instala y reinicia.
/// </summary>
public class AutoUpdater : MonoBehaviour
{
    public static AutoUpdater Instance { get; private set; }

    [System.Serializable]
    public class UpdateCheckResponse
    {
        public bool updateAvailable;
        public string currentVersion;
        public string latestVersion;
        public string downloadUrl;
        public string sha256;
        public long size;
        public string releaseNotes;
        public bool mandatory;
    }

    // Estado
    public bool UpdateAvailable { get; private set; }
    public bool UpdateDownloaded { get; private set; }
    public string LatestVersion { get; private set; }
    public string ReleaseNotes { get; private set; }
    public float DownloadProgress { get; private set; }

    private string downloadedZipPath;
    private bool isChecking;
    private bool isDownloading;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Check automático si auto-update está habilitado
        if (SimulatorConfig.Instance?.data.autoUpdate == true)
        {
            StartCoroutine(AutoCheckAndDownload());
        }
    }

    IEnumerator AutoCheckAndDownload()
    {
        // Esperar un frame para que todo se inicialice
        yield return null;

        yield return CheckForUpdate(null);

        if (UpdateAvailable && !UpdateDownloaded)
        {
            Debug.Log($"[AutoUpdater] Nueva versión {LatestVersion} disponible, descargando...");
            yield return DownloadUpdate();
        }

        // Si hay update pendiente de una sesión anterior, verificar si es hora de instalar
        CheckPendingInstall();
    }

    /// <summary>
    /// Verifica si hay una nueva versión disponible.
    /// El callback recibe (updateAvailable, latestVersion).
    /// </summary>
    public IEnumerator CheckForUpdate(System.Action<bool, string> onResult)
    {
        if (isChecking) yield break;
        isChecking = true;

        string baseUrl = SimulatorConfig.Instance?.data.apiBaseUrl
            ?? "https://d6twaegbhg.execute-api.us-east-1.amazonaws.com";
        string url = $"{baseUrl}/simulator/update-check?currentVersion={Application.version}";

        Debug.Log($"[AutoUpdater] Verificando actualizaciones: {url}");

        using (var request = UnityWebRequest.Get(url))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<UpdateCheckResponse>(request.downloadHandler.text);
                    UpdateAvailable = response.updateAvailable;
                    LatestVersion = response.latestVersion;
                    ReleaseNotes = response.releaseNotes;

                    if (response.updateAvailable)
                    {
                        Debug.Log($"[AutoUpdater] Actualización disponible: {response.latestVersion}");
                        // Guardar URL para descarga
                        PlayerPrefs.SetString("PendingUpdateUrl", response.downloadUrl ?? "");
                        PlayerPrefs.SetString("PendingUpdateVersion", response.latestVersion ?? "");
                        PlayerPrefs.Save();
                    }
                    else
                    {
                        Debug.Log("[AutoUpdater] El simulador está al día");
                    }

                    onResult?.Invoke(response.updateAvailable, response.latestVersion);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[AutoUpdater] Error parseando respuesta: {e.Message}");
                    onResult?.Invoke(false, null);
                }
            }
            else
            {
                Debug.LogWarning($"[AutoUpdater] Error verificando: {request.error}");
                onResult?.Invoke(false, null);
            }
        }

        isChecking = false;
    }

    /// <summary>Descarga el ZIP de la nueva versión en background.</summary>
    public IEnumerator DownloadUpdate()
    {
        if (isDownloading) yield break;

        string downloadUrl = PlayerPrefs.GetString("PendingUpdateUrl", "");
        if (string.IsNullOrEmpty(downloadUrl))
        {
            Debug.LogWarning("[AutoUpdater] No hay URL de descarga");
            yield break;
        }

        isDownloading = true;
        DownloadProgress = 0f;

        string tempDir = Path.Combine(Application.persistentDataPath, "updates");
        if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

        string version = PlayerPrefs.GetString("PendingUpdateVersion", "unknown");
        downloadedZipPath = Path.Combine(tempDir, $"Tlax2026MVP-v{version}.zip");

        // Si ya se descargó previamente, no re-descargar
        if (File.Exists(downloadedZipPath))
        {
            Debug.Log("[AutoUpdater] ZIP ya descargado previamente");
            UpdateDownloaded = true;
            isDownloading = false;
            yield break;
        }

        Debug.Log($"[AutoUpdater] Descargando: {downloadUrl}");

        using (var request = UnityWebRequest.Get(downloadUrl))
        {
            request.timeout = 600; // 10 min para archivos grandes

            var op = request.SendWebRequest();

            while (!op.isDone)
            {
                DownloadProgress = request.downloadProgress;
                yield return null;
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                File.WriteAllBytes(downloadedZipPath, request.downloadHandler.data);
                UpdateDownloaded = true;
                DownloadProgress = 1f;
                Debug.Log($"[AutoUpdater] Descarga completada: {downloadedZipPath} ({request.downloadHandler.data.Length / 1048576}MB)");

                PlayerPrefs.SetString("PendingUpdateZip", downloadedZipPath);
                PlayerPrefs.Save();
            }
            else
            {
                Debug.LogWarning($"[AutoUpdater] Error descargando: {request.error}");
                DownloadProgress = 0f;
            }
        }

        isDownloading = false;
    }

    /// <summary>
    /// Verifica si hay un update descargado pendiente de instalar.
    /// Se instala automáticamente si es hora (>= 17:00) o si es mandatory.
    /// </summary>
    void CheckPendingInstall()
    {
        string pendingZip = PlayerPrefs.GetString("PendingUpdateZip", "");
        if (string.IsNullOrEmpty(pendingZip) || !File.Exists(pendingZip)) return;

        downloadedZipPath = pendingZip;
        UpdateDownloaded = true;

        // Instalar automáticamente si es después de las 17:00
        int hour = System.DateTime.Now.Hour;
        if (hour >= 17)
        {
            Debug.Log("[AutoUpdater] Es después de las 17:00, instalando actualización pendiente...");
            InstallUpdate();
        }
    }

    /// <summary>
    /// Instala la actualización: escribe update.bat, lo ejecuta y cierra Unity.
    /// Llamado por AdminPanel o automáticamente al final del día.
    /// </summary>
    public void InstallUpdate()
    {
        if (string.IsNullOrEmpty(downloadedZipPath) || !File.Exists(downloadedZipPath))
        {
            Debug.LogWarning("[AutoUpdater] No hay ZIP para instalar");
            return;
        }

        string appDir = Path.GetDirectoryName(Application.dataPath); // Parent of _Data
        string exeName = Path.GetFileName(System.Environment.GetCommandLineArgs()[0]);
        string exePath = Path.Combine(appDir, exeName);
        string stagingDir = Path.Combine(Application.persistentDataPath, "updates", "staging");
        string batPath = Path.Combine(Application.persistentDataPath, "update.bat");

        // Escribir batch script
        string batContent = $@"@echo off
echo Instalando actualizacion del simulador...
timeout /t 3 /nobreak >nul

echo Extrayendo archivos...
powershell -Command ""Expand-Archive -Path '{downloadedZipPath}' -DestinationPath '{stagingDir}' -Force""

echo Copiando archivos nuevos...
for /d %%D in (""{stagingDir}\*"") do (
    xcopy /s /e /y ""%%D\*"" ""{appDir}\""
)

echo Limpiando...
rmdir /s /q ""{stagingDir}""
del ""{downloadedZipPath}""

echo Reiniciando simulador...
start """" ""{exePath}""
exit
";

        File.WriteAllText(batPath, batContent);
        Debug.Log($"[AutoUpdater] Batch escrito en: {batPath}");

        // Limpiar prefs de update pendiente
        PlayerPrefs.DeleteKey("PendingUpdateUrl");
        PlayerPrefs.DeleteKey("PendingUpdateVersion");
        PlayerPrefs.DeleteKey("PendingUpdateZip");
        PlayerPrefs.Save();

        // Ejecutar batch y cerrar Unity
        var processInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = batPath,
            UseShellExecute = true,
            CreateNoWindow = false,
        };

        System.Diagnostics.Process.Start(processInfo);
        Application.Quit();
    }
}
