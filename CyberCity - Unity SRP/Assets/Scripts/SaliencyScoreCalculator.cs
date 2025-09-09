using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;

public class SaliencyScoreCalculator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera referenceCamera;
    [SerializeField] private float attentionRange = 50f; // max distance NPC can "see"

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
        WriteCSVLine("Frame,ObjectID,PosX,PosY,PosZ,Motion,Proximity,Color,Luminance,IsBest");
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

        // Gather lists of cues for adaptive weight calculation
        List<float> motions = new List<float>();
        List<float> proximities = new List<float>();
        List<float> colors = new List<float>();
        List<float> luminances = new List<float>();
        List<float> angulars = new List<float>();

        foreach (var obj in salientObjects)
        {
            if (obj == null) continue;
            Renderer rend = obj.GetComponentInChildren<Renderer>();
            if (rend == null || !IsVisibleToCamera(rend, referenceCamera, obj)) continue;

            motions.Add(obj.NormalizedMotion);
            proximities.Add(obj.SizeByProximity);
            colors.Add(obj.NormalizedColorContrast);
            luminances.Add(obj.NormalizedLuminanceContrast);
            angulars.Add(obj.NormalizedAngularVelocity);
        }

        // Compute spreads
        float spreadM = ComputeSpread(motions);
        float spreadP = ComputeSpread(proximities);
        float spreadC = ComputeSpread(colors);
        float spreadL = ComputeSpread(luminances);
        float spreadA = ComputeSpread(angulars);

        float totalSpread = spreadM + spreadP + spreadC + spreadL + spreadA + 1e-6f;

        // Normalize to adaptive weights
        float wM = spreadM / totalSpread;
        float wP = spreadP / totalSpread;
        float wC = spreadC / totalSpread;
        float wL = spreadL / totalSpread;
        float wA = spreadA / totalSpread;

        #if UNITY_EDITOR
        Debug.Log($"Adaptive Weights → M:{wM:F2}, A:{wA:F2}, P:{wP:F2}, C:{wC:F2}, L:{wL:F2}");
        #endif

        // Now compute per-object scores with adaptive weights
        foreach (var obj in salientObjects)
        {
            if (obj == null) continue;
            Renderer rend = obj.GetComponentInChildren<Renderer>();
            if (rend == null || !IsVisibleToCamera(rend, referenceCamera, obj)) continue;

            float motion = obj.NormalizedMotion;
            float proximity = obj.SizeByProximity;
            float color = obj.NormalizedColorContrast;
            float luminance = obj.NormalizedLuminanceContrast;
            float angularVelocity = obj.NormalizedAngularVelocity;

            float saliencyScore = ComputeScore(motion, proximity, color, luminance, angularVelocity,
                                               wM, wP, wC, wL, wA);

            if (saliencyScore > bestScore)
            {
                bestScore = saliencyScore;
                bestTransform = obj.transform;
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
        string line = $"{frame},{obj.ObjectID},{pos.x:F2},{pos.y:F2},{pos.z:F2},{motion:F4},{proximity:F4},{color:F4},{luminance:F4},{isLooking}";
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
    public float ComputeScore(float motion, float proximity, float color, float luminance, float angularVelocity,
                          float wM, float wP, float wC, float wL, float wA)
    {
        float score = ((wM * motion) +
                      (wP * proximity) +
                      (wC * color) +
                      (wL * luminance) +
                      (wA * angularVelocity)) / 5; // This is to keep it in [0,1] range

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
        
    private bool IsVisibleToCamera(Renderer rend, Camera cam, SalientObject obj)
    {
        if (cam == null) return false;
        Plane[] planes      = GeometryUtility.CalculateFrustumPlanes(cam);
        float distance      = Vector3.Distance(npcEyeTransform.position, obj.transform.position);
        bool inView         = ((GeometryUtility.TestPlanesAABB(planes, rend.bounds)) && (distance <= attentionRange));

        return inView;
    }

    private float ComputeSpread(List<float> values)
    {
        if (values.Count == 0) return 0f;
        values.Sort();
        int p10 = Mathf.FloorToInt(0.1f * (values.Count - 1));
        int p90 = Mathf.FloorToInt(0.9f * (values.Count - 1));
        return Mathf.Max(0f, values[p90] - values[p10]);
    }
}
