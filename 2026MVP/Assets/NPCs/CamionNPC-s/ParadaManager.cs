using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParadaManager : MonoBehaviour
{
    public List<Pasajero> pasajero;
    

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
        if (other.CompareTag("puerta"))
        {
            StartCoroutine(ColaPasaje());
        }
    }

    public IEnumerator ColaPasaje()
    {
        for (int i = 0; i < pasajero.Count; i++)
        {

            yield return new WaitForSeconds(5);
            pasajero[i].BuscoAsiento();

        }
        
    }
   
}
