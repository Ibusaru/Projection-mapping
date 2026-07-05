using System.Collections.Generic;
using UnityEngine;

public partial class FishActor
{
    private void UpdateSwimAnimation()
    {
        float baseReferenceSpeed = Mathf.Max(0.1f, baseSpeed * animationSpeedAtBaseSwim);
        float speedRatio = Mathf.Clamp(currentSpeed / baseReferenceSpeed, 0.35f, 2.25f);
        currentSwimEffort = Mathf.Lerp(
            currentSwimEffort,
            Mathf.InverseLerp(0.45f, 1.65f, speedRatio),
            1f - Mathf.Exp(-animationSmooth * Time.deltaTime)
        );

        float targetAnimationSpeed = Mathf.Clamp(
            speedRatio * animationSpeedMultiplier,
            minAnimationSpeed,
            maxAnimationSpeed
        );
        currentAnimationSpeed = Mathf.Lerp(
            currentAnimationSpeed,
            targetAnimationSpeed,
            1f - Mathf.Exp(-animationSmooth * Time.deltaTime)
        );

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator != null)
            {
                animator.speed = animatorBaseSpeeds[i] * currentAnimationSpeed;
            }
        }
    }

    private void CacheProceduralAnimationBones()
    {
        Transform searchRoot = modelRoot != null ? modelRoot : transform;
        proceduralTailRoot = FindChildByNamePart(searchRoot, "tail");
        proceduralSpineRoot = FindChildByNamePart(searchRoot, "spine_back");
        if (proceduralSpineRoot == null)
        {
            proceduralSpineRoot = FindChildByNamePart(searchRoot, "spine");
        }

        proceduralTailBaseLocalRotation = proceduralTailRoot != null
            ? proceduralTailRoot.localRotation
            : Quaternion.identity;
        proceduralSpineBaseLocalRotation = proceduralSpineRoot != null
            ? proceduralSpineRoot.localRotation
            : Quaternion.identity;
    }

    private void ApplyProceduralSwimPose()
    {
        float swimAnimationRate = Mathf.Lerp(0.86f, 1.18f, currentSwimEffort);
        float tailAmplitude = Mathf.Lerp(slowTailSwayDegrees, fastTailSwayDegrees, currentSwimEffort);
        float wave = Mathf.Sin((Time.time + schoolingNoiseSeed) * tailSwayFrequency * swimAnimationRate);
        float curiousYaw = Time.time < curiousLookUntil ? Mathf.Sin(Time.time * 5.2f) * 7f : 0f;
        float bodySway = wave * tailAmplitude + curiousYaw;

        if (modelRoot != null && modelRoot != transform)
        {
            modelRoot.localRotation = baseModelLocalRotation * Quaternion.Euler(0f, bodySway, 0f);
        }

        if (proceduralSpineRoot != null && proceduralSpineRoot != modelRoot)
        {
            proceduralSpineRoot.localRotation = proceduralSpineBaseLocalRotation
                * Quaternion.Euler(0f, -bodySway * Mathf.Max(0f, proceduralSpineSwayMultiplier), 0f);
        }

        if (proceduralTailRoot != null && proceduralTailRoot != modelRoot)
        {
            float tailYaw = wave * tailAmplitude * Mathf.Max(0f, proceduralTailSwayMultiplier);
            proceduralTailRoot.localRotation = proceduralTailBaseLocalRotation
                * Quaternion.Euler(0f, tailYaw, tailYaw * 0.18f);
        }
    }

    private static Transform FindChildByNamePart(Transform root, string namePart)
    {
        if (root == null || string.IsNullOrEmpty(namePart))
        {
            return null;
        }

        string needle = namePart.ToLowerInvariant();
        Queue<Transform> queue = new Queue<Transform>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();
            if (current != root && current.name.ToLowerInvariant().Contains(needle))
            {
                return current;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                queue.Enqueue(current.GetChild(i));
            }
        }

        return null;
    }

    private Vector3 BlendSchoolingDirection(Vector3 direction)
    {
        if (!enableSchooling)
        {
            schoolDirection = Vector3.Lerp(schoolDirection, Vector3.zero, 0.3f);
            return direction;
        }

        if (Time.time < nextSchoolUpdateTime)
        {
            return ApplySchoolDirection(direction);
        }

        nextSchoolUpdateTime = Time.time + schoolUpdateSeconds + Random.Range(0f, 0.12f);
        IReadOnlyList<FishActor> fishes = AllActiveFishes;
        Vector3 alignment = Vector3.zero;
        Vector3 cohesionCenter = Vector3.zero;
        Vector3 separation = Vector3.zero;
        Vector3 averageForward = Vector3.zero;
        Vector3 gatherCenter = Vector3.zero;
        Vector3 gatherForward = Vector3.zero;
        float totalNeighborWeight = 0f;
        float totalGatherWeight = 0f;
        int neighborCount = 0;
        Vector3 position = transform.position;
        Vector3 forward = StableForward(direction);
        float neighborRadiusValue = Mathf.Max(0.1f, neighborRadius);
        float neighborRadiusSqr = neighborRadiusValue * neighborRadiusValue;
        float gatherRadiusValue = Mathf.Max(neighborRadiusValue, schoolGatherRadius);
        float gatherRadiusSqr = gatherRadiusValue * gatherRadiusValue;
        float queryRadiusSqr = releasedFish ? neighborRadiusSqr : Mathf.Max(neighborRadiusSqr, gatherRadiusSqr);
        float separationRadiusValue = Mathf.Max(0.05f, separationRadius);
        float sameColumnRadiusValue = Mathf.Max(0.05f, sameColumnAvoidanceRadius);
        float verticalSeparationRadiusValue = Mathf.Max(0.05f, verticalSeparationRadius);
        float visionDot = Mathf.Cos(Mathf.Clamp(schoolVisionAngle, 30f, 360f) * 0.5f * Mathf.Deg2Rad);

        for (int i = 0; i < fishes.Count; i++)
        {
            FishActor neighbor = fishes[i];
            if (neighbor == null || neighbor == this)
            {
                continue;
            }

            Vector3 offset = neighbor.transform.position - position;
            float distanceSqr = offset.sqrMagnitude;
            if (distanceSqr > queryRadiusSqr || distanceSqr < 0.0001f)
            {
                continue;
            }

            float distance = Mathf.Sqrt(distanceSqr);
            Vector3 toNeighbor = offset / distance;
            Vector3 neighborForward = neighbor.transform.forward;
            float horizontalDistance = new Vector2(offset.x, offset.z).magnitude;
            bool personalSpace = distance < separationRadiusValue || horizontalDistance < sameColumnRadiusValue;
            bool schoolMate = CanSchoolWith(neighbor);

            if (!schoolMate && !personalSpace)
            {
                continue;
            }

            if (schoolMate && !releasedFish && !neighbor.releasedFish && distanceSqr <= gatherRadiusSqr)
            {
                float gatherWeight = Mathf.Lerp(0.25f, 1f, 1f - distance / gatherRadiusValue);
                gatherCenter += neighbor.transform.position * gatherWeight;
                gatherForward += StableDirection(neighborForward, forward) * gatherWeight;
                totalGatherWeight += gatherWeight;
            }

            if (distanceSqr > neighborRadiusSqr)
            {
                continue;
            }

            if (schoolMate && !personalSpace && Vector3.Dot(forward, toNeighbor) < visionDot)
            {
                continue;
            }

            float distanceWeight = Mathf.Lerp(0.35f, 1f, 1f - distance / neighborRadiusValue);
            float neighborWeight = distanceWeight * (schoolMate ? NeighborSchoolingWeight(neighbor) : 0.35f);
            if (neighborWeight <= 0.001f)
            {
                continue;
            }

            if (schoolMate)
            {
                neighborCount++;
                Vector3 softenedForward = ScaleVertical(StableDirection(neighborForward, forward), 0.65f);
                if (softenedForward.sqrMagnitude > 0.001f)
                {
                    alignment += softenedForward.normalized * neighborWeight;
                }

                averageForward += StableDirection(neighborForward, forward) * neighborWeight;
                cohesionCenter += neighbor.transform.position * neighborWeight;
                totalNeighborWeight += neighborWeight;
            }

            if (distance < separationRadiusValue)
            {
                Vector3 separationAxis = ScaleVertical(toNeighbor, 0.85f);
                if (separationAxis.sqrMagnitude > 0.001f)
                {
                    float personalSpaceWeight = 1f - distance / separationRadiusValue;
                    separation -= separationAxis.normalized * personalSpaceWeight * neighborWeight / Mathf.Max(distance, 0.35f);
                }
            }

            if (horizontalDistance < sameColumnRadiusValue)
            {
                Vector3 horizontalEscape = horizontalDistance > 0.001f
                    ? new Vector3(-offset.x, 0f, -offset.z).normalized
                    : RandomEscapeDirection(neighbor);
                float columnWeight = 1f - horizontalDistance / sameColumnRadiusValue;
                separation += horizontalEscape * columnWeight * sameColumnAvoidanceWeight * neighborWeight;
            }

            float verticalDistance = Mathf.Abs(offset.y);
            if (verticalDistance < verticalSeparationRadiusValue)
            {
                float pushDirection = offset.y >= 0f ? -1f : 1f;
                separation += Vector3.up * pushDirection * (1f - verticalDistance / verticalSeparationRadiusValue) * verticalSeparationWeight * neighborWeight;
            }
        }

        float schoolingInfluence = EffectiveSchoolingStrength();
        Vector3 boidDirection = Vector3.zero;
        if (neighborCount > 0 && alignment.sqrMagnitude > 0.001f)
        {
            boidDirection += alignment.normalized * alignmentWeight * schoolingInfluence;
        }

        if (neighborCount > 0 && totalNeighborWeight > 0.001f)
        {
            Vector3 schoolForward = averageForward.sqrMagnitude > 0.001f
                ? StableDirection(averageForward, forward)
                : forward;
            Vector3 cohesionTarget = cohesionCenter / totalNeighborWeight + SchoolFormationOffset(schoolForward);
            Vector3 cohesionOffset = ScaleVertical(cohesionTarget - position, verticalCohesionScale + verticalSchoolingWeight);
            if (cohesionOffset.sqrMagnitude > 0.001f)
            {
                float cohesionPressure = Mathf.InverseLerp(
                    0.35f,
                    Mathf.Max(1.5f, neighborRadiusValue * 0.75f),
                    cohesionOffset.magnitude
                );
                boidDirection += cohesionOffset.normalized
                    * cohesionWeight
                    * Mathf.Lerp(0.55f, 1.25f, cohesionPressure)
                    * schoolingInfluence;
            }
        }

        if (!releasedFish && totalGatherWeight > 0.001f)
        {
            float gatherInfluence = Mathf.Lerp(0.35f, 1f, schoolingInfluence);
            Vector3 gatheredCenter = gatherCenter / totalGatherWeight;
            Vector3 gatherOffset = ScaleVertical(gatheredCenter - position, verticalCohesionScale);
            float gatherDistance = gatherOffset.magnitude;
            if (gatherDistance > schoolGatherDeadZone && gatherOffset.sqrMagnitude > 0.001f)
            {
                float gatherPressure = Mathf.InverseLerp(schoolGatherDeadZone, gatherRadiusValue, gatherDistance);
                boidDirection += gatherOffset.normalized * schoolGatherWeight * gatherPressure * gatherInfluence;
            }

            if (gatherForward.sqrMagnitude > 0.001f)
            {
                boidDirection += StableDirection(gatherForward, forward) * schoolGatherForwardWeight * gatherInfluence;
            }
        }

        Vector3 homeDirection = SchoolGroupHomeDirection(position, schoolingInfluence);
        if (homeDirection.sqrMagnitude > 0.001f)
        {
            boidDirection += homeDirection;
        }

        if (separation.sqrMagnitude > 0.001f)
        {
            boidDirection += separation.normalized * Mathf.Lerp(soloSeparationWeight, separationWeight, schoolingInfluence);
        }

        if (neighborCount == 0 && totalGatherWeight <= 0.001f && boidDirection.sqrMagnitude <= 0.001f)
        {
            schoolDirection = Vector3.Lerp(schoolDirection, DepthDrift(schoolingInfluence), 0.25f);
            return ApplySchoolDirection(direction);
        }

        boidDirection += DepthDrift(schoolingInfluence);
        boidDirection += SchoolNoiseDirection() * schoolNoiseWeight * (1f - schoolingInfluence * 0.35f);
        schoolDirection = Vector3.Lerp(schoolDirection, boidDirection, 0.5f);
        return ApplySchoolDirection(direction);
    }

    private Vector3 LimitVerticalSwim(Vector3 direction)
    {
        Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);
        if (horizontal.sqrMagnitude < 0.001f)
        {
            horizontal = new Vector3(transform.forward.x, 0f, transform.forward.z);
        }

        if (horizontal.sqrMagnitude < 0.001f)
        {
            horizontal = Vector3.forward;
        }

        float verticalLimit = Mathf.Max(maxVerticalSwimDirection, 0.1f);
        float vertical = Mathf.Clamp(direction.y, -verticalLimit, verticalLimit);
        return (horizontal.normalized + Vector3.up * vertical).normalized;
    }

    private void UpdateSchoolingMode()
    {
        if (Time.time >= nextSchoolModeTime)
        {
            PickNextSchoolingMode(false);
        }

        float targetStrength = isSchoolingMode ? 1f : 0f;
        currentSchoolStrength = Mathf.MoveTowards(
            currentSchoolStrength,
            targetStrength,
            schoolingBlendSpeed * Time.deltaTime
        );
    }

    private void PickNextSchoolingMode(bool initial)
    {
        isSchoolingMode = Random.value < schoolModeChance;
        Vector2 durationRange = isSchoolingMode ? schoolingSecondsRange : soloSecondsRange;
        nextSchoolModeTime = Time.time + Random.Range(durationRange.x, durationRange.y);

        if (isSchoolingMode)
        {
            PickSchoolSlot();
        }

        if (initial)
        {
            currentSchoolStrength = isSchoolingMode ? Random.Range(0.65f, 1f) : Random.Range(0f, 0.25f);
        }
    }

    private void PickSchoolSlot()
    {
        float sideJitter = Mathf.Abs(schoolSlotSideRange.y - schoolSlotSideRange.x) * 0.035f;
        float depthJitter = Mathf.Abs(schoolSlotDepthRange.y - schoolSlotDepthRange.x) * 0.12f;
        float forwardJitter = Mathf.Abs(schoolSlotForwardRange.y - schoolSlotForwardRange.x) * 0.045f;
        schoolFormationOffset = new Vector3(
            Random.Range(-sideJitter, sideJitter),
            Random.Range(-depthJitter, depthJitter),
            Random.Range(-forwardJitter, forwardJitter)
        );
    }

    private Vector3 RandomEscapeDirection(FishActor neighbor)
    {
        float seed = schoolingNoiseSeed * 0.73f + neighbor.schoolingNoiseSeed * 1.37f;
        return new Vector3(Mathf.Cos(seed), 0f, Mathf.Sin(seed)).normalized;
    }

    private Vector3 ApplySchoolDirection(Vector3 direction)
    {
        Vector3 blendedDirection = direction + schoolDirection;
        return blendedDirection.sqrMagnitude > 0.001f ? blendedDirection.normalized : direction;
    }

    private float EffectiveSchoolingStrength()
    {
        float strength = currentSchoolStrength;
        if (releasedFish)
        {
            strength *= Mathf.Clamp01(releasedFishSchoolingMultiplier);
        }

        return Mathf.Clamp01(strength);
    }

    private float NeighborSchoolingWeight(FishActor neighbor)
    {
        return neighbor.releasedFish ? Mathf.Clamp01(releasedFishNeighborWeight) : 1f;
    }

    private Vector3 SchoolFormationOffset(Vector3 schoolForward)
    {
        Vector3 flatForward = new Vector3(schoolForward.x, 0f, schoolForward.z);
        if (flatForward.sqrMagnitude < 0.001f)
        {
            flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);
        }

        if (flatForward.sqrMagnitude < 0.001f)
        {
            flatForward = Vector3.forward;
        }

        flatForward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, flatForward).normalized;
        Vector3 localSlot = SchoolFormationSlotLocalOffset();
        return right * localSlot.x
            + Vector3.up * localSlot.y
            + flatForward * localSlot.z;
    }

    private Vector3 SchoolFormationSlotLocalOffset()
    {
        int cohortIndex = SchoolCohortIndex(out int cohortCount);
        cohortIndex = Mathf.Max(0, cohortIndex);
        cohortCount = Mathf.Max(1, cohortCount);

        int laneCount = Mathf.Clamp(
            Mathf.CeilToInt(Mathf.Sqrt(cohortCount) * 0.9f),
            1,
            MaxSchoolFormationLanes
        );
        laneCount = Mathf.Min(laneCount, cohortCount);
        if (cohortCount >= 3 && laneCount % 2 == 0)
        {
            laneCount++;
        }
        laneCount = Mathf.Min(laneCount, cohortCount);

        int row = cohortIndex / laneCount;
        int lane = cohortIndex % laneCount;
        int maxRow = Mathf.Max(1, Mathf.CeilToInt(cohortCount / (float)laneCount) - 1);
        float laneT = laneCount <= 1 ? 0.5f : lane / (float)(laneCount - 1);
        if (row % 2 == 1)
        {
            float stagger = 0.5f / Mathf.Max(1, laneCount - 1);
            laneT = Mathf.Clamp01(laneT + stagger);
        }

        float rowT = row / (float)maxRow;
        float depthT = Mathf.Repeat(cohortIndex * 0.381966f + row * 0.17f, 1f);
        return new Vector3(
            Mathf.Lerp(schoolSlotSideRange.x, schoolSlotSideRange.y, laneT),
            Mathf.Lerp(schoolSlotDepthRange.x, schoolSlotDepthRange.y, depthT),
            Mathf.Lerp(schoolSlotForwardRange.y, schoolSlotForwardRange.x, rowT)
        ) + schoolFormationOffset;
    }

    private int SchoolCohortIndex(out int cohortCount)
    {
        int index = -1;
        cohortCount = 0;
        for (int i = 0; i < ActiveFishes.Count; i++)
        {
            FishActor fish = ActiveFishes[i];
            if (!IsSchoolFormationMember(fish))
            {
                continue;
            }

            if (fish == this)
            {
                index = cohortCount;
            }

            cohortCount++;
        }

        return index;
    }

    private bool IsSchoolFormationMember(FishActor fish)
    {
        return CanSchoolWith(fish);
    }

    private bool CanSchoolWith(FishActor fish)
    {
        if (fish == null || fish == this || fish.species == "jellyfish" || species == "jellyfish")
        {
            return false;
        }

        if (fish.releasedFish != releasedFish)
        {
            return false;
        }

        bool hasGroup = schoolGroupId != UnassignedSchoolGroupId;
        bool otherHasGroup = fish.schoolGroupId != UnassignedSchoolGroupId;
        if (hasGroup || otherHasGroup)
        {
            return hasGroup && otherHasGroup && schoolGroupId == fish.schoolGroupId;
        }

        return true;
    }

    private Vector3 SchoolGroupHomeDirection(Vector3 position, float schoolingInfluence)
    {
        if (schoolGroupId == UnassignedSchoolGroupId)
        {
            return Vector3.zero;
        }

        Vector3 offset = ScaleVertical(schoolGroupCenter - position, 0.35f);
        float distance = offset.magnitude;
        if (distance < schoolGroupRadius || offset.sqrMagnitude < 0.001f)
        {
            return Vector3.zero;
        }

        float pressure = Mathf.InverseLerp(schoolGroupRadius, schoolGroupRadius * 2.2f, distance);
        return offset.normalized * schoolGroupHomeWeight * pressure * Mathf.Lerp(0.45f, 1f, schoolingInfluence);
    }

    private Vector3 DepthDrift(float schoolingInfluence)
    {
        return Vector3.up
            * Mathf.Sin(Time.time * 0.37f + SpawnTime + schoolingNoiseSeed)
            * preferredDepthDrift
            * Mathf.Lerp(1f, 0.45f, schoolingInfluence);
    }

    private Vector3 SchoolNoiseDirection()
    {
        float seed = schoolingNoiseSeed + Time.time * 0.23f;
        Vector3 noise = new Vector3(
            Mathf.Sin(seed * 1.7f),
            Mathf.Sin(seed * 0.9f) * 0.25f,
            Mathf.Cos(seed * 1.3f)
        );
        return noise.sqrMagnitude > 0.001f ? noise.normalized : Vector3.zero;
    }

    private Vector3 StableForward(Vector3 fallback)
    {
        return StableDirection(transform.forward, fallback);
    }

    private static Vector3 StableDirection(Vector3 direction, Vector3 fallback)
    {
        if (direction.sqrMagnitude > 0.001f)
        {
            return direction.normalized;
        }

        if (fallback.sqrMagnitude > 0.001f)
        {
            return fallback.normalized;
        }

        return Vector3.forward;
    }

    private static Vector3 ScaleVertical(Vector3 vector, float verticalScale)
    {
        return new Vector3(vector.x, vector.y * Mathf.Clamp01(verticalScale), vector.z);
    }

    private Vector3 BlendCameraAwareness(Vector3 direction)
    {
        if (releasedFish)
        {
            return direction;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return direction;
            }
        }

        Vector3 toCamera = mainCamera.transform.position - transform.position;
        float distance = toCamera.magnitude;
        if (distance > cameraAwarenessDistance || distance < 0.001f)
        {
            return direction;
        }

        float awareness = 1f - distance / cameraAwarenessDistance;
        curiousLookUntil = Time.time + curiousLookSeconds * awareness;
        Vector3 awayFromCamera = -toCamera;
        awayFromCamera.y = Mathf.Clamp(awayFromCamera.y, -0.2f, 0.2f);
        if (awayFromCamera.sqrMagnitude < 0.001f)
        {
            awayFromCamera = transform.position - mainCamera.transform.position + RandomEscapeDirection(this);
        }

        float avoidance = Mathf.Clamp01((cameraLookWeight + cameraAvoidanceWeight) * awareness);
        Vector3 awareDirection = Vector3.Lerp(direction, awayFromCamera.normalized, avoidance);
        return awareDirection.normalized;
    }
}
