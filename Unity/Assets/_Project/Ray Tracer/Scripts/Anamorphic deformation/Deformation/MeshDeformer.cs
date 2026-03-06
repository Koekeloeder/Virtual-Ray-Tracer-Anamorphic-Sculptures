using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshDeformer : MonoBehaviour
{
    [Header("Scene Setup")]
    [Tooltip("Original mesh to deform")]
    public Mesh originalMesh;

    [Tooltip("Camera/eye position for ray origin")]
    public Transform eyeTransform;

    [Tooltip("Object that Deforms the mesh (mirror or refractive surface)")]
    public Deformer deformer;

    private float refractiveIndex;

    [Tooltip("Epsilon for floating point offset")]
    private float epsilon = 0.001f;

    [Header("Output")]
    public bool deformOnStart = true;

    private Mesh deformedMesh;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    void Start()
    {
        refractiveIndex = deformer.getRefractiveIndex();
        if (deformOnStart)
        {
            DeformMesh();
        }
    }

    [ContextMenu("Deform Mesh")]
    public void DeformMesh()
    {
        if (!ValidateSetup())
            return;

        // Get original vertices, translate them in the world space so we can calculate the deformation properly
        Vector3[] originalVertices = originalMesh.vertices;
        int[] triangles = originalMesh.triangles;
        Vector2[] uvs = originalMesh.uv;

        // Transform to world space
        Vector3[] worldVertices = new Vector3[originalVertices.Length];
        for (int i = 0; i < originalVertices.Length; i++)
        {
            worldVertices[i] = transform.TransformPoint(originalVertices[i]);
        }

        // Deform vertices
        Vector3[] deformedWorld = DeformObject(worldVertices);

        // Transform back to local space
        Vector3[] deformedLocal = new Vector3[deformedWorld.Length];
        for (int i = 0; i < deformedWorld.Length; i++)
        {
            deformedLocal[i] = transform.InverseTransformPoint(deformedWorld[i]);
        }

        // Create deformed mesh
        if (deformedMesh == null)
        {
            deformedMesh = new Mesh();
            deformedMesh.name = "Deformed " + originalMesh.name;
        }

        deformedMesh.Clear();
        deformedMesh.vertices = deformedLocal;
        deformedMesh.triangles = triangles;
        deformedMesh.uv = uvs;

        // Since refraction does not change the winding order, we only will fix the winding if the deformer is not refractive
        if (!deformer.isRefractive)
        {
            FixWinding(deformedMesh);
        }

        // Recalculate normals
        deformedMesh.RecalculateNormals();
        deformedMesh.RecalculateBounds();

        // Apply to mesh filter
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }
        if (meshCollider == null)
        {
            meshCollider = GetComponent<MeshCollider>();
        }

        meshFilter.sharedMesh = deformedMesh;
        meshCollider.sharedMesh = deformedMesh;
    }

    bool ValidateSetup()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            Debug.LogError("No MeshFilter component!");
            return false;
        }

        if (originalMesh == null)
        {
            Debug.LogError("No original mesh assigned!");
            return false;
        }

        if (eyeTransform == null)
        {
            Debug.LogError("No eye transform assigned!");
            return false;
        }

        if (deformer == null)
        {
            Debug.LogError("No deformer assigned!");
            return false;
        }

        return true;
    }
     //For every vertex in the mesh, the ray starting from the camera going to the vertex is
     //calculated. A function that calculates the deformed vertex is then called.
    Vector3[] DeformObject(Vector3[] worldVertices)
    {
        Vector3[] deformedVertices = new Vector3[worldVertices.Length];
        Vector3 eye = eyeTransform.position;

        for (int i = 0; i < worldVertices.Length; ++i)
        {
            Vector3 targetPoint = worldVertices[i];

            // Create ray from eye to vertex
            Vector3 direction = (targetPoint - eye).normalized;
            Ray ray = new Ray(eye, direction);

            // Trace through deformer
            Vector3 deformedPoint = Trace(ray, targetPoint);

            deformedVertices[i] = deformedPoint;
        }

        return deformedVertices;
    }

    Vector3 Trace(Ray ray, Vector3 targetPoint)
    {
        // Cast ray to find intersection with deformer
        RaycastHit hit;
        if (!deformer.Intersect(ray, out hit))
        {
            // No intersection - return original point
            return targetPoint;
        }

        Vector3 P = hit.point;
        Vector3 N = hit.normal.normalized;
        Vector3 incident = ray.direction.normalized;

        if (deformer.isRefractive)
        {
            // Refractive medium
            return TraceRefraction(P, N, incident, targetPoint);
        }
        else
        {
            // Reflective medium
            return TraceReflection(P, N, incident, targetPoint);
        }
    }

    Vector3 TraceReflection(Vector3 P, Vector3 N, Vector3 incident, Vector3 targetPoint)
    {
        // Flip normal if needed
        if (Vector3.Dot(incident, N) > 0.0f)
        {
            N = -N;
        }

        // Reflect ray
        Vector3 R = Vector3.Reflect(incident, N).normalized;

        // Calculate distance and apply delta
        float distance = Vector3.Distance(targetPoint, P);
        return P + R * distance;
    }

    Vector3 TraceRefraction(Vector3 P, Vector3 N, Vector3 incident, Vector3 targetPoint)
    {
        float airIOR = 1.0f;
        float objectIOR = deformer.refractiveIndex;

        // Schlick's approximation to determine dominant ray
        float kr0 = Mathf.Pow((airIOR - objectIOR) / (airIOR + objectIOR), 2);
        float cosTheta = Mathf.Abs(Vector3.Dot(incident, N));
        float kr = kr0 + (1.0f - kr0) * Mathf.Pow(1.0f - cosTheta, 5);
        float kt = 1.0f - kr;

        // If reflection dominates, use reflection instead
        if (kr > kt)
            return TraceReflection(P, N, incident, targetPoint);

        // First refraction: air -> object
        Vector3 Rinside = Refract(incident, N, airIOR, objectIOR);
        if (Rinside.magnitude < 1e-9f)
            return targetPoint;
        Rinside.Normalize();

        // Cast ray inside object
        Ray insideRay = new Ray(P + Rinside * epsilon, Rinside);
        RaycastHit insideHit;
        if (!deformer.Intersect(insideRay, out insideHit))
            return targetPoint;

        Vector3 Pout = insideHit.point;
        Vector3 Nout = insideHit.normal.normalized;

        if (Vector3.Dot(Rinside, Nout) > 0.0f)
            Nout = -Nout;

        // Second refraction: object -> air
        Vector3 Rout = Refract(Rinside, Nout, objectIOR, airIOR);
        if (Rout.magnitude < 1e-9f)
            return targetPoint;
        Rout.Normalize();

        float dist = Vector3.Distance(targetPoint, Pout);
        return Pout + Rout * dist;
    }

    Vector3 Refract(Vector3 I, Vector3 N, float n1, float n2)
    {
        I = I.normalized;
        N = N.normalized;
        float eta = n1 / n2;
        float cosI = -Vector3.Dot(N, I);
        float sinT2 = eta * eta * (1f - cosI * cosI);
        if (sinT2 > 1f) return Vector3.zero;
        float cosT = Mathf.Sqrt(1f - sinT2);
        return eta * I + (eta * cosI - cosT) * N;
    }

    void FixWinding(Mesh mesh)
    {
        // Swaps indices 1 and 2 of each triangle because deformation changes the winding order.
        int[] triangles = mesh.triangles;
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            int temp = triangles[i + 1];
            triangles[i + 1] = triangles[i + 2];
            triangles[i + 2] = temp;
        }
        mesh.triangles = triangles;
    }
    void OnDestroy()
    {
        if (deformedMesh != null)
        {
            Destroy(deformedMesh);
        }
    }
}