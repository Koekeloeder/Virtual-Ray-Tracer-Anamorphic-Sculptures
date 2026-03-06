using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Abstract class describing the medium that deforms the mesh.
public abstract class Deformer : MonoBehaviour
{
    public bool isRefractive = false;
    public float refractiveIndex = 1.5f;

    public abstract bool Intersect(Ray ray, out RaycastHit hit);
    public abstract string GetDeformerType();

    public float getRefractiveIndex()
    {
        return refractiveIndex;
    }

}