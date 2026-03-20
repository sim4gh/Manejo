using TMPro;
using UnityEngine;

public class ZonaCarga : MonoBehaviour
{

    public bool camionCargando = false;
    public bool descargando = false;
    public string TipoZona;
    public float contadorPorCaja = 0;
    public GameObject alertas;
    public int cajas = 0;
    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("CajaCamion"))
        {
            if (TipoZona=="ZonaDescarga")
            {
                descargando=true;
            }
            else
            {
                camionCargando = true;

            }

        }
      
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("CajaCamion"))
        {
            camionCargando = false ;
            descargando = false;
            alertas.SetActive(false);

        }
    }

    public void Descarga()
    {
        contadorPorCaja += Time.deltaTime;
        if (contadorPorCaja >= 3)
        {

            cajas--;
            contadorPorCaja = 0;
            if (cajas <= 0)
            {
                alertas.GetComponent<TextMeshProUGUI>().text = "DESCARGA COMPLETA!";
                alertas.SetActive(true);

            }
        }
    }

    public void Cargando()
    {
        contadorPorCaja += Time.deltaTime;
        if (contadorPorCaja >= 3)
        {
           
            cajas--;
            contadorPorCaja = 0;
            if (cajas <= 0)
            {
                alertas.GetComponent<TextMeshProUGUI>().text = "CARGADO!";
                alertas.SetActive(true);
                
            }
        }
    }
    void Update()
    {

        if (camionCargando)
        {
            Cargando();
        }
        if (descargando)
        {
            Descarga();
        }

    }
}
