using UnityEngine;                                                                                                                                                         
using System.Collections.Generic;                                                                                                                                          
using System.IO;                                                                                                                                                           
using System;                                                                                                                                                              
                                                                                                                                                                            
public class TelemetryLogger : MonoBehaviour                                                                                                                               
{                                                                                                                                                                          
    public static TelemetryLogger Instance;                                                                                                                                
                                                                                                                                                                            
    [System.Serializable]                                                                                                                                                  
    public class TelemetryEvent                                                                                                                                            
    {                                                                                                                                                                      
        public string timestamp;                                                                                                                                           
        public string eventType;                                                                                                                                           
        public string description;                                                                                                                                         
        public int points;                                                                                                                                                 
        public float speed;                                                                                                                                                
    }                                                                                                                                                                      
                                                                                                                                                                            
    [System.Serializable]
    public class TelemetryData
    {
        public string sessionId;
        public string sessionStart;
        public string sessionEnd;
        public string tramiteId;
        public float examDurationSeconds;
        public int finalScore;
        public int finalDistanceMeters;
        public List<TelemetryEvent> events = new List<TelemetryEvent>();
    }                                                                                                                                                                  
                                                                                                                                                                            
    public TelemetryData data = new TelemetryData();                                                                                                                       
    private float sessionStartTime;                                                                                                                                        
                                                                                                                                                                            
    void Awake()                                                                                                                                                           
    {                                                                                                                                                                      
        Instance = this;                                                                                                                                                   
        data.sessionStart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");                                                                                                  
        sessionStartTime = Time.time;                                                                                                                                      
    }                                                                                                                                                                      
                                                                                                                                                                            
    public void LogEvent(string eventType, string description, int points, float speed)                                                                                    
    {                                                                                                                                                                      
        TelemetryEvent evt = new TelemetryEvent                                                                                                                            
        {                                                                                                                                                                  
            timestamp = (Time.time - sessionStartTime).ToString("F2") + "s",                                                                                               
            eventType = eventType,                                                                                                                                         
            description = description,                                                                                                                                     
            points = points,                                                                                                                                               
            speed = Mathf.Round(speed * 10f) / 10f                                                                                                                         
        };                                                                                                                                                                 
        data.events.Add(evt);                                                                                                                                              
        Debug.Log($"Logged: {eventType} - {description}");                                                                                                                 
    }                                                                                                                                                                      
                                                                                                                                                                            
    public void ExportToJSON(int finalScore)                                          
    {                                                                                 
        Debug.Log("ExportToJSON called with score: " + finalScore);                   
                                                                                        
        data.sessionEnd = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        data.sessionId = GameManager.Instance?.SessionId ?? "";
        data.tramiteId = GameManager.Instance?.Expediente ?? "";
        data.examDurationSeconds = ExamTimer.Instance?.examDuration ?? 300f;
        data.finalScore = finalScore;
        data.finalDistanceMeters = ExamTimer.Instance?.GetDistanceMeters() ?? 0;                                            
                                                                                        
        string json = JsonUtility.ToJson(data, true);                                 
        string filename = $"telemetry_{DateTime.Now:yyyyMMdd_HHmmss}.json";           
        string path = Path.Combine(Application.persistentDataPath, filename);         
                                                                                        
        Debug.Log("Saving to path: " + path);                                         
                                                                                        
        File.WriteAllText(path, json);                                                
        Debug.Log($"Telemetry exported to: {path}");                                  
                                                                                        
        NotificationManager.Instance?.ShowNotification(                               
            "Telemetría exportada", Color.green);                                     
    }                                                                                                                                                                    
} 