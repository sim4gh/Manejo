using System.Collections.Generic;
using UnityEngine;
public class BusAsientosManager : MonoBehaviour
{
    private void Update()
    {
        ChecoAsiento();
    }
    public void ChecoAsiento()
    {
        foreach (Transform asientosDisponibles in transform)
        {
            Asiento asiento = asientosDisponibles.GetComponent<Asiento>();
            if (asiento.lugaresDisponibles <= 0)
            {
                asiento.gameObject.SetActive(false);
            }
            else if (!asiento.gameObject.activeSelf)
            {
                asiento.gameObject.SetActive(true);
            }
        }
    }
}