using Gley.UrbanSystem;
using UnityEngine;

public class RagDollPasajeroManager : MonoBehaviour
{
    public Rigidbody[] rbs;
    public Collider[] colliders;
    public Animator animator;

    private void Start()
    {
        animator = GetComponent<Animator>();
        rbs = GetComponentsInChildren<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        ActivoRagdoll(false);
    }

    public void ActivoRagdoll(bool activo)
    {
        animator.enabled = !activo;

     
        foreach (Rigidbody rb in rbs)
        {
            rb.isKinematic = !activo;
        }

        foreach (Collider collider in colliders)
        {
          
            if (collider.isTrigger)
            {
                collider.enabled = !activo;
            }
            else
            {
                collider.enabled = activo;

            }
        }
    }

    public void MePegas()
    {
        ActivoRagdoll(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerCar>())
        {
            MePegas();
        }
    }
}
