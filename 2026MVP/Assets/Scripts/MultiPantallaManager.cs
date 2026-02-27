using UnityEngine;

public class MultiPantallaManager : MonoBehaviour
{
    void Start()
    {
        if (Display.displays.Length > 1)
        {
            Display.displays[1].Activate();
        }
    }
}
