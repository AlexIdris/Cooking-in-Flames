using UnityEngine;
using System.IO;

public class CaptureAllBurgers : MonoBehaviour
{
    public Camera cam;
    public RenderTexture rt;
    public string fileName = "CapturedBurgers";

    public void Capture()
    {
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = rt;

        cam.Render();

        Texture2D image = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
        image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        image.Apply();

        byte[] bytes = image.EncodeToPNG();
        File.WriteAllBytes(Application.dataPath + "/" + fileName + ".png", bytes);

        RenderTexture.active = currentRT;

        Debug.Log("Saved PNG: " + fileName);


    }

    void Start()
    {
        Capture();
    }
}