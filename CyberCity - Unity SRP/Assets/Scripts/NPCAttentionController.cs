using System;
using System.IO;
using System.Text;
using UnityEngine;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using static UnityEditor.PlayerSettings;

public class NPCAttentionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform headBone;
    [SerializeField] private SaliencyScoreCalculator saliencyCalculator;

    [Header("Head Movement Settings")]
    [SerializeField] private float headTurnSpeed = 3.0f;
    [SerializeField] private float maxHorizontalAngle = 80.0f;
    [SerializeField] private float maxVerticalAngle = 45.0f;

    private Transform currentTarget;
    private string csvPath;

    private void Start()
    {
        csvPath = Path.Combine(Application.persistentDataPath, "SaliencyLogOutput.csv");

        if (!File.Exists(csvPath))
        {
            WriteCSVLine("Frame,ForwardX,ForwardY,ForwardZ");
        }
    }


    private void Update()
    {
        // Ask calculator which object is most salient (and visible)
        currentTarget = saliencyCalculator.GetMostSalientObject();

        if (currentTarget != null && headBone != null)
        {
            if (IsTargetWithinView(currentTarget.position))
            {
                RotateHeadTowardsTarget(currentTarget);
            }
            else
            {
                // Optional: smoothly reset to forward direction if target out of view
                ResetHeadRotation();
            }
        }
        else if (headBone != null)
        {
            // Optional: smoothly reset to forward direction
            ResetHeadRotation();
        }

        int frame = Time.frameCount;
        // Log NPC transform.forward each frame
        LogNPCForward(frame, headBone);
    }
        
    private void RotateHeadTowardsTarget(Transform target)
    {
        Vector3 direction = (target.position - headBone.position).normalized;

        // LookRotation assumes head forward = +Z
        Quaternion targetRotation = Quaternion.LookRotation(direction);//Character is not positioned appropriately

        headBone.rotation = Quaternion.Slerp(
            headBone.rotation,
            targetRotation,
            Time.deltaTime * headTurnSpeed
        );
    }

    private void ResetHeadRotation()
    {
        Quaternion forwardRotation = transform.rotation; // NPC body forward
        headBone.rotation = Quaternion.Slerp(
            headBone.rotation,
            forwardRotation,
            Time.deltaTime * headTurnSpeed
        );
    }

    private bool IsTargetWithinView(Vector3 targetPosition)
    {
        Vector3 toTarget = (targetPosition - headBone.position).normalized;
        Vector3 forward = transform.forward;

        // Horizontal angle (ignore Y difference)
        float horizontalAngle = Vector3.Angle(forward, new Vector3(toTarget.x, 0, toTarget.z));
        if (horizontalAngle > maxHorizontalAngle) return false;

        // Vertical angle (difference between forward and target direction, minus horizontal component)
        float verticalAngle = Vector3.Angle(forward, toTarget) - horizontalAngle;
        if (Mathf.Abs(verticalAngle) > maxVerticalAngle) return false;

        return true;
    }
    private void LogNPCForward(int frame, Transform headBone)
    {
        Vector3 forward = headBone.transform.forward;
        string line = $"{frame},{forward.x:F2},{forward.y:F2},{forward.z:F2}";

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
}
