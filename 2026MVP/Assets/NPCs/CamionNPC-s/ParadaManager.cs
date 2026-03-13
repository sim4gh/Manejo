using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class ParadaManager : MonoBehaviour
{
    public List<Pasajero> pasajero;
    private bool _colaActiva = false;
    private bool _bajadaActiva = false;
    public string NumParada;
    public SimpleSpeedGauge movimientoCarro;
    public bool _Estacionado = false;
    public float tiempo = 0;
    public bool  aumenta = false;

    public void Start()
    {
        foreach (Transform pasajeros in transform)
        {
            Pasajero pasaje = pasajeros.GetComponent<Pasajero>();
            pasajero.Add(pasaje);
        }
    }
    public void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("puerta")) return;

        bool carroDetenido = movimientoCarro.velocidadActual == "0";

        if (!carroDetenido)
        {
            aumenta = false;
            tiempo = 0;
            _Estacionado = false;
            return;
        }

        aumenta = true;
        tiempo += Time.deltaTime;

        if (tiempo >= 5f)
        {
            _Estacionado = true;
        }

        if (!_Estacionado) return;

        if (!_colaActiva)
        {
            StartCoroutine(ColaPasaje());
        }

        if (!_bajadaActiva)
        {
            StartCoroutine(ColaBajada());
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("puerta"))
        {
            _colaActiva = false;
            _bajadaActiva = false;
        }
    }
    public IEnumerator ColaPasaje()
    {
        _colaActiva = true;
        for (int i = 0; i < pasajero.Count; i++)
        {
           
            yield return new WaitForSeconds(5);
            pasajero[i]._Activo = true;
        }
        _colaActiva = false;
    }
    public IEnumerator ColaBajada()
    {
        _bajadaActiva = true;
        List<Pasajero> abordo = CamionManager.instancia.pasajerosABordo;
        for (int i = abordo.Count - 1; i >= 0; i--)
        {
            if (abordo[i]._Sentado && abordo[i].Mi_Parada == NumParada)
            {
                yield return new WaitForSeconds(3);
                abordo[i]._BajarActivado = true;
            }
        }
    }
}