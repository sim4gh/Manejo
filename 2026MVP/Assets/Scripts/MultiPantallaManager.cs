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
        if (!camaraCentro)    camaraCentro    = FindCameraByName("Camara_Centro", "CamaraCentro", "CockpitCamera");
        if (!camaraIzquierda) camaraIzquierda = FindCameraByName("Camara_Izquierda", "CamaraIzquierda");
        if (!camaraDerecha)   camaraDerecha   = FindCameraByName("Camara_Derecha", "CamaraDerecha");
    }

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
