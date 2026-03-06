using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

/// <summary>
/// Bakes original, deformed, and compensated lighting to UV-space textures by making use of a seperate shader.
/// IMPORTANT!!!!
/// Requires MeshCompensation to have been run on the deformed mesh first.
/// So first go to the mirror scene, and enable color compensation! Without this the bake's are wrong
/// Run DiffUtility afterwards to compare results.
/// </summary>
public class UVSpaceShaderBaker : MonoBehaviour
{
    [Header("Meshes")]
    public MeshFilter originalMeshFilter;
    public MeshFilter deformedMeshFilter;

    [Header("Lighting")]
    public Light directionalLight;
    public float ambient = 0.2f;

    [Header("Material")]
    public Color baseColor = Color.white;

    [Header("Output")]
    public int textureSize = 1024;
    public string outputFolder = "Assets/BakedTextures";
    public Shader bakerShader;

    [Header("Crop")]
    public bool cropEnabled = true;
    public int cropX = 0;
    public int cropY = 0;
    public int cropWidth = 512;
    public int cropHeight = 512;

    enum BakeMode { Unlit, OrigLit, DefLit, Compensated, UnlitCompensated }

#if UNITY_EDITOR
    [ContextMenu("Bake All Textures")]
    public void BakeAllTextures()
    {
        if (!Validate())
        {
            return;
        }

        Mesh origMesh = originalMeshFilter.sharedMesh;
        Mesh defMesh  = deformedMeshFilter.GetComponent<MeshCompensation>()?.GetRuntimeMesh()
                        ?? deformedMeshFilter.sharedMesh;
        Transform origTf = originalMeshFilter.transform;
        Transform defTf  = deformedMeshFilter.transform;

        Vector3 lightDir = directionalLight != null
            ? (-directionalLight.transform.forward).normalized
            : origTf.TransformDirection(Vector3.back).normalized;

        Save(Bake(origMesh, origTf, origMesh, origTf, lightDir, BakeMode.Unlit),              "Original_Unlit.png");
        Save(Bake(origMesh, origTf, origMesh, origTf, lightDir, BakeMode.OrigLit),            "Original_Lit.png");
        Save(Bake(defMesh,  defTf,  origMesh, origTf, lightDir, BakeMode.Unlit),              "Deformed_Unlit.png");
        Save(Bake(defMesh,  defTf,  origMesh, origTf, lightDir, BakeMode.DefLit),             "Deformed_Lit.png");
        Save(Bake(defMesh,  defTf,  origMesh, origTf, lightDir, BakeMode.Compensated),        "Deformed_Compensated.png");
        Save(Bake(defMesh,  defTf,  origMesh, origTf, lightDir, BakeMode.UnlitCompensated),   "Deformed_Unlit_Compensated.png");

#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
        Debug.Log($"Bakes saved to {outputFolder}");
    }

    // Bake the texture, 
    Texture2D Bake(Mesh mesh, Transform meshTf,
                   Mesh origMesh, Transform origTf,
                   Vector3 lightDir, BakeMode mode)
    {
        int       vCount    = mesh.vertexCount;
        Vector3[] normals   = mesh.normals;
        Vector3[] origNorms = origMesh.normals;
        Color[]   comp      = mesh.colors;
        bool      hasComp   = comp != null && comp.Length == vCount;

        Color[] vertexColors = new Color[vCount];
        for (int i = 0; i < vCount; i++)
        {
            Vector3 N     = meshTf.TransformDirection(normals[i]).normalized;
            Vector3 origN = origTf.TransformDirection(origNorms[i]).normalized;

            float origNdotL = Mathf.Max(0f, Vector3.Dot(origN, -lightDir)) + ambient;
            float defNdotL  = Mathf.Max(0f, Vector3.Dot(N,      lightDir)) + ambient;
            //pass on the light results, so that the shader handles correctly.
            float lightMult = mode switch
            {
                BakeMode.OrigLit          => origNdotL,
                BakeMode.DefLit           => defNdotL,
                BakeMode.Compensated      => defNdotL * (hasComp ? comp[i].r : 1f),
                BakeMode.UnlitCompensated => hasComp ? comp[i].r : 1f,
                _                         => 1f  // Unlit
            };

            vertexColors[i] = new Color(lightMult, lightMult, lightMult, 1f);
        }

        Mesh bakeMesh = new Mesh();
        bakeMesh.vertices  = mesh.vertices;
        bakeMesh.uv        = mesh.uv;
        bakeMesh.colors    = vertexColors;
        bakeMesh.triangles = mesh.triangles;

        var rt  = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32);
        var mat = new Material(bakerShader);
        mat.SetColor("_BaseColor", baseColor);

        var cmd = new CommandBuffer { name = "UVSpaceBake" };
        cmd.SetRenderTarget(rt);
        cmd.ClearRenderTarget(true, true, Color.black);
        cmd.DrawMesh(bakeMesh, Matrix4x4.identity, mat, 0, 0);
        Graphics.ExecuteCommandBuffer(cmd);
        cmd.Release();

        RenderTexture.active = rt;
        var result = new Texture2D(textureSize, textureSize, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
        result.Apply();
        RenderTexture.active = null;
        rt.Release();
        DestroyImmediate(bakeMesh);
        DestroyImmediate(mat);

        if (cropEnabled)
        {
            result = Crop(result, new RectInt(cropX, cropY, cropWidth, cropHeight));
        }
        return result;
    }

    Texture2D Crop(Texture2D src, RectInt b)
    {
        var dst = new Texture2D(b.width, b.height, TextureFormat.RGB24, false);
        dst.SetPixels(src.GetPixels(b.x, b.y, b.width, b.height));
        dst.Apply();
        return dst;
    }

    void Save(Texture2D tex, string name)
    {
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }
        File.WriteAllBytes(Path.Combine(outputFolder, name), tex.EncodeToPNG());
    }
#endif

    bool Validate()
    {
        if (!originalMeshFilter || !deformedMeshFilter)
        { 
            Debug.LogError("Assign both mesh filters!");
            return false;
        }
        if (!bakerShader)
        { 
            Debug.LogError("Assign the baker shader!");
            return false;
        }
        if (!directionalLight)
        {
            Debug.LogWarning("No light assigned");
        }
        if (originalMeshFilter.sharedMesh.vertexCount != deformedMeshFilter.sharedMesh.vertexCount)
        {
            Debug.LogWarning("Vertex counts differ");
        }
        return true;
    }
}