using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Meta.XR;
using Meta.XR.MRUtilityKit;
using System;
using TMPro; 

public class QuestCameraBridge : MonoBehaviour
{
    private bool isGameOver = false;

    private bool isPortalOpening = false;

    private bool allowReplayCollision = false;


    [Header("Flash Effect Object")]
public GameObject flickerObject;  

public int flickerCount = 4;
public float flickerSpeed = 0.07f; 


    [Header("Game Over UI")]
public TMP_Text gameOverText;
private float gameStartTime;

    [Header("Replay Object")]
public Transform replayChilli;
private Collider replayChilliCollider;

    [Header("Player Lives")]
public GameObject[] lives;


public GameObject health;

private int livesRemaining;

    [Header("Coin Prefab")]
public Transform coinPrefab;

    public TMP_Text scoreText3D;
private int score = 0;

    public GameObject findPlateInstruction;

    public GameObject centerGazeInstruction;  

    public GameObject forkToStartInstruction;   // NEW

    private float nextSpawnTime = 0f;

    public float portalAnimationDuration = 0.5f;

    public Text targetText;
    public string number;

    [Header("Debug UI")]
    public Text uiText;

    [Header("Left/Right Passthrough Cameras")]
    public PassthroughCameraAccess leftCamera;
    public PassthroughCameraAccess rightCamera;

    [Header("UI Preview")]
    public Image boxLeft;
    public Image boxRight;
    public RawImage passthroughImage;

    [Header("Markers")]
    public Transform plateCenter3D;
    public Transform forkCenter3D;
    public Transform centerCamera;

    [Header("Plate Corners")]
    public Transform plateTL3D;
    public Transform plateTR3D;
    public Transform plateBL3D;
    public Transform plateBR3D;

    [Header("Portal Marker")]
    public Transform portal;

    [Header("Portal Spawn Edges")]
    public Transform portalLeft;
    public Transform portalRight;

    [Header("Edge Object")]
    public Transform edges;

    [Header("Enemy Prefab")]
    public Transform enemyPrefab;

    [Header("Cannon Colliders")]
    public Collider leftCollider;
    public Collider rightCollider;
    public Collider bottomCollider;

    [Header("Enemy Spawn Settings")]
    public float enemyScale = 1f;
    public float spawnInterval = 2f;

    public float cornerSmoothing = 10f;

    private float spawnTimer = 0f;
    private bool allowSpawning = false;

    private List<Transform> activeEnemies = new List<Transform>();
    private Collider forkCollider;

    private AndroidJavaClass plugin;
    private AndroidJavaObject unityActivity;

    private bool cameraStartedAfterPermission = false;

    const float modelInputSize = 320f;
    const float rawCameraHeight = 240f;
    const float padY = (modelInputSize - rawCameraHeight) * 0.5f;

    private int currentModel = 1;
    private bool plateFrozen = false;

    [Header("Environment Raycasting")]
    public EnvironmentRaycastManager raycastManager;

    [Header("Plate Hit Result Object")]
    public Transform plateHitResult;
    private Transform confirmPlatePosition;

    [Header("Start Game Object")]
    public Transform playChili;
    private Collider playChiliCollider;

    bool hasStereoDetections = false;

    [Header("Effects")]
    public Transform explosionPrefab;

    void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        plugin = new AndroidJavaClass("com.example.myplugin.MyPlugin");
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

        string aarVersion = plugin.CallStatic<string>("init", unityActivity, 0);
        if (targetText)
            targetText.text = "Quest:" + number + "\nAndroid:" + aarVersion;
#endif

        HidePlateCorners();

        if (portal) portal.gameObject.SetActive(false);
        if (edges) edges.gameObject.SetActive(false);
        if (forkCenter3D)
        {
            forkCenter3D.gameObject.SetActive(false);
            forkCenter3D.position = new Vector3(0, -9, 0);
        }

        if (forkCenter3D)
            forkCollider = forkCenter3D.GetComponentInChildren<Collider>();

        confirmPlatePosition = plateHitResult ? plateHitResult.Find("Button") : null;

        if (playChili)
        {
            playChiliCollider = playChili.GetComponent<Collider>();
            playChili.gameObject.SetActive(false);
        }

        void Awake()
{
    if (flickerObject)
        flickerObject.SetActive(false);
}

    }

    private IEnumerator Flicker(float duration = 0.15f)
{
     if (!flickerObject)
        yield break;

    // If a flicker is already running, reset it
    flickerObject.SetActive(false);

    for (int i = 0; i < flickerCount; i++)
    {
        flickerObject.SetActive(true);
        yield return new WaitForSeconds(flickerSpeed);

        flickerObject.SetActive(false);
        yield return new WaitForSeconds(flickerSpeed);
    }
}


    void Start()
    {
        livesRemaining = lives != null ? lives.Length : 0;
            gameStartTime = Time.time;
        RequestPermissions();
    }

    void RequestPermissions()
    {
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission("horizonos.permission.HEADSET_CAMERA"))
            UnityEngine.Android.Permission.RequestUserPermission("horizonos.permission.HEADSET_CAMERA");

        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.CAMERA"))
            UnityEngine.Android.Permission.RequestUserPermission("android.permission.CAMERA");
    }

    void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR

        if (!cameraStartedAfterPermission)
        {
            bool hasHeadsetCam = UnityEngine.Android.Permission.HasUserAuthorizedPermission("horizonos.permission.HEADSET_CAMERA");
            bool hasCam = UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.CAMERA");

            if (hasHeadsetCam && hasCam)
            {
                plugin.CallStatic("startCamera");
                cameraStartedAfterPermission = true;
            }
        }

        if (!cameraStartedAfterPermission)
            return;

        if (!leftCamera.IsPlaying || !rightCamera.IsPlaying)
            return;

        hasStereoDetections = plugin.CallStatic<bool>("hasStereoDetections");

        // ⭐ ENABLE/DISABLE findPlateInstruction BASED ON MODE & DETECTIONS
        if (currentModel == 1)
            findPlateInstruction.SetActive(!hasStereoDetections);
        else
            findPlateInstruction.SetActive(false);

        // ⭐ ENABLE/DISABLE centerGazeInstruction BASED ON PLATE MODE & FREEZE STATE
        if (currentModel == 1 && !plateFrozen)
        {
            centerGazeInstruction.SetActive(hasStereoDetections);
             
        }else
        {
            centerGazeInstruction.SetActive(false);
        }
            


        if (currentModel == 0)
        {
            if (!hasStereoDetections)
            {
                forkCenter3D.gameObject.SetActive(false);
                forkCenter3D.position = new Vector3(0, -9, 0);
            }
            else
            {
                forkCenter3D.gameObject.SetActive(true);
            }

            if (!allowSpawning && playChili && playChiliCollider && forkCollider)
            {
                if (forkCollider.bounds.Intersects(playChiliCollider.bounds))
                {
                    if (explosionPrefab)
                    {
                        Transform explosion = Instantiate(
                            explosionPrefab,
                            playChili.position,
                            Quaternion.identity
                        );
                        explosion.SetParent(null);

                        Transform tiny = explosion.Find("TinyExplosion");
                        if (tiny)
                            tiny.gameObject.SetActive(true);
                    }

                    if (!isPortalOpening)
{
    isPortalOpening = true;
    StartCoroutine(AnimatePortalOpen(portalAnimationDuration));
}

                }
            }
        }

        if (hasStereoDetections)
        {
            if (currentModel == 1)
            {
                EnablePlateMarker();
                DisableForkMarker();
                allowSpawning = false;

                ProcessPlateMode(!plateFrozen);
            }
            else
            {
                DisablePlateMarker();
                EnableForkMarker();
                ProcessForkMode();
            }
        }

        if (currentModel == 0 && allowSpawning)
        {
            spawnTimer += Time.deltaTime;

            if (spawnTimer >= nextSpawnTime)
            {
                spawnTimer = 0f;

                SpawnEnemyAtRandomPosition();

                nextSpawnTime = UnityEngine.Random.Range(spawnInterval * 0.7f, spawnInterval * 1.3f);
            }
        }

        if (currentModel == 0)
            CheckForkEnemyCollisions();

     CheckReplayCollision();       

#endif
    }

    void LateUpdate()
    {
        if (currentModel != 1) return;
        if (!plateCenter3D || !plateHitResult || !centerCamera) return;
        if (!raycastManager) return;
        if (!EnvironmentRaycastManager.IsSupported) return;

        Vector3 dir = (plateCenter3D.position - centerCamera.position).normalized;
        Ray ray = new Ray(centerCamera.position, dir);

        if (raycastManager.Raycast(ray, out EnvironmentRaycastHit envHit))
        {
            if (envHit.status == EnvironmentRaycastHitStatus.Hit)
            {
                plateHitResult.position =
                    Vector3.Lerp(plateHitResult.position, envHit.point, Time.deltaTime * cornerSmoothing);
            }
        }
    }

    void EnablePlateMarker() { if (plateCenter3D) plateCenter3D.gameObject.SetActive(true); }
    void DisablePlateMarker() { if (plateCenter3D) plateCenter3D.gameObject.SetActive(false); }

    void EnableForkMarker() { if (forkCenter3D) forkCenter3D.gameObject.SetActive(true); }
    void DisableForkMarker() { if (forkCenter3D) { forkCenter3D.gameObject.SetActive(false); forkCenter3D.position = new Vector3(0, -9, 0); } }

    public void OnPlateSelected()
    {
        if (currentModel == 1 && !plateFrozen)
            FreezePlateAndSwitchToFork();
    }

    private void FreezePlateAndSwitchToFork()
    {
        plateFrozen = true;

        if (plateHitResult && playChili)
        {
            playChili.position = new Vector3(plateHitResult.position.x, plateHitResult.position.y + 0.5f, plateHitResult.position.z);
            playChili.gameObject.SetActive(true);
            StartCoroutine(AnimateYDown(playChili.transform, 1, plateHitResult.position.y));
            plateHitResult.gameObject.SetActive(false);
        }

        Vector3 mid = (plateTL3D.position + plateTR3D.position) * 0.5f;
        Vector3 rayStart = mid + Vector3.up * 0.25f;
        Ray ray = new Ray(rayStart, Vector3.down);

        if (raycastManager &&
            EnvironmentRaycastManager.IsSupported &&
            raycastManager.Raycast(ray, out var envHit) &&
            envHit.status == EnvironmentRaycastHitStatus.Hit)
        {
            float targetY = mid.y;
            float maxYDifference = 0.2f;
            Vector3 hitPoint = envHit.point;

            if (Mathf.Abs(hitPoint.y - targetY) > maxYDifference)
            {
                hitPoint.y = targetY;
            }

            portal.position = new Vector3(
                hitPoint.x,
                hitPoint.y + 0.01f,
                hitPoint.z
            );
        }
        else
        {
            portal.position = mid;
        }

        Vector3 lookDir = portal.position - centerCamera.position;
        lookDir.y = 0;
        portal.rotation = Quaternion.LookRotation(lookDir);
        portal.gameObject.SetActive(true);

        if (edges && plateBL3D && plateBR3D)
        {
            edges.position = (plateBL3D.position + plateBR3D.position) * 0.5f;
            edges.rotation = Quaternion.LookRotation(
                (plateBR3D.position - plateBL3D.position).normalized,
                Vector3.up
            );
            edges.gameObject.SetActive(true);
        }

        currentModel = 0;

#if UNITY_ANDROID && !UNITY_EDITOR
        plugin.CallStatic("switchModel", 0);
#endif
    }

    public IEnumerator AnimateYDown(Transform target, float duration, float endY)
    {

        if (target == null) yield break;

try
    {
        Vector3 start = target.localPosition;
        Vector3 end = new Vector3(start.x, endY, start.z);

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;

            float eased = t * t;

            target.localPosition = Vector3.Lerp(start, end, eased);

            yield return null;
        }

        target.localPosition = end;
         }
    finally
    {
     forkToStartInstruction.SetActive(true);

    }
    }

    private void StartForkGame()
    {
        scoreText3D.gameObject.SetActive(true);
        health.gameObject.SetActive(true);
        if (!allowSpawning)
        {
            allowSpawning = true;
        }
    }

    private System.Collections.IEnumerator AnimatePortalOpen(float portalAnimationDuration)
    {
        if (playChili) playChili.gameObject.SetActive(false);

        if (!portal) yield break;

        Transform mesh = portal.Find("Mesh");
        if (!mesh) yield break;

        Vector3 startScale = new Vector3(0f, 0f, 0.0015f);
        Vector3 endScale = new Vector3(0.2f, 0.2f, 0.0015f);

        mesh.localScale = startScale;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / portalAnimationDuration;
            float eased = Mathf.SmoothStep(0f, 1f, t);
            mesh.localScale = Vector3.Lerp(startScale, endScale, eased);
            yield return null;
        }

        mesh.localScale = endScale;

        yield return new WaitForSeconds(1f);

        StartForkGame();

        isPortalOpening = false;
    }

    private void SpawnEnemyAtRandomPosition()
    {
        if (!enemyPrefab || !portal || !portalLeft || !portalRight)
            return;

        Vector3 rightDir = (portalRight.position - portalLeft.position).normalized;
        float width = Vector3.Distance(portalLeft.position, portalRight.position);

        float offset = UnityEngine.Random.Range(-width * 0.5f, width * 0.5f);
        Vector3 spawnPos = portal.position + rightDir * offset;

        Transform enemy = Instantiate(enemyPrefab, spawnPos, portal.rotation);
        enemy.SetParent(null);
        enemy.localScale = Vector3.one * enemyScale;
        enemy.rotation *= Quaternion.Euler(0, 180, 0);

        var cannon = enemy.GetComponent<CannonController>();
        if (cannon)
        {
            cannon.userCamera = centerCamera;
            cannon.leftCollider = leftCollider;
            cannon.rightCollider = rightCollider;
            cannon.bottomCollider = bottomCollider;
            cannon.raycastManager = raycastManager;
            cannon.explosionPrefab = explosionPrefab;
            cannon.questBridge = this; 
            cannon.enabled = true;
        }

        activeEnemies.Add(enemy);
    }

    private void CheckForkEnemyCollisions()
    {
        if (!forkCenter3D)
            return;

        if (forkCollider == null)
            forkCollider = forkCenter3D.GetComponentInChildren<Collider>();

        if (forkCollider == null)
            return;

        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            Transform enemy = activeEnemies[i];
            if (!enemy)
            {
                activeEnemies.RemoveAt(i);
                continue;
            }

            Collider enemyCol = enemy.GetComponentInChildren<Collider>();
            if (!enemyCol)
                continue;

            if (forkCollider.bounds.Intersects(enemyCol.bounds))
            {
                if (explosionPrefab)
                {
                    Transform explosion = Instantiate(
                        explosionPrefab,
                        enemy.position,
                        Quaternion.identity
                    );
                    explosion.SetParent(null);

                    Transform tiny = explosion.Find("TinyExplosion");
                    if (tiny)
                        tiny.gameObject.SetActive(true);
                    
                    if (coinPrefab)
                    {
                        var coin = Instantiate(coinPrefab, enemy.position, Quaternion.identity);
                        coin.GetComponent<CoinMagnet>().forkCenter3D = forkCenter3D;
                        coin.GetComponent<CoinMagnet>().questCameraBridge = this; 
                        coin.SetParent(null);
                    }
                        
                }

                Destroy(enemy.gameObject);
                activeEnemies.RemoveAt(i);
            }
        }
    }

    void ProcessForkMode()
    {
        float[] stereoBB = plugin.CallStatic<float[]>("getStereoBoundingBoxes");

        if (stereoBB == null || stereoBB.Length < 12)
        {
            return;
        }

        float[] bbL = new float[6];
        Array.Copy(stereoBB, 0, bbL, 0, 6);

        float[] bbR = new float[6];
        Array.Copy(stereoBB, 6, bbR, 0, 6);

        if (bbL == null || bbR == null) return;

        Vector3 pos = ComputeCenter(bbL, bbR);

        if (forkCenter3D)
            forkCenter3D.position = pos;
    }

    void ProcessPlateMode(bool liveUpdate)
    {
        if (!hasStereoDetections)
            return;

        float[] bb = plugin.CallStatic<float[]>("getStereoBoundingBoxes");

        if (bb == null || bb.Length < 12)
            return;

        float[] bbL = { bb[0], bb[1], bb[2], bb[3], bb[4], bb[5] };
        float[] bbR = { bb[6], bb[7], bb[8], bb[9], bb[10], bb[11] };

        Vector3 center = ComputeCenter(bbL, bbR);

        if (plateCenter3D)
            plateCenter3D.position = center;

        if (confirmPlatePosition)
        {
            Vector3 lookPos = new Vector3(
                centerCamera.position.x,
                confirmPlatePosition.position.y,
                centerCamera.position.z
            );
            confirmPlatePosition.LookAt(lookPos);
        }

        if (!liveUpdate)
            return;

        ComputePlateCorners(bbL, bbR, center);

        if (portal)
            portal.gameObject.SetActive(false);
    }

    private void ComputePlateCorners(float[] bbL, float[] bbR, Vector3 center)
    {
        float xminL = bbL[0], yminL = bbL[1], xmaxL = bbL[2], ymaxL = bbL[3];
        float xminR = bbR[0], yminR = bbR[1], xmaxR = bbR[2], ymaxR = bbR[3];

        Vector2 L_TL = ToViewport(xminL, yminL);
        Vector2 L_TR = ToViewport(xmaxL, yminL);
        Vector2 R_TL = ToViewport(xminR, yminR);
        Vector2 R_TR = ToViewport(xmaxR, yminR);

        Vector3 TL = TriangulateStereo(L_TL, R_TL);
        Vector3 TR = TriangulateStereo(L_TR, R_TR);

        Vector3 horizontal = (TR - TL).normalized;
        Vector3 vertical = Vector3.Cross(horizontal, Vector3.up).normalized;

        if (Vector3.Dot(vertical, (center - TL)) < 0)
            vertical = -vertical;

        float depth = Vector3.Distance(center, TL);

        Vector3 BL = TL + vertical * depth;
        Vector3 BR = TR + vertical * depth;

        plateTL3D.position = TL;
        plateTR3D.position = TR;
        plateBL3D.position = BL;
        plateBR3D.position = BR;

        plateTL3D.gameObject.SetActive(true);
        plateTR3D.gameObject.SetActive(true);
        plateBL3D.gameObject.SetActive(true);
        plateBR3D.gameObject.SetActive(true);
    }

    private Vector3 ComputeCenter(float[] bbL, float[] bbR)
    {
        float cxL = (bbL[0] + bbL[2]) * 0.5f;
        float cyL = (bbL[1] + bbL[3]) * 0.5f;
        float cxR = (bbR[0] + bbR[2]) * 0.5f;
        float cyR = (bbR[1] + bbR[3]) * 0.5f;

        Vector2 vL = ToViewport(cxL, cyL);
        Vector2 vR = ToViewport(cxR, cyR);

        Ray rL = rightCamera.ViewportPointToRay(vL);
        Ray rR = leftCamera.ViewportPointToRay(vR);

        return Triangulate(rL.origin, rL.direction, rR.origin, rR.direction);
    }

    private Vector2 ToViewport(float xNorm, float yNorm)
    {
        float cy = (yNorm * modelInputSize - padY) / rawCameraHeight;
        float cx = Mathf.Clamp01(xNorm);
        return new Vector2(cx, 1f - Mathf.Clamp01(cy));
    }

    private Vector3 TriangulateStereo(Vector2 vL, Vector2 vR)
    {
        Ray rL = rightCamera.ViewportPointToRay(vL);
        Ray rR = leftCamera.ViewportPointToRay(vR);
        return Triangulate(rL.origin, rL.direction, rR.origin, rR.direction);
    }

    private void DrawUIBox(float[] bb, Image uiBox)
    {
        if (!uiBox) return;

        float xmin = bb[0], ymin = bb[1], xmax = bb[2], ymax = bb[3];
        float cx = (xmin + xmax) * 0.5f;
        float cy = (ymin + ymax) * 0.5f;

        RectTransform rect = passthroughImage.rectTransform;
        float w = rect.rect.width;
        float h = rect.rect.height;

        RectTransform box = uiBox.rectTransform;
        box.sizeDelta = new Vector2((xmax - xmin) * w, (ymax - ymin) * h);
        box.localPosition = new Vector3((cx - 0.5f) * w, (0.5f - cy) * h, 0);

        uiBox.gameObject.SetActive(true);
    }

    private void HidePlateCorners()
    {
        if (plateTL3D) plateTL3D.gameObject.SetActive(false);
        if (plateTR3D) plateTR3D.gameObject.SetActive(false);
        if (plateBL3D) plateBL3D.gameObject.SetActive(false);
        if (plateBR3D) plateBR3D.gameObject.SetActive(false);
    }

public void OnCoinCollected()
{
    score++;

    if (scoreText3D != null)
        scoreText3D.text = score.ToString("D4");
}


    private Vector3 Triangulate(Vector3 o1, Vector3 d1, Vector3 o2, Vector3 d2)
    {
        d1.Normalize();
        d2.Normalize();

        Vector3 r = o2 - o1;

        float a = Vector3.Dot(d1, d1);
        float b = Vector3.Dot(d1, d2);
        float c = Vector3.Dot(d2, d2);
        float d = Vector3.Dot(d1, r);
        float e = Vector3.Dot(d2, r);

        float denom = a * c - b * b;
        if (Mathf.Abs(denom) < 1e-6)
            return o1 + d1 * 1.5f;

        float s = (b * e - c * d) / denom;
        float t = (a * e - b * d) / denom;

        return (o1 + d1 * s + o2 + d2 * t) * 0.5f;
    }

public void OnEnemyJetpackFinished()
{
    if (livesRemaining <= 0)
        return;

  StartCoroutine(Flicker());

    // Decrement BEFORE hiding
    livesRemaining--;

    // Hide the correct heart (backwards)
    int indexToHide = livesRemaining;

    if (indexToHide >= 0 && indexToHide < lives.Length)
    {
        if (lives[indexToHide] != null)
            lives[indexToHide].SetActive(false);
    }

    // Trigger Game Over
    if (livesRemaining == 0)
    {
        GameOver();
    }
}

private IEnumerator EnableReplayCollisionAfterDelay(float delay)
{
    yield return new WaitForSeconds(delay);
    allowReplayCollision = true;
}



private void GameOver()
{
    Debug.Log("GAME OVER!");

    allowSpawning = false;
  isGameOver = true;
    foreach (Transform enemy in activeEnemies)
    {
        if (enemy)
            Destroy(enemy.gameObject);
    }
    activeEnemies.Clear();

    // Show replay chilli
    if (replayChilli && plateHitResult)
    {
        replayChilli.position = plateHitResult.position;
        replayChilli.gameObject.SetActive(true);
    }

    if (replayChilliCollider == null && replayChilli)
        replayChilliCollider = replayChilli.GetComponent<Collider>();

    // Disable replay detection for now
    allowReplayCollision = false;
   

    // Start delayed enable
    StartCoroutine(EnableReplayCollisionAfterDelay(3f));

    // Show game over text etc.
    if (gameOverText != null)
    {
        float totalDuration = Time.time - gameStartTime;
        gameOverText.text =
            $"Your Score : {score}, " +
            $"Total Duration : {totalDuration:0.0}s\n\n" +
            $"Fork red chilli to play again.";
    }
}


private void CheckReplayCollision()
{
    if (!allowReplayCollision)
        return;

    if (!forkCenter3D || replayChilli == null || !replayChilli.gameObject.activeSelf)
        return;

    if (forkCollider == null)
        forkCollider = forkCenter3D.GetComponentInChildren<Collider>();

    if (replayChilliCollider == null)
        replayChilliCollider = replayChilli.GetComponent<Collider>();

    if (forkCollider == null || replayChilliCollider == null)
        return;

    if (forkCollider.bounds.Intersects(replayChilliCollider.bounds))
    {
        Debug.Log("Replay Chili Hit! Restarting game...");

        replayChilli.gameObject.SetActive(false);
        allowReplayCollision = false;  // stop further triggers
        if (isGameOver)
            {
                 RestartGame();
            }
       
        
    }
}



private void RestartGame()
{
     isGameOver = false; 
    // Reset score
    score = 0;
    scoreText3D.text = "0000";

    // Reset lives
    livesRemaining = lives.Length;
    foreach (var heart in lives)
        heart.SetActive(true);

    // Reset timer
    gameStartTime = Time.time;

    // Clear game over text
    if (gameOverText != null)
        gameOverText.text = "";

    // Restart spawning
    allowSpawning = true;
    spawnTimer = 0f;

    Debug.Log("Game restarted!");
}




}
