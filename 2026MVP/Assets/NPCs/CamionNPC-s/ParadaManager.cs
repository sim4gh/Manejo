using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParadaManager : MonoBehaviour
{
    public List<Pasajero> pasajero;
    public int peatones = 5;


    public void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("puerta"))
        {
            StartCoroutine(ColaPasaje());
        }
    }

    public IEnumerator ColaPasaje()
    {
        for (int i = 0; i < pasajero.Count; i++)
        {

            yield return new WaitForSeconds(3);
            pasajero[i].BuscoAsiento();

        }
        
    }
   
}
