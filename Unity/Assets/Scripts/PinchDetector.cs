using UnityEngine;

public class PinchDetector : MonoBehaviour
{
    public OVRHand hand;
    public QuestCameraBridge questCameraBridge;

    private bool wasPinching = false;

    void Update()
    {
        bool isPinching = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);

        if (isPinching && !wasPinching)
        {
            Debug.Log("Right pinch");
            questCameraBridge.OnPlateSelected();   // Only triggers once
        }

        wasPinching = isPinching;
    }
}
