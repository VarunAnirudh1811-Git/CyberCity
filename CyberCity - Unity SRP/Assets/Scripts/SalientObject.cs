using Unity.VisualScripting;
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
    [SerializeField] private Collider objectCollider;
    //[SerializeField] private Color backgroundColor;

    [Header("Parameters")]
    [Tooltip("Sigma value for divisive normalization in motion cue.")]
    [SerializeField] private float sigma = 10f;
    [SerializeField] private float angularSigma = 180f; // tune this value: at 180 deg/sec, output = 0.5

    [Header("Debug (Read-Only)")]
    [SerializeField, ReadOnly] private float normalizedMotion;
    [SerializeField, ReadOnly] private float sizeByProximity;
    [SerializeField, ReadOnly] private float normalizedColorContrast;
    [SerializeField, ReadOnly] private float normalizedLuminanceContrast;
    [SerializeField, ReadOnly] private float normalizedAngularVelocity;
    [SerializeField, ReadOnly] private Color backgroundColor;
    [SerializeField, ReadOnly] private int objectID;
    [SerializeField, ReadOnly] private Color avgColorDebug;


    // Cached values
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Renderer rend;
    private static int nextID = 0;
    private float rootThree;
    public int ObjectID => objectID;

    private void Awake()
    {
        objectID            = nextID++;
        rootThree           = Mathf.Sqrt(3f); // For color contrast normalization

        lastPosition        = transform.position;

        rend                = GetComponent<Renderer>();

        npcEyeTransform     = Camera.main?.transform; // Default to main camera if not set
                        
    }

    private void Update()
    {
        ComputeMotion();
        ComputeSizeByProximity();
        ComputeColorAndLuminanceContrast();
        ComputeAngularVelocity();
    }
        
    private void ComputeMotion()
    {
        float displacement  = (transform.position - lastPosition).magnitude;
        float speed         = displacement / Mathf.Max(Time.deltaTime, 1e-6f);

        normalizedMotion    = speed / (speed + sigma);

        lastPosition        = transform.position;
    }
    private void ComputeAngularVelocity()
    {
        Quaternion deltaRotation        = transform.rotation * Quaternion.Inverse(lastRotation);
        deltaRotation.ToAngleAxis(out float angle, out _);
        float angularSpeed              = angle / Mathf.Max(Time.deltaTime, 1e-6f);
        
        normalizedAngularVelocity       = angularSpeed / (angularSpeed + angularSigma);
        lastRotation                    = transform.rotation;
    }        
    private void ComputeSizeByProximity()
    {
        if (npcEyeTransform == null || objectCollider == null)
        {
            sizeByProximity = 0f;            
            return;
        }
        float size          = Mathf.Max(objectCollider.bounds.size.x, objectCollider.bounds.size.y, objectCollider.bounds.size.z);
        float distance      = Vector3.Distance(transform.position, npcEyeTransform.position);
        
        sizeByProximity     = size/ (size + distance);
    }
    private void ComputeColorAndLuminanceContrast()
    {
        backgroundColor     = Camera.main != null ? Camera.main.backgroundColor : Color.gray;

        Color avgColor      = Color.white;

        if (targetTexture != null)
        {
            try
            {
                Color32[] texColors = targetTexture.GetPixels32();
                int total = texColors.Length;
                if (total == 0) avgColor = Color.white;

                float r = 0f, g = 0f, b = 0f;

                for (int i = 0; i < total; i++)
                {
                    r += texColors[i].r;
                    g += texColors[i].g;
                    b += texColors[i].b;
                }

                // Divide by total and normalize to [0..1]
                float rf = r / (255f * total);
                float gf = g / (255f * total);
                float bf = b / (255f * total);

                avgColor = new Color(rf, gf, bf, 1f); // full alpha
            }
            catch
            {
                // Not readable, fallback
                avgColor = Color.white;
            }
        }

        // --- Color Contrast (Euclidean distance in RGB) ---
        float colorDist = Vector3.Distance (
        new Vector3(avgColor.r, avgColor.g, avgColor.b),
        new Vector3(backgroundColor.r, backgroundColor.g, backgroundColor.b) );
        normalizedColorContrast = Mathf.Clamp01(colorDist / rootThree);

        // --- Luminance Contrast ---
        float objLum = 0.2126f * avgColor.r + 0.7152f * avgColor.g + 0.0722f * avgColor.b;
        float bgLum = 0.2126f * backgroundColor.r + 0.7152f * backgroundColor.g + 0.0722f * backgroundColor.b;

        float lumContrast = Mathf.Abs(objLum - bgLum);
        normalizedLuminanceContrast = lumContrast;
    }

    /// <summary> Expose raw normalized cues. </summary>
    public float NormalizedMotion => normalizedMotion;
    public float SizeByProximity => sizeByProximity;
    public float NormalizedAngularVelocity => normalizedAngularVelocity;
    public float NormalizedColorContrast => normalizedColorContrast;
    public float NormalizedLuminanceContrast => normalizedLuminanceContrast;

    /// <summary> Returns feature vector (motion, proximity, color, luminance, saliencyScore). </summary>
    public float[] GetFeatureVector()
    {
        return new float[]
        {
            normalizedMotion,
            sizeByProximity,
            normalizedAngularVelocity,
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
