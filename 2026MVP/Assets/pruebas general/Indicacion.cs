using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Gley.UrbanSystem;
public class Indicacion : MonoBehaviour
{
    public GameObject indicacion_gameobject;
    public TextMeshProUGUI indicacion_texto;
    public string indicacion;

    private void Awake()
    {
        indicacion_gameobject = GameObject.Find("Sistema_Indicaciones");

        GameObject texto = GameObject.Find("Text_Indicacion");

        indicacion_texto = texto.GetComponent<TextMeshProUGUI>();

    }

    public IEnumerator CorrutinaMostrarIndicacion()
    {
        indicacion_gameobject.SetActive(true);
        indicacion_texto.text = indicacion;
        yield return new WaitForSeconds(2);
        indicacion_gameobject.SetActive(false);
        indicacion_texto.text = "";
    }

    public void DarIndicacion()
    {
        StartCoroutine(CorrutinaMostrarIndicacion());
    }

    // ─── OnTriggerEnter ─────────────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("automovil"))
        {
            PlayerCar jugador = other.gameObject.GetComponent<PlayerCar>();

            if (jugador != null)
            {
                DarIndicacion();
            }
            
        }
    }

    // ─── OnTriggerExit ──────────────────────────────────────────────
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("automovil"))
        {
            
        }
    }

}
