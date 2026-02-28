using Gley.TrafficSystem;
using UnityEngine;

public class activarPeaton : MonoBehaviour
{
    public peatones peaton;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerComponent>())
        {
            peaton.puedoAvanzar= true;
            peaton.caminar.Play("Running");
            Debug.Log("si esta");
        }
    }


    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerComponent>())
        {
            peaton.salgoColision= true;
            peaton.puedoAvanzar = false;
            peaton.DesactivarRagdoll();
            peaton.ResetearPeaton();
            peaton.caminar.Play("Sad Idle");
        }
    }
}
