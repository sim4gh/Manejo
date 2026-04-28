using Gley.TrafficSystem;
using UnityEngine;

public class Semaforos : MonoBehaviour
{

    public bool _SemaforoVerdeActivo = false;
   

    public void OnTriggerStay(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerComponent>())
        {
            foreach(Transform semaforo in transform)
            {
                if (semaforo.gameObject.activeSelf)
                {
                    if(semaforo.gameObject.name== "GreenLightOn")
                    {
                        _SemaforoVerdeActivo = true;
                        Debug.Log("ADELANTE");
                    }
                    else
                    {
                        _SemaforoVerdeActivo=false;
                    }
                }
            }
            
        }
    }


    public void OnTriggerExit(Collider other)
    {
        if (other.gameObject.GetComponent<PlayerComponent>()&&_SemaforoVerdeActivo==false)
        {
            Debug.Log("[Semaforos] Infracción: cruce en rojo");
        }
    }
 
}
