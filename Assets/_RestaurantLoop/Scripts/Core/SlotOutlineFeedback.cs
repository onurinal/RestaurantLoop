using DG.Tweening;
using UnityEngine;

namespace RestaurantLoop.Core
{
    public readonly struct SlotOutlineAnimationSettings
    {
        public float GuidanceMinimumAlpha { get; }
        public float GuidanceHalfCycleDuration { get; }
        public float RejectionFadeInDuration { get; }
        public float RejectionFadeOutDuration { get; }
        public float RejectionGapDuration { get; }
        public int RejectionFlashCount { get; }

        public static SlotOutlineAnimationSettings Default => new SlotOutlineAnimationSettings(
            guidanceMinimumAlpha: 0.3f,
            guidanceHalfCycleDuration: 0.7f,
            rejectionFadeInDuration: 0.5f,
            rejectionFadeOutDuration: 1.25f,
            rejectionGapDuration: 0.25f,
            rejectionFlashCount: 1);

        public SlotOutlineAnimationSettings(
            float guidanceMinimumAlpha,
            float guidanceHalfCycleDuration,
            float rejectionFadeInDuration,
            float rejectionFadeOutDuration,
            float rejectionGapDuration,
            int rejectionFlashCount)
        {
            GuidanceMinimumAlpha = Mathf.Clamp01(guidanceMinimumAlpha);
            GuidanceHalfCycleDuration = Mathf.Max(0.01f, guidanceHalfCycleDuration);
            RejectionFadeInDuration = Mathf.Max(0.01f, rejectionFadeInDuration);
            RejectionFadeOutDuration = Mathf.Max(0.01f, rejectionFadeOutDuration);
            RejectionGapDuration = Mathf.Max(0f, rejectionGapDuration);
            RejectionFlashCount = Mathf.Max(1, rejectionFlashCount);
        }
    }

    /// <summary>
    /// Drives the authored InteractionOutline child of a slot without creating per-slot materials.
    /// All animation uses unscaled time because power-up selection pauses gameplay.
    /// </summary>
    public sealed class SlotOutlineFeedback
    {
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        private readonly Transform outlineTransform;
        private readonly Renderer[] outlineRenderers;
        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        private readonly Vector3 baseLocalScale;

        private Tween animation;
        private float currentAlpha;
        private Color outlineColor = Color.white;

        private SlotOutlineFeedback(Transform outlineTransform)
        {
            this.outlineTransform = outlineTransform;
            outlineRenderers = outlineTransform.GetComponentsInChildren<Renderer>(true);
            baseLocalScale = outlineTransform.localScale;
        }

        public static SlotOutlineFeedback Create(Transform slotTransform)
        {
            Transform outlineTransform = slotTransform != null
                ? slotTransform.Find(BaseSlot.InteractionOutlineChildName)
                : null;

            return outlineTransform != null ? new SlotOutlineFeedback(outlineTransform) : null;
        }

        public bool ContainsRenderer(Renderer renderer)
        {
            if (renderer == null) return false;

            for (int i = 0; i < outlineRenderers.Length; i++)
            {
                if (outlineRenderers[i] == renderer) return true;
            }

            return false;
        }

        public void HideImmediate()
        {
            KillAnimation();
            SetVisible(false);
        }

        public void SetGuidanceActive(bool active, Color color, SlotOutlineAnimationSettings settings)
        {
            KillAnimation();

            if (!active)
            {
                SetVisible(false);
                return;
            }

            outlineColor = color;
            SetVisible(true);
            SetScale(1f);
            SetAlpha(settings.GuidanceMinimumAlpha);
            animation = DOTween.Sequence()
                .SetUpdate(true)
                .Append(DOTween.To(() => currentAlpha, SetAlpha, 1f, settings.GuidanceHalfCycleDuration))
                .Append(DOTween.To(() => currentAlpha, SetAlpha, settings.GuidanceMinimumAlpha,
                    settings.GuidanceHalfCycleDuration))
                .SetLoops(-1)
                .SetEase(Ease.InOutSine);
        }

        public void PulseTwice(Color color, SlotOutlineAnimationSettings settings)
        {
            KillAnimation();
            outlineColor = color;
            SetVisible(true);
            SetScale(1f);
            SetAlpha(0f);

            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            for (int i = 0; i < settings.RejectionFlashCount; i++)
            {
                sequence.Append(DOTween.To(() => currentAlpha, SetAlpha, 1f, settings.RejectionFadeInDuration));
                sequence.Append(DOTween.To(() => currentAlpha, SetAlpha, 0f, settings.RejectionFadeOutDuration));
                if (i < settings.RejectionFlashCount - 1) sequence.AppendInterval(settings.RejectionGapDuration);
            }

            animation = sequence.OnComplete(() =>
            {
                animation = null;
                SetVisible(false);
            });
        }

        public void Dispose()
        {
            HideImmediate();
        }

        private void KillAnimation()
        {
            animation?.Kill();
            animation = null;
        }

        private void SetVisible(bool visible)
        {
            if (outlineTransform == null) return;

            if (visible)
            {
                outlineTransform.gameObject.SetActive(true);
                return;
            }

            currentAlpha = 0f;
            SetAlpha(0f);
            SetScale(1f);
            outlineTransform.gameObject.SetActive(false);
        }

        private void SetScale(float multiplier)
        {
            if (outlineTransform != null) outlineTransform.localScale = baseLocalScale * multiplier;
        }

        private void SetAlpha(float alpha)
        {
            currentAlpha = Mathf.Clamp01(alpha);
            Color color = outlineColor;
            color.a = currentAlpha;

            for (int rendererIndex = 0; rendererIndex < outlineRenderers.Length; rendererIndex++)
            {
                Renderer renderer = outlineRenderers[rendererIndex];
                if (renderer == null) continue;

                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null) continue;

                    renderer.GetPropertyBlock(propertyBlock, materialIndex);
                    if (material.HasProperty(BaseColorProperty)) propertyBlock.SetColor(BaseColorProperty, color);
                    if (material.HasProperty(ColorProperty)) propertyBlock.SetColor(ColorProperty, color);
                    renderer.SetPropertyBlock(propertyBlock, materialIndex);
                    propertyBlock.Clear();
                }
            }
        }
    }
}
