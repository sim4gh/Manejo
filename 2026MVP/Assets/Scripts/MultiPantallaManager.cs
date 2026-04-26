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
        if (!camaraCentro)    camaraCentro    = FindCameraByName("Camara_Centro");
        if (!camaraIzquierda) camaraIzquierda = FindCameraByName("Camara_Izquierda");
        if (!camaraDerecha)   camaraDerecha   = FindCameraByName("Camara_Derecha");
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
        if (camaraCentro)    { camaraCentro.targetDisplay = 0; camaraCentro.enabled = true; }
        if (camaraIzquierda) { camaraIzquierda.targetDisplay = 1; camaraIzquierda.enabled = true; }
        if (camaraDerecha)   { camaraDerecha.targetDisplay = 2; camaraDerecha.enabled = true; }
        Debug.Log("[MultiPantalla] Modo 3 pantallas");
    }

    static Camera FindCameraByName(string name)
    {
        var go = GameObject.Find(name);
        return go ? go.GetComponent<Camera>() : null;
    }
}
