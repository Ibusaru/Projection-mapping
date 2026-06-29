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

    private Vector3 BlendSchoolingDirection(Vector3 direction)
    {
        if (!enableSchooling)
        {
            schoolDirection = Vector3.Lerp(schoolDirection, Vector3.zero, 0.3f);
            return direction;
        }

        if (Time.time < nextSchoolUpdateTime)
        {
            return (direction + schoolDirection * currentSchoolStrength).normalized;
        }

        nextSchoolUpdateTime = Time.time + schoolUpdateSeconds + Random.Range(0f, 0.12f);
        IReadOnlyList<FishActor> fishes = AllActiveFishes;
        Vector3 alignment = Vector3.zero;
        Vector3 cohesion = Vector3.zero;
        Vector3 separation = Vector3.zero;
        Vector3 averageForward = Vector3.zero;
        float averageNeighborY = 0f;
        int neighborCount = 0;

        for (int i = 0; i < fishes.Count; i++)
        {
            FishActor neighbor = fishes[i];
            if (neighbor == null || neighbor == this)
            {
                continue;
            }

            Vector3 offset = neighbor.transform.position - transform.position;
            Vector3 horizontalOffset = new Vector3(offset.x, 0f, offset.z);
            float horizontalDistance = horizontalOffset.magnitude;
            if (horizontalDistance > neighborRadius)
            {
                continue;
            }

            neighborCount++;
            Vector3 neighborForward = neighbor.transform.forward;
            Vector3 horizontalForward = new Vector3(neighborForward.x, 0f, neighborForward.z);
            alignment += horizontalForward;
            averageForward += horizontalForward;
            cohesion += new Vector3(neighbor.transform.position.x, 0f, neighbor.transform.position.z);
            averageNeighborY += neighbor.transform.position.y;

            if (horizontalDistance < sameColumnAvoidanceRadius)
            {
                Vector3 escape = horizontalDistance > 0.001f
                    ? -horizontalOffset.normalized
                    : RandomEscapeDirection(neighbor);
                separation += escape * sameColumnAvoidanceWeight * (1f - horizontalDistance / sameColumnAvoidanceRadius);
            }
            else if (horizontalDistance < separationRadius)
            {
                separation -= horizontalOffset.normalized * (1f - horizontalDistance / separationRadius);
            }

            float verticalDistance = Mathf.Abs(offset.y);
            if (verticalDistance < verticalSeparationRadius)
            {
                float pushDirection = offset.y >= 0f ? -1f : 1f;
                separation += Vector3.up * pushDirection * (1f - verticalDistance / verticalSeparationRadius) * verticalSeparationWeight;
            }
        }

        if (neighborCount == 0)
        {
            schoolDirection = Vector3.Lerp(schoolDirection, Vector3.zero, 0.45f);
            return (direction + schoolDirection * currentSchoolStrength).normalized;
        }

        float schoolingInfluence = currentSchoolStrength;
        alignment = alignment.normalized * alignmentWeight * schoolingInfluence;
        Vector3 schoolForward = averageForward.sqrMagnitude > 0.001f
            ? averageForward.normalized
            : new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        if (schoolForward.sqrMagnitude < 0.001f)
        {
            schoolForward = Vector3.forward;
        }

        Vector3 schoolRight = new Vector3(schoolForward.z, 0f, -schoolForward.x);
        Vector3 slotOffset = schoolRight * schoolSlotOffset.x + schoolForward * schoolSlotOffset.y;
        Vector3 cohesionTarget = cohesion / neighborCount + slotOffset;
        Vector3 horizontalCohesion = cohesionTarget - new Vector3(transform.position.x, 0f, transform.position.z);
        cohesion = horizontalCohesion.normalized * cohesionWeight * schoolingInfluence;
        float depthOffset = (averageNeighborY / neighborCount) - transform.position.y;
        cohesion += Vector3.up * Mathf.Clamp(depthOffset, -1f, 1f) * verticalSchoolingWeight * schoolingInfluence;
        cohesion += Vector3.up * Mathf.Sin(Time.time * 0.37f + SpawnTime) * preferredDepthDrift * (1f - schoolingInfluence * 0.35f);
        separation = separation.normalized * Mathf.Lerp(soloSeparationWeight, separationWeight, schoolingInfluence);
        schoolDirection = Vector3.Lerp(schoolDirection, alignment + cohesion + separation, 0.5f);
        return (direction + schoolDirection).normalized;
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

        float vertical = Mathf.Clamp(direction.y, -maxVerticalSwimDirection, maxVerticalSwimDirection);
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
        schoolSlotOffset = new Vector2(
            Random.Range(schoolSlotSideRange.x, schoolSlotSideRange.y),
            Random.Range(schoolSlotForwardRange.x, schoolSlotForwardRange.y)
        );
    }

    private Vector3 RandomEscapeDirection(FishActor neighbor)
    {
        float seed = schoolingNoiseSeed * 0.73f + neighbor.schoolingNoiseSeed * 1.37f;
        return new Vector3(Mathf.Cos(seed), 0f, Mathf.Sin(seed)).normalized;
    }

    private Vector3 BlendCameraAwareness(Vector3 direction)
    {
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
