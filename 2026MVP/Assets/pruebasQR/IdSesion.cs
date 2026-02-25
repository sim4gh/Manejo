using UnityEngine;

public class IdSesion : MonoBehaviour
{
    public static string _Mi_ID = "";

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }


}
