using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cylinder : Deformer
{
    private float radius;
    private float height;

    public override bool Intersect(Ray ray, out RaycastHit hit)
    {
        hit = new RaycastHit();

        Ray localRay = new Ray(
            transform.InverseTransformPoint(ray.origin),
            transform.InverseTransformDirection(ray.direction)
        );

        Vector3 o = localRay.origin;
        Vector3 d = localRay.direction;

        float a = d.x * d.x + d.z * d.z;
        if (Mathf.Abs(a) < 1e-6f)
        {
            return false;
        }

        float b = 2f * (o.x * d.x + o.z * d.z);
        float c = o.x * o.x + o.z * o.z - radius * radius;

        float disc = b * b - 4f * a * c;
        if (disc < 0f)
        {
            return false;
        }

         float sqrtD = Mathf.Sqrt(disc);
        float t0 = (-b - sqrtD) / (2f * a);
        float t1 = (-b + sqrtD) / (2f * a);

        if (t0 > t1) (t0, t1) = (t1, t0);

        float t = -1f;

        if (t0 > 0f)
        {
            float y = o.y + t0 * d.y;
            if (y >= -height / 2f && y <= height / 2f)
                t = t0;
        }

        if (t < 0f && t1 > 0f)
        {
            float y = o.y + t1 * d.y;
            if (y >= -height / 2f && y <= height / 2f)
                t = t1;
        }

        if (t < 0f)
            return false;

        Vector3 localHit = o + t * d;

        hit.point = transform.TransformPoint(localHit);
        Vector3 localNormal = new Vector3(localHit.x, 0f, localHit.z).normalized;
        hit.normal = transform.TransformDirection(localNormal).normalized;
        hit.distance = t * ray.direction.magnitude;

        return true;
    }

    public override string GetDeformerType()
    {
        return "cylinder";
    }

    void Awake()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (!mf || !mf.sharedMesh)
        {
            Debug.LogError("Cylinder requires a MeshFilter with a mesh.");
            return;
        }

        Bounds b = mf.sharedMesh.bounds;

        // Assumes cylinder aligned to Y axis
        radius = Mathf.Max(b.extents.x, b.extents.z);
        height = b.size.y;
    }
}
