using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class BorderlandsVisualManager : MonoBehaviour
{
    [Header("Sobel Outline Settings")]
    [Tooltip("Outline thickness in screen pixels.")]
    [Range(0.5f, 5.0f)]
    public float outlineThickness = 1.5f;

    [Tooltip("Outline color.")]
    public Color outlineColor = Color.black;

    [Tooltip("Depth sensitivity. Lower values detect more outer edges.")]
    [Range(0.01f, 0.5f)]
    public float depthThreshold = 0.05f;

    [Tooltip("Normal sensitivity. Lower values detect more inner creases/edges.")]
    [Range(0.05f, 1.0f)]
    public float normalThreshold = 0.4f;

    private GameObject customPassObj;
    private Material outlineMaterial;

    private void Awake()
    {
        InitializeEffects();
    }

    private void OnDestroy()
    {
        CleanUpOldVolumes();
    }

    private void Update()
    {
        // Allow modifying outline parameters in real-time
        UpdateMaterialProperties();
    }

    private void OnValidate()
    {
        // Allow modifying settings in real-time inside the inspector
        UpdateMaterialProperties();
    }

    private void InitializeEffects()
    {
        // 1. Clean up old instances first to prevent duplicates
        CleanUpOldVolumes();

        // 2. Find and load the custom Sobel outline shader
        Shader outlineShader = Shader.Find("FullScreen/BorderlandsOutline");
        if (outlineShader == null)
        {
            Debug.LogError("BorderlandsOutline shader (FullScreen/BorderlandsOutline) not found! Ensure the shader is placed inside Assets and has no compilation errors.");
            return;
        }

        outlineMaterial = new Material(outlineShader);
        UpdateMaterialProperties();

        // 3. Create Custom Pass Volume for outlines
        customPassObj = new GameObject("BorderlandsCustomPassVolume");
        customPassObj.transform.parent = this.transform;
        
        CustomPassVolume customPassVolume = customPassObj.AddComponent<CustomPassVolume>();
        customPassVolume.injectionPoint = CustomPassInjectionPoint.AfterOpaqueDepthAndNormal;
        customPassVolume.isGlobal = true;

        FullScreenCustomPass outlinePass = new FullScreenCustomPass
        {
            name = "Borderlands Sobel Outline",
            fullscreenPassMaterial = outlineMaterial
        };
        customPassVolume.customPasses.Add(outlinePass);

        Debug.Log("Borderlands Outlines successfully initialized programmatically!");
    }

    private void UpdateMaterialProperties()
    {
        if (outlineMaterial != null)
        {
            outlineMaterial.SetFloat("_OutlineThickness", outlineThickness);
            outlineMaterial.SetColor("_OutlineColor", outlineColor);
            outlineMaterial.SetFloat("_DepthThreshold", depthThreshold);
            outlineMaterial.SetFloat("_NormalThreshold", normalThreshold);
        }
    }

    private void CleanUpOldVolumes()
    {
        // Find existing components in child gameobjects and clean them up
        foreach (Transform child in transform)
        {
            if (child.name == "BorderlandsCustomPassVolume")
            {
                DestroyImmediate(child.gameObject);
            }
        }

        if (outlineMaterial != null)
        {
            DestroyImmediate(outlineMaterial);
            outlineMaterial = null;
        }
    }
}
