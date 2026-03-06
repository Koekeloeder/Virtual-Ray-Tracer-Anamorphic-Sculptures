using UnityEngine;

/// <summary>
/// Mesh Compensation - Bakes compensation factor to the vertex color of a mesh
/// Calculates per vertex compensation based on lighting differences, 
/// such that the original shading of the mesh is restored in the deformed view in the mirror.
/// </summary>
public class MeshCompensation : MonoBehaviour
{
    [Header("Mesh Setup")]
    public Mesh originalMesh;
    public Light directionalLight;

    [Header("Face Colors")]
    public bool enableFaceColors = false;
    [Tooltip("Colors for cube faces: Front, Left, Right, Top, Bottom (Back uses compensation only)")]
    public Color[] faceColors = new Color[]
    {
        Color.blue,    // Front
        Color.green,   // Left
        Color.yellow,  // Right
        Color.magenta, // Top
        Color.cyan     // Bottom
    };

    [Header("Mode")]
    public bool colorCompensation = true;

    public bool autoUpdateOnChange = true;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh runtimeMesh;
    public Mesh GetRuntimeMesh() => runtimeMesh;
    private Material runtimeMaterial;

    private bool prevColorCompensation;
    private bool prevEnableFaceColors;

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        CalculateCompensation();
        StorePreviousValues();
    }

    void Update()
    {
        if (autoUpdateOnChange && HasParametersChanged())
        {
            CalculateCompensation();
            StorePreviousValues();
        }
    }

    bool HasParametersChanged()
    {
        return colorCompensation != prevColorCompensation ||
            enableFaceColors != prevEnableFaceColors;
    }

    void StorePreviousValues()
    {
        prevColorCompensation = colorCompensation;
        prevEnableFaceColors = enableFaceColors;
    }

    [ContextMenu("Calculate Compensation")]
    public void CalculateCompensation()
    {
        if (!ValidateSetup()) return;

        InitializeRuntimeMesh();
        
        if (colorCompensation)
        {
            BakeCompensationVertexColor();
            UpdateShaderKeywords();
        }
        else
        {
            ClearVertexColors();
        }
    }

    // Main function that provides the deformed mesh with vertex color compensation values
    void BakeCompensationVertexColor()
    {
        Vector3[] originalVertices = originalMesh.vertices;
        Vector3[] originalNormals = originalMesh.normals;
        Vector3[] deformedNormals = runtimeMesh.normals;

        Color[] vertexColors = new Color[originalVertices.Length];

        // Get lighting properties
        // It is important to note that since we activate the color compensation in the mirror view.
        // That we need to flip the light direction to get the direction on the original mesh.
        // That is negated to correctly calculate what happens on the deformed mesh.
        // A neat extension would be to improve the logic here such that this function provides correct results whenever it is pressed before switching the view as well.
        Vector3 lightDir = -directionalLight.transform.forward;
        Color lightColor = directionalLight.color * directionalLight.intensity;

        for (int i = 0; i < originalVertices.Length; i++)
        {

            // Check if this vertex should use face color instead
            if (enableFaceColors)
            {
                Color faceColor = GetFaceColor(originalNormals[i]);

                if (faceColor.a < 0.5f) // Not back face - encode face color in vertex color
                {
                    vertexColors[i] = faceColor; // alpha = 0.0 means "face color mode"
                    continue;
                }
            }
            Vector3 origNormalWorld = transform.TransformDirection(originalNormals[i]).normalized;


            float ambient = 0.2f;
            // So here the light is flipped to emulate the lighting on the original mesh
            float origNdotL = Mathf.Max(0, Vector3.Dot(origNormalWorld, -lightDir)) + ambient;
            Color origDiffuse = origNdotL * lightColor;
            
            Vector3 defNormalWorld = transform.TransformDirection(deformedNormals[i]).normalized;

            float defNdotL = Mathf.Max(0, Vector3.Dot(defNormalWorld, lightDir)) + ambient;
            Color defDiffuse = defNdotL * lightColor;

            Color compensation = new Color(
                SafeDivide(origDiffuse.r, defDiffuse.r),
                SafeDivide(origDiffuse.g, defDiffuse.g),
                SafeDivide(origDiffuse.b, defDiffuse.b),
                1f
                );
            vertexColors[i] = compensation;
        }
        runtimeMesh.colors = vertexColors;
    }

    //Helper function that allows us to color the different mesh faces
    Color GetFaceColor(Vector3 normal)
    {
        // Get absolute values to find dominant axis
        float absX = Mathf.Abs(normal.x);
        float absY = Mathf.Abs(normal.y);
        float absZ = Mathf.Abs(normal.z);

        // Find which axis is dominant
        if (absZ > absX && absZ > absY)
        {
            // Z axis dominant
            if (normal.z > 0)
                return new Color(faceColors[0].r, faceColors[0].g, faceColors[0].b, 0.0f); // Front
            else
                return new Color(1, 1, 1, 1.0f); // Back - alpha=0 means use compensation
        }
        else if (absX > absY)
        {
            // X axis dominant
            if (normal.x < 0)
                return new Color(faceColors[1].r, faceColors[1].g, faceColors[1].b, 0.0f); // Left
            else
                return new Color(faceColors[2].r, faceColors[2].g, faceColors[2].b, 0.0f); // Right
        }
        else
        {
            // Y axis dominant
            if (normal.y > 0)
                return new Color(faceColors[3].r, faceColors[3].g, faceColors[3].b, 0.0f); // Top
            else
                return new Color(faceColors[4].r, faceColors[4].g, faceColors[4].b, 0.0f); // Bottom
        }
    }

    //To make sure that we do not divide by zero, or we get very extreme values we clamp.
    float SafeDivide(float numerator, float denominator)
    {
        if (denominator > 0.0001f)
            return Mathf.Clamp(numerator / denominator, 0f, 10f);
        else if (numerator > 0.0001f)
            return 10f;
        else
            return 1f;
    }

    //Update the shader keyword, so that the shader can apply the vertex color to the basecolor.
    void UpdateShaderKeywords()
    {
        if (runtimeMaterial == null) return;

        runtimeMaterial.DisableKeyword("COMPENSATION_RATIO");
        if (colorCompensation)
        {
            runtimeMaterial.EnableKeyword("COMPENSATION_RATIO");
        }
    }
    void ClearVertexColors()
    {
        if (runtimeMesh)
        {
            Color[] white = new Color[runtimeMesh.vertexCount];
            for (int i = 0; i < white.Length; i++)
                white[i] = Color.white;
            runtimeMesh.colors = white;
        }

        if (runtimeMaterial != null)
        {
            runtimeMaterial.DisableKeyword("COMPENSATION_RATIO");
        }
    }


    bool ValidateSetup()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (!meshFilter || !meshRenderer || !originalMesh || !directionalLight)
        {
            Debug.LogError("Missing components! Need MeshFilter, MeshRenderer, originalMesh, and directionalLight");
            return false;
        }
        return true;
    }

    void InitializeRuntimeMesh()
    {
        if (!runtimeMesh)
        {
            runtimeMesh = Instantiate(meshFilter.sharedMesh);
            meshFilter.mesh = runtimeMesh;

            Color[] white = new Color[runtimeMesh.vertexCount];
            for (int i = 0; i < white.Length; i++)
                white[i] = Color.white;
            runtimeMesh.colors = white;
        }

        runtimeMaterial = meshRenderer.material;
    }

    void OnDestroy()
    {
        if (runtimeMesh) Destroy(runtimeMesh);
        if (runtimeMaterial) Destroy(runtimeMaterial);
    }
}