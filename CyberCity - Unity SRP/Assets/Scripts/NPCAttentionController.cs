using UnityEngine;

public class NPCAttentionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform headBone;
    [SerializeField] private SaliencyScoreCalculator saliencyCalculator;

    [Header("Head Movement Settings")]
    [SerializeField] private float headTurnSpeed = 3.0f;

    private Transform currentTarget;

    private void Update()
    {
        // Ask calculator which object is most salient (and visible)
        currentTarget = saliencyCalculator.GetMostSalientObject();

        if (currentTarget != null && headBone != null)
        {
            RotateHeadTowardsTarget(currentTarget);
        }
        else if (headBone != null)
        {
            // Optional: smoothly reset to forward direction
            ResetHeadRotation();
        }
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
}
