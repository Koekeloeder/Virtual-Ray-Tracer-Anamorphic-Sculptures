using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sphere : Deformer
{

    public override bool Intersect(Ray ray, out RaycastHit hit)
    {

        float radius = .5f * Mathf.Abs(transform.lossyScale.x);
        hit = new RaycastHit();

        Vector3 center = transform.position;
        Vector3 oc = ray.origin - center;

        // Ray-sphere intersection (quadratic formula)
        float a = Vector3.Dot(ray.direction, ray.direction);
        float b = 2.0f * Vector3.Dot(oc, ray.direction);
        float c = Vector3.Dot(oc, oc) - radius * radius;
        float discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
        {
            return false; // No intersection
        }

        float t = (-b - Mathf.Sqrt(discriminant)) / (2.0f * a);
        if (t < 0)
        {
            t = (-b + Mathf.Sqrt(discriminant)) / (2.0f * a);
        }

        if (t < 0)
        {
            return false;
        }

        Vector3 hitPoint = ray.origin + ray.direction * t;
        Vector3 normal = (hitPoint - center).normalized;

        hit.point = hitPoint;
        hit.normal = normal;
        hit.distance = t;

        return true;
    }

    public override string GetDeformerType()
    {
        return isRefractive ? "sphere_lens" : "sphere_mirror";
    }

    void Start()
    {
        GetComponent<Renderer>().material.SetFloat("_Ior", refractiveIndex);
    }
}