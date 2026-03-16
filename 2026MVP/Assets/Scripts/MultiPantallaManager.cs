using UnityEngine;

public class MultiPantallaManager : MonoBehaviour
{
    void Start()
    {
       
        if (Display.displays.Length > 2)
        {
            Display.displays[1].Activate();
            Display.displays[2].Activate();

        }
    }
}
