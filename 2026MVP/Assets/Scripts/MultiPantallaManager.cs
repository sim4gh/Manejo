using UnityEngine;

public class MultiPantallaManager : MonoBehaviour
{
    public static MultiPantallaManager Instance { get; private set; }

    [Header("Cámaras (auto-find por nombre si quedan vacías)")]
    public Camera camaraCentro;
    public Camera camaraIzquierda;
    public Camera camaraDerecha;

    void Awake()
    {
        Instance = this;
        if (!camaraCentro)    camaraCentro    = FindCameraByName("Camara_Centro", "CamaraCentro");
        if (!camaraIzquierda) camaraIzquierda = FindCameraByName("Camara_Izquierda", "CamaraIzquierda");
        if (!camaraDerecha)   camaraDerecha   = FindCameraByName("Camara_Derecha", "CamaraDerecha");

        // Fallback rig (caso moto): si NINGUNA cámara nombrada se encontró,
        // buscamos un padre tipo "CockpitCamera" que sea solo un Transform con
        // varias Camera hijas (una por display) y las identificamos por yaw —
        // la frontal tiene yaw≈0, las laterales tienen yaw negativo (izq) o
        // positivo (der). Gating estricto (todas null, no any null) para evitar
        // sets híbridos si una escena rara tuviera ambas convenciones a la vez:
        // mejor mantener "named-set" o "rig-set" de forma coherente.
        if (camaraCentro == null && camaraIzquierda == null && camaraDerecha == null)
        {
            ResolveFromCockpitRig("CockpitCamera");
        }
    }

    void ResolveFromCockpitRig(string rigName)
    {
        GameObject rig = GameObject.Find(rigName);
        if (rig == null) return;
        Camera[] cams = rig.GetComponentsInChildren<Camera>(true);
        if (cams.Length == 0) return;

        // Pase 1: identificar el centro como la cámara con menor |yaw|.
        Camera center = null;
        float bestAbsCenter = float.MaxValue;
        foreach (var c in cams)
        {
            float abs = Mathf.Abs(NormalizeYaw(c.transform.localEulerAngles.y));
            if (abs < bestAbsCenter) { center = c; bestAbsCenter = abs; }
        }

        // Pase 2: entre las restantes, izq = yaw más negativo, der = yaw más positivo.
        Camera left = null, right = null;
        float bestLeftYaw = 0f, bestRightYaw = 0f;
        foreach (var c in cams)
        {
            if (c == center) continue;
            float y = NormalizeYaw(c.transform.localEulerAngles.y);
            if (y < bestLeftYaw)  { left  = c; bestLeftYaw  = y; }
            if (y > bestRightYaw) { right = c; bestRightYaw = y; }
        }

        if (camaraCentro    == null && center != null) { camaraCentro    = center; Debug.Log($"[MultiPantalla] Rig '{rigName}': centro = {center.name} (yaw≈{NormalizeYaw(center.transform.localEulerAngles.y):0.0}°)"); }
        if (camaraIzquierda == null && left   != null) { camaraIzquierda = left;   Debug.Log($"[MultiPantalla] Rig '{rigName}': izq = {left.name} (yaw≈{bestLeftYaw:0.0}°)"); }
        if (camaraDerecha   == null && right  != null) { camaraDerecha   = right;  Debug.Log($"[MultiPantalla] Rig '{rigName}': der = {right.name} (yaw≈{bestRightYaw:0.0}°)"); }
    }

    static float NormalizeYaw(float y) => y > 180f ? y - 360f : y;

    void Start()
    {
        for (int i = 1; i < Display.displays.Length; i++)
        {
            Display.displays[i].Activate();
        }
        Apply();
    }

    public void Apply()
    {
        int wanted = SimulatorConfig.Instance != null
            ? SimulatorConfig.Instance.data.displayCount
            : 3;

        if (wanted == 3 && Display.displays.Length < 3) wanted = 1;

        if (wanted == 1) AplicarModoUnaPantalla();
        else             AplicarModoTresPantallas();
    }

    void AplicarModoUnaPantalla()
    {
        if (camaraCentro)    { camaraCentro.targetDisplay = 0; camaraCentro.enabled = true; }
        if (camaraIzquierda) { camaraIzquierda.enabled = false; }
        if (camaraDerecha)   { camaraDerecha.enabled = false; }
        Debug.Log("[MultiPantalla] Modo 1 pantalla");
    }

    void AplicarModoTresPantallas()
    {
        var cfg = SimulatorConfig.Instance?.data;
        int dc = cfg?.displayCenter ?? 0;
        int dl = cfg?.displayLeft   ?? 1;
        int dr = cfg?.displayRight  ?? 2;

        int max = Display.displays.Length - 1;
        if (dc < 0 || dc > max || dl < 0 || dl > max || dr < 0 || dr > max
            || dc == dl || dc == dr || dl == dr)
        {
            Debug.LogWarning($"[MultiPantalla] Mapping inválido ({dc},{dl},{dr}), usando defaults 0,1,2");
            dc = 0; dl = 1; dr = 2;
        }

        if (camaraCentro)    { camaraCentro.targetDisplay = dc; camaraCentro.enabled = true; }
        if (camaraIzquierda) { camaraIzquierda.targetDisplay = dl; camaraIzquierda.enabled = true; }
        if (camaraDerecha)   { camaraDerecha.targetDisplay = dr; camaraDerecha.enabled = true; }
        Debug.Log($"[MultiPantalla] Modo 3 pantallas (centro={dc}, izq={dl}, der={dr})");
    }

    static Camera FindCameraByName(params string[] names)
    {
        foreach (var name in names)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                var cam = go.GetComponent<Camera>();
                if (cam != null) return cam;
            }
        }
        Debug.LogWarning($"[MultiPantalla] No se encontró cámara: {string.Join(", ", names)}");
        return null;
    }
}
