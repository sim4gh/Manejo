using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class ManagerMainMenu : MonoBehaviour
{
    public GameObject configuracion;
    public bool activar;

    public int NoPeatones;
    public int NoCarros;

    [Header("InputFields")]
    public TMP_InputField inputPeatones;
    public TMP_InputField inputCarros;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Cargar valores guardados en PlayerPrefs (si existen)
        NoPeatones = PlayerPrefs.GetInt("NoPeatones", 0);
        NoCarros = PlayerPrefs.GetInt("NoCarros", 0);

        // Mostrar los valores cargados en los InputFields
        inputPeatones.text = NoPeatones.ToString();
        inputCarros.text = NoCarros.ToString();

        // Suscribirse a los eventos de cambio
        inputPeatones.onValueChanged.AddListener(OnPeatonesChanged);
        inputCarros.onValueChanged.AddListener(OnCarrosChanged);
    }

    void OnPeatonesChanged(string value)
    {
        if (int.TryParse(value, out int resultado))
        {
            NoPeatones = resultado;
            PlayerPrefs.SetInt("NoPeatones", NoPeatones);
            PlayerPrefs.Save();
        }
    }

    void OnCarrosChanged(string value)
    {
        if (int.TryParse(value, out int resultado))
        {
            NoCarros = resultado;
            PlayerPrefs.SetInt("NoCarros", NoCarros);
            PlayerPrefs.Save();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.P))
        {
            if (activar == false)
            {
                activar = true;
            }
            else
            {
                activar = false;
            }

            configuracion.SetActive(activar);
        }
    }

    void OnDestroy()
    {
        // Buena práctica: desuscribirse al destruir el objeto
        inputPeatones.onValueChanged.RemoveListener(OnPeatonesChanged);
        inputCarros.onValueChanged.RemoveListener(OnCarrosChanged);
    }
}
