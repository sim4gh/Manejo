using UnityEngine;

public class CargoLluviaLoader : MonoBehaviour
{
    public GameObject objetoLluvia;

    void Start()
    {
        objetoLluvia.SetActive(PlayerPrefs.GetInt("Cargolluvia", 0) == 1);
    }
}
