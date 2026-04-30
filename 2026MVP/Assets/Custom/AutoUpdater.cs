using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System.Security.Cryptography;

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
        CleanupOldUpdates();
    }

    public void ProcessPendingUpdate(SimulatorApiClient.PendingUpdateData data)
    {
        if (data == null) return;
        if (data.status == "INSTALLED") return;
        if (isDownloading || isProcessing) return;

        pendingData = data;
        UpdateAvailable = true;
        LatestVersion = data.version;
        ReleaseNotes = data.releaseNotes;
        CurrentStatus = data.status;

        if (data.status == "PENDING" || data.status == "FAILED"
            || data.status == "DOWNLOADING" || data.status == "DOWNLOADED"
            || data.status == "INSTALLING")
        {
            if (IsScheduledTimeReached(data.scheduledAfter))
            {
                Debug.Log($"[AutoUpdater] Update v{data.version} programado, iniciando descarga...");
                isDownloading = true;
                isProcessing = true;
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

        return true;
    }

    void FailUpdate(string reason)
    {
        Debug.LogWarning($"[AutoUpdater] Update falló: {reason}");
        CurrentStatus = "FAILED";
        isDownloading = false;
        isProcessing = false;
        StartCoroutine(SimulatorApiClient.ReportUpdateStatus("FAILED", reason, pendingData?.version, null));
    }

    IEnumerator RequestAndDownload()
    {
        if (pendingData == null)
        {
            FailUpdate("No pending update data");
            yield break;
        }

        // 1. Solicitar URL de descarga (con 1 retry)
        Debug.Log($"[AutoUpdater] Solicitando URL de descarga para v{pendingData.version}...");
        SimulatorApiClient.UpdateUrlResponse urlResponse = null;

        yield return SimulatorApiClient.RequestUpdateUrl(pendingData.version, (resp) => urlResponse = resp);

        if (urlResponse == null || string.IsNullOrEmpty(urlResponse.downloadUrl))
        {
            Debug.Log("[AutoUpdater] Primer intento fallido, reintentando en 3s...");
            yield return new WaitForSeconds(3f);
            yield return SimulatorApiClient.RequestUpdateUrl(pendingData.version, (resp) => urlResponse = resp);
        }

        if (urlResponse == null || string.IsNullOrEmpty(urlResponse.downloadUrl))
        {
            FailUpdate("No se pudo obtener URL de descarga tras 2 intentos");
            yield break;
        }

        if (string.IsNullOrEmpty(urlResponse.sha256))
        {
            FailUpdate("Servidor no proporcionó SHA256, abortando por seguridad");
            yield break;
        }

        // 2. Reportar DOWNLOADING
        yield return SimulatorApiClient.ReportUpdateStatus("DOWNLOADING", null, pendingData.version, null);
        CurrentStatus = "DOWNLOADING";

        // 3. Preparar directorio de descarga
        string tempDir = Path.Combine(Application.persistentDataPath, "updates");
        bool tempDirFailed = false;
        try
        {
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
        }
        catch (System.Exception e)
        {
            FailUpdate($"No se pudo crear directorio updates: {e.Message}");
            tempDirFailed = true;
        }
        if (tempDirFailed) yield break;

        downloadedZipPath = Path.Combine(tempDir, $"{Application.productName}-v{pendingData.version}.zip");

        // Si ya existe el archivo (descarga previa), verificar hash
        bool existingZipValid = false;
        if (File.Exists(downloadedZipPath))
        {
            try
            {
                string existingHash = ComputeFileSHA256(downloadedZipPath);
                if (existingHash == urlResponse.sha256)
                {
                    Debug.Log("[AutoUpdater] ZIP ya descargado y verificado previamente");
                    UpdateDownloaded = true;
                    DownloadProgress = 1f;
                    existingZipValid = true;
                }
                else
                {
                    File.Delete(downloadedZipPath);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AutoUpdater] Error verificando ZIP existente: {e.Message}");
                try { File.Delete(downloadedZipPath); } catch { }
            }
        }

        if (existingZipValid)
        {
            yield return OnDownloadComplete(urlResponse.sha256);
            yield break;
        }

        Debug.Log($"[AutoUpdater] Descargando: {urlResponse.downloadUrl.Substring(0, System.Math.Min(100, urlResponse.downloadUrl.Length))}...");

        using (var request = UnityWebRequest.Get(urlResponse.downloadUrl))
        {
            request.timeout = 1200;
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
                try
                {
                    long fileSize = new FileInfo(downloadedZipPath).Length;
                    Debug.Log($"[AutoUpdater] Descarga completada: {downloadedZipPath} ({fileSize / 1048576}MB)");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[AutoUpdater] No se pudo leer tamaño del ZIP: {e.Message}");
                }

                yield return OnDownloadComplete(urlResponse.sha256);
            }
            else
            {
                FailUpdate($"Download error: {request.error}");
            }
        }
    }

    IEnumerator OnDownloadComplete(string expectedSha256)
    {
        Debug.Log("[AutoUpdater] Verificando SHA256...");

        string actualHash = null;
        try
        {
            actualHash = ComputeFileSHA256(downloadedZipPath);
        }
        catch (System.Exception e)
        {
            FailUpdate($"Error calculando SHA256: {e.Message}");
        }
        if (actualHash == null) yield break;

        if (actualHash != expectedSha256)
        {
            Debug.LogWarning($"[AutoUpdater] SHA256 no coincide! Esperado: {expectedSha256}, Actual: {actualHash}");
            try { File.Delete(downloadedZipPath); } catch { }
            FailUpdate("SHA256 mismatch");
            yield break;
        }

        Debug.Log("[AutoUpdater] SHA256 verificado correctamente");

        UpdateDownloaded = true;
        yield return SimulatorApiClient.ReportUpdateStatus("DOWNLOADED", null, pendingData?.version, null);
        CurrentStatus = "DOWNLOADED";
        isDownloading = false;

        Debug.Log("[AutoUpdater] Procediendo a instalar...");
        yield return InstallUpdateCoroutine();
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

    static string NormalizeBatPath(string p)
    {
        return Path.GetFullPath(p).Replace('/', '\\');
    }

    public void InstallUpdate()
    {
        StartCoroutine(InstallUpdateCoroutine());
    }

    IEnumerator InstallUpdateCoroutine()
    {
        if (string.IsNullOrEmpty(downloadedZipPath) || !File.Exists(downloadedZipPath))
        {
            FailUpdate("ZIP not found at install time");
            yield break;
        }

        // Wait for INSTALLING report to reach the backend before quitting
        yield return SimulatorApiClient.ReportUpdateStatus("INSTALLING", null, pendingData?.version, null);
        CurrentStatus = "INSTALLING";
        yield return new WaitForSeconds(0.5f);

        string appDir = NormalizeBatPath(Path.GetDirectoryName(Application.dataPath));
        string exeName = Path.GetFileName(System.Environment.GetCommandLineArgs()[0]);
        string exePath = NormalizeBatPath(Path.Combine(appDir, exeName));
        string stagingDir = NormalizeBatPath(Path.Combine(Application.persistentDataPath, "updates", "staging"));
        string batPath = NormalizeBatPath(Path.Combine(Application.persistentDataPath, "update.bat"));
        string installLog = NormalizeBatPath(Path.Combine(appDir, "install.log"));
        string zipPath = NormalizeBatPath(downloadedZipPath);

        // Escape % as %% for bat context, ' as '' for PowerShell single-quote context
        string batInstallLog = installLog.Replace("%", "%%");
        string batStagingDir = stagingDir.Replace("%", "%%");
        string batAppDir = appDir.Replace("%", "%%");
        string batExePath = exePath.Replace("%", "%%");
        string batZipPath = zipPath.Replace("%", "%%");

        string psZipPath = zipPath.Replace("'", "''");
        string psStagingDir = stagingDir.Replace("'", "''");
        string psAppDir = appDir.Replace("'", "''");

        string psFlags = "-NoProfile -ExecutionPolicy Bypass -Command";
        string batContent = $@"@echo off
echo Instalando actualizacion del simulador v{LatestVersion}...
echo. >> ""{batInstallLog}""
echo === Install v{LatestVersion} === %DATE% %TIME% >> ""{batInstallLog}""

echo Esperando a que Unity cierre...
set WAIT=0
:waitloop
tasklist /FI ""IMAGENAME eq {exeName}"" 2>NUL | find /I ""{exeName}"" >NUL
if errorlevel 1 goto continue_install
if %WAIT% GEQ 30 (
    echo WARN: Unity no cerro tras 30s, continuando...
    echo %DATE% %TIME% WARN: Unity did not exit after 30s >> ""{batInstallLog}""
    goto continue_install
)
timeout /t 2 /nobreak >nul
set /a WAIT+=2
goto waitloop
:continue_install
echo %DATE% %TIME% Unity closed, proceeding >> ""{batInstallLog}""

echo Extrayendo archivos y limpiando Mark-of-the-Web...
powershell {psFlags} ""Expand-Archive -Path '{psZipPath}' -DestinationPath '{psStagingDir}' -Force; Get-ChildItem -Path '{psStagingDir}' -Recurse -File | Unblock-File""
if errorlevel 1 (
    echo ERROR: Fallo la extraccion del ZIP
    echo %DATE% %TIME% Expand-Archive FAILED >> ""{batInstallLog}""
    goto fail
)
echo %DATE% %TIME% Expand-Archive OK >> ""{batInstallLog}""

echo Copiando archivos nuevos...
xcopy /s /e /y ""{batStagingDir}\*"" ""{batAppDir}\""
if errorlevel 1 (
    echo ERROR: xcopy fallo
    echo %DATE% %TIME% xcopy FAILED >> ""{batInstallLog}""
    goto fail
)
echo %DATE% %TIME% xcopy OK >> ""{batInstallLog}""

echo Limpiando Mark-of-the-Web del directorio destino...
powershell {psFlags} ""Get-ChildItem -Path '{psAppDir}' -Recurse -File | Unblock-File""
echo %DATE% %TIME% Unblock-File appDir done >> ""{batInstallLog}""

echo Limpiando archivos temporales...
rmdir /s /q ""{batStagingDir}""
del ""{batZipPath}""
echo %DATE% %TIME% temp cleanup done >> ""{batInstallLog}""

echo Reiniciando simulador...
echo %DATE% %TIME% launching {exeName} >> ""{batInstallLog}""
start """" ""{batExePath}""

echo Borrando este script...
del ""%~f0""
exit

:fail
echo %DATE% %TIME% INSTALL FAILED, relaunching original exe >> ""{batInstallLog}""
start """" ""{batExePath}""
del ""%~f0""
exit
";

        try
        {
            File.WriteAllText(batPath, batContent);
        }
        catch (System.Exception e)
        {
            FailUpdate($"No se pudo escribir update.bat: {e.Message}");
            yield break;
        }

        // Unblock-File on the bat itself — WDAC may flag it as web-downloaded
        try
        {
            var unblock = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Unblock-File -Path '{batPath.Replace("'", "''")}'\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            System.Diagnostics.Process.Start(unblock)?.WaitForExit(5000);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AutoUpdater] Unblock-File falló (no fatal): {e.Message}");
        }

        Debug.Log($"[AutoUpdater] Batch escrito en: {batPath}");

        PlayerPrefs.DeleteKey("PendingUpdateUrl");
        PlayerPrefs.DeleteKey("PendingUpdateVersion");
        PlayerPrefs.DeleteKey("PendingUpdateZip");
        PlayerPrefs.Save();

        var processInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{batPath}\"",
            UseShellExecute = false,
            CreateNoWindow = false,
        };

        try
        {
            System.Diagnostics.Process.Start(processInfo);
        }
        catch (System.Exception e)
        {
            FailUpdate($"Bat exec failed: {e.Message}");
            yield break;
        }
        Application.Quit();
    }

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
