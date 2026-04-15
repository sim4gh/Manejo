using Gley.UrbanSystem;
using System.Collections;

using UnityEngine;

public class tren : MonoBehaviour
{
    public float velocidad = 10f;
    private float angulofinal=0;
    private float anguloActual = -90f;
    public Transform pluma;
    public bool abajo = false;
    public Transform pluma2;
    void OnTriggerStay(Collider other)
    {
        if (other.GetComponent<PlayerCar>())
        {
            StartCoroutine(MovimientoPluma());
        }
    }
    IEnumerator MovimientoPluma() 
    {
        if (abajo==false)
        {
            anguloActual = Mathf.MoveTowards(anguloActual, angulofinal, velocidad * Time.deltaTime);
            pluma.localEulerAngles = new Vector3(0f, 0f, anguloActual);
            pluma2.localEulerAngles = new Vector3(0f, 0f, anguloActual);
            if (anguloActual==0) 
            {
                abajo = true;
            } 
        }

        yield return new WaitForSeconds(7f);

        if (abajo==true)
        {
            anguloActual = Mathf.MoveTowards(anguloActual, -90, velocidad * Time.deltaTime);
            pluma.localEulerAngles = new Vector3(0f, 0f, anguloActual);
            pluma2.localEulerAngles = new Vector3(0f, 0f, anguloActual);
        }

    }

    


}
