using UnityEngine;
#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif

/// <summary>
/// P/Invoke wrapper para Logitech Steering Wheel SDK (LogitechSteeringWheelEnginesWrapper.dll).
/// Solo activo en Windows x64. En Mac/Linux y en Windows sin DLL todos los métodos son no-op.
/// El SDK soporta G923, G29, G27, G25, Driving Force GT, etc. — no aplica a HORI.
/// </summary>
public static class LogitechFFB
{
    private const int WHEEL_INDEX = 0;
    private static bool initialized;
    private static bool sdkAvailable;

#if UNITY_STANDALONE_WIN
    private const string DLL = "LogitechSteeringWheelEnginesWrapper";

    [DllImport(DLL)] private static extern bool LogiSteeringInitialize(bool ignoreXInputControllers);
    [DllImport(DLL)] private static extern bool LogiUpdate();
    [DllImport(DLL)] private static extern bool LogiIsConnected(int index);
    [DllImport(DLL)] private static extern bool LogiPlayConstantForce(int index, int magnitudePercentage);
    [DllImport(DLL)] private static extern bool LogiStopConstantForce(int index);
    [DllImport(DLL)] private static extern bool LogiPlayBumpyRoadEffect(int index, int magnitudePercentage);
    [DllImport(DLL)] private static extern bool LogiStopBumpyRoadEffect(int index);
    [DllImport(DLL)] private static extern bool LogiSteeringShutdown();
#endif

    /// <summary>Llamar una vez al boot. Idempotente. Retorna false si SDK no disponible.</summary>
    public static bool TryInitialize()
    {
#if UNITY_STANDALONE_WIN
        if (initialized) return sdkAvailable;
        initialized = true;

        // La DLL es PE32+ Windows x64 — solo cargable en Windows player o editor.
        // En el editor de macOS/Linux, P/Invoke fallaría con DllNotFoundException aunque
        // el archivo exista, porque el SO no puede ejecutar binarios Windows. Skipeamos
        // silenciosamente (un Debug.Log informativo, no un Warning).
        if (Application.platform != RuntimePlatform.WindowsEditor &&
            Application.platform != RuntimePlatform.WindowsPlayer)
        {
            sdkAvailable = false;
            Debug.Log("[LogitechFFB] Plataforma no-Windows: FFB inactivo (esperado en editor macOS/Linux).");
            return false;
        }

        try
        {
            sdkAvailable = LogiSteeringInitialize(false);
            if (!sdkAvailable)
                Debug.Log("[LogitechFFB] LogiSteeringInitialize devolvió false (no wheel detectado o ya inicializado).");
            else
                Debug.Log("[LogitechFFB] SDK inicializado.");
            return sdkAvailable;
        }
        catch (System.DllNotFoundException)
        {
            sdkAvailable = false;
            Debug.LogWarning("[LogitechFFB] DLL no presente en Plugins/x86_64. FFB deshabilitado.");
            return false;
        }
        catch (System.Exception e)
        {
            sdkAvailable = false;
            Debug.LogWarning($"[LogitechFFB] Init failed: {e.Message}");
            return false;
        }
#else
        initialized = true;
        sdkAvailable = false;
        return false;
#endif
    }

    /// <summary>Llamar cada frame (en LateUpdate). El SDK requiere update para mantener efectos vivos.</summary>
    public static void Update()
    {
#if UNITY_STANDALONE_WIN
        if (!sdkAvailable) return;
        try { LogiUpdate(); }
        catch { sdkAvailable = false; }
#endif
    }

    public static bool IsConnected()
    {
#if UNITY_STANDALONE_WIN
        if (!sdkAvailable) return false;
        try { return LogiIsConnected(WHEEL_INDEX); }
        catch { return false; }
#else
        return false;
#endif
    }

    /// <summary>
    /// Reproduce constant force. magnitudePctSigned: -100..+100. Signo determina dirección
    /// (positivo = derecha, negativo = izquierda en convención Logitech).
    /// </summary>
    public static void PlayConstantForce(int magnitudePctSigned)
    {
#if UNITY_STANDALONE_WIN
        if (!sdkAvailable) return;
        try { LogiPlayConstantForce(WHEEL_INDEX, Mathf.Clamp(magnitudePctSigned, -100, 100)); }
        catch { }
#endif
    }

    public static void StopConstantForce()
    {
#if UNITY_STANDALONE_WIN
        if (!sdkAvailable) return;
        try { LogiStopConstantForce(WHEEL_INDEX); }
        catch { }
#endif
    }

    /// <summary>Rumble corto. magnitudePct: 0..100.</summary>
    public static void PlayBumpyRoad(int magnitudePct)
    {
#if UNITY_STANDALONE_WIN
        if (!sdkAvailable) return;
        try { LogiPlayBumpyRoadEffect(WHEEL_INDEX, Mathf.Clamp(magnitudePct, 0, 100)); }
        catch { }
#endif
    }

    public static void StopBumpyRoad()
    {
#if UNITY_STANDALONE_WIN
        if (!sdkAvailable) return;
        try { LogiStopBumpyRoadEffect(WHEEL_INDEX); }
        catch { }
#endif
    }

    public static void Shutdown()
    {
#if UNITY_STANDALONE_WIN
        if (!sdkAvailable) return;
        try { LogiSteeringShutdown(); }
        catch { }
        sdkAvailable = false;
        initialized = false;
#endif
    }
}
