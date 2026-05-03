using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

public class AutoUpdater : MonoBehaviour
{
    public static AutoUpdater Instance { get; private set; }

    // Tope local de intentos por versión. Mismo que el límite del backend
    // (simulator-update-status.ts ABANDON_THRESHOLD = 3). Cualquiera de los
    // dos detiene primero — convergente. Cubre el caso de red caída donde
    // los reports de FAILED nunca llegan al backend.
    const int MAX_LOCAL_ATTEMPTS = 3;

    // PlayerPrefs keys
    const string ATTEMPT_KEY_PREFIX = "Update_LocalAttempts_";   // + version
    const string LAST_ATTEMPTED_VERSION_KEY = "Update_LastAttemptedVersion";
    const string ATTEMPT_VERSIONS_INDEX_KEY = "Update_AttemptVersionsIndex"; // CSV de versiones con counter

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
    private bool _localAttemptBumpedThisSession;

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

        // Si la versión que está corriendo coincide con la última que intentamos
        // instalar, el OTA tuvo éxito — limpiar todos los counters locales y el
        // índice. Esto cierra el ciclo: counters solo persisten si el install
        // realmente nunca se completó.
        string lastAttempted = PlayerPrefs.GetString(LAST_ATTEMPTED_VERSION_KEY, "");
        if (!string.IsNullOrEmpty(lastAttempted) && lastAttempted == Application.version)
        {
            Debug.Log($"[AutoUpdater] Install OK detectado (Application.version={Application.version} = lastAttempted). Limpiando contadores.");
            ClearAttemptCounters();
        }
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

        // Crash recovery: si la última versión intentada coincide con la pendiente
        // y este boot es la primera vez que entramos a ProcessPendingUpdate para esta
        // versión, asumir que un intento previo crasheó (Unity murió antes de reportar
        // FAILED) y bumpear el contador local. Cubre el case "kiosko crashea durante
        // descarga → backend nunca recibe FAILED → contador remoto no sube".
        string lastAttemptedVer = PlayerPrefs.GetString(LAST_ATTEMPTED_VERSION_KEY, "");
        string attemptKey = ATTEMPT_KEY_PREFIX + data.version;

        if (!_localAttemptBumpedThisSession
            && lastAttemptedVer == data.version
            && data.status != "DOWNLOADED" && data.status != "INSTALLED")
        {
            int prev = PlayerPrefs.GetInt(attemptKey, 0);
            PlayerPrefs.SetInt(attemptKey, prev + 1);
            RememberAttemptVersion(data.version);
            _localAttemptBumpedThisSession = true;
            Debug.Log($"[AutoUpdater] Crash recovery: bumpeando contador local de v{data.version} a {prev + 1}");
        }

        int localAttempts = PlayerPrefs.GetInt(attemptKey, 0);
        if (localAttempts >= MAX_LOCAL_ATTEMPTS)
        {
            Debug.LogWarning($"[AutoUpdater] v{data.version} abandonada localmente tras {localAttempts} intentos. " +
                "Sube versión nueva para reintentar.");
            return;
        }

        if (data.status == "PENDING" || data.status == "FAILED"
            || data.status == "DOWNLOADING" || data.status == "DOWNLOADED"
            || data.status == "INSTALLING")
        {
            if (IsScheduledTimeReached(data.scheduledAfter))
            {
                Debug.Log($"[AutoUpdater] Update v{data.version} programado, iniciando descarga (intento {localAttempts + 1}/{MAX_LOCAL_ATTEMPTS})...");
                PlayerPrefs.SetString(LAST_ATTEMPTED_VERSION_KEY, data.version);
                RememberAttemptVersion(data.version);
                PlayerPrefs.Save();
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

    /// <summary>Registra una versión en el índice CSV para poder limpiar todas las keys de intentos al instalar exitosamente.</summary>
    void RememberAttemptVersion(string version)
    {
        string idx = PlayerPrefs.GetString(ATTEMPT_VERSIONS_INDEX_KEY, "");
        if (string.IsNullOrEmpty(idx))
        {
            PlayerPrefs.SetString(ATTEMPT_VERSIONS_INDEX_KEY, version);
            return;
        }
        foreach (var v in idx.Split(','))
        {
            if (v == version) return;
        }
        PlayerPrefs.SetString(ATTEMPT_VERSIONS_INDEX_KEY, idx + "," + version);
    }

    /// <summary>Limpia todas las keys de attempt counters + last attempted version. Llamar tras install exitoso.</summary>
    void ClearAttemptCounters()
    {
        string idx = PlayerPrefs.GetString(ATTEMPT_VERSIONS_INDEX_KEY, "");
        if (!string.IsNullOrEmpty(idx))
        {
            foreach (var v in idx.Split(','))
            {
                if (!string.IsNullOrEmpty(v))
                    PlayerPrefs.DeleteKey(ATTEMPT_KEY_PREFIX + v);
            }
        }
        PlayerPrefs.DeleteKey(ATTEMPT_VERSIONS_INDEX_KEY);
        PlayerPrefs.DeleteKey(LAST_ATTEMPTED_VERSION_KEY);
        PlayerPrefs.Save();
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

        // Incrementar contador local. Con crash recovery, ya pudo haberse incrementado
        // al inicio de la sesión — _localAttemptBumpedThisSession lo evita.
        if (!_localAttemptBumpedThisSession && !string.IsNullOrEmpty(pendingData?.version))
        {
            string attemptKey = ATTEMPT_KEY_PREFIX + pendingData.version;
            int prev = PlayerPrefs.GetInt(attemptKey, 0);
            PlayerPrefs.SetInt(attemptKey, prev + 1);
            RememberAttemptVersion(pendingData.version);
            PlayerPrefs.Save();
            _localAttemptBumpedThisSession = true;
            Debug.Log($"[AutoUpdater] Contador local v{pendingData.version}: {prev + 1}/{MAX_LOCAL_ATTEMPTS}");
        }

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
        // SHA256 corre en thread pool (Task.Run) — sin bloquear main thread.
        // Para un ZIP de ~600MB esto evita un freeze de 5-15s al 99% CPU.
        bool existingZipValid = false;
        if (File.Exists(downloadedZipPath))
        {
            var hashTask = Task.Run(() => ComputeFileSHA256(downloadedZipPath));
            while (!hashTask.IsCompleted) yield return null;

            if (hashTask.IsFaulted)
            {
                var hashErr = hashTask.Exception?.InnerException ?? hashTask.Exception;
                Debug.LogWarning($"[AutoUpdater] Error verificando ZIP existente: {hashErr?.Message}");
                try { File.Delete(downloadedZipPath); } catch { }
            }
            else if (hashTask.Result == urlResponse.sha256)
            {
                Debug.Log("[AutoUpdater] ZIP ya descargado y verificado previamente");
                UpdateDownloaded = true;
                DownloadProgress = 1f;
                existingZipValid = true;
            }
            else
            {
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

        // SHA256 en thread pool para no bloquear main thread (~5-15s sobre ZIP de 600MB).
        var hashTask = Task.Run(() => ComputeFileSHA256(downloadedZipPath));
        while (!hashTask.IsCompleted) yield return null;

        if (hashTask.IsFaulted)
        {
            var hashErr = hashTask.Exception?.InnerException ?? hashTask.Exception;
            FailUpdate($"Error calculando SHA256: {hashErr?.Message}");
            yield break;
        }

        string actualHash = hashTask.Result;

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
if %WAIT% GEQ 10 (
    echo WARN: Unity no cerro tras 10s, forzando taskkill...
    echo %DATE% %TIME% WARN: Unity did not exit after 10s, forcing taskkill /F >> ""{batInstallLog}""
    taskkill /F /IM ""{exeName}"" >NUL 2>&1
    timeout /t 3 /nobreak >nul
    goto continue_install
)
timeout /t 2 /nobreak >nul
set /a WAIT+=2
goto waitloop
:continue_install
echo %DATE% %TIME% Unity closed (or force-killed), proceeding >> ""{batInstallLog}""

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

        // Unblock-File on the bat itself — WDAC may flag it as web-downloaded.
        // Worker thread para no bloquear main thread (PowerShell cold start ~1s).
        // Profile de Jason mostraba 1031ms self-time aquí — freeze visible en kiosko.
        string psBatPath = batPath.Replace("'", "''");
        var unblockTask = Task.Run(() =>
        {
            try
            {
                var unblock = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Unblock-File -Path '{psBatPath}'\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                System.Diagnostics.Process.Start(unblock)?.WaitForExit(5000);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AutoUpdater] Unblock-File falló (no fatal): {e.Message}");
            }
        });
        while (!unblockTask.IsCompleted) yield return null;

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
