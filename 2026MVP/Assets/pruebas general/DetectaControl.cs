using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Debug script that lists ALL connected input devices and their controls.
/// Attach to a GameObject with a TextMeshProUGUI to display device info on screen.
/// </summary>
public class DetectaControl : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI infoText;

    [Header("Settings")]
    [Tooltip("Seconds between device scans")]
    public float refreshInterval = 2f;

    private float timer;

    void Start()
    {
        timer = 0f;
        ScanDevices();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= refreshInterval)
        {
            timer = 0f;
            ScanDevices();
        }
    }

    void ScanDevices()
    {
        var devices = InputSystem.devices;
        string result = $"=== INPUT DEVICES ({devices.Count}) ===\n\n";

        foreach (var device in devices)
        {
            result += $"--- {device.displayName} ---\n";
            result += $"  Layout: {device.layout}\n";
            result += $"  Path: <{device.layout}>\n";
            result += $"  Description: {device.description}\n";

            // List all controls (axes, buttons, etc.)
            int controlCount = 0;
            foreach (var control in device.allControls)
            {
                // Skip parent/container controls, only show leaf controls
                if (control.children.Count > 0)
                    continue;

                string valueStr = "";
                try
                {
                    if (control is InputControl<float> floatCtrl)
                        valueStr = floatCtrl.ReadValue().ToString("F3");
                    else if (control is InputControl<Vector2> vec2Ctrl)
                        valueStr = vec2Ctrl.ReadValue().ToString("F3");
                    else
                        valueStr = control.ReadValueAsObject()?.ToString() ?? "null";
                }
                catch
                {
                    valueStr = "?";
                }

                result += $"    {control.path} = {valueStr}\n";
                controlCount++;
            }

            result += $"  ({controlCount} controls)\n\n";
        }

        Debug.Log(result);

        if (infoText != null)
            infoText.text = result;
    }
}
