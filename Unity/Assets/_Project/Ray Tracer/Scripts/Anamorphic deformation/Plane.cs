using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Plane : Deformer
{
    [Header("Mirror Properties")]
    private Vector3 mirrorNormal = Vector3.up;
    private float mirrorSize = 10f;

    void Awake()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf && mf.sharedMesh)
        {
            Bounds b = mf.sharedMesh.bounds;
            Vector3 scale = transform.lossyScale;
            mirrorSize = Mathf.Max(b.size.x * scale.x, b.size.z * scale.z);
        }
    }

    public override bool Intersect(Ray ray, out RaycastHit hit)
    {
        hit = new RaycastHit();

        Vector3 planePoint = transform.position;
        Vector3 planeNormal = transform.TransformDirection(mirrorNormal).normalized;

        // Ray-plane intersection
        float denom = Vector3.Dot(planeNormal, ray.direction);
        if (Mathf.Abs(denom) < 1e-6f)
        {
            return false; // Ray parallel to plane
        }

        float t = Vector3.Dot(planePoint - ray.origin, planeNormal) / denom;
        if (t < 0)
        {
            return false; // Intersection behind ray origin
        }

        Vector3 hitPoint = ray.origin + ray.direction * t;

        // Check if within mirror bounds (simple square check)
        Vector3 localHit = transform.InverseTransformPoint(hitPoint);
        if (Mathf.Abs(localHit.x) > mirrorSize / 2 || Mathf.Abs(localHit.z) > mirrorSize / 2)
        {
            return false; // Outside mirror bounds
        }

        hit.point = hitPoint;
        hit.normal = planeNormal;
        hit.distance = t;

        return true;
    }

    public override string GetDeformerType()
    {
        return "quad";
    }
}