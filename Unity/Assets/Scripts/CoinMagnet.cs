using UnityEngine;

public class CoinMagnet : MonoBehaviour
{
    [Header("Animation Settings")]
    public float delayBeforeMove = 0.5f;       // Time before flying
    public float moveDuration = 0.5f;          // How long movement lasts
    public float destroyDelay = 0.5f;        // Delay before destroying

    [Header("References")]
    public Transform forkCenter3D;
    public QuestCameraBridge questCameraBridge;  // <--- New reference

    private Vector3 startPosition;
    private bool isMoving = false;
    private float moveTimer = 0f;

    void Start()
    {
        startPosition = transform.position;
        Invoke(nameof(BeginMovement), delayBeforeMove);
    }

    void BeginMovement()
    {
        if (forkCenter3D == null)
        {
            Debug.LogWarning("CoinFlyToFork: forkCenter3D is not assigned.");
            return;
        }

        isMoving = true;
        moveTimer = 0f;
    }

    void Update()
    {
        if (!isMoving || forkCenter3D == null)
            return;

        moveTimer += Time.deltaTime;
        float t = Mathf.Clamp01(moveTimer / moveDuration);

        // Ease-in (starts slow, speeds up)
        float eased = t * t;

        transform.position = Vector3.Lerp(startPosition, forkCenter3D.position, eased);

        // Reached the fork
        if (t >= 1f)
        {
            isMoving = false;
            Invoke(nameof(FinishAndNotify), destroyDelay);
        }
    }

    void FinishAndNotify()
    {
        // Notify QuestCameraBridge
        if (questCameraBridge != null)
        {
            questCameraBridge.OnCoinCollected();
        }

        Destroy(gameObject);
    }
}
