
using UnityEngine;

public class ControlNiebla : MonoBehaviour
{
    private void Start()
    {
        RenderSettings.fog = true;
        RenderSettings.fogColor = Color.gray;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = -40.3f;
        RenderSettings.fogEndDistance = 438f;
        
    }
}
