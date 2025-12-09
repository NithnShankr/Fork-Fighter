using UnityEngine;
using Meta.XR;
using Meta.XR.MRUtilityKit;

public class CannonController : MonoBehaviour
{
    [Header("Bridge")]
public QuestCameraBridge questBridge;   // reference to call back into bridge


    [Header("Fade Body On Jetpack")]
public URPAlphaReducer bodyFader;


    [Header("Character Selection Input")]
    public CharacterSelection characterSelection;

    [Header("References")]
    public Transform horizontalPivot;
    public Transform verticalPivot;
    public Transform projectileSpawnPoint;
    public GameObject projectilePrefab;
    public Transform userCamera;

    [Header("Green Character")]
    public Transform greenCharacter;
    public float headLookSpeed = 5f;

    [Header("Colliders (Triggers)")]
    public Collider leftCollider;
    public Collider rightCollider;
    public Collider bottomCollider;

    [Header("Unit Movement")]
    public float moveSpeed = 1.5f;
    private bool stopMovement = false;

    [Header("Wheel Rotation")]
    public Transform wheelFrontLeft;
    public Transform wheelFrontRight;
    public Transform wheelBackLeft;
    public Transform wheelBackRight;
    public float wheelRotationSpeed = 300f;

    [Header("Rotation Timing")]
    public bool rotationEnabled = false;

    [Header("Jetpack Settings")]
    public float jetpackAcceleration = 20f;
    public float jetpackMaxSpeed = 10f;
    public float stopDistanceFromCamera = 0.3f;

    private Rigidbody greenRb;
    private bool jetpackActive = false;
    private bool jetpackFinished = false;

    [Header("Jetpack Delay After Trigger")]
    public float jetpackDelaySeconds = 1f;

    private bool triggerActivated = false;
    private float triggerTime = 0f;

    [Header("Shooting Settings")]
    public float shootForce = 15f;
    public float fireInterval = 2f;
    private float fireTimer = 0f;
    private bool stopShooting = false;

    [Header("Surface Follow Settings")]
    public EnvironmentRaycastManager raycastManager;
    public float carHeightOffset = 0.01f;

    [Header("Explosion")]
    public Transform explosionPrefab;   // << NEW field passed from QuestCameraBridge

    private bool autoTriggerUsed = false;
    private float startTime;

    private void Start()
    {
        startTime = Time.time;
         fireTimer = fireInterval - 1f;
    }

    private void LateUpdate()
    {
        if (userCamera == null) return;

        if (!triggerActivated && !autoTriggerUsed)
        {
            if (Time.time >= startTime + 15f)
            {
                autoTriggerUsed = true;
                ForceTrigger();
            }
        }

        if (!stopMovement)
        {
            MoveUnitForwardHorizontally();
            RotateWheels();
        }

        AimAtUser();

        if (!stopShooting)
            HandleShooting();

        HandleJetpackDelay();

        if (rotationEnabled && !jetpackFinished)
            RotateCharacterTowardCamera();

        PerformSurfaceRaycast();
    }

    private void RotateWheels()
    {
        float rot = wheelRotationSpeed * Time.deltaTime;

        if (wheelFrontLeft) wheelFrontLeft.Rotate(rot, 0, 0, Space.Self);
        if (wheelFrontRight) wheelFrontRight.Rotate(rot, 0, 0, Space.Self);
        if (wheelBackLeft) wheelBackLeft.Rotate(rot, 0, 0, Space.Self);
        if (wheelBackRight) wheelBackRight.Rotate(rot, 0, 0, Space.Self);
    }

    private void PerformSurfaceRaycast()
    {
        if (raycastManager == null) return;
        if (!EnvironmentRaycastManager.IsSupported) return;

        Ray ray = new Ray(transform.position + Vector3.up * 0.25f, Vector3.down);

        if (raycastManager.Raycast(ray, out var hit) &&
            hit.status == EnvironmentRaycastHitStatus.Hit)
        {
            Vector3 targetPos = hit.point + hit.normal * carHeightOffset;

            transform.position = Vector3.Lerp(
                transform.position, targetPos, Time.deltaTime * 10f);

            Vector3 fwd = transform.forward;
            Vector3 projectedForward = Vector3.ProjectOnPlane(fwd, hit.normal);
            transform.rotation = Quaternion.LookRotation(projectedForward, hit.normal);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == leftCollider || other == rightCollider || other == bottomCollider)
        {
            ForceTrigger();
        }
    }

    private void ForceTrigger()
    {
        if (triggerActivated) return;

        triggerActivated = true;
        triggerTime = Time.time;

        stopMovement = true;
        rotationEnabled = true;
    }

    private void HandleJetpackDelay()
    {
         
        if (!triggerActivated || jetpackActive || jetpackFinished) return;

        if (Time.time >= triggerTime + jetpackDelaySeconds)
            StartJetpack();
    }

    private void StartJetpack()
    {
        jetpackActive = true;
        stopShooting = true;

         if (bodyFader != null)
        bodyFader.enabled = true;

        // Unparent the green character
        greenCharacter.SetParent(null);

        // ---------------------------------------------------------
        // NEW: shrink the enemy body & spawn explosion effect
        // ---------------------------------------------------------
        transform.localScale = Vector3.zero;

        // if (explosionPrefab)
        // {
        //     Transform explosion = Instantiate(
        //         explosionPrefab,
        //         transform.position,   // explosion at cannon's old position
        //         Quaternion.identity
        //     );
        //     explosion.SetParent(null);

        //     Transform tiny = explosion.Find("TinyExplosion");
        //     if (tiny) tiny.gameObject.SetActive(true);
        // }
        // ---------------------------------------------------------

        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
            Destroy(box);

        greenRb = greenCharacter.GetComponent<Rigidbody>();
        if (greenRb == null)
            greenRb = greenCharacter.gameObject.AddComponent<Rigidbody>();

        greenRb.useGravity = false;
        greenRb.linearDamping = 0;

        
    }

    private void FixedUpdate()
    {
        if (!jetpackActive || jetpackFinished) return;

        Vector3 dir = userCamera.position - greenCharacter.position;
        float dist = dir.magnitude;

        if (dist <= (stopDistanceFromCamera/4f))
        {
            FinishJetpackFlight();
            return;
        }

        dir.Normalize();
        greenRb.AddForce(dir * jetpackAcceleration, ForceMode.Acceleration);

        if (greenRb.linearVelocity.magnitude > jetpackMaxSpeed)
        {
            greenRb.linearVelocity =
                greenRb.linearVelocity.normalized * jetpackMaxSpeed;
        }
    }

    private void FinishJetpackFlight()
    {
        jetpackActive = false;
        jetpackFinished = true;

        greenRb.linearVelocity = Vector3.zero;

        rotationEnabled = false;

         Transform impact = greenCharacter.Find("Impact");
    if (impact != null)
    {
        impact.gameObject.SetActive(true);

        // Optional: unparent if needed
        impact.SetParent(null);

        // Optional: snap to exact spot
        // impact.position = greenCharacter.position;
    }

        greenCharacter.gameObject.SetActive(false);

if (questBridge != null)
{
    questBridge.OnEnemyJetpackFinished();   // <-- call bridge function
}


        Destroy(gameObject);
    }

    private void RotateCharacterTowardCamera()
    {
        if (!greenCharacter) return;

        Vector3 dir = userCamera.position - greenCharacter.position;

        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir, Vector3.up);
            Quaternion offset = Quaternion.Euler(90, 0, 0);

            greenCharacter.rotation = Quaternion.Slerp(
                greenCharacter.rotation, lookRot * offset, Time.deltaTime * headLookSpeed);
        }
    }

    private void MoveUnitForwardHorizontally()
    {
        Vector3 forward = transform.forward;
        forward.y = 0;
        forward.Normalize();
        transform.position += forward * moveSpeed * Time.deltaTime;
    }

    private void AimAtUser()
    {
        Vector3 targetPos = userCamera.position;

        Vector3 flatTarget = new Vector3(
            targetPos.x, horizontalPivot.position.y, targetPos.z);

        horizontalPivot.LookAt(flatTarget);

        Vector3 direction = targetPos - verticalPivot.position;
        Vector3 localDir = horizontalPivot.InverseTransformDirection(direction);

        float pitch = Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg;
        verticalPivot.localRotation = Quaternion.Euler(-pitch, 0, 0);
    }

    private void HandleShooting()
    {
        fireTimer += Time.deltaTime;

        if (fireTimer >= fireInterval)
        {
            fireTimer = 0f;
            ShootProjectile();
        }
    }

    private void ShootProjectile()
    {
        GameObject proj = Instantiate(
            projectilePrefab,
            projectileSpawnPoint.position,
            projectileSpawnPoint.rotation);

  if (characterSelection != null)
    {
        // Assign projectile material to child "cannonBall"
        Transform cannonBall = proj.transform.Find("cannonBall");
        if (cannonBall)
        {
            Renderer rend = cannonBall.GetComponent<Renderer>();
            if (rend)
                rend.material = characterSelection.projectileMaterial;
        }

        Transform splash = proj.transform.Find("splash");
        if (splash)
        {
            Renderer rend = splash.GetComponent<Renderer>();
            if (rend)
                rend.material = characterSelection.splashMaterial;
        }
    }

        ProjectileHoming homing = proj.AddComponent<ProjectileHoming>();

        homing.target = userCamera;
        homing.acceleration = jetpackAcceleration * 0.5f;
        homing.maxSpeed = jetpackMaxSpeed * 0.5f;
        homing.stopDistance = stopDistanceFromCamera;
        homing.userCamera = userCamera;

        Rigidbody rb = proj.GetComponent<Rigidbody>();
        if (rb == null) rb = proj.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.linearDamping = 0;
    }
}

// ============================================================================
//  PROJECTILE HOMING COMPONENT
// ============================================================================
public class ProjectileHoming : MonoBehaviour
{
    public float aimHeightOffset = 0.05f;

    public Transform target;
    public float acceleration = 15f;
    public float maxSpeed = 8f;
    public float stopDistance = 0.25f;
    public Transform userCamera;

    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = 0;
    }

    private void FixedUpdate()
    {
        if (!target) return;

        Vector3 adjustedTarget = target.position + Vector3.up * aimHeightOffset;
        Vector3 dir = adjustedTarget - transform.position;

        float dist = dir.magnitude;

        if (dist <= stopDistance)
        {
            rb.linearVelocity = Vector3.zero;

            Transform splash = transform.Find("splash");
            if (splash != null)
            {
                splash.gameObject.SetActive(true);
                splash.SetParent(null);
                splash.SetParent(userCamera);

                Vector3 lookDir = userCamera.forward;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f)
                    splash.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
            }

            Destroy(gameObject);
            return;
        }

        dir.Normalize();
        rb.AddForce(dir * acceleration, ForceMode.Acceleration);

        if (rb.linearVelocity.magnitude > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;

        if (rb.linearVelocity.sqrMagnitude > 0.001f)
            transform.forward = rb.linearVelocity.normalized;
    }
}
