using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public partial class FishActor
{
    private const float MinReadableNicknameTextViewportHeight = 0.055f;
    private const float BelowLineNicknameLayoutPenalty = 0.12f;
    private const float MaxNicknameTagRevealDistance = 6.2f;
    private const float MinNicknameTagRevealSeconds = 0.64f;
    private const float MinNicknameTagRetreatSeconds = 0.24f;
    private const float StickyNicknameTagOverflowTolerance = 0.025f;
    private const float MaxNicknameTextViewportWidth = 0.46f;
    private static TMP_FontAsset cachedNicknameJapaneseFontAsset;
    private static bool attemptedNicknameJapaneseFontAsset;
    private static bool warnedNicknameJapaneseFontFallback;

    public bool IsNicknameTagVisibleForCamera =>
        cameraFocused
        && nicknameTagRevealProgress >= 0.98f
        && (
            (nicknameTagLine != null && nicknameTagLine.enabled)
            || (nicknameLabel != null && nicknameLabel.gameObject.activeInHierarchy)
            || (nicknameFallbackLabel != null && nicknameFallbackLabel.gameObject.activeInHierarchy)
        );

    private void UpdateLabel()
    {
        if (!ShouldUseNicknameTag())
        {
            HideNicknameTag();
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                LogNicknameTagDebug("no main camera");
                HideNicknameTag();
                return;
            }
        }

        Vector3 anchorPosition = LabelAnchorPosition();
        float distance = Vector3.Distance(mainCamera.transform.position, anchorPosition);
        bool shouldReveal = ShouldRevealNicknameTag(anchorPosition, distance);
        float reveal = AdvanceNicknameTagReveal(shouldReveal);

        if (reveal <= 0.001f)
        {
            HideNicknameWorldTag();
            return;
        }

        EnsureNicknameLabel();
        EnsureNicknameTagLine();
        if (!HasNicknameTagLabel() || nicknameTagLine == null)
        {
            LogNicknameTagDebug("no world label component");
            HideNicknameWorldTag();
            return;
        }

        ShowNicknameWorldTag(anchorPosition, reveal);
    }

    private void EnsureNicknameLabel()
    {
        if (!ShouldUseNicknameTag() || !createNicknameLabelWhenMissing)
        {
            return;
        }

        CleanupDuplicateNicknameLabelObjects();

        bool needsJapaneseFont = ShouldUseNicknameFallbackText();
        TMP_FontAsset preferredFontAsset = needsJapaneseFont
            ? ResolveNicknameJapaneseFontAsset()
            : ResolveNicknameFontAsset();
        if (preferredFontAsset != null)
        {
            EnsureNicknameTmpLabel(preferredFontAsset);
            ApplyNicknameTmpFont(nicknameLabel, preferredFontAsset);
        }

        if (needsJapaneseFont && preferredFontAsset == null)
        {
            HideNicknameTmpLabel();
        }

        if (needsJapaneseFont && preferredFontAsset == null && nicknameFallbackLabel == null)
        {
            EnsureNicknameFallbackLabel();
        }

        if (nicknameFallbackLabel == null && nicknameLabel == null)
        {
            EnsureNicknameTmpLabel();
        }

        if (nicknameLabel == null && useBuiltInNicknameFallback && nicknameFallbackLabel == null)
        {
            EnsureNicknameFallbackLabel();
        }
    }

    private void EnsureNicknameTmpLabel(TMP_FontAsset fontAsset = null)
    {
        if (nicknameLabel != null)
        {
            return;
        }

        fontAsset ??= ResolveNicknameFontAsset();
        if (fontAsset == null && !warnedMissingTmpResources)
        {
            Debug.LogWarning("FishActor: TextMesh Pro font asset was not found; nickname labels may not render until TMP Essential Resources are imported.");
            warnedMissingTmpResources = true;
        }

        if (fontAsset == null)
        {
            return;
        }

        GameObject labelObject = new GameObject("Nickname Label");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = Vector3.up;

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = nicknameTagFontSize;
        label.color = new Color(0.9f, 1f, 1f, 0.92f);
        label.richText = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.text = Nickname;
        ApplyNicknameTmpFont(label, fontAsset);

        Renderer labelRenderer = label.GetComponent<Renderer>();
        if (labelRenderer != null)
        {
            labelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            labelRenderer.receiveShadows = false;
            labelRenderer.sortingOrder = 200;
        }

        nicknameLabel = label;
        nicknameLabel.gameObject.SetActive(false);
    }

    private void CleanupDuplicateNicknameLabelObjects()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null || !IsGeneratedNicknameLabelObject(child))
            {
                continue;
            }

            bool isCurrentLabel = nicknameLabel != null && child == nicknameLabel.transform;
            bool isCurrentFallback = nicknameFallbackLabel != null && child == nicknameFallbackLabel.transform;
            if (isCurrentLabel || isCurrentFallback)
            {
                continue;
            }

            DestroyGeneratedNicknameObject(child.gameObject);
        }
    }

    private bool IsGeneratedNicknameLabelObject(Transform child)
    {
        return child.name == "Nickname Label"
            || child.name == "Nickname Label Fallback";
    }

    private static void DestroyGeneratedNicknameObject(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }

    private void HideNicknameTmpLabel()
    {
        if (nicknameLabel == null)
        {
            return;
        }

        nicknameLabel.gameObject.SetActive(false);
        Renderer renderer = nicknameLabel.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = false;
        }
    }

    private void EnsureNicknameFallbackLabel()
    {
        if (nicknameFallbackLabel != null)
        {
            return;
        }

        GameObject labelObject = new GameObject("Nickname Label Fallback");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = Vector3.up;

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 128;
        label.characterSize = NicknameFallbackCharacterSize();
        label.color = new Color(0.9f, 1f, 1f, 0.96f);
        label.text = Nickname;

        Font font = ResolveNicknameFallbackFont();

        if (font != null)
        {
            label.font = font;
            font.RequestCharactersInTexture(label.text, label.fontSize, label.fontStyle);
        }

        nicknameFallbackRenderer = label.GetComponent<Renderer>();
        if (nicknameFallbackRenderer != null)
        {
            nicknameFallbackRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            nicknameFallbackRenderer.receiveShadows = false;
            nicknameFallbackRenderer.sortingOrder = 210;
            nicknameFallbackRenderer.sharedMaterial = CreateNicknameFallbackMaterial(font, label.color);

            Material fallbackMaterial = nicknameFallbackRenderer.sharedMaterial;
            if (fallbackMaterial != null)
            {
                fallbackMaterial.renderQueue = 4000;
                if (fallbackMaterial.HasProperty("_ZTest"))
                {
                    fallbackMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                }
            }
        }

        nicknameFallbackLabel = label;
        nicknameFallbackLabel.gameObject.SetActive(false);
    }

    private void EnsureNicknameTagLine()
    {
        if (!ShouldUseNicknameTag() || nicknameTagLine != null)
        {
            return;
        }

        GameObject lineObject = new GameObject("Nickname Tag Line");
        lineObject.transform.SetParent(transform, false);

        nicknameTagLine = lineObject.AddComponent<LineRenderer>();
        nicknameTagLine.useWorldSpace = true;
        nicknameTagLine.positionCount = 3;
        nicknameTagLine.startWidth = nicknameTagLineWidth;
        nicknameTagLine.endWidth = nicknameTagLineWidth;
        nicknameTagLine.numCapVertices = 3;
        nicknameTagLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        nicknameTagLine.receiveShadows = false;
        Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (lineShader == null)
        {
            lineShader = Shader.Find("Sprites/Default");
        }

        if (lineShader != null)
        {
            Material lineMaterial = new Material(lineShader);
            nicknameTagLine.sharedMaterial = lineMaterial;
            lineMaterial.renderQueue = 4000;
            if (lineMaterial.HasProperty("_ZTest"))
            {
                lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
        }

        nicknameTagLine.startColor = new Color(0.88f, 1f, 1f, 0.72f);
        nicknameTagLine.endColor = new Color(0.88f, 1f, 1f, 0.92f);
        nicknameTagLine.enabled = false;
    }

    private void HideNicknameTag()
    {
        HideNicknameWorldTag();
    }

    private void HideNicknameWorldTag()
    {
        if (nicknameLabel != null)
        {
            nicknameLabel.gameObject.SetActive(false);
        }

        if (nicknameFallbackLabel != null)
        {
            nicknameFallbackLabel.gameObject.SetActive(false);
        }

        if (nicknameTagLine != null)
        {
            nicknameTagLine.enabled = false;
        }
    }

    private Vector3 LabelAnchorPosition()
    {
        return VisualCenter + Vector3.up * (CameraFocusRadius * nicknameTagAnchorLift);
    }

    private float NicknameVisibleDistance(Vector3 anchorPosition)
    {
        float revealDistance = Mathf.Clamp(nicknameTagRevealDistance, 0.1f, MaxNicknameTagRevealDistance);
        if (nicknameTagRevealProgress > 0.01f)
        {
            revealDistance += Mathf.Max(0f, nicknameTagRevealHysteresisDistance);
        }

        float legacyVisibleDistance = releasedFish
            ? Mathf.Max(labelVisibleDistance, nearbyLabelVisibleDistance, focusedLabelVisibleDistance)
            : (showDefaultNicknameWhenNearby
                ? Mathf.Max(defaultNearbyLabelVisibleDistance, defaultForwardConeLabelDistance, defaultFocusedLabelVisibleDistance)
                : defaultFocusedLabelVisibleDistance);

        if (IsInNicknameForwardCone(anchorPosition))
        {
            legacyVisibleDistance = Mathf.Max(
                legacyVisibleDistance,
                releasedFish ? labelForwardConeDistance : defaultForwardConeLabelDistance
            );
        }

        return legacyVisibleDistance > 0f
            ? Mathf.Min(revealDistance, legacyVisibleDistance)
            : revealDistance;
    }

    private bool IsInNicknameForwardCone(Vector3 anchorPosition)
    {
        if (mainCamera == null)
        {
            return false;
        }

        Vector3 toLabel = anchorPosition - mainCamera.transform.position;
        if (toLabel.sqrMagnitude < 0.001f)
        {
            return true;
        }

        Vector3 direction = toLabel.normalized;
        if (Vector3.Dot(mainCamera.transform.forward, direction) <= 0f)
        {
            return false;
        }

        float angle = Vector3.Angle(mainCamera.transform.forward, direction);
        return angle <= Mathf.Clamp(labelForwardConeAngle, 4f, 120f);
    }

    private bool ShouldRevealNicknameTag(Vector3 anchorPosition, float distance)
    {
        if (!cameraFocused)
        {
            return false;
        }

        if (!IsNicknameAnchorInsideViewport(anchorPosition))
        {
            LogNicknameTagDebug("outside camera frame");
            return false;
        }

        float apparentRadius = NicknameApparentFocusRadiusViewport(anchorPosition);
        float minApparentRadius = Mathf.Max(0f, nicknameTagMinApparentRadiusViewport);
        if (nicknameTagRevealProgress > 0.01f)
        {
            minApparentRadius *= 0.82f;
        }

        if (apparentRadius < minApparentRadius)
        {
            LogNicknameTagDebug($"focused but too small apparentRadius={apparentRadius:0.000} min={minApparentRadius:0.000}");
            return false;
        }

        float visibleDistance = NicknameVisibleDistance(anchorPosition);
        if (distance > visibleDistance)
        {
            LogNicknameTagDebug($"focused but outside reveal range distance={distance:0.00} visible={visibleDistance:0.00}");
            return false;
        }

        return true;
    }

    private float AdvanceNicknameTagReveal(bool shouldReveal)
    {
        float target = shouldReveal ? 1f : 0f;
        float duration = shouldReveal
            ? Mathf.Max(nicknameTagRevealSeconds, MinNicknameTagRevealSeconds)
            : Mathf.Max(nicknameTagRetreatSeconds, MinNicknameTagRetreatSeconds);
        if (duration <= 0.001f)
        {
            nicknameTagRevealProgress = target;
        }
        else
        {
            nicknameTagRevealProgress = Mathf.MoveTowards(
                nicknameTagRevealProgress,
                target,
                Time.deltaTime / duration
            );
        }

        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(nicknameTagRevealProgress));
    }

    private float NicknameApparentFocusRadiusViewport(Vector3 anchorPosition)
    {
        if (mainCamera == null)
        {
            return 0f;
        }

        Vector3 viewport = mainCamera.WorldToViewportPoint(anchorPosition);
        if (viewport.z <= 0.01f)
        {
            return 0f;
        }

        float worldHeight = mainCamera.orthographic
            ? mainCamera.orthographicSize * 2f
            : 2f * viewport.z * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return CameraFocusRadius / Mathf.Max(0.0001f, worldHeight);
    }

    private bool IsNicknameAnchorInsideViewport(Vector3 anchorPosition)
    {
        if (mainCamera == null)
        {
            return false;
        }

        Vector3 viewport = mainCamera.WorldToViewportPoint(anchorPosition);
        if (viewport.z <= 0.01f)
        {
            return false;
        }

        float padding = Mathf.Min(0.015f, Mathf.Clamp(nicknameTagViewportPadding, 0f, 0.45f) * 0.35f);
        return viewport.x >= padding
            && viewport.x <= 1f - padding
            && viewport.y >= padding
            && viewport.y <= 1f - padding;
    }

    private struct NicknameTagLayout
    {
        public bool valid;
        public Vector3 anchorViewport;
        public Vector2 elbowViewport;
        public Vector2 endViewport;
        public int horizontalSide;
        public int textSide;
        public float distanceScale;
        public float textHeightViewport;
        public float textGapViewport;
        public float maxTextWidthViewport;
        public float lineWidthViewport;
        public float fitScale;
    }

    private void ShowNicknameWorldTag(Vector3 anchorPosition, float reveal)
    {
        if (nicknameTagLine == null || mainCamera == null)
        {
            return;
        }

        NicknameTagLayout layout = PickNicknameTagLayout(anchorPosition);
        if (!layout.valid)
        {
            HideNicknameWorldTag();
            return;
        }

        nicknameTagLine.gameObject.SetActive(true);
        Vector3 elbowPosition = ViewportToWorld(layout.elbowViewport, layout.anchorViewport.z);
        Vector3 endPosition = ViewportToWorld(layout.endViewport, layout.anchorViewport.z);

        float diagonalProgress = Mathf.Clamp01(reveal / 0.52f);
        float horizontalProgress = Mathf.Clamp01((reveal - 0.42f) / 0.58f);
        Vector3 currentElbow = Vector3.Lerp(anchorPosition, elbowPosition, diagonalProgress);
        Vector3 currentEnd = horizontalProgress > 0f
            ? Vector3.Lerp(elbowPosition, endPosition, horizontalProgress)
            : currentElbow;

        ApplyNicknameTagWorldScale(reveal, layout);
        nicknameTagLine.positionCount = 3;
        nicknameTagLine.SetPosition(0, anchorPosition);
        nicknameTagLine.SetPosition(1, currentElbow);
        nicknameTagLine.SetPosition(2, currentEnd);
        nicknameTagLine.startColor = new Color(0.88f, 1f, 1f, 0.56f * reveal);
        nicknameTagLine.endColor = new Color(0.88f, 1f, 1f, 0.92f * reveal);
        nicknameTagLine.enabled = reveal > 0.01f;

        float textReveal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.56f, 1f, reveal));
        ShowNicknameLabel(layout, textReveal);
    }

    private NicknameTagLayout PickNicknameTagLayout(Vector3 anchorPosition)
    {
        NicknameTagLayout best = default;
        if (mainCamera == null)
        {
            return best;
        }

        Vector3 anchorViewport = mainCamera.WorldToViewportPoint(anchorPosition);
        if (anchorViewport.z <= 0.01f)
        {
            return best;
        }

        float distanceScale = NicknameTagDistanceScale();
        int preferredSide = anchorViewport.x >= 0.5f ? -1 : 1;
        int[] horizontalSides = { preferredSide, -preferredSide };
        int[] verticalSides = { 1 };
        float minFitScale = Mathf.Clamp(nicknameTagMinFitScale, 0.25f, 1f);
        float padding = Mathf.Clamp(nicknameTagViewportPadding, 0.02f, 0.22f);
        float bestScore = float.PositiveInfinity;

        if (TryPickStableNicknameTagLayout(anchorViewport, distanceScale, minFitScale, padding, out NicknameTagLayout stable))
        {
            return stable;
        }

        for (int verticalIndex = 0; verticalIndex < verticalSides.Length; verticalIndex++)
        {
            for (int sideIndex = 0; sideIndex < horizontalSides.Length; sideIndex++)
            {
                for (float fitScale = 1f; fitScale >= minFitScale; fitScale -= 0.08f)
                {
                    NicknameTagLayout candidate = BuildNicknameTagLayout(
                        anchorViewport,
                        horizontalSides[sideIndex],
                        verticalSides[verticalIndex],
                        distanceScale,
                        fitScale
                    );
                    if (NicknameTagFitsViewport(candidate, padding))
                    {
                        RememberNicknameTagLayout(candidate);
                        return candidate;
                    }

                    float overflow = NicknameTagViewportOverflow(candidate, padding);
                    float score = overflow + (candidate.textSide < 0 ? BelowLineNicknameLayoutPenalty : 0f);
                    if (!best.valid
                        || score < bestScore
                        || (Mathf.Abs(score - bestScore) <= 0.0001f && candidate.fitScale > best.fitScale))
                    {
                        best = candidate;
                        bestScore = score;
                    }
                }
            }
        }

        best.valid = true;
        RememberNicknameTagLayout(best);
        return best;
    }

    private bool TryPickStableNicknameTagLayout(Vector3 anchorViewport, float distanceScale, float minFitScale, float padding, out NicknameTagLayout layout)
    {
        layout = default;
        if (!hasStableNicknameTagLayout)
        {
            return false;
        }

        int horizontalSide = stableNicknameTagHorizontalSide >= 0 ? 1 : -1;
        int textSide = 1;
        float startingFitScale = Mathf.Clamp(stableNicknameTagFitScale, minFitScale, 1f);
        for (float fitScale = startingFitScale; fitScale >= minFitScale; fitScale -= 0.08f)
        {
            NicknameTagLayout candidate = BuildNicknameTagLayout(
                anchorViewport,
                horizontalSide,
                textSide,
                distanceScale,
                fitScale
            );
            if (NicknameTagViewportOverflow(candidate, padding) <= StickyNicknameTagOverflowTolerance)
            {
                RememberNicknameTagLayout(candidate);
                layout = candidate;
                return true;
            }
        }

        return false;
    }

    private NicknameTagLayout BuildNicknameTagLayout(Vector3 anchorViewport, int horizontalSide, int textSide, float distanceScale, float fitScale)
    {
        float layoutScale = distanceScale * fitScale;
        float diagonalX = Mathf.Max(0.025f, Mathf.Abs(nicknameTagOffset.x)) * layoutScale;
        float diagonalY = Mathf.Max(0.035f, Mathf.Abs(nicknameTagOffset.y)) * layoutScale;
        float widthRatio = Mathf.Clamp(nicknameTagTextMaxWidthRatio, 0.25f, 1.2f);
        float lineLength = Mathf.Max(0.07f, nicknameTagHorizontalLength) * layoutScale;
        float baseTextHeight = Mathf.Max(0.025f, nicknameTagTextViewportHeight);
        float textHeight = Mathf.Max(MinReadableNicknameTextViewportHeight, baseTextHeight * layoutScale);
        float textGap = Mathf.Clamp(Mathf.Max(0.0015f, nicknameTagTextLift) * layoutScale, 0.0022f, 0.006f);
        float targetTextWidth = EstimateNicknameTextWidthViewport(textHeight);
        lineLength = Mathf.Max(lineLength, targetTextWidth / widthRatio);
        Vector2 anchor = new Vector2(anchorViewport.x, anchorViewport.y);
        Vector2 elbow = anchor + new Vector2(horizontalSide * diagonalX, textSide * diagonalY);
        Vector2 end = elbow + new Vector2(horizontalSide * lineLength, 0f);
        Vector2 lineCenter = (elbow + end) * 0.5f;
        float maxTextWidth = Mathf.Min(
            Mathf.Max(lineLength * widthRatio, targetTextWidth),
            NicknameSafeTextWidthViewport(lineCenter.x)
        );

        return new NicknameTagLayout
        {
            valid = true,
            anchorViewport = anchorViewport,
            elbowViewport = elbow,
            endViewport = end,
            horizontalSide = horizontalSide >= 0 ? 1 : -1,
            textSide = textSide >= 0 ? 1 : -1,
            distanceScale = layoutScale,
            textHeightViewport = textHeight,
            textGapViewport = textGap,
            maxTextWidthViewport = maxTextWidth,
            lineWidthViewport = Mathf.Max(0.0012f, nicknameTagLineWidth) * Mathf.Lerp(0.9f, 1.25f, NicknameTagDistanceScaleRatio(distanceScale)) * fitScale,
            fitScale = fitScale
        };
    }

    private float EstimateNicknameTextWidthViewport(float textHeight)
    {
        float units = 0f;
        string text = string.IsNullOrEmpty(Nickname) ? " " : Nickname;
        for (int i = 0; i < text.Length; i++)
        {
            units += NicknameCharacterWidthUnits(text[i]);
        }

        float estimatedWidth = textHeight * Mathf.Clamp(units, 2.2f, 10.8f) * 0.82f;
        return Mathf.Clamp(estimatedWidth, textHeight * 2.4f, MaxNicknameTextViewportWidth);
    }

    private static float NicknameCharacterWidthUnits(char character)
    {
        if (char.IsWhiteSpace(character))
        {
            return 0.35f;
        }

        return character <= 255 ? 0.58f : 1f;
    }

    private float NicknameSafeTextWidthViewport(float centerX)
    {
        float padding = Mathf.Clamp(nicknameTagViewportPadding, 0.02f, 0.22f);
        float halfWidth = Mathf.Min(centerX - padding, 1f - padding - centerX);
        return Mathf.Clamp(halfWidth * 2f, 0.04f, MaxNicknameTextViewportWidth);
    }

    private void RememberNicknameTagLayout(NicknameTagLayout layout)
    {
        if (!layout.valid)
        {
            return;
        }

        hasStableNicknameTagLayout = true;
        stableNicknameTagHorizontalSide = layout.horizontalSide >= 0 ? 1 : -1;
        stableNicknameTagTextSide = layout.textSide >= 0 ? 1 : -1;
        stableNicknameTagFitScale = Mathf.Clamp(layout.fitScale, 0.25f, 1f);
    }

    private void ResetStableNicknameTagLayout()
    {
        hasStableNicknameTagLayout = false;
        stableNicknameTagHorizontalSide = 1;
        stableNicknameTagTextSide = 1;
        stableNicknameTagFitScale = 1f;
    }

    private bool NicknameTagFitsViewport(NicknameTagLayout layout, float padding)
    {
        return NicknameTagViewportOverflow(layout, padding) <= 0.0001f;
    }

    private float NicknameTagViewportOverflow(NicknameTagLayout layout, float padding)
    {
        Vector2 lineCenter = (layout.elbowViewport + layout.endViewport) * 0.5f;
        float halfTextWidth = layout.maxTextWidthViewport * 0.5f;
        float textMinY;
        float textMaxY;
        if (layout.textSide >= 0)
        {
            textMinY = layout.elbowViewport.y + layout.textGapViewport;
            textMaxY = textMinY + layout.textHeightViewport;
        }
        else
        {
            textMaxY = layout.elbowViewport.y - layout.textGapViewport;
            textMinY = textMaxY - layout.textHeightViewport;
        }

        float minX = Mathf.Min(layout.anchorViewport.x, Mathf.Min(layout.elbowViewport.x, Mathf.Min(layout.endViewport.x, lineCenter.x - halfTextWidth)));
        float maxX = Mathf.Max(layout.anchorViewport.x, Mathf.Max(layout.elbowViewport.x, Mathf.Max(layout.endViewport.x, lineCenter.x + halfTextWidth)));
        float minY = Mathf.Min(layout.anchorViewport.y, Mathf.Min(layout.elbowViewport.y, textMinY));
        float maxY = Mathf.Max(layout.anchorViewport.y, Mathf.Max(layout.elbowViewport.y, textMaxY));
        return Mathf.Max(0f, padding - minX)
            + Mathf.Max(0f, maxX - (1f - padding))
            + Mathf.Max(0f, padding - minY)
            + Mathf.Max(0f, maxY - (1f - padding));
    }

    private void ApplyNicknameTagWorldScale(float reveal, NicknameTagLayout layout)
    {
        float inverseRootScale = InverseMaxScale(transform.lossyScale);
        Vector3 labelScale = Vector3.one * inverseRootScale;
        if (nicknameLabel != null)
        {
            nicknameLabel.fontSize = nicknameTagFontSize;
            nicknameLabel.transform.localScale = labelScale;
        }

        if (nicknameFallbackLabel != null)
        {
            nicknameFallbackLabel.characterSize = NicknameFallbackCharacterSize();
            nicknameFallbackLabel.transform.localScale = labelScale;
        }

        if (nicknameTagLine != null)
        {
            nicknameTagLine.transform.localScale = Vector3.one * inverseRootScale;
            float lineWidth = ViewportHeightToWorld(layout.lineWidthViewport, layout.anchorViewport.z)
                * Mathf.Lerp(0.45f, 1f, reveal);
            nicknameTagLine.startWidth = lineWidth;
            nicknameTagLine.endWidth = lineWidth;
        }
    }

    private float NicknameTagDistanceScale()
    {
        if (mainCamera == null)
        {
            return 1f;
        }

        float distance = Vector3.Distance(mainCamera.transform.position, LabelAnchorPosition());
        float nearDistance = Mathf.Max(0.1f, nicknameTagNearScaleDistance);
        float farDistance = Mathf.Max(nearDistance + 0.1f, nicknameTagFarScaleDistance);
        float minScale = Mathf.Max(0.1f, Mathf.Min(nicknameTagDistanceScaleRange.x, nicknameTagDistanceScaleRange.y));
        float maxScale = Mathf.Max(minScale, Mathf.Max(nicknameTagDistanceScaleRange.x, nicknameTagDistanceScaleRange.y));
        float distanceRatio = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(nearDistance, farDistance, distance));
        return Mathf.Lerp(minScale, maxScale, distanceRatio);
    }

    private float NicknameTagDistanceScaleRatio(float distanceScale)
    {
        float minScale = Mathf.Max(0.1f, Mathf.Min(nicknameTagDistanceScaleRange.x, nicknameTagDistanceScaleRange.y));
        float maxScale = Mathf.Max(minScale + 0.001f, Mathf.Max(nicknameTagDistanceScaleRange.x, nicknameTagDistanceScaleRange.y));
        return Mathf.InverseLerp(minScale, maxScale, distanceScale);
    }

    private static float InverseMaxScale(Vector3 scale)
    {
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        return maxScale > 0.001f ? 1f / maxScale : 1f;
    }

    private bool HasNicknameTagLabel()
    {
        return nicknameFallbackLabel != null || nicknameLabel != null;
    }

    private Vector3 ViewportToWorld(Vector2 viewport, float depth)
    {
        return mainCamera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, depth));
    }

    private float ViewportHeightToWorld(float viewportHeight, float depth)
    {
        if (mainCamera == null)
        {
            return viewportHeight;
        }

        Vector3 bottom = mainCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
        Vector3 top = mainCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f + viewportHeight, depth));
        return Vector3.Distance(bottom, top);
    }

    private float ViewportWidthToWorld(float viewportWidth, float depth)
    {
        if (mainCamera == null)
        {
            return viewportWidth;
        }

        Vector3 left = mainCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
        Vector3 right = mainCamera.ViewportToWorldPoint(new Vector3(0.5f + viewportWidth, 0.5f, depth));
        return Vector3.Distance(left, right);
    }

    private void ShowNicknameLabel(NicknameTagLayout layout, float alpha)
    {
        bool showText = alpha > 0.01f;
        bool showFallback = ShouldShowNicknameFallbackLabel();
        if (nicknameLabel != null)
        {
            nicknameLabel.text = Nickname;
            nicknameLabel.color = new Color(0.9f, 1f, 1f, 0.92f * alpha);
            nicknameLabel.gameObject.SetActive(showText && !showFallback);
            Renderer labelRenderer = nicknameLabel.GetComponent<Renderer>();
            if (labelRenderer != null)
            {
                labelRenderer.enabled = showText && !showFallback;
            }

            if (showText && !showFallback)
            {
                nicknameLabel.ForceMeshUpdate();
                RefreshNicknameTmpMaterial(nicknameLabel);
                PlaceNicknameLabel(nicknameLabel.transform, labelRenderer, layout, true);
            }
        }

        if (nicknameFallbackLabel != null)
        {
            nicknameFallbackLabel.text = Nickname;
            nicknameFallbackLabel.color = new Color(0.9f, 1f, 1f, 0.96f * alpha);
            RefreshNicknameFallbackMaterial();
            nicknameFallbackLabel.gameObject.SetActive(showText && showFallback);
            if (nicknameFallbackRenderer != null)
            {
                nicknameFallbackRenderer.enabled = showText && showFallback;
            }

            if (showText && showFallback)
            {
                PlaceNicknameLabel(nicknameFallbackLabel.transform, nicknameFallbackRenderer, layout, false);
            }
        }
    }

    private bool ShouldShowNicknameFallbackLabel()
    {
        if (nicknameFallbackLabel == null)
        {
            return false;
        }

        if (nicknameLabel == null)
        {
            return true;
        }

        if (!ShouldUseNicknameFallbackText())
        {
            return false;
        }

        return cachedNicknameJapaneseFontAsset == null || nicknameLabel.font != cachedNicknameJapaneseFontAsset;
    }

    private void PlaceNicknameLabel(Transform labelTransform, Renderer labelRenderer, NicknameTagLayout layout, bool forceTmpUpdate)
    {
        if (labelTransform == null || labelRenderer == null || mainCamera == null)
        {
            return;
        }

        Vector2 lineCenterViewport = (layout.elbowViewport + layout.endViewport) * 0.5f;
        float textCenterY = layout.textSide >= 0
            ? layout.elbowViewport.y + layout.textGapViewport + layout.textHeightViewport * 0.5f
            : layout.elbowViewport.y - layout.textGapViewport - layout.textHeightViewport * 0.5f;
        labelTransform.position = ViewportToWorld(new Vector2(lineCenterViewport.x, textCenterY), layout.anchorViewport.z);
        OrientNicknameTransform(labelTransform);
        if (forceTmpUpdate && nicknameLabel != null)
        {
            nicknameLabel.ForceMeshUpdate();
        }

        NormalizeNicknameLabelSize(labelTransform, labelRenderer, layout);
        AlignNicknameLabelToLine(labelTransform, labelRenderer, layout);
    }

    private void NormalizeNicknameLabelSize(Transform labelTransform, Renderer labelRenderer, NicknameTagLayout layout)
    {
        if (!TryMeasureRendererOnCameraPlane(labelRenderer, out float minRight, out float maxRight, out float minUp, out float maxUp))
        {
            return;
        }

        float width = Mathf.Max(0.0001f, maxRight - minRight);
        float height = Mathf.Max(0.0001f, maxUp - minUp);
        float targetHeight = Mathf.Max(
            0.0001f,
            ViewportHeightToWorld(Mathf.Max(MinReadableNicknameTextViewportHeight, layout.textHeightViewport), layout.anchorViewport.z)
        );
        float maxWidth = Mathf.Max(0.0001f, ViewportWidthToWorld(layout.maxTextWidthViewport, layout.anchorViewport.z));
        float scaleFactor = targetHeight / height;
        if (width * scaleFactor > maxWidth)
        {
            scaleFactor = maxWidth / width;
        }

        labelTransform.localScale *= Mathf.Clamp(scaleFactor, 0.08f, 24f);
    }

    private void AlignNicknameLabelToLine(Transform labelTransform, Renderer labelRenderer, NicknameTagLayout layout)
    {
        if (!TryMeasureRendererOnCameraPlane(labelRenderer, out float minRight, out float maxRight, out float minUp, out float maxUp))
        {
            return;
        }

        Vector3 cameraRight = mainCamera.transform.right;
        Vector3 cameraUp = mainCamera.transform.up;
        Vector3 lineCenter = ViewportToWorld((layout.elbowViewport + layout.endViewport) * 0.5f, layout.anchorViewport.z);
        float desiredRight = Vector3.Dot(lineCenter, cameraRight);
        float desiredUp = Vector3.Dot(lineCenter, cameraUp);
        float currentRight = (minRight + maxRight) * 0.5f;
        float gap = ViewportHeightToWorld(layout.textGapViewport, layout.anchorViewport.z);
        float upShift = layout.textSide >= 0
            ? desiredUp + gap - minUp
            : desiredUp - gap - maxUp;
        labelTransform.position += cameraRight * (desiredRight - currentRight) + cameraUp * upShift;
    }

    private bool TryMeasureRendererOnCameraPlane(Renderer renderer, out float minRight, out float maxRight, out float minUp, out float maxUp)
    {
        if (renderer == null || mainCamera == null)
        {
            minRight = minUp = float.PositiveInfinity;
            maxRight = maxUp = float.NegativeInfinity;
            return false;
        }

        TMP_Text tmpText = renderer.GetComponent<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.ForceMeshUpdate();
            if (TryMeasureBoundsOnCameraPlane(tmpText.textBounds, tmpText.transform, out minRight, out maxRight, out minUp, out maxUp))
            {
                return true;
            }
        }

        return TryMeasureBoundsOnCameraPlane(renderer.bounds, null, out minRight, out maxRight, out minUp, out maxUp);
    }

    private bool TryMeasureBoundsOnCameraPlane(Bounds bounds, Transform boundsTransform, out float minRight, out float maxRight, out float minUp, out float maxUp)
    {
        minRight = minUp = float.PositiveInfinity;
        maxRight = maxUp = float.NegativeInfinity;
        if (mainCamera == null || bounds.size.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        Vector3 cameraRight = mainCamera.transform.right;
        Vector3 cameraUp = mainCamera.transform.up;
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + new Vector3(extents.x * x, extents.y * y, extents.z * z);
                    if (boundsTransform != null)
                    {
                        corner = boundsTransform.TransformPoint(corner);
                    }

                    float right = Vector3.Dot(corner, cameraRight);
                    float up = Vector3.Dot(corner, cameraUp);
                    minRight = Mathf.Min(minRight, right);
                    maxRight = Mathf.Max(maxRight, right);
                    minUp = Mathf.Min(minUp, up);
                    maxUp = Mathf.Max(maxUp, up);
                }
            }
        }

        return minRight < maxRight && minUp < maxUp;
    }

    private void OrientNicknameTransform(Transform labelTransform)
    {
        if (labelTransform == null || mainCamera == null)
        {
            return;
        }

        labelTransform.rotation = Quaternion.LookRotation(mainCamera.transform.forward, mainCamera.transform.up);
    }

    private float NicknameFallbackCharacterSize()
    {
        return Mathf.Clamp(nicknameTagFontSize * 0.18f, 0.055f, 0.16f);
    }

    private bool ShouldUseNicknameTag()
    {
        return releasedFish;
    }

    private bool ShouldUseNicknameFallbackText()
    {
        if (!useBuiltInNicknameFallback || string.IsNullOrEmpty(Nickname))
        {
            return false;
        }

        for (int i = 0; i < Nickname.Length; i++)
        {
            if (Nickname[i] > 255)
            {
                return true;
            }
        }

        return false;
    }

    private void LogNicknameTagDebug(string reason)
    {
        if (!logNicknameTagDebug || Time.time < nextNicknameTagDebugTime)
        {
            return;
        }

        nextNicknameTagDebugTime = Time.time + Mathf.Max(0.5f, nicknameTagDebugInterval);
        Debug.LogWarning(
            $"FishActor: nickname tag hidden for '{Nickname}' reason='{reason}', focused={cameraFocused}, " +
            $"hasTMP={nicknameLabel != null}, hasFallback={nicknameFallbackLabel != null}, released={releasedFish}, " +
            $"rootScale={transform.lossyScale}."
        );
    }

    private static TMP_FontAsset ResolveNicknameFontAsset()
    {
        TMP_FontAsset fontAsset = TMP_Settings.defaultFontAsset;
        return fontAsset != null
            ? fontAsset
            : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }

    private static TMP_FontAsset ResolveNicknameJapaneseFontAsset()
    {
        if (cachedNicknameJapaneseFontAsset != null)
        {
            return cachedNicknameJapaneseFontAsset;
        }

        if (attemptedNicknameJapaneseFontAsset)
        {
            return null;
        }

        attemptedNicknameJapaneseFontAsset = true;
        TMP_FontAsset resourceFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Nickname Japanese SDF");
        if (resourceFont != null)
        {
            cachedNicknameJapaneseFontAsset = resourceFont;
            return cachedNicknameJapaneseFontAsset;
        }

        cachedNicknameJapaneseFontAsset = CreateNicknameJapaneseFontAssetFromFile();
        if (cachedNicknameJapaneseFontAsset != null)
        {
            cachedNicknameJapaneseFontAsset.name = "Nickname Japanese Dynamic SDF";
            return cachedNicknameJapaneseFontAsset;
        }

        Font sourceFont = Font.CreateDynamicFontFromOSFont(
            new[]
            {
                "Yu Gothic UI",
                "Yu Gothic",
                "Meiryo",
                "Noto Sans CJK JP",
                "Noto Sans JP",
                "MS PGothic",
                "MS Gothic"
            },
            96
        );
        if (sourceFont == null)
        {
            WarnNicknameJapaneseFontFallback("OS Japanese font was not found");
            return null;
        }

        try
        {
            cachedNicknameJapaneseFontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                96,
                9,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic
            );
        }
        catch (System.Exception exception)
        {
            WarnNicknameJapaneseFontFallback(exception.Message);
            cachedNicknameJapaneseFontAsset = null;
        }

        if (cachedNicknameJapaneseFontAsset == null)
        {
            WarnNicknameJapaneseFontFallback("TMP returned a null font asset");
            return null;
        }

        cachedNicknameJapaneseFontAsset.name = "Nickname Japanese Dynamic SDF";
        return cachedNicknameJapaneseFontAsset;
    }

    private static TMP_FontAsset CreateNicknameJapaneseFontAssetFromFile()
    {
        string fontPath = ResolveNicknameJapaneseFontPath();
        if (string.IsNullOrEmpty(fontPath))
        {
            return null;
        }

        try
        {
            Font sourceFont = new Font(fontPath);
            if (sourceFont == null)
            {
                return null;
            }

            return TMP_FontAsset.CreateFontAsset(
                sourceFont,
                96,
                9,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic
            );
        }
        catch (System.Exception exception)
        {
            WarnNicknameJapaneseFontFallback(exception.Message);
            return null;
        }
    }

    private static string ResolveNicknameJapaneseFontPath()
    {
        string fontsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Fonts);
        string windowsPath = System.Environment.GetEnvironmentVariable("WINDIR");
        string[] fontDirectories =
        {
            fontsPath,
            !string.IsNullOrEmpty(windowsPath) ? System.IO.Path.Combine(windowsPath, "Fonts") : null,
            @"C:\Windows\Fonts"
        };
        string[] fontFiles =
        {
            "YuGothR.ttc",
            "YuGothM.ttc",
            "YuGothB.ttc",
            "meiryo.ttc",
            "meiryob.ttc",
            "msgothic.ttc"
        };

        for (int directoryIndex = 0; directoryIndex < fontDirectories.Length; directoryIndex++)
        {
            string directory = fontDirectories[directoryIndex];
            if (string.IsNullOrEmpty(directory))
            {
                continue;
            }

            for (int fileIndex = 0; fileIndex < fontFiles.Length; fileIndex++)
            {
                string path = System.IO.Path.Combine(directory, fontFiles[fileIndex]);
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }

    private static void WarnNicknameJapaneseFontFallback(string reason)
    {
        if (warnedNicknameJapaneseFontFallback)
        {
            return;
        }

        warnedNicknameJapaneseFontFallback = true;
        Debug.LogWarning($"FishActor: Japanese TMP font unavailable; using high-resolution TextMesh fallback. {reason}");
    }

    private static Material ResolveNicknameFontMaterial(TMP_FontAsset fontAsset)
    {
        if (fontAsset != null && fontAsset.material != null)
        {
            return fontAsset.material;
        }

        Material material = Resources.Load<Material>("Fonts & Materials/LiberationSans SDF - Drop Shadow");
        return material != null
            ? material
            : Resources.Load<Material>("Fonts & Materials/LiberationSans SDF");
    }

    private void ApplyNicknameTmpFont(TMP_Text label, TMP_FontAsset fontAsset)
    {
        if (label == null || fontAsset == null)
        {
            return;
        }

        Texture expectedTexture = fontAsset.material != null ? fontAsset.material.mainTexture : null;
        if (label.font == fontAsset
            && label.fontSharedMaterial != null
            && (expectedTexture == null || label.fontSharedMaterial.mainTexture == expectedTexture))
        {
            return;
        }

        label.font = fontAsset;
        Material fontMaterial = ResolveNicknameFontMaterial(fontAsset);
        if (fontMaterial != null)
        {
            label.fontSharedMaterial = CreateNicknameFontMaterial(fontMaterial);
            RefreshNicknameTmpMaterial(label);
        }
    }

    private static Material CreateNicknameFontMaterial(Material source)
    {
        Material material = new Material(source);
        Texture sourceTexture = source != null ? source.mainTexture : null;
        Shader overlayShader = Shader.Find("TextMeshPro/Distance Field Overlay");
        if (overlayShader != null)
        {
            material.shader = overlayShader;
        }

        if (sourceTexture != null)
        {
            material.mainTexture = sourceTexture;
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", sourceTexture);
            }
        }

        material.renderQueue = 4000;
        if (material.HasProperty("_ZTest"))
        {
            material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        }

        return material;
    }

    private static void RefreshNicknameTmpMaterial(TMP_Text label)
    {
        if (label == null || label.font == null || label.fontSharedMaterial == null)
        {
            return;
        }

        Material sourceMaterial = label.font.material;
        Texture texture = sourceMaterial != null ? sourceMaterial.mainTexture : null;
        if (texture == null)
        {
            texture = label.font.atlasTexture;
        }

        if (texture == null)
        {
            return;
        }

        label.fontSharedMaterial.mainTexture = texture;
        if (label.fontSharedMaterial.HasProperty("_MainTex"))
        {
            label.fontSharedMaterial.SetTexture("_MainTex", texture);
        }
    }

    private void RefreshNicknameFallbackMaterial()
    {
        if (nicknameFallbackLabel == null || nicknameFallbackRenderer == null)
        {
            return;
        }

        Font font = nicknameFallbackLabel.font;
        if (font == null)
        {
            return;
        }

        font.RequestCharactersInTexture(Nickname, nicknameFallbackLabel.fontSize, nicknameFallbackLabel.fontStyle);
        if (nicknameFallbackRenderer.sharedMaterial == null)
        {
            nicknameFallbackRenderer.sharedMaterial = CreateNicknameFallbackMaterial(font, nicknameFallbackLabel.color);
        }

        Material material = nicknameFallbackRenderer.sharedMaterial;
        if (material == null || font.material == null)
        {
            return;
        }

        material.mainTexture = font.material.mainTexture;
        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", font.material.mainTexture);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", nicknameFallbackLabel.color);
        }
    }

    private static Material CreateNicknameFallbackMaterial(Font font, Color color)
    {
        Shader shader = Shader.Find("GUI/Text Shader");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = shader != null
            ? new Material(shader)
            : (font != null && font.material != null ? new Material(font.material) : null);
        if (material == null)
        {
            return null;
        }

        if (font != null && font.material != null)
        {
            material.mainTexture = font.material.mainTexture;
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", font.material.mainTexture);
            }
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        material.renderQueue = 4000;
        return material;
    }

    private static Font ResolveNicknameFallbackFont()
    {
        Font font = Font.CreateDynamicFontFromOSFont(
            new[]
            {
                "Yu Gothic",
                "Meiryo",
                "MS Gothic",
                "Noto Sans CJK JP",
                "Arial"
            },
            64
        );
        if (font != null)
        {
            return font;
        }

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
