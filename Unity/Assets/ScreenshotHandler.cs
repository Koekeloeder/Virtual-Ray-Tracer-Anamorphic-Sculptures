using UnityEngine;
using System.Collections;
using System.IO;

/// <summary>
/// Just a helper file to create screenshots to evaluate results
/// </summary>
public class ScreenshotHandler : MonoBehaviour
{
    [Header("Capture Settings")]
    public KeyCode captureKey = KeyCode.P;
    public int width = 1920;
    public int height = 1080;
    public string outputFolder = "Assets/Screenshots";
    public string filename = "Screenshot";
    public bool deformed = false;

    public RectInt deformedCropRect = new RectInt(530, 345, 390, 130);
    public RectInt originalCropRect = new RectInt(530, 345, 390, 130);

    private RectInt cropRect;

    [Header("Overlay")]
    public bool showOverlay = true;
    public Camera captureCamera;

    void Update()
    {
        if (Input.GetKeyDown(captureKey))
            StartCoroutine(Capture());
    }

    IEnumerator Capture()
    {
        yield return new WaitForEndOfFrame();

        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        var full = new Texture2D(width, height, TextureFormat.RGB24, false);
        full.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        full.Apply();

        cropRect = originalCropRect;
        if(deformed)
        {
            cropRect = deformedCropRect;
        }
        var crop = new Texture2D(cropRect.width, cropRect.height, TextureFormat.RGB24, false);
        crop.SetPixels(full.GetPixels(cropRect.x, cropRect.y, cropRect.width, cropRect.height));
        crop.Apply();

        string path = Path.Combine(outputFolder, filename + ".png");
        File.WriteAllBytes(path, crop.EncodeToPNG());
        Debug.Log($"Saved: {path}");

        Destroy(full);
        Destroy(crop);
    }
    
    void DrawOverlayRect(RectInt r, float sx, float sy, Color fill, Color border, string label)
    {
        float guiX = r.x * sx;
        float guiY = (height - r.y - r.height) * sy;
        float guiW = r.width * sx;
        float guiH = r.height * sy;

        var fillTex = new Texture2D(1, 1);
        fillTex.SetPixel(0, 0, fill); fillTex.Apply();
        GUI.DrawTexture(new Rect(guiX, guiY, guiW, guiH), fillTex);

        float b = 2f;
        var borderTex = new Texture2D(1, 1);
        borderTex.SetPixel(0, 0, border); borderTex.Apply();
        GUI.DrawTexture(new Rect(guiX, guiY, guiW, b), borderTex);
        GUI.DrawTexture(new Rect(guiX, guiY + guiH - b, guiW, b), borderTex);
        GUI.DrawTexture(new Rect(guiX, guiY, b, guiH), borderTex);
        GUI.DrawTexture(new Rect(guiX + guiW - b, guiY, b, guiH), borderTex);

        GUI.Label(new Rect(guiX + 4, guiY + 4, 200, 20),
                  $"{label}  {r.width}x{r.height}px",
                  new GUIStyle { normal = { textColor = border }, fontStyle = FontStyle.Bold });

        DestroyImmediate(fillTex);
        DestroyImmediate(borderTex);
    }

    void OnGUI()
    {
        if (!showOverlay || !captureCamera) return;

        float scaleX = (float)Screen.width / width;
        float scaleY = (float)Screen.height / height;

        if (deformed)
        {
            DrawOverlayRect(deformedCropRect, scaleX, scaleY,
                 new Color(1f, 0.5f, 0f, 0.2f), new Color(1f, 0.5f, 0f, 1f),
                 "Deformed crop");
        }
        else
        {
            DrawOverlayRect(originalCropRect, scaleX, scaleY,
                            new Color(0f, 1f, 0f, 0.25f), new Color(0f, 1f, 0f, 1f),
                            "Original crop");
        }
    }
}
