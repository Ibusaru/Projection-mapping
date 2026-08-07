using System;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class OceanAdminCameraController : MonoBehaviour
{
    private enum AdminCameraMode
    {
        Roam,
        Aerial,
        FishFocus
    }

    [Header("References")]
    [SerializeField] private OceanCameraRig automaticRig;
    [SerializeField] private OceanEnvironment oceanEnvironment;

    [Header("Aerial View")]
    [SerializeField] private float aerialHeight = 42f;
    [SerializeField] private float aerialBackDistance = 64f;
    [SerializeField] private float aerialHoldSeconds = 7f;
    [SerializeField] private float aerialArrivalDistance = 2.5f;
    [SerializeField] private float aerialMaximumSeconds = 18f;

    [Header("Fish Focus")]
    [SerializeField] private float fishFollowDistance = 5.4f;
    [SerializeField] private float fishFollowHeight = 0.8f;
    [SerializeField] private float focusLookAhead = 1.4f;
    [SerializeField] private float fishFocusHoldSeconds = 7f;
    [SerializeField] private float fishFocusArrivalDistance = 1.25f;
    [SerializeField] private float fishFocusMaximumSeconds = 18f;

    [Header("Motion")]
    [SerializeField] private float positionSmoothTime = 1.25f;
    [SerializeField] private float maximumSpeed = 24f;
    [SerializeField] private float rotationSpeed = 2.6f;

    private AdminCameraMode mode = AdminCameraMode.Roam;
    private FishActor focusedFish;
    private Vector3 positionVelocity;
    private float aerialStartedAt;
    private float aerialArrivedAt = -1f;
    private float fishFocusStartedAt;
    private float fishFocusArrivedAt = -1f;

    private void Awake()
    {
        if (automaticRig == null)
        {
            automaticRig = GetComponent<OceanCameraRig>();
        }

        if (oceanEnvironment == null)
        {
            oceanEnvironment = FindAnyObjectByType<OceanEnvironment>();
        }
    }

    private void OnDisable()
    {
        ClearFocusedFish();
        if (automaticRig != null)
        {
            automaticRig.enabled = true;
        }
    }

    private void LateUpdate()
    {
        switch (mode)
        {
            case AdminCameraMode.Aerial:
                UpdateAerialView();
                break;
            case AdminCameraMode.FishFocus:
                UpdateFishFocus();
                break;
        }
    }

    public void ShowAerialView()
    {
        ClearFocusedFish();
        if (automaticRig != null)
        {
            mode = AdminCameraMode.Roam;
            automaticRig.RequestDroneOverview();
            Debug.Log("OceanAdminCameraController: requested moving aerial tour.");
            return;
        }

        mode = AdminCameraMode.Aerial;
        aerialStartedAt = Time.time;
        aerialArrivedAt = -1f;
        BeginManualControl();
        Debug.Log("OceanAdminCameraController: switched to aerial view.");
    }

    public void ResumeRoam()
    {
        ClearFocusedFish();
        mode = AdminCameraMode.Roam;
        positionVelocity = Vector3.zero;
        aerialArrivedAt = -1f;
        fishFocusArrivedAt = -1f;
        if (automaticRig != null)
        {
            automaticRig.RequestUnderwaterRoam();
        }
        Debug.Log("OceanAdminCameraController: resumed automatic roaming.");
    }

    public void ResumeFishFocus()
    {
        ClearFocusedFish();
        mode = AdminCameraMode.Roam;
        positionVelocity = Vector3.zero;
        aerialArrivedAt = -1f;
        fishFocusArrivedAt = -1f;
        if (automaticRig != null)
        {
            automaticRig.RequestFishFocus();
        }
        Debug.Log("OceanAdminCameraController: resumed automatic fish focus.");
    }

    public bool FocusFish(string fishId)
    {
        FishActor targetFish = FindFish(fishId);
        if (targetFish == null)
        {
            Debug.LogWarning($"OceanAdminCameraController: fish id='{fishId}' was not found.");
            return false;
        }

        ClearFocusedFish();
        mode = AdminCameraMode.Roam;
        positionVelocity = Vector3.zero;
        fishFocusArrivedAt = -1f;
        if (automaticRig != null && automaticRig.RequestSpecificFishFocus(targetFish))
        {
            Debug.Log($"OceanAdminCameraController: requested normal cinematic focus for '{targetFish.Nickname}' ({fishId}).");
            return true;
        }

        focusedFish = targetFish;
        focusedFish.SetCameraFocused(true);
        mode = AdminCameraMode.FishFocus;
        fishFocusStartedAt = Time.time;
        fishFocusArrivedAt = -1f;
        BeginManualControl();
        Debug.Log($"OceanAdminCameraController: focused fish '{focusedFish.Nickname}' ({fishId}).");
        return true;
    }

    private void BeginManualControl()
    {
        positionVelocity = Vector3.zero;
        if (automaticRig != null)
        {
            automaticRig.enabled = false;
        }
    }

    private void UpdateAerialView()
    {
        Vector3 lookTarget = Vector3.zero;
        Vector3 desiredPosition = Vector3.up * Mathf.Max(1f, aerialHeight)
            + Vector3.back * Mathf.Max(1f, aerialBackDistance);
        if (oceanEnvironment != null)
        {
            oceanEnvironment.GetDroneInterestPoints(out Vector3 beachCenter, out Vector3 waterInterest);
            Vector3 shoreward = Vector3.ProjectOnPlane(beachCenter - waterInterest, Vector3.up);
            shoreward = shoreward.sqrMagnitude > 0.001f ? shoreward.normalized : Vector3.right;
            lookTarget = Vector3.Lerp(waterInterest, beachCenter, 0.34f);
            desiredPosition = lookTarget
                - shoreward * Mathf.Max(1f, aerialBackDistance)
                + Vector3.up * Mathf.Max(1f, aerialHeight);
        }

        MoveAndLook(desiredPosition, lookTarget);

        if (Vector3.Distance(transform.position, desiredPosition) <= Mathf.Max(0.1f, aerialArrivalDistance))
        {
            if (aerialArrivedAt < 0f)
            {
                aerialArrivedAt = Time.time;
            }
            else if (Time.time - aerialArrivedAt >= Mathf.Max(0f, aerialHoldSeconds))
            {
                ResumeFishFocus();
                return;
            }
        }

        if (Time.time - aerialStartedAt >= Mathf.Max(aerialHoldSeconds, aerialMaximumSeconds))
        {
            ResumeFishFocus();
        }
    }

    private void UpdateFishFocus()
    {
        if (focusedFish == null || !focusedFish.isActiveAndEnabled)
        {
            ResumeFishFocus();
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(focusedFish.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        float radius = Mathf.Max(0.5f, focusedFish.CameraFocusRadius);
        float distance = Mathf.Max(fishFollowDistance, radius * 3.2f);
        Vector3 focusPoint = focusedFish.VisualCenter;
        Vector3 desiredPosition = focusPoint - forward * distance + Vector3.up * fishFollowHeight;
        Vector3 lookTarget = focusPoint + forward * focusLookAhead;
        MoveAndLook(desiredPosition, lookTarget);

        if (Vector3.Distance(transform.position, desiredPosition) <= Mathf.Max(0.1f, fishFocusArrivalDistance))
        {
            if (fishFocusArrivedAt < 0f)
            {
                fishFocusArrivedAt = Time.time;
            }
            else if (Time.time - fishFocusArrivedAt >= Mathf.Max(0f, fishFocusHoldSeconds))
            {
                ResumeAfterFishFocus();
                return;
            }
        }

        float maximumSeconds = Mathf.Max(fishFocusHoldSeconds, fishFocusMaximumSeconds);
        if (Time.time - fishFocusStartedAt >= maximumSeconds)
        {
            ResumeAfterFishFocus();
        }
    }

    private void ResumeAfterFishFocus()
    {
        FishActor completedFish = focusedFish;
        ClearFocusedFish();
        mode = AdminCameraMode.Roam;
        positionVelocity = Vector3.zero;
        fishFocusArrivedAt = -1f;
        if (automaticRig != null)
        {
            automaticRig.RequestNextFishFocus(completedFish);
        }
        Debug.Log("OceanAdminCameraController: completed admin focus and advanced to the next fish.");
    }

    private void MoveAndLook(Vector3 desiredPosition, Vector3 lookTarget)
    {
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            Mathf.Max(0.05f, positionSmoothTime),
            Mathf.Max(0.1f, maximumSpeed)
        );

        Vector3 direction = lookTarget - transform.position;
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, rotationSpeed) * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);
    }

    private void ClearFocusedFish()
    {
        if (focusedFish != null)
        {
            focusedFish.SetCameraFocused(false);
            focusedFish = null;
        }
    }

    private static FishActor FindFish(string fishId)
    {
        if (string.IsNullOrWhiteSpace(fishId))
        {
            return null;
        }

        foreach (FishActor fish in FishActor.AllActiveFishes)
        {
            if (fish != null
                && fish.IsReleasedFish
                && string.Equals(fish.SourceId, fishId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return fish;
            }
        }

        return null;
    }
}
