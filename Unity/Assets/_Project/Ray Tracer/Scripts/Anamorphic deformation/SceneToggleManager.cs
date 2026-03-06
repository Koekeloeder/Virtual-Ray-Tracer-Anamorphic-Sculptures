using UnityEngine;

/// <summary>
/// Manages toggling between original scene and mirror scene.
/// Uses a single directional light that flips direction.
/// </summary>
public class SceneToggleManager : MonoBehaviour
{
    [Header("Original Scene")]
    [Tooltip("The original mesh (non-deformed) to show/hide")]
    public GameObject originalMeshObject;

    [Header("Mirror Scene")]
    [Tooltip("The deformed mesh to show/hide")]
    public GameObject deformedMeshObject;
    [Tooltip("The mirror object to show/hide")]
    public GameObject mirrorObject;

    [Header("Lighting")]
    [Tooltip("The single directional light that will flip direction")]
    public Light directionalLight;

    public bool showingMirrorScene = false;
    private static SceneToggleManager instance = null;

    /// <summary>
    /// Get the current SceneToggleManager instance.
    /// </summary>
    public static SceneToggleManager Get()
    {
        return instance;
    }

    /// <summary>
    /// Toggle between original scene and mirror scene
    /// </summary>
    /// <param name="showMirrorScene">True to show mirror scene, false to show original scene</param>
    public void ToggleMirrorScene(bool showMirrorScene)
    {
        if (showMirrorScene)
        {
            ShowMirrorScene();
        }
        else
        {
            ShowOriginalScene();
        }
    }

    /// <summary>
    /// Show the mirror scene (hide original scene)
    /// </summary>
    private void ShowMirrorScene()
    {
        // Hide original scene
        if (originalMeshObject != null)
            originalMeshObject.SetActive(false);

        // Show mirror scene
        if (mirrorObject != null)
            mirrorObject.SetActive(true);
        if (deformedMeshObject != null)
            deformedMeshObject.SetActive(true);

        // If we were showing original, flip the light
        if (!showingMirrorScene && directionalLight != null)
        {
            directionalLight.transform.forward = -directionalLight.transform.forward;
        }

        showingMirrorScene = true;
        Debug.Log("Switched to Mirror Scene");
    }

    /// <summary>
    /// Show the original scene (hide mirror scene)
    /// </summary>
    private void ShowOriginalScene()
    {
        // Show original scene
        if (originalMeshObject != null)
            originalMeshObject.SetActive(true);

        // Hide mirror scene
        if (mirrorObject != null)
            mirrorObject.SetActive(false);
        if (deformedMeshObject != null)
            deformedMeshObject.SetActive(false);

        // If we were showing mirror, flip the light back
        if (showingMirrorScene && directionalLight != null)
        {
            directionalLight.transform.forward = -directionalLight.transform.forward;
        }

        showingMirrorScene = false;
        Debug.Log("Switched to Original Scene");
    }

    /// <summary>
    /// Toggle to the opposite scene
    /// </summary>
    public void Toggle()
    {
        ToggleMirrorScene(!showingMirrorScene);
    }

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        // Always start with original scene
        ShowOriginalScene();
    }

    private void Update()
    {
        // Keyboard shortcut: M to toggle
        if (Input.GetKeyDown(KeyCode.M))
        {
            Toggle();
        }
    }
}