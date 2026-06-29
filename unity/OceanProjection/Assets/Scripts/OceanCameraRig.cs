using System.Collections.Generic;
using UnityEngine;

public class OceanCameraRig : MonoBehaviour
{
    private enum DiverIntent
    {
        Cruise,
        ApproachFish,
        ApproachSchool,
        DriftPast
    }

    [SerializeField] private Transform target;
    [SerializeField] private Vector3 center = Vector3.zero;
    [SerializeField] private Vector3 orbitSize = new Vector3(10f, 3f, 8f);
    [SerializeField] private float orbitSeconds = 38f;
    [SerializeField] private float lookAhead = 2.5f;

    [Header("Diver Follow")]
    [SerializeField] private bool focusFish = true;
    [SerializeField] private float targetRefreshSeconds = 4.8f;
    [SerializeField] private bool prioritizeReleasedFish = true;
    [SerializeField] private float followDistance = 4.2f;
    [SerializeField] private float sideOffset = 1.15f;
    [SerializeField] private float heightOffset = 0.55f;
    [SerializeField] private float forwardLookAhead = 2.4f;
    [SerializeField] private float minFocusDistance = 4f;
    [SerializeField] private float maxFocusDistance = 10f;
    [SerializeField] private float focusRadiusMultiplier = 3f;
    [SerializeField] private float positionSmoothTime = 2.4f;
    [SerializeField] private float rotationSmooth = 1.55f;
    [SerializeField] private float driftAmplitude = 0.42f;
    [SerializeField] private float driftSpeed = 0.28f;
    [SerializeField] private float maximumCameraSpeed = 5.5f;
    [Header("Diver Behavior")]
    [SerializeField] private float scanIntervalSeconds = 2.2f;
    [SerializeField] private float fishInterestDistance = 24f;
    [SerializeField] private float schoolInterestDistance = 34f;
    [SerializeField] private float schoolNeighborRadius = 6.5f;
    [SerializeField] private int minimumSchoolSize = 5;
    [SerializeField] private float fishApproachDistance = 5.2f;
    [SerializeField] private float schoolApproachDistance = 8.5f;
    [SerializeField] private Vector2 fishObserveSeconds = new Vector2(3.5f, 6f);
    [SerializeField] private Vector2 schoolObserveSeconds = new Vector2(5.5f, 9f);
    [SerializeField] private Vector2 cruiseSeconds = new Vector2(4f, 8f);
    [SerializeField] private float diverLookAhead = 1.8f;
    [SerializeField] private float schoolRadiusPadding = 1.35f;
    [SerializeField] private float relaxedCameraSpeed = 2.8f;
    [SerializeField] private float approachCameraSpeed = 4.8f;
    [Header("Diver Observation Angles")]
    [SerializeField] private Vector2 frontViewAngleRange = new Vector2(14f, 28f);
    [SerializeField] private Vector2 diagonalViewAngleRange = new Vector2(38f, 62f);
    [SerializeField] private Vector2 sideViewAngleRange = new Vector2(72f, 90f);
    [SerializeField] private Vector2 rearApproachTurnAngleRange = new Vector2(68f, 88f);
    [SerializeField] private float observationOrbitSmoothTime = 1.8f;
    [SerializeField] private float observationOrbitMaxDegreesPerSecond = 85f;

    private FishActor focusedFish;
    private DiverIntent intent = DiverIntent.Cruise;
    private Vector3 positionVelocity;
    private Vector3 currentFocusPoint;
    private Vector3 currentFocusForward = Vector3.forward;
    private Vector3 cruiseDestination;
    private Vector3 lastSchoolCenter;
    private float currentFocusRadius = 1f;
    private float currentObservationAngleDegrees = 70f;
    private float targetObservationAngleDegrees = 70f;
    private float observationAngleVelocity;
    private float nextTargetRefreshTime;
    private float intentUntilTime;
    private bool hasObservationAngle;
    private bool hasPlacedInitialCamera;

    private void LateUpdate()
    {
        if (focusFish)
        {
            UpdateDiverFollowCamera();
            return;
        }

        float t = Time.time / Mathf.Max(1f, orbitSeconds) * Mathf.PI * 2f;
        Vector3 position = center + new Vector3(
            Mathf.Sin(t) * orbitSize.x,
            Mathf.Sin(t * 0.45f) * orbitSize.y,
            Mathf.Cos(t) * orbitSize.z
        );

        transform.position = position;

        Vector3 lookTarget = target != null
            ? target.position
            : center + new Vector3(Mathf.Sin(t + 0.8f) * lookAhead, 0f, Mathf.Cos(t + 0.8f) * lookAhead);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(lookTarget - transform.position),
            1.8f * Time.deltaTime
        );
    }

    private void UpdateDiverFollowCamera()
    {
        RefreshDiverIntentIfNeeded();

        if (intent == DiverIntent.Cruise || (focusedFish == null && intent != DiverIntent.ApproachSchool))
        {
            UpdateFallbackDiverCruise();
            return;
        }

        UpdateCurrentFocus();
        Vector3 focusForward = FlattenDirection(currentFocusForward);
        Vector3 focusRight = Vector3.Cross(Vector3.up, focusForward).normalized;
        if (focusRight.sqrMagnitude < 0.001f)
        {
            focusRight = transform.right;
        }
        Vector3 observationDirection = ObservationDirectionFromFocus(focusForward, focusRight);

        float driftTime = Time.time * driftSpeed;
        float desiredDistance = intent == DiverIntent.ApproachSchool
            ? schoolApproachDistance + currentFocusRadius * schoolRadiusPadding
            : Mathf.Clamp(fishApproachDistance + currentFocusRadius * focusRadiusMultiplier, minFocusDistance, maxFocusDistance);
        float dynamicLookAhead = forwardLookAhead + diverLookAhead + currentFocusRadius * 0.45f;

        Vector3 breathingDrift = DiverDrift(driftTime, currentFocusRadius);
        Vector3 passOffset = intent == DiverIntent.DriftPast
            ? focusRight * Mathf.Sin(Time.time * 0.35f + SpawnTimeSeed()) * 2.2f
            : Vector3.zero;
        float observationSide = currentObservationAngleDegrees >= 0f ? 1f : -1f;

        Vector3 desiredPosition = currentFocusPoint
            + observationDirection * desiredDistance
            + focusRight * observationSide * (sideOffset + currentFocusRadius * 0.18f)
            + Vector3.up * (heightOffset + currentFocusRadius * 0.12f)
            + breathingDrift
            + passOffset;

        float speed = intent == DiverIntent.ApproachFish || intent == DiverIntent.ApproachSchool
            ? approachCameraSpeed
            : relaxedCameraSpeed;
        speed = Mathf.Min(speed, Mathf.Max(0.1f, maximumCameraSpeed));

        float trailingLookAheadBlend = Mathf.InverseLerp(105f, 170f, Mathf.Abs(currentObservationAngleDegrees));
        float observationLead = Mathf.Lerp(0.45f + currentFocusRadius * 0.12f, dynamicLookAhead, trailingLookAheadBlend);
        Vector3 lookTarget = currentFocusPoint
            + focusForward * observationLead
            + Vector3.up * Mathf.Sin(driftTime) * 0.18f;

        bool snapped = MoveCamera(desiredPosition, lookTarget, positionSmoothTime, speed);
        LookAtTarget(lookTarget, rotationSmooth, snapped);
    }

    private void UpdateCurrentFocus()
    {
        if (intent == DiverIntent.ApproachSchool)
        {
            currentFocusPoint = lastSchoolCenter;
            return;
        }

        if (focusedFish == null)
        {
            return;
        }

        Transform fishTransform = focusedFish.transform;
        currentFocusPoint = focusedFish.VisualCenter;
        currentFocusForward = fishTransform.forward.sqrMagnitude > 0.001f
            ? fishTransform.forward.normalized
            : currentFocusForward;
        currentFocusRadius = focusedFish.CameraFocusRadius;
    }

    private Vector3 ObservationDirectionFromFocus(Vector3 focusForward, Vector3 focusRight)
    {
        if (!hasObservationAngle)
        {
            ChooseObservationAngle(currentFocusPoint, focusForward);
        }

        currentObservationAngleDegrees = Mathf.SmoothDampAngle(
            currentObservationAngleDegrees,
            targetObservationAngleDegrees,
            ref observationAngleVelocity,
            observationOrbitSmoothTime,
            observationOrbitMaxDegreesPerSecond
        );

        float radians = currentObservationAngleDegrees * Mathf.Deg2Rad;
        Vector3 direction = focusForward * Mathf.Cos(radians) + focusRight * Mathf.Sin(radians);
        if (direction.sqrMagnitude < 0.001f)
        {
            return focusRight;
        }

        return direction.normalized;
    }

    private void ChooseObservationAngle(Vector3 focusPoint, Vector3 focusForward)
    {
        Vector3 flatForward = FlattenDirection(focusForward);
        Vector3 focusRight = Vector3.Cross(Vector3.up, flatForward).normalized;
        if (focusRight.sqrMagnitude < 0.001f)
        {
            focusRight = transform.right;
        }

        Vector3 toCamera = transform.position - focusPoint;
        Vector3 flatToCamera = new Vector3(toCamera.x, 0f, toCamera.z);
        bool hasCameraDirection = flatToCamera.sqrMagnitude > 0.001f;
        float side = 1f;
        if (hasCameraDirection)
        {
            flatToCamera.Normalize();
            side = Vector3.Dot(flatToCamera, focusRight) >= 0f ? 1f : -1f;
        }

        if (Random.value < 0.28f)
        {
            side *= -1f;
        }

        bool approachedFromBehind = hasCameraDirection && Vector3.Dot(flatToCamera, flatForward) < -0.25f;
        float angleMagnitude = approachedFromBehind
            ? RandomInRange(rearApproachTurnAngleRange)
            : PickObservationAngleMagnitude();

        targetObservationAngleDegrees = angleMagnitude * side;
        currentObservationAngleDegrees = hasCameraDirection
            ? Vector3.SignedAngle(flatForward, flatToCamera, Vector3.up)
            : targetObservationAngleDegrees;
        observationAngleVelocity = 0f;
        hasObservationAngle = true;
    }

    private float PickObservationAngleMagnitude()
    {
        float roll = Random.value;
        if (roll < 0.24f)
        {
            return RandomInRange(frontViewAngleRange);
        }

        if (roll < 0.66f)
        {
            return RandomInRange(diagonalViewAngleRange);
        }

        return RandomInRange(sideViewAngleRange);
    }

    private static float RandomInRange(Vector2 range)
    {
        return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }

    private Vector3 DiverDrift(float driftTime, float radius)
    {
        float scale = Mathf.Clamp(1f + radius * 0.08f, 1f, 1.35f);
        return new Vector3(
            Mathf.Sin(driftTime * 1.7f) * driftAmplitude * scale,
            Mathf.Sin(driftTime * 2.1f + 0.4f) * driftAmplitude * 0.55f,
            Mathf.Cos(driftTime * 1.3f) * driftAmplitude * scale
        );
    }

    private float SpawnTimeSeed()
    {
        return currentFocusPoint.x * 0.13f + currentFocusPoint.z * 0.17f;
    }

    private void UpdateFallbackDiverCruise()
    {
        if (Time.time >= intentUntilTime || cruiseDestination == Vector3.zero)
        {
            PickCruiseDestination();
        }

        float driftTime = Time.time * driftSpeed;
        Vector3 desiredPosition = cruiseDestination + DiverDrift(driftTime, 1f);

        bool snapped = MoveCamera(
            desiredPosition,
            center,
            positionSmoothTime * 1.1f,
            Mathf.Min(relaxedCameraSpeed, Mathf.Max(0.1f, maximumCameraSpeed))
        );

        Vector3 forward = FlattenDirection(cruiseDestination - transform.position);
        Vector3 lookTarget = target != null
            ? target.position
            : transform.position + forward * lookAhead + Vector3.up * Mathf.Sin(driftTime) * 0.2f;
        LookAtTarget(lookTarget, rotationSmooth * 0.9f, snapped);
    }

    private void PickCruiseDestination()
    {
        float t = Time.time / Mathf.Max(1f, orbitSeconds * 1.35f) * Mathf.PI * 2f;
        cruiseDestination = center + new Vector3(
            Mathf.Sin(t + Random.Range(-0.65f, 0.65f)) * orbitSize.x * 0.75f,
            Mathf.Sin(t * 0.37f + Random.Range(-0.4f, 0.4f)) * orbitSize.y * 0.55f,
            Mathf.Cos(t * 0.82f + Random.Range(-0.65f, 0.65f)) * orbitSize.z * 0.75f
        );
        intentUntilTime = Time.time + Random.Range(cruiseSeconds.x, cruiseSeconds.y);
    }

    private void RefreshDiverIntentIfNeeded()
    {
        if (Time.time < nextTargetRefreshTime && Time.time < intentUntilTime)
        {
            return;
        }

        IReadOnlyList<FishActor> fishes = FishActor.AllActiveFishes;
        if (fishes == null || fishes.Count == 0)
        {
            focusedFish = null;
            intent = DiverIntent.Cruise;
            nextTargetRefreshTime = Time.time + ScanDelay();
            return;
        }

        bool hasReleasedFish = prioritizeReleasedFish && HasReleasedFish(fishes);
        bool foundSchool = TryFindSchool(fishes, out Vector3 schoolCenter, out Vector3 schoolForward, out float schoolRadius, out int schoolSize);
        FishActor fish = PickCinematicFish(fishes, hasReleasedFish);

        if (!hasReleasedFish && foundSchool && (fish == null || schoolSize >= minimumSchoolSize + 2 || Random.value < 0.68f))
        {
            focusedFish = null;
            intent = DiverIntent.ApproachSchool;
            lastSchoolCenter = schoolCenter;
            currentFocusPoint = schoolCenter;
            currentFocusForward = schoolForward;
            currentFocusRadius = Mathf.Max(1.5f, schoolRadius);
            ChooseObservationAngle(currentFocusPoint, currentFocusForward);
            intentUntilTime = Time.time + Random.Range(schoolObserveSeconds.x, schoolObserveSeconds.y);
        }
        else if (fish != null)
        {
            focusedFish = fish;
            intent = Random.value < 0.22f ? DiverIntent.DriftPast : DiverIntent.ApproachFish;
            currentFocusPoint = fish.VisualCenter;
            currentFocusForward = fish.transform.forward;
            currentFocusRadius = fish.CameraFocusRadius;
            ChooseObservationAngle(currentFocusPoint, currentFocusForward);
            intentUntilTime = Time.time + Random.Range(fishObserveSeconds.x, fishObserveSeconds.y);
        }
        else
        {
            focusedFish = null;
            intent = DiverIntent.Cruise;
            hasObservationAngle = false;
            PickCruiseDestination();
        }

        nextTargetRefreshTime = Time.time + ScanDelay();
    }

    private bool TryFindSchool(IReadOnlyList<FishActor> fishes, out Vector3 schoolCenter, out Vector3 schoolForward, out float schoolRadius, out int schoolSize)
    {
        schoolCenter = Vector3.zero;
        schoolForward = transform.forward;
        schoolRadius = 0f;
        schoolSize = 0;

        Vector3 bestCenter = Vector3.zero;
        Vector3 bestForward = Vector3.zero;
        float bestRadius = 0f;
        float bestScore = float.NegativeInfinity;
        int bestCount = 0;

        for (int i = 0; i < fishes.Count; i++)
        {
            FishActor anchor = fishes[i];
            if (anchor == null)
            {
                continue;
            }

            Vector3 centerSum = Vector3.zero;
            Vector3 forwardSum = Vector3.zero;
            float farthest = 0f;
            int count = 0;

            for (int j = 0; j < fishes.Count; j++)
            {
                FishActor candidate = fishes[j];
                if (candidate == null)
                {
                    continue;
                }

                Vector3 offset = candidate.transform.position - anchor.transform.position;
                Vector3 horizontalOffset = new Vector3(offset.x, 0f, offset.z);
                if (horizontalOffset.magnitude > schoolNeighborRadius || Mathf.Abs(offset.y) > 2.2f)
                {
                    continue;
                }

                centerSum += candidate.transform.position;
                forwardSum += FlattenDirection(candidate.transform.forward);
                farthest = Mathf.Max(farthest, horizontalOffset.magnitude);
                count++;
            }

            if (count < minimumSchoolSize)
            {
                continue;
            }

            Vector3 candidateCenter = centerSum / count;
            float distance = Vector3.Distance(transform.position, candidateCenter);
            if (distance > schoolInterestDistance)
            {
                continue;
            }

            float forwardScore = Vector3.Dot(transform.forward, (candidateCenter - transform.position).normalized);
            float score = count * 3f + forwardScore * 2f - distance * 0.12f + Random.Range(0f, 1.2f);
            if (score > bestScore)
            {
                bestScore = score;
                bestCenter = candidateCenter;
                bestForward = forwardSum.sqrMagnitude > 0.001f ? forwardSum.normalized : transform.forward;
                bestRadius = farthest;
                bestCount = count;
            }
        }

        if (bestCount < minimumSchoolSize)
        {
            return false;
        }

        schoolCenter = bestCenter;
        schoolForward = FlattenDirection(bestForward);
        schoolRadius = bestRadius;
        schoolSize = bestCount;
        return true;
    }

    private Vector3 FlattenDirection(Vector3 direction)
    {
        Vector3 flat = new Vector3(direction.x, 0f, direction.z);
        if (flat.sqrMagnitude < 0.001f)
        {
            flat = Vector3.forward;
        }

        return flat.normalized;
    }

    private float ScanDelay()
    {
        return Mathf.Max(0.6f, Mathf.Min(scanIntervalSeconds, targetRefreshSeconds));
    }

    private FishActor PickCinematicFish(IReadOnlyList<FishActor> fishes, bool releasedOnly)
    {
        FishActor best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < fishes.Count; i++)
        {
            FishActor fish = fishes[i];
            if (fish == null)
            {
                continue;
            }

            if (releasedOnly && !fish.IsReleasedFish)
            {
                continue;
            }

            Vector3 toFish = fish.transform.position - transform.position;
            float distance = toFish.magnitude;
            if (!releasedOnly && distance > fishInterestDistance)
            {
                continue;
            }

            float forwardScore = Vector3.Dot(transform.forward, toFish.normalized);
            float idealDistance = releasedOnly ? followDistance * 1.15f : followDistance * 1.6f;
            float distanceScore = -Mathf.Abs(distance - idealDistance);
            float centerScore = -Vector3.Distance(fish.transform.position, center) * 0.08f;
            float releasedScore = fish.IsReleasedFish ? 60f : 0f;
            float score = releasedScore + forwardScore * 3.5f + distanceScore + centerScore + Random.Range(0f, 0.75f);

            if (score > bestScore)
            {
                bestScore = score;
                best = fish;
            }
        }

        return best;
    }

    private static bool HasReleasedFish(IReadOnlyList<FishActor> fishes)
    {
        for (int i = 0; i < fishes.Count; i++)
        {
            FishActor fish = fishes[i];
            if (fish != null && fish.IsReleasedFish)
            {
                return true;
            }
        }

        return false;
    }

    private bool MoveCamera(Vector3 desiredPosition, Vector3 lookTarget, float smoothTime, float maxSpeed)
    {
        if (!hasPlacedInitialCamera)
        {
            transform.position = desiredPosition;
            positionVelocity = Vector3.zero;
            hasPlacedInitialCamera = true;
            return true;
        }

        Vector3 nextPosition = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            smoothTime,
            maxSpeed
        );
        transform.position = nextPosition;

        return false;
    }

    private void LookAtTarget(Vector3 lookTarget, float smooth, bool snap)
    {
        if (snap)
        {
            SnapLookAt(lookTarget);
            return;
        }

        SmoothLookAt(lookTarget, smooth);
    }

    private void SnapLookAt(Vector3 lookTarget)
    {
        Vector3 lookDirection = lookTarget - transform.position;
        if (lookDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    private void SmoothLookAt(Vector3 lookTarget, float smooth)
    {
        Vector3 lookDirection = lookTarget - transform.position;
        if (lookDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(lookDirection.normalized, Vector3.up),
            smooth * Time.deltaTime
        );
    }
}
