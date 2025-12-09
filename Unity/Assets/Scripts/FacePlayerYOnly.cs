using UnityEngine;

public class FacePlayerYOnly : MonoBehaviour
{
    [Header("Player Head Reference (Camera)")]
    public Transform playerHead;

    [Header("Rotation Settings")]
    public float rotationLerpSpeed = 5f;

    void Update()
    {
        if (playerHead == null) return;

        // Direction toward player, but ignore vertical tilt
        Vector3 dir = playerHead.position - transform.position;
        dir.y = 0f; // keep rotation only on Y axis

        if (dir.sqrMagnitude < 0.0001f) return; // avoid zero-direction errors

        Quaternion targetRot = Quaternion.LookRotation(dir);

        // Smooth rotation
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRot,
            Time.deltaTime * rotationLerpSpeed
        );
    }
}
