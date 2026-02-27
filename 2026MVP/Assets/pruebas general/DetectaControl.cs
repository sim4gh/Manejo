using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
public class DetectaControl : MonoBehaviour
{
    public TextMeshProUGUI texto;
    void Start()
    {
        foreach (var device in InputSystem.devices)
        {
            Debug.Log("Device: " + device.displayName +
                      " | Layout: " + device.layout);

            texto.text = "Device: " + device.displayName +
                      " | Layout: " + device.layout;
        }
    }
}
