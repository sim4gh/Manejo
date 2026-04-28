using UnityEngine;                                                                
using TMPro;                                                                      
using System.Collections;                                                         
                                                                                
public class NotificationManager : MonoBehaviour                                  
{                                                                                 
    public static NotificationManager Instance;                                   
                                                                                
    public TextMeshProUGUI notificationText;                                      
    public float displayDuration = 2f;                                            
                                                                                
    private Coroutine hideCoroutine;                                              
                                                                                
    void Awake()                                                                  
    {                                                                             
        Instance = this;                                                          
        if (notificationText != null)                                             
            notificationText.gameObject.SetActive(false);                         
    }                                                                             
                                                                                
    public void ShowNotification(string message, Color color)
    {
        if (SimulatorConfig.Instance != null && !SimulatorConfig.Instance.data.showNotifications) return;
        if (notificationText == null) return;                                     
                                                                                
        notificationText.text = message;                                          
        notificationText.color = color;                                           
        notificationText.gameObject.SetActive(true);                              
                                                                                
        if (hideCoroutine != null)                                                
            StopCoroutine(hideCoroutine);                                         
        hideCoroutine = StartCoroutine(HideAfterDelay());                         
    }                                                                             
                                                                                
    IEnumerator HideAfterDelay()                                                  
    {                                                                             
        yield return new WaitForSeconds(displayDuration);                         
        notificationText.gameObject.SetActive(false);                             
    }                                                                             
}     