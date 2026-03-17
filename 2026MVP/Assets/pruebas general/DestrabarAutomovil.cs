using UnityEngine;
using System.Collections;
public class DestrabarAutomovil : MonoBehaviour
{
    [Header("Desplazamiento de rescate (relativo)")]
    public Vector3 posicionRescate = new Vector3(0f, 0f, 0f);

    [Header("Configuración")]
    public float tiempoEspera = 5f;

    private bool yaActivado = false;

    [Header("UI")]
    public GameObject mensajeUI; // 👈 Arrastra aquí tu GameObject del Canvas
    private void Start()
    {
        if (mensajeUI != null)
            mensajeUI.SetActive(false); // Se asegura que inicie apagado
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("automovil") && !yaActivado)
        {
            yaActivado = true;
            StartCoroutine(RedirigirAutomovil(other.gameObject));
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("automovil") && !yaActivado)
        {
            yaActivado = true;
            StartCoroutine(RedirigirAutomovil(collision.gameObject));
        }
    }

    private IEnumerator RedirigirAutomovil(GameObject automovil)
    {
        Debug.Log("Automóvil atascado detectado. Redirigiendo en " + tiempoEspera + " segundos...");

        if (mensajeUI != null)
            mensajeUI.SetActive(true);

        yield return new WaitForSeconds(tiempoEspera);

        if (automovil != null)
        {
            Rigidbody rb = automovil.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            automovil.transform.position += posicionRescate; // ✅ Desplazamiento relativo
            Debug.Log("Automóvil desplazado a: " + automovil.transform.position);
        }

        if (mensajeUI != null)
            mensajeUI.SetActive(false);

        yaActivado = false;
    }
}
