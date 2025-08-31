using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class SaliencyScoreCalculator : MonoBehaviour
{
    [Header("Empirical Equation Weights")]
    [SerializeField] [Range(0f, 1f)] private float motionWeight = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float proximityWeight = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float colorWeight = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float luminanceWeight = 0.25f;
   
    [Header("Debug (Read-Only)")]
    [SerializeField, ReadOnly] private Transform mostSalientObject;
    [SerializeField, ReadOnly] private float mostSalientScore;

    private List<SalientObject> salientObjects = new List<SalientObject>();
    private string csvPath;
    
    private void Start()
    {
        csvPath = Path.Combine(Application.persistentDataPath, "SaliencyLog.csv");
        salientObjects.AddRange(FindObjectsByType<SalientObject>(FindObjectsSortMode.None));

        Debug.Log("Logging saliency data to: " + csvPath);
    }

    private void Update()
    {
        CalculateSaliencyScores();
    }

    
    /// Main saliency calculation loop – finds the most salient object.
    
    private void CalculateSaliencyScores()
    {
        float bestScore = -1f;
        Transform bestTransform = null;
        string bestObjectName = "None"; // Stores the name of most salient object
        int frame = Time.frameCount;

        foreach (var obj in salientObjects)
        {
            if (obj == null)
                continue;
            // Ensure renderer is visible
            Renderer rend = obj.GetComponentInChildren<Renderer>();
            
            float motion = obj.NormalizedMotion;
            float proximity = obj.NormalizedProximity;
            float color = obj.NormalizedColorContrast;
            float luminance = obj.NormalizedLuminanceContrast;

            // Call the score function
            float saliencyScore = ComputeScore(motion, proximity, color, luminance);

            // Track most salient
            if (rend != null && rend.isVisible && saliencyScore > bestScore)
            {
                bestScore = saliencyScore;
                bestTransform = obj.transform;
                bestObjectName = obj.name;
            }

            LogObjectData(frame, obj.name, obj.transform.position, motion, proximity, color, luminance, bestObjectName);
        }

        mostSalientScore = bestScore;
        mostSalientObject = bestTransform;
    }

    private void LogObjectData(int frame, string name, Vector3 position, float motion, float proximity, float color, float luminance, string bestObjectName)
    {
        int isBest = (name == bestObjectName) ? 1 : 0;

        string logEntry = $"{frame},{name},{name}+'.X.'+{position.x:F2},{position.y:F2},{position.z:F2},{motion:F4},{proximity:F4},{color:F4},{luminance:F4},{bestObjectName}\n";
        
        WriteCSVLine(logEntry);
    }

    private void WriteCSVLine(string line)
    {
        try
        {
            using (StreamWriter sw = new StreamWriter(csvPath, true, Encoding.UTF8))
            {
                sw.Write(line);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("CSV Write Error: " + e.Message);
        }
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
