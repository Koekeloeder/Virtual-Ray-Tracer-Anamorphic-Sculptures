using UnityEngine;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Reads saved images from disk, computes difference images, and reports error stats.
/// Run after UVSpaceShaderBaker or ScreenshotComparison have produced their outputs.
/// and assign the desired
/// Two diff outputs per pair:
///  - Raw :absolute difference per channel
///  - Normalized no error = black, max error = white
/// </summary>
public class DiffUtility : MonoBehaviour
{
    [Header("Input Images")]
    public Texture2D referenceTexture;
    public Texture2D compareTextureA;
    public Texture2D compareTextureB;

    [Tooltip("The compensation texture to multiply with the base color (e.g. UV bake output)")]
    public Texture2D compensationTexture;

    [Header("Output Folder")]
    public string outputFolder = "Assets/Screenshots";

    [Header("Results (read-only)")]
    public Texture2D diffARaw;
    public Texture2D diffANormalized;
    public Texture2D diffBRaw;
    public Texture2D diffBNormalized;

    public int cuttOff = 2;

    [ContextMenu("Compute Diffs")]
    public void ComputeDiffs()
    {
        if (referenceTexture == null || compareTextureA == null || compareTextureB == null)
        {
            Debug.LogError("Missing components");
            return;
        }

        Texture2D reference = DuplicateTexture(referenceTexture);
        Texture2D a = DuplicateTexture(compareTextureA);
        Texture2D b = DuplicateTexture(compareTextureB);

        // Resize a and b to match reference dimensions if needed
        if (a.width != reference.width || a.height != reference.height)
        {
            a = Resize(a, reference.width, reference.height);
        }
        if (b.width != reference.width || b.height != reference.height)
        {
            b = Resize(b, reference.width, reference.height);
        }

        // cutt off a bit of the texture such that we have no background noise(you can set this value)
        reference = Inset(reference, cuttOff);
        a = Inset(a, cuttOff);
        b = Inset(b, cuttOff);

        float minErr, meanErr, maxErr;

        diffARaw = ComputeRaw(reference, a);
        diffANormalized = ComputeNormalized(reference, a, out minErr, out meanErr, out maxErr);
        Debug.Log($"[Diff] A vs Reference\n" +
                  $"  min:{minErr:F4}  mean:{meanErr:F4}  max:{maxErr:F4}");

        diffBRaw = ComputeRaw(reference, b);
        diffBNormalized = ComputeNormalized(reference, b, out minErr, out meanErr, out maxErr);
        Debug.Log($"[Diff] B vs Reference\n" +
                  $"  min:{minErr:F4}  mean:{meanErr:F4}  max:{maxErr:F4}");

        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }
        Write(diffARaw, $"Diff_{compareTextureA.name}_Raw.png");
        Write(diffANormalized, $"Diff_{compareTextureA.name}_Normalized.png");
        Write(diffBRaw, $"Diff_{compareTextureB.name}_Raw.png");
        Write(diffBNormalized, $"Diff_{compareTextureB.name}_Normalized.png");

#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
        Debug.Log($"Diffs saved to {outputFolder}");
    }

    // Duplicates a texture into a readable RGB24 format
    Texture2D DuplicateTexture(Texture2D src)
    {
        RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        Texture2D dst = new Texture2D(src.width, src.height, TextureFormat.RGB24, false);
        dst.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        dst.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return dst;
    }

    // Resize textures to match
    Texture2D Resize(Texture2D src, int targetW, int targetH)
    {
        var rt = new RenderTexture(targetW, targetH, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        var dst = new Texture2D(targetW, targetH, TextureFormat.RGB24, false);
        dst.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0);
        dst.Apply();
        RenderTexture.active = null;
        rt.Release();
        return dst;
    }
    // Computes raw diff
    Texture2D ComputeRaw(Texture2D a, Texture2D b)
    {
        Color[] pa = a.GetPixels(), pb = b.GetPixels();
        Color[] d = new Color[pa.Length];
        for (int i = 0; i < pa.Length; i++)
            d[i] = new Color(Mathf.Abs(pa[i].r - pb[i].r),
                             Mathf.Abs(pa[i].g - pb[i].g),
                             Mathf.Abs(pa[i].b - pb[i].b));
        var t = new Texture2D(a.width, a.height, TextureFormat.RGB24, false);
        t.SetPixels(d);
        t.Apply();
        return t;
    }

    // Computes normalized diff
    Texture2D ComputeNormalized(Texture2D a, Texture2D b,
                                out float minError, out float meanError, out float maxError)
    {
        Color[] pa = a.GetPixels(), pb = b.GetPixels();
        float[] err = new float[pa.Length];

        minError = float.MaxValue;
        maxError = 0f;
        float sum = 0f;

        for (int i = 0; i < pa.Length; i++)
        {
            err[i] = Mathf.Max(Mathf.Abs(pa[i].r - pb[i].r),
                               Mathf.Abs(pa[i].g - pb[i].g),
                               Mathf.Abs(pa[i].b - pb[i].b));
            if (err[i] < minError) minError = err[i];
            if (err[i] > maxError) maxError = err[i];
            sum += err[i];
        }
        meanError = sum / pa.Length;

        float scale = maxError > 0.0001f ? 1f / maxError : 1f;
        Color[] d = new Color[pa.Length];
        for (int i = 0; i < pa.Length; i++)
        { float v = err[i] * scale; d[i] = new Color(v, v, v); }

        var t = new Texture2D(a.width, a.height, TextureFormat.RGB24, false);
        t.SetPixels(d);
        t.Apply();
        return t;
    }

    // This is used to cut off a bit off the mesh, to make sure that we have no pixels from the background
    Texture2D Inset(Texture2D src, int pixels)
    {
        int w = src.width - pixels * 2;
        int h = src.height - pixels * 2;
        var dst = new Texture2D(w, h, TextureFormat.RGB24, false);
        dst.SetPixels(src.GetPixels(pixels, pixels, w, h));
        dst.Apply();
        return dst;
    }

    void Write(Texture2D tex, string name)
        => File.WriteAllBytes(Path.Combine(outputFolder, name), tex.EncodeToPNG());
}