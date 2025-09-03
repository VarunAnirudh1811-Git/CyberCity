using UnityEditor;
using UnityEngine;

/// Represents an object that can be attended to in a rule-based visual attention system.
/// Computes bottom-up saliency cues: motion, proximity, color contrast, and luminance.

public class SalientObject : MonoBehaviour
{
   
    [Header("References")]
    private Transform npcEyeTransform;
    [Tooltip("Optional: Assign the material to be analyzed for color/luminance. ")]
    [SerializeField] private Texture2D targetTexture;
    //[SerializeField] private Color backgroundColor;

    [Header("Parameters")]
    [Tooltip("Sigma value for divisive normalization in motion cue.")]
    [SerializeField] private float sigma = 0.1f;
    [Tooltip("Maximum distance considered for proximity cue.")]
    [SerializeField] private float maxDistance = 10f;

    [Header("Debug (Read-Only)")]
    [SerializeField, ReadOnly] private float normalizedMotion;
    [SerializeField, ReadOnly] private float normalizedProximity;
    [SerializeField, ReadOnly] private float normalizedColorContrast;
    [SerializeField, ReadOnly] private float normalizedLuminanceContrast;
    [SerializeField, ReadOnly] private Color backgroundColor;
    [SerializeField, ReadOnly] private int objectID;
    

    // Cached values
    private Vector3 lastPosition;
    private Renderer rend;
    private static int nextID = 0;
    public int ObjectID => objectID;

    private void Awake()
    {
        objectID = nextID++;

        lastPosition = transform.position;

        //if (targetMaterial == null)
        //{
        //    Renderer rend = GetComponent<Renderer>();
        //    if (rend != null && rend.sharedMaterial != null)
        //        targetMaterial = rend.sharedMaterial;
        //}
        rend = GetComponent<Renderer>();

        npcEyeTransform = Camera.main?.transform; // Default to main camera if not set
                        
    }

    private void Update()
    {
        ComputeMotion();
        ComputeProximity();
        ComputeColorAndLuminanceContrast();
    }

    /// Computes motion cue using divisive normalization.
    private void ComputeMotion()
    {
        float displacement = (transform.position - lastPosition).magnitude;
        float speed = displacement / Mathf.Max(Time.deltaTime, 1e-6f);
        normalizedMotion = speed / (speed + sigma);
        lastPosition = transform.position;
    }

    /// Computes proximity cue relative to NPC eye transform. 
    private void ComputeProximity()
    {
        if (npcEyeTransform == null)
        {
            normalizedProximity = 0f;
            return;
        }

        float distance = Vector3.Distance(transform.position, npcEyeTransform.position);
        normalizedProximity = 1f - Mathf.Clamp01(distance / maxDistance);
    }

    /// <summary> Computes average albedo color (from texture or material) and derives color & luminance contrast. </summary>
    private void ComputeColorAndLuminanceContrast()
    {
        backgroundColor = Camera.main != null ? Camera.main.backgroundColor : Color.gray;

        //if (targetMaterial == null)
        //{
        //    normalizedColorContrast = 0f;
        //    normalizedLuminanceContrast = 0f;
        //    return;
        //}

        Color avgColor = rend != null ? rend.material.color : Color.white;

        if (targetTexture != null)
        {
            try
            {
                Color[] pixels = targetTexture.GetPixels();
                if (pixels.Length > 0)
                {
                    avgColor = Color.black;
                    foreach (var c in pixels)
                        avgColor += c;
                    avgColor /= pixels.Length;
                }
            }
            catch
            {
                // Texture not readable → stick with material color
            }
        }

        // --- Color Contrast (Euclidean distance in RGB) ---
        float colorDist = Vector3.Distance(
        new Vector3(avgColor.r, avgColor.g, avgColor.b),
        new Vector3(backgroundColor.r, backgroundColor.g, backgroundColor.b)
        );
        normalizedColorContrast = Mathf.Clamp01(colorDist);

        // --- Luminance Contrast ---
        float objLum = 0.2126f * avgColor.r + 0.7152f * avgColor.g + 0.0722f * avgColor.b;
        float bgLum = 0.2126f * backgroundColor.r + 0.7152f * backgroundColor.g + 0.0722f * backgroundColor.b;

        float lumContrast = Mathf.Abs(objLum - bgLum);
        normalizedLuminanceContrast = Mathf.Clamp01(lumContrast);
    }

    /// <summary> Expose raw normalized cues. </summary>
    public float NormalizedMotion => normalizedMotion;
    public float NormalizedProximity => normalizedProximity;
    public float NormalizedColorContrast => normalizedColorContrast;
    public float NormalizedLuminanceContrast => normalizedLuminanceContrast;

    /// <summary> Returns feature vector (motion, proximity, color, luminance, saliencyScore). </summary>
    public float[] GetFeatureVector()
    {
        return new float[]
        {
            normalizedMotion,
            normalizedProximity,
            normalizedColorContrast,
            normalizedLuminanceContrast            
        };
    }
}

#region --- ReadOnly Inspector Support ---
public class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;
        EditorGUI.PropertyField(position, property, label, true);
        GUI.enabled = true;
    }
}
#endif
#endregion
