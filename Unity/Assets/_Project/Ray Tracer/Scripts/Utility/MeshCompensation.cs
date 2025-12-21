using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Per-pixel mesh compensation - passes data to shader for fragment-level calculation
/// </summary>
public class MeshCompensation : MonoBehaviour
{
    [Header("Mesh Setup")]
    public Mesh originalMesh;
    public Light sceneLight;
    public Transform viewPoint;
    [Tooltip("Deformer is the object that deforms the mesh (mirror or refractive surface)")]
    public Deformer deformer;

    [Header("Compensation Parameters")]
    [Range(0.1f, 4.0f)] public float minCompensation = 0.25f;
    [Range(0.1f, 4.0f)] public float maxCompensation = 4.0f;

    [Header("Lighting Parameters")]
    public float lightIntensity = 1.0f;

    [Header("Anisotropy")]
    [Range(0.0f, 1.0f)] public float baseSmoothness = 0.5f;
    [Range(-1.0f, 1.0f)] public float baseAnisotropy = 0.0f;
    [Range(0.0f, 1.0f)] public float baseMetallic = 0.0f;

    [Header("Mode")]
    public bool colorCompensation = true;
    public bool enableAnisotropicData = false;

    [Header("Runtime")]
    public bool autoUpdateOnChange = false;

    private MeshFilter meshFilter;
    private Mesh runtimeMesh;
    private Material runtimeMaterial;
    private Vector3 originalLightPos;
    private Vector3 deformedLightPos;

    // Track previous values to detect changes
    private float prevBaseSmoothness;
    private float prevBaseAnisotropy;
    private float prevBaseMetallic;

    void Start()
    {
        CalculateCompensation();
        StorePreviousValues();
    }

    void Update()
    {
        if (autoUpdateOnChange && HasParametersChanged())
        {
            if (enableAnisotropicData)
            {
                CalculateAnisotropicData();
            }
            UpdateShaderParameters();
            StorePreviousValues();
        }
        else if (runtimeMaterial)
        {
            // Always update shader parameters for runtime changes
            UpdateShaderParameters();
        }
    }

    bool HasParametersChanged()
    {
        return !Mathf.Approximately(baseSmoothness, prevBaseSmoothness) ||
               !Mathf.Approximately(baseAnisotropy, prevBaseAnisotropy) ||
               !Mathf.Approximately(baseMetallic, prevBaseMetallic);
    }

    void StorePreviousValues()
    {
        prevBaseSmoothness = baseSmoothness;
        prevBaseAnisotropy = baseAnisotropy;
        prevBaseMetallic = baseMetallic;
    }

    public void CalculateCompensation()
    {
        if (!ValidateSetup()) return;

        InitializeRuntimeMesh();
        CalculateLightPositions();
        StoreOriginalNormalsInUV();
        StoreOriginalVerticesInUV();

        if (enableAnisotropicData)
            CalculateAnisotropicData();

        UpdateShaderParameters();
    }

    bool ValidateSetup()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (!meshFilter || !originalMesh || !sceneLight)
        {
            Debug.LogError("Missing components!");
            return false;
        }

        Renderer renderer = GetComponent<Renderer>();
        if (renderer && renderer.sharedMaterial)
        {
            runtimeMaterial = renderer.material;
        }

        return true;
    }

    void InitializeRuntimeMesh()
    {
        if (!runtimeMesh)
        {
            runtimeMesh = Instantiate(meshFilter.sharedMesh);
            runtimeMesh.name = "Runtime mesh";

            if (runtimeMesh.tangents == null || runtimeMesh.tangents.Length == 0)
            {
                runtimeMesh.RecalculateTangents();
            }

            meshFilter.mesh = runtimeMesh;
        }
    }

    // This is used to calculate correct light positions to correctly compare how light affects the mesh.

    void CalculateLightPositions()
    {
        deformedLightPos = sceneLight.transform.position;

        Vector3 origCentroid = CalculateCentroid(originalMesh.vertices);
        Vector3 defCentroid = CalculateCentroid(runtimeMesh.vertices);

        Vector3 lightOffset = deformedLightPos - defCentroid;

        originalLightPos = origCentroid;
        if (deformer.isRefractive)
        {
            originalLightPos += lightOffset;
        }
        else
        {
            originalLightPos -= lightOffset;
        }
    }

    // We store the normals of the original mesh in UV1 to help with calculations inside the shader
    void StoreOriginalNormalsInUV()
    {
        Vector3[] originalNormals = originalMesh.normals;
        Vector3[] deformedNormals = runtimeMesh.normals;

        List<Vector4> uv1 = new List<Vector4>();

        for (int i = 0; i < originalNormals.Length; i++)
        {
            Vector3 originalNormalWorld = transform.TransformDirection(originalNormals[i]).normalized;
            Vector3 deformedNormalWorld = transform.TransformDirection(deformedNormals[i]).normalized;

            float cosAngle = Vector3.Dot(originalNormalWorld, deformedNormalWorld);
            float angle = Mathf.Acos(Mathf.Clamp(cosAngle, -1f, 1f)) * Mathf.Rad2Deg;

            uv1.Add(new Vector4(originalNormalWorld.x, originalNormalWorld.y, originalNormalWorld.z, angle));
        }

        runtimeMesh.SetUVs(1, uv1);
    }

    // We store the vertices of the original mesh in UV2 to help with calculations inside the shader
    void StoreOriginalVerticesInUV()
    {
        Vector3[] origVertices = originalMesh.vertices;
        List<Vector4> uv2 = new List<Vector4>(origVertices.Length);
        for (int i = 0; i < origVertices.Length; i++)
        {
            Vector3 origWorld = transform.TransformPoint(origVertices[i]);
            uv2.Add(new Vector4(origWorld.x, origWorld.y, origWorld.z, 1.0f));
        }

        runtimeMesh.SetUVs(2, uv2);
    }

    void CalculateAnisotropicData()
    {
        Vector3[] origNormals = originalMesh.normals;
        Vector3[] defNormals = runtimeMesh.normals;

        if (runtimeMesh.tangents == null || runtimeMesh.tangents.Length == 0)
        {
            runtimeMesh.RecalculateTangents();
        }

        List<Vector4> uv3 = new List<Vector4>();

        for (int i = 0; i < origNormals.Length; i++)
        {
            Vector3 origNormWorld = transform.TransformDirection(origNormals[i]).normalized;
            Vector3 defNormWorld = transform.TransformDirection(defNormals[i]).normalized;

            float cosAngle = Vector3.Dot(origNormWorld, defNormWorld);

            // Handle flipped normals from winding reversal
            if (cosAngle < 0)
            {
                defNormWorld = -defNormWorld;
                cosAngle = -cosAngle;
            }

            float angle = Mathf.Acos(Mathf.Clamp(cosAngle, -1f, 1f)) * Mathf.Rad2Deg;

            float deviationFactor = Mathf.Clamp01(angle / 60f);
            // Apply power curve to make low deviations more impactful
            deviationFactor = Mathf.Pow(deviationFactor, 0.7f);

            // Smoothness: More deformation = rougher surface
            float smoothness = baseSmoothness - (deviationFactor * 0.4f);
            smoothness = Mathf.Clamp(smoothness, 0.05f, 0.95f);

            // The deviation naturally modulates anisotropy - deformed areas get stretched highlights
            float anisotropy = baseAnisotropy + (deviationFactor * 0.6f);
            anisotropy = Mathf.Clamp(anisotropy, -0.9f, 0.9f);

            // Metallic with subtle increase
            float metallic = baseMetallic + (deviationFactor * baseMetallic * 0.3f);
            metallic = Mathf.Clamp01(metallic);

            // In UV3 we add all the calculated values to aid in our anisotropy calculation
            uv3.Add(new Vector4(smoothness, anisotropy, metallic, deviationFactor));
        }

        runtimeMesh.SetUVs(3, uv3);
    }

    void UpdateShaderParameters()
    {
        if (!runtimeMaterial) return;

        // Set all the shader parameters
        runtimeMaterial.SetVector("_ViewPos", viewPoint.transform.position);
        runtimeMaterial.SetVector("_OriginalLightPos", originalLightPos);
        runtimeMaterial.SetVector("_DeformedLightPos", deformedLightPos);
        runtimeMaterial.SetFloat("_CompensationMin", minCompensation);
        runtimeMaterial.SetFloat("_CompensationMax", maxCompensation);
        runtimeMaterial.SetFloat("_SoftClampStrength", 0.8f);
        runtimeMaterial.SetFloat("_LightIntensity", lightIntensity);

        runtimeMaterial.SetFloat("_ColorCompensation", colorCompensation ? 1.0f : 0.0f);
        runtimeMaterial.SetFloat("_AnisotropicCompensation", enableAnisotropicData ? 1.0f : 0.0f);

        // Enable/disable keywords for shader variants
        if (colorCompensation)
        {
            runtimeMaterial.EnableKeyword("COLOR_COMPENSATION");
        }
        else
        {
            runtimeMaterial.DisableKeyword("COLOR_COMPENSATION");
        }

        if (enableAnisotropicData)
        {
            runtimeMaterial.EnableKeyword("ANISOTROPIC_COMPENSATION");
            runtimeMaterial.SetInt("_MaterialID", 2);
            runtimeMaterial.EnableKeyword("_MATERIAL_FEATURE_ANISOTROPY");
            runtimeMaterial.SetFloat("_Anisotropy", baseAnisotropy);
            runtimeMaterial.SetFloat("_Smoothness", baseSmoothness);
            runtimeMaterial.SetFloat("_Metallic", baseMetallic);
        }
        else
        {
            runtimeMaterial.DisableKeyword("ANISOTROPIC_COMPENSATION");
            runtimeMaterial.SetInt("_MaterialID", 1);
            runtimeMaterial.DisableKeyword("_MATERIAL_FEATURE_ANISOTROPY");
        }
    }

    Vector3 CalculateCentroid(Vector3[] vertices)
    {
        Vector3 sum = Vector3.zero;
        foreach (var v in vertices)
            sum += transform.TransformPoint(v);
        return sum / vertices.Length;
    }

    void OnDestroy()
    {
        if (runtimeMesh) Destroy(runtimeMesh);
        if (runtimeMaterial) Destroy(runtimeMaterial);
    }
}
