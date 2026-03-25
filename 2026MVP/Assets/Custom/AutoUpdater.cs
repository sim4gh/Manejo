using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System.Security.Cryptography;

/// <summary>
/// Auto-updater para el simulador Unity.
/// Flujo heartbeat-driven:
/// 1. Heartbeat devuelve pendingUpdate → se almacena aquí.
/// 2. Al llegar scheduledAfter → solicita presigned URL vía /simulator/request-update.
/// 3. Descarga ZIP, verifica SHA256.
/// 4. Genera update.bat, reinicia.
/// 5. Al reiniciar, register envía nuevo appVersion → backend limpia pendingUpdate.
/// </summary>
public class AutoUpdater : MonoBehaviour
{
    public static AutoUpdater Instance { get; private set; }

    // ── Estado público ────────────────────────────────────────────────
    public bool UpdateAvailable { get; private set; }
    public bool UpdateDownloaded { get; private set; }
    public string LatestVersion { get; private set; }
    public string ReleaseNotes { get; private set; }
    public float DownloadProgress { get; private set; }
    public string CurrentStatus { get; private set; } = "IDLE";

    // ── Estado interno ────────────────────────────────────────────────
    private SimulatorApiClient.PendingUpdateData pendingData;
    private string downloadedZipPath;
    private bool isDownloading;
    private bool isProcessing;

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
        // Limpiar archivos residuales de updates anteriores
        CleanupOldUpdates();
    }

    /// <summary>
    /// Llamado por SendHeartbeat() cuando el heartbeat devuelve pendingUpdate.
    /// </summary>
    public void ProcessPendingUpdate(SimulatorApiClient.PendingUpdateData data)
    {
        if (data == null) return;

        // Si ya se instaló o estamos en medio de un proceso, ignorar
        if (data.status == "INSTALLED") return;
        if (isDownloading || isProcessing) return;

        pendingData = data;
        UpdateAvailable = true;
        LatestVersion = data.version;
        ReleaseNotes = data.releaseNotes;
        CurrentStatus = data.status;

        // Si el status ya es DOWNLOADING/DOWNLOADED del lado del backend (retry), permitir re-descarga
        if (data.status == "PENDING" || data.status == "FAILED")
        {
            // Verificar si ya llegó la hora de scheduledAfter
            if (IsScheduledTimeReached(data.scheduledAfter))
            {
                Debug.Log($"[AutoUpdater] Update v{data.version} programado, iniciando descarga...");
                StartCoroutine(RequestAndDownload());
            }
            else
            {
                Debug.Log($"[AutoUpdater] Update v{data.version} pendiente, esperando horario: {data.scheduledAfter}");
            }
        }
    }

    bool IsScheduledTimeReached(string scheduledAfter)
    {
        if (string.IsNullOrEmpty(scheduledAfter)) return true;

        if (System.DateTime.TryParse(scheduledAfter, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out System.DateTime scheduled))
        {
            return System.DateTime.UtcNow >= scheduled.ToUniversalTime();
        }

        // Si no se puede parsear, proceder de inmediato
        return true;
    }

    /// <summary>
    /// Solicita URL de descarga y descarga el ZIP.
    /// </summary>
    IEnumerator RequestAndDownload()
    {
        if (isDownloading || pendingData == null) yield break;
        isDownloading = true;
        isProcessing = true;

        // 1. Solicitar presigned URL
        Debug.Log($"[AutoUpdater] Solicitando URL de descarga para v{pendingData.version}...");
        SimulatorApiClient.UpdateUrlResponse urlResponse = null;

        yield return SimulatorApiClient.RequestUpdateUrl(pendingData.version, (resp) => urlResponse = resp);

        if (urlResponse == null || string.IsNullOrEmpty(urlResponse.downloadUrl))
        {
            Debug.LogWarning("[AutoUpdater] No se pudo obtener URL de descarga");
            yield return SimulatorApiClient.ReportUpdateStatus("FAILED", "No se pudo obtener URL de descarga", null);
            isDownloading = false;
            isProcessing = false;
            CurrentStatus = "FAILED";
            yield break;
        }

        // 2. Reportar DOWNLOADING
        yield return SimulatorApiClient.ReportUpdateStatus("DOWNLOADING", null, null);
        CurrentStatus = "DOWNLOADING";

        // 3. Descargar ZIP
        string tempDir = Path.Combine(Application.persistentDataPath, "updates");
        if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
        downloadedZipPath = Path.Combine(tempDir, $"Tlax2026MVP-v{pendingData.version}.zip");

        // Si ya existe el archivo (descarga previa), verificar hash
        if (File.Exists(downloadedZipPath))
        {
            string existingHash = ComputeFileSHA256(downloadedZipPath);
            if (!string.IsNullOrEmpty(urlResponse.sha256) && existingHash == urlResponse.sha256)
            {
                Debug.Log("[AutoUpdater] ZIP ya descargado y verificado previamente");
                UpdateDownloaded = true;
                DownloadProgress = 1f;
                yield return OnDownloadComplete(urlResponse.sha256);
                yield break;
            }
            else
            {
                File.Delete(downloadedZipPath);
            }
        }

        Debug.Log($"[AutoUpdater] Descargando: {urlResponse.downloadUrl.Substring(0, 100)}...");

        using (var request = UnityWebRequest.Get(urlResponse.downloadUrl))
        {
            request.timeout = 1200; // 20 min para archivos grandes (>500MB)
            request.downloadHandler = new DownloadHandlerFile(downloadedZipPath);

            var op = request.SendWebRequest();

            while (!op.isDone)
            {
                DownloadProgress = request.downloadProgress;
                yield return null;
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                DownloadProgress = 1f;
                long fileSize = new FileInfo(downloadedZipPath).Length;
                Debug.Log($"[AutoUpdater] Descarga completada: {downloadedZipPath} ({fileSize / 1048576}MB)");

                yield return OnDownloadComplete(urlResponse.sha256);
            }
            else
            {
                Debug.LogWarning($"[AutoUpdater] Error descargando: {request.error}");
                yield return SimulatorApiClient.ReportUpdateStatus("FAILED", $"Download error: {request.error}", null);
                CurrentStatus = "FAILED";
                isDownloading = false;
                isProcessing = false;
            }
        }
    }

    IEnumerator OnDownloadComplete(string expectedSha256)
    {
        // 4. Verificar SHA256
        if (!string.IsNullOrEmpty(expectedSha256))
        {
            Debug.Log("[AutoUpdater] Verificando SHA256...");
            string actualHash = ComputeFileSHA256(downloadedZipPath);

            if (actualHash != expectedSha256)
            {
                Debug.LogWarning($"[AutoUpdater] SHA256 no coincide! Esperado: {expectedSha256}, Actual: {actualHash}");
                File.Delete(downloadedZipPath);
                yield return SimulatorApiClient.ReportUpdateStatus("FAILED", "SHA256 mismatch", null);
                CurrentStatus = "FAILED";
                isDownloading = false;
                isProcessing = false;
                yield break;
            }

            Debug.Log("[AutoUpdater] SHA256 verificado correctamente");
        }

        UpdateDownloaded = true;
        yield return SimulatorApiClient.ReportUpdateStatus("DOWNLOADED", null, null);
        CurrentStatus = "DOWNLOADED";
        isDownloading = false;

        // 5. Instalar inmediatamente (ya pasó el scheduledAfter)
        Debug.Log("[AutoUpdater] Procediendo a instalar...");
        InstallUpdate();
    }

    string ComputeFileSHA256(string filePath)
    {
        using (var sha256 = SHA256.Create())
        using (var stream = File.OpenRead(filePath))
        {
            byte[] hash = sha256.ComputeHash(stream);
            var sb = new System.Text.StringBuilder(64);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    /// <summary>
    /// Instala la actualización: escribe update.bat, lo ejecuta y cierra Unity.
    /// </summary>
    public void InstallUpdate()
    {
        if (string.IsNullOrEmpty(downloadedZipPath) || !File.Exists(downloadedZipPath))
        {
            Debug.LogWarning("[AutoUpdater] No hay ZIP para instalar");
            return;
        }

        // Reportar INSTALLING (fire and forget — no yield porque vamos a cerrar)
        StartCoroutine(SimulatorApiClient.ReportUpdateStatus("INSTALLING", null, null));
        CurrentStatus = "INSTALLING";

        string appDir = Path.GetDirectoryName(Application.dataPath);
        string exeName = Path.GetFileName(System.Environment.GetCommandLineArgs()[0]);
        string exePath = Path.Combine(appDir, exeName);
        string stagingDir = Path.Combine(Application.persistentDataPath, "updates", "staging");
        string batPath = Path.Combine(Application.persistentDataPath, "update.bat");

        string batContent = $@"@echo off
echo Instalando actualizacion del simulador v{LatestVersion}...
timeout /t 3 /nobreak >nul

echo Extrayendo archivos...
powershell -Command ""Expand-Archive -Path '{downloadedZipPath}' -DestinationPath '{stagingDir}' -Force""
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Fallo la extraccion del ZIP
    exit /b 1
)

echo Copiando archivos nuevos...
for /d %%D in (""{stagingDir}\*"") do (
    xcopy /s /e /y ""%%D\*"" ""{appDir}\""
)

echo Limpiando archivos temporales...
rmdir /s /q ""{stagingDir}""
del ""{downloadedZipPath}""

echo Reiniciando simulador...
start """" ""{exePath}""

echo Borrando este script...
del ""%~f0""
exit
";

        File.WriteAllText(batPath, batContent);
        Debug.Log($"[AutoUpdater] Batch escrito en: {batPath}");

        // Limpiar PlayerPrefs de sistema antiguo
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

    /// <summary>
    /// Limpia archivos residuales de updates anteriores al iniciar.
    /// </summary>
    void CleanupOldUpdates()
    {
        string updatesDir = Path.Combine(Application.persistentDataPath, "updates");
        if (Directory.Exists(updatesDir))
        {
            try
            {
                Directory.Delete(updatesDir, true);
                Debug.Log("[AutoUpdater] Directorio updates limpiado");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AutoUpdater] Error limpiando updates: {e.Message}");
            }
        }

        // Limpiar PlayerPrefs del sistema antiguo
        if (!string.IsNullOrEmpty(PlayerPrefs.GetString("PendingUpdateZip", "")))
        {
            PlayerPrefs.DeleteKey("PendingUpdateUrl");
            PlayerPrefs.DeleteKey("PendingUpdateVersion");
            PlayerPrefs.DeleteKey("PendingUpdateZip");
            PlayerPrefs.Save();
            Debug.Log("[AutoUpdater] PlayerPrefs de update antiguo limpiados");
        }
    }
}
