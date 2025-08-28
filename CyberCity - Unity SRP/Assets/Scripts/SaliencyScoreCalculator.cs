using System;
using System.Collections.Generic;
using UnityEngine;

public class SaliencyScoreCalculator : MonoBehaviour
{
    [Header("Empirical Equation Weights")]
    [SerializeField] [Range(0f, 1f)] private float motionWeight = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float proximityWeight = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float colorWeight = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float luminanceWeight = 0.25f;

    [Header("Debug (Read-Only)")]
    [SerializeField] private Transform mostSalientObject;
    [SerializeField] private float mostSalientScore;

    private List<SalientObject> salientObjects = new List<SalientObject>();

    private void Start()
    {
        // Cache all SalientObjects in scene
        salientObjects.AddRange(FindObjectsByType<SalientObject>(FindObjectsSortMode.None));
    }

    private void Update()
    {
        CalculateSaliencyScores();
    }

    /// <summary>
    /// Main saliency calculation loop – finds the most salient object.
    /// </summary>
    private void CalculateSaliencyScores()
    {
        float bestScore = -1f;
        Transform bestTransform = null;

        foreach (var obj in salientObjects)
        {
            // Ensure renderer is visible
            Renderer rend = obj.GetComponent<Renderer>();
            if (rend != null && !rend.isVisible)
                continue;

            float motion = obj.NormalizedMotion;
            float proximity = obj.NormalizedProximity;
            float color = obj.NormalizedColorContrast;
            float luminance = obj.NormalizedLuminanceContrast;

            // Call the score function
            float saliencyScore = ComputeScore(motion, proximity, color, luminance);

            // Track most salient
            if (saliencyScore > bestScore)
            {
                bestScore = saliencyScore;
                bestTransform = obj.transform;
            }
        }

        mostSalientScore = bestScore;
        mostSalientObject = bestTransform;
    }

    /// Computes saliency score from four normalized cues.
    
    public float ComputeScore(float motion, float proximity, float color, float luminance)
    {
        float score =
            (motionWeight * motion) +
            (proximityWeight * proximity) +
            (colorWeight * color) +
            (luminanceWeight * luminance);

        return Mathf.Clamp01(score);
    }

     /// Get position of most salient object (null if none found).
    public Vector3? GetMostSalientPosition()
    {
        return mostSalientObject != null ? mostSalientObject.position : (Vector3?)null;
    }

    /// Get reference to the most salient object.
    public Transform GetMostSalientObject()
    {
        return mostSalientObject;
    }
}
