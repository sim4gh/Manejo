using UnityEngine;

public class BusKiller : MonoBehaviour
{


    public void OnCollisionEnter(Collision collision)
    {
        
        if (collision.gameObject.CompareTag("automovil"))
        {
            gameObject.SetActive(false);
        }
    }
}
