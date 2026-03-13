using UnityEngine;
using UnityEngine.SceneManagement;

public class BotonCambioEscena : MonoBehaviour
{
    public string nombreEscena = "UrbanExample";
public void CambioEscena()
    {
        SceneManager.LoadScene(nombreEscena);
    }
}
