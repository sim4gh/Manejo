using UnityEngine;
using System.Collections;
public class DesactivarBici : MonoBehaviour
{
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("automovil") || collision.gameObject.CompareTag("Player"))
        {

            StartCoroutine(desactivar());
        
        
        }
         
    }

    IEnumerator desactivar() 
    {
        yield return new WaitForSeconds(2f);
        gameObject.SetActive(false);
    }

}
