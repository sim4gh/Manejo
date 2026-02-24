using QRCoder;
using UnityEngine;
using UnityEngine.UI;

public class QRGenerator : MonoBehaviour
{
    public RawImage qrDisplay; // Arrastra tu RawImage aquí en el Inspector
    public string text = "https://dev.dvv66coi1awdq.amplifyapp.com";

    void Start()
    {
        qrDisplay.texture = GenerateQR(text);
    }

    Texture2D GenerateQR(string data)
    {
        QRCodeGenerator qrGenerator = new QRCodeGenerator();
        QRCodeData qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
        PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
        byte[] qrCodeBytes = qrCode.GetGraphic(20);

        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(qrCodeBytes);

        return texture;
    }
}
