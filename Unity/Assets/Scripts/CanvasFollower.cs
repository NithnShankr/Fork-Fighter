using UnityEngine;
using System.Collections;
using Meta.XR;
public class CanvasFOVAligner : MonoBehaviour
{
    [Header("Links")]
    public PassthroughCameraAccess cameraAccess; // Assign Meta passthrough component
    public RectTransform rawImageRect;           // Assign RawImage RectTransform

    [Header("Canvas Positioning")]
    public float canvasDistance = 1.0f;
    public bool lockYawOnly = true;

    IEnumerator Start()
    {
        if (cameraAccess == null)
        {
            Debug.LogError("CanvasFOVAligner: cameraAccess is not assigned!");
            yield break;
        }

        // Wait for passthrough camera to start
        while (!cameraAccess.IsPlaying)
            yield return null;

        ApplyFOVScaling();
    }

    void LateUpdate()
    {
        UpdateCanvasPose();
    }

    // -------------------------------------------------------
    // Scale RawImage to match headset passthrough FOV
    // -------------------------------------------------------
    void ApplyFOVScaling()
    {
        float pixelWidth = rawImageRect.sizeDelta.x;

        var leftRay  = cameraAccess.ViewportPointToRay(new Vector2(0f, 0.5f));
        var rightRay = cameraAccess.ViewportPointToRay(new Vector2(1f, 0.5f));

        float fovDeg = Vector3.Angle(leftRay.direction, rightRay.direction);
        float fovRad = fovDeg * Mathf.Deg2Rad;

        float worldWidth = 2f * canvasDistance * Mathf.Tan(fovRad / 2f);
        float scale = worldWidth / pixelWidth;

        rawImageRect.localScale = new Vector3(scale, scale, scale);

        Debug.Log($"ApplyFOVScaling pixelWidth = {pixelWidth}, FOV Deg = {fovDeg}, Scale = {scale}");
    }

    // -------------------------------------------------------
    // Position canvas in front of the user's head
    // -------------------------------------------------------
    void UpdateCanvasPose()
    {
        var pose = cameraAccess.GetCameraPose();

        Vector3 targetPos = pose.position + pose.rotation * Vector3.forward * canvasDistance;

        Quaternion targetRot;
        if (lockYawOnly)
        {
            targetRot = Quaternion.Euler(0, pose.rotation.eulerAngles.y, 0);
        }
        else
        {
            targetRot = pose.rotation;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;
    }
}
