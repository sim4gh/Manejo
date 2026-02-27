using System.Collections;
using System.Linq;
using UnityEngine;

public class peatones : MonoBehaviour
{
    [Header("Movimiento")]
    public Transform puntoDestino;
    public float velocidad = 5f;
    public Rigidbody rb;

    [Header("Animaci�n")]
    public Animator caminar;

    [Header("Referencias")]
    public GameObject peaton;
    public Collider principal;

    public bool pego = false;

    private Vector3 posicionInicial;
    private Quaternion rotacionInicial;

    private Collider[] colliders;
    private Rigidbody[] rigidbodies;

    private Vector3[] posicionesIniciales;
    private Quaternion[] rotacionesIniciales;

    void Start()
    {
        posicionInicial = transform.position;
        rotacionInicial = transform.rotation;

        ObtenerComponentesRagdoll();
        GuardarTransformacionesIniciales();
        DesactivarRagdoll();

        pego = false;
        caminar.Play("Running");
    }

    void FixedUpdate()
    {
        if (!pego)
        {
            Vector3 mov = Vector3.MoveTowards(
                rb.position,
                puntoDestino.position,
                velocidad * Time.fixedDeltaTime
            );

            rb.MovePosition(mov);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("automovil") ||
            collision.gameObject.CompareTag("Player"))
        {
            if (!pego)
            {
                pego = true;
                ActivarRagdoll();
                StartCoroutine(RutinaColision());
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("punto"))
        {
            StartCoroutine(RutinaPunto());
        }
    }

    IEnumerator RutinaColision()
    {
        caminar.enabled = false;

        yield return new WaitForSeconds(2f);

        peaton.SetActive(false);

        yield return new WaitForSeconds(3f);

        ResetearPeaton();
    }

    IEnumerator RutinaPunto()
    {
        pego = true;

        caminar.enabled = false;
        peaton.SetActive(false);

        yield return new WaitForSeconds(2f);

        ResetearPeaton();
    }

    public void ResetearPeaton()
    {
        transform.position = posicionInicial;
        transform.rotation = rotacionInicial;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        DesactivarRagdoll();

        peaton.SetActive(true);
        caminar.enabled = true;
        caminar.Play("Running");

        pego = false;
    }

    void ActivarRagdoll()
    {
        caminar.enabled = false;

        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = true;

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = false;
        }

        principal.enabled = false;
        rb.isKinematic = true;
    }

    void DesactivarRagdoll()
    {
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;

            rigidbodies[i].transform.localPosition = posicionesIniciales[i];
            rigidbodies[i].transform.localRotation = rotacionesIniciales[i];

            rigidbodies[i].linearVelocity = Vector3.zero;
            rigidbodies[i].angularVelocity = Vector3.zero;
        }

        principal.enabled = true;
        rb.isKinematic = false;
    }

    void ObtenerComponentesRagdoll()
    {
        colliders = GetComponentsInChildren<Collider>()
            .Where(c => c != principal)
            .ToArray();

        rigidbodies = GetComponentsInChildren<Rigidbody>()
            .Where(r => r != rb)
            .ToArray();
    }

    void GuardarTransformacionesIniciales()
    {
        posicionesIniciales = new Vector3[rigidbodies.Length];
        rotacionesIniciales = new Quaternion[rigidbodies.Length];

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            posicionesIniciales[i] = rigidbodies[i].transform.localPosition;
            rotacionesIniciales[i] = rigidbodies[i].transform.localRotation;
        }
    }
}