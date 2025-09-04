using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;

public class SaliencyScoreCalculator : MonoBehaviour
{
    [Header("Empirical Equation Weights")]
    [SerializeField] [Range(0f, 1f)] private float motionWeight = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float proximityWeight = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float colorWeight = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float luminanceWeight = 0.25f;
    [SerializeField] private float attentionRange = 20f; // max distance NPC can "see"

    [Header("Debug (Read-Only)")]
    [SerializeField, ReadOnly] private Transform mostSalientObject;
    [SerializeField, ReadOnly] private float mostSalientScore;

    private List<SalientObject> salientObjects = new List<SalientObject>();
    private string csvPath;
    public Transform npcEyeTransform;

    private void Start()
    {
        csvPath = Path.Combine(Application.persistentDataPath, "SaliencyLog.csv");
        salientObjects.AddRange(FindObjectsByType<SalientObject>(FindObjectsSortMode.None));
        npcEyeTransform = Camera.main?.transform; // Default to main camera if not set

        Debug.Log("Logging saliency data to: " + csvPath);
        WriteCSVLine("Frame,ObjectID,PosX,PosY,PosZ,Motion,Proximity,Color,Luminance,Score,IsBest");
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
        int frame = Time.frameCount;

        foreach (var obj in salientObjects)
        {
            if (obj == null) continue;
                        
            float motion = obj.NormalizedMotion;
            float proximity = obj.NormalizedProximity;
            float color = obj.NormalizedColorContrast;
            float luminance = obj.NormalizedLuminanceContrast;

            // Call the score function
            float saliencyScore = ComputeScore(motion, proximity, color, luminance);

            float distance = Vector3.Distance(npcEyeTransform.position, obj.transform.position);
            // Track most salient
            Renderer rend = obj.GetComponentInChildren<Renderer>();
            if (rend != null && rend.isVisible && saliencyScore > bestScore)
            {
                if (distance <= attentionRange)   // 👈 Only accept if within range
                {
                    bestScore = saliencyScore;
                    bestTransform = obj.transform;
                }
            }
            bool isBest = (bestTransform == obj.transform);
            LogObjectData(frame, obj, motion, proximity, color, luminance, isBest);
        }

        mostSalientScore = bestScore;
        mostSalientObject = bestTransform;
    }
       
    private void LogObjectData(int frame, SalientObject obj, float motion, float proximity, float color, float luminance, bool isBest)
    {
        int isLooking = isBest ? 1 : 0;
        Vector3 pos = obj.transform.position;
        string line = $"{frame},{obj.ObjectID},{pos.x:F2},{pos.y:F2},{pos.z:F2},{motion:F4},{proximity:F4},{color:F4},{luminance:F4}";
        WriteCSVLine(line);
    }

    private void WriteCSVLine(string line)
    {
        try
        {
            using (StreamWriter sw = new StreamWriter(csvPath, true, Encoding.UTF8))
            {
                sw.WriteLine(line);
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

    public Vector3? GetMostSalientPosition()
    {
        return mostSalientObject != null ? mostSalientObject.position : (Vector3?)null;
    }
    public Transform GetMostSalientObject()
    {
        return mostSalientObject;
    }

    private bool IsVisibleToNPC(Transform target)
    {
        if (npcEyeTransform == null) return false;

        Vector3 dir = (target.position - npcEyeTransform.position).normalized;
        float dist = Vector3.Distance(npcEyeTransform.position, target.position);

        if (Physics.Raycast(npcEyeTransform.position, dir, out RaycastHit hit, dist))
        {
            return hit.transform == target; // Only visible if first hit is the target
        }

        return false;
    }
}
