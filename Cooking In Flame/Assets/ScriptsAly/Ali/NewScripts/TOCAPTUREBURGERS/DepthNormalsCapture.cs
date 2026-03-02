using UnityEngine;
using System.IO;

public class DepthNormalsCaptureLinear : MonoBehaviour
{
    public int textureSize = 1024;
    [HideInInspector] public RenderTexture outputRT;
    private Material depthNormalsMat;

    void Awake()
    {
        // 1️⃣ Find Unity's built-in depth+normals shader
        Shader shader = Shader.Find("Hidden/Internal-DepthNormalsTexture");
        if (shader == null)
        {
            Debug.LogError("Built-in depth+normals shader not found!");
            return;
        }

        // 2️⃣ Create a material with it
        depthNormalsMat = new Material(shader);

        // 3️⃣ Create a RenderTexture manually using ARGBFloat for full linear precision
        outputRT = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        outputRT.Create();
    }

    // Call this whenever you want to capture
    public void Capture()
    {
        if (!depthNormalsMat) return;

        // 4️⃣ Blit the shader output into the RenderTexture
        Graphics.Blit(null, outputRT, depthNormalsMat);

        // 5️⃣ Save the RenderTexture as PNG in linear space
        SavePNGLinear(outputRT, "DepthNormalsLinear.png");
    }

    private void SavePNGLinear(RenderTexture rt, string fileName)
    {
        RenderTexture current = RenderTexture.active;
        RenderTexture.active = rt;

        // Use TextureFormat.RGBAFloat and linear = true
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(Application.dataPath + "/" + fileName, bytes);

        RenderTexture.active = current;
        Destroy(tex);

        Debug.Log("Saved linear PNG: " + fileName);
    }
}