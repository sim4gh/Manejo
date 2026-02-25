using UnityEngine;
using UnityEngine.SceneManagement;

public class BotonCambioEscena : MonoBehaviour
{
public void CambioEscena()
    {
        SceneManager.LoadScene("UrbanExample");
    }
}
