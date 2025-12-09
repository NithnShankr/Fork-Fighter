using UnityEngine;

public class PlaneFollowPlayer : MonoBehaviour
{
    [Header("Player Head Reference (Camera)")]
    public Transform playerHead;

    [Header("Settings")]
    public float distanceInFront = 1.5f;
    public float heightOffset = 0.0f;
    public float positionLerpSpeed = 5f;
    public float rotationLerpSpeed = 5f;

    void Update()
    {
        if (playerHead == null) return;

        // Target position: in front of head
        Vector3 targetPos = playerHead.position +
                            (playerHead.forward * distanceInFront) +
                            new Vector3(0, heightOffset, 0);

        // Move smoothly
        transform.position = Vector3.Lerp(
            transform.position,
            targetPos,
            Time.deltaTime * positionLerpSpeed
        );

        // Target rotation: face the player
        Quaternion targetRot = Quaternion.LookRotation(
            playerHead.position - transform.position
        );

        // Rotate smoothly
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRot,
            Time.deltaTime * rotationLerpSpeed
        );
    }
}
