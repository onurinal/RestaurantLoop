using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Slot implementation for the Queue system with automatic spawn-lock visual resetting.
    /// </summary>
    public class QueueSlot : BaseSlot
    {
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        private readonly List<RendererColorState> rendererColorStates = new List<RendererColorState>();
        private MaterialPropertyBlock propertyBlock;

        private readonly struct RendererColorState
        {
            public readonly Renderer Renderer;
            public readonly int MaterialIndex;
            public readonly int ColorPropertyId;
            public readonly Color BaseColor;

            public RendererColorState(Renderer renderer, int materialIndex, int colorPropertyId, Color baseColor)
            {
                Renderer = renderer;
                MaterialIndex = materialIndex;
                ColorPropertyId = colorPropertyId;
                BaseColor = baseColor;
            }
        }

        /// <summary>
        /// Dims this slot and its current stack during customer spawning.
        /// Fully restores native material colors the exact frame spawning ends.
        /// </summary>
        public void SetSpawnLockedVisual(bool locked, float brightness)
        {
            propertyBlock ??= new MaterialPropertyBlock();

            if (!locked)
            {
                // Fully wipe property block overrides across all renderers and material indices
                Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer r = renderers[i];
                    if (r == null || r.GetComponent<TMP_Text>() != null || IsInteractionOutlineRenderer(r)) continue;

                    r.SetPropertyBlock(null);

                    // Renderer.sharedMaterials allocates a new array on every access, and it
                    // was being read again on each iteration of the loop condition.
                    Material[] sharedMaterials = r.sharedMaterials;
                    if (sharedMaterials != null)
                    {
                        for (int m = 0; m < sharedMaterials.Length; m++)
                        {
                            r.SetPropertyBlock(null, m);
                        }
                    }
                }

                rendererColorStates.Clear();
                return;
            }

            if (rendererColorStates.Count == 0)
            {
                CaptureRendererColors();
            }

            brightness = Mathf.Clamp01(brightness);

            for (int i = 0; i < rendererColorStates.Count; i++)
            {
                RendererColorState state = rendererColorStates[i];
                if (state.Renderer == null) continue;

                Color targetColor = state.BaseColor;
                targetColor.r *= brightness;
                targetColor.g *= brightness;
                targetColor.b *= brightness;

                state.Renderer.GetPropertyBlock(propertyBlock, state.MaterialIndex);
                propertyBlock.SetColor(state.ColorPropertyId, targetColor);
                state.Renderer.SetPropertyBlock(propertyBlock, state.MaterialIndex);
                propertyBlock.Clear();
            }
        }

        private void CaptureRendererColors()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            propertyBlock ??= new MaterialPropertyBlock();

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null || renderer.GetComponent<TMP_Text>() != null || IsInteractionOutlineRenderer(renderer)) continue;

                // Clear temporary overrides first so we read pure un-dimmed material colors
                renderer.SetPropertyBlock(null);

                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null) continue;

                    int colorPropertyId = material.HasProperty(BaseColorProperty)
                        ? BaseColorProperty
                        : material.HasProperty(ColorProperty)
                            ? ColorProperty
                            : -1;
                    if (colorPropertyId < 0) continue;

                    // Strictly record raw material color from sharedMaterial asset
                    Color baseColor = material.GetColor(colorPropertyId);

                    rendererColorStates.Add(new RendererColorState(
                        renderer,
                        materialIndex,
                        colorPropertyId,
                        baseColor));
                }
            }
        }

        public override void OnStackTapped(StackItem stack)
        {
            if (PowerUpManager.Instance != null && PowerUpManager.Instance.IsClearColorSelectionActive)
            {
                PowerUpManager.Instance.TrySelectClearColorStack(stack);
                return;
            }

            if (PowerUpManager.Instance != null && PowerUpManager.Instance.IsHandSelectionActive)
            {
                PowerUpManager.Instance.TrySelectHandStack(stack);
                return;
            }

            QueueColumn column = GetComponentInParent<QueueColumn>();

            if (column != null && column.FrontSlot == this)
            {
                column.TrySendFrontStackToBelt();
                return;
            }

            // Deeper queue rows stay locked unless Hand selection is active
            stack?.Shake();
            QueueManager.Instance?.PulseAvailableMoveTargets();
        }
    }
}
