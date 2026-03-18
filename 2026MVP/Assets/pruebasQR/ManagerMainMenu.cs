using UnityEngine;
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

    void Start()
    {
        NoPeatones = PlayerPrefs.GetInt("NoPeatones", 0);
        NoCarros = PlayerPrefs.GetInt("NoCarros", 0);

        inputPeatones.text = NoPeatones.ToString();
        inputCarros.text = NoCarros.ToString();

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

    public void ActivarCargolluvia()
    {
        PlayerPrefs.SetInt("Cargolluvia", 1);
        PlayerPrefs.Save();
    }

    public void DesactivarCargolluvia()
    {
        PlayerPrefs.SetInt("Cargolluvia", 0);
        PlayerPrefs.Save();
    }

    void Update()
    {
        //if (Input.GetKey(KeyCode.P))
        //{
        //    if (activar == false)
        //    {
        //        activar = true;
        //    }
        //    else
        //    {
        //        activar = false;
        //    }

        //    configuracion.SetActive(activar);
        //}
    }

    void OnDestroy()
    {
        inputPeatones.onValueChanged.RemoveListener(OnPeatonesChanged);
        inputCarros.onValueChanged.RemoveListener(OnCarrosChanged);
    }
}
