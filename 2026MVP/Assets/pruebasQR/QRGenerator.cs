using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using QRCoder;

public class QRGenerator : MonoBehaviour
{
    [Header("UI")]
    public RawImage qrDisplay;

    [Header("API")]
    public string baseUrl = "https://d6twaegbhg.execute-api.us-east-1.amazonaws.com/kiosk/sessions";

    private string idSession = "";
    private Coroutine pollingCoroutine;

    void Start()
    {
        StartCoroutine(APIConsulta());
    }

    #region GENERAR QR

    Texture2D GeneraQR(string data)
    {
        QRCodeGenerator qrGenerator = new QRCodeGenerator();
        QRCodeData qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
        PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
        byte[] qrCodeBytes = qrCode.GetGraphic(20);

        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(qrCodeBytes);

        return texture;
    }

    #endregion

    #region POST CREAR SESIÓN

    IEnumerator APIConsulta()
    {
        string jsonBody = "{}";

        UnityWebRequest request = new UnityWebRequest(baseUrl, "POST");

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
           // Debug.LogError($"Error {request.responseCode}: {request.error}");
            request.Dispose();
            yield break;
        }

        string json = request.downloadHandler.text;

        SessionResponse response = JsonUtility.FromJson<SessionResponse>(json);

        idSession = response.sessionId;

        //Debug.Log("Session ID: " + idSession);
        //Debug.Log("Expires At: " + response.expiresAt);
       // Debug.Log("Verify URL: " + response.verifyUrl);

        qrDisplay.texture = GeneraQR(response.verifyUrl);
        IdSesion._Mi_ID = idSession;
        pollingCoroutine = StartCoroutine(ChecandoAPIVerificacion());

        request.Dispose();
    }

    #endregion

    #region POLLING CADA 10 SEGUNDOS

    IEnumerator ChecandoAPIVerificacion()
    {
        while (true)
        {
            yield return StartCoroutine(APIVerificacion());
            yield return new WaitForSeconds(10f);
        }
    }

    IEnumerator APIVerificacion()
    {
        if (string.IsNullOrEmpty(idSession))
            yield break;

        string statusUrl = baseUrl + "/" + idSession + "/status";

        UnityWebRequest request = UnityWebRequest.Get(statusUrl);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            //Debug.LogError($"Error {request.responseCode}: {request.error}");
            request.Dispose();
            yield break;
        }

        string json = request.downloadHandler.text;

        StatusResponse response = JsonUtility.FromJson<StatusResponse>(json);

       // Debug.Log("Status: " + response.status);

        if (response.status == "verified")
        {
           // Debug.Log("Sesión verificada en: " + response.verifiedAt);

            if (pollingCoroutine != null)
                StopCoroutine(pollingCoroutine);

            SceneManager.LoadScene("UrbanExample");
        }

        request.Dispose();
    }

    #endregion

    #region MODELOS JSON

    [System.Serializable]
    public class SessionResponse
    {
        public string sessionId;
        public string expiresAt;
        public string verifyUrl;
    }

    [System.Serializable]
    public class StatusResponse
    {
        public string status;
        public string verifiedAt;
    }

    #endregion
}