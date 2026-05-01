using UnityEngine;

public class IdSesion : MonoBehaviour
{
    public static string _Mi_ID = "";

    private static IdSesion _instance;

    // Sin guard, cada LoadScene("MainMenu") instancia otro GO que se manda a
    // DontDestroyOnLoad y nunca muere — leak lineal en kiosko que cicla
    // MainMenu→escena→MainMenu decenas de veces al día.
    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
