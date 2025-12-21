using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Deformer : MonoBehaviour
{
    public bool isRefractive = false;
    public float refractiveIndex = 1.5f;

    public abstract bool Intersect(Ray ray, out RaycastHit hit);
    public abstract string GetDeformerType();
}
