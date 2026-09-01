using UnityEngine;

namespace RestaurantLoop.Core
{
    public class FoodCell : MonoBehaviour, IInteractable
    {
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private Material neutralMaterial;

        private Material[][] authoredMaterials;
        private Material[][] materialBuffers;

        private void Awake()
        {
            CacheRenderers();
        }

        public void OnTap()
        {
            StackItem stackItem = GetComponentInChildren<StackItem>();

            if (stackItem == null)
            {
                BaseSlot parentSlot = GetComponentInParent<BaseSlot>();
                if (parentSlot != null)
                {
                    stackItem = parentSlot.GetComponentInChildren<StackItem>();
                }
            }

            if (stackItem != null)
            {
                stackItem.OnTap();
            }
        }

        public void SetFood(ItemDataSO itemData)
        {
            if (this == null) return;
            SetMaterial(itemData != null ? itemData.CellMaterial : null);
        }

        public void SetMaterial(Material material)
        {
            if (this == null) return;
            CacheRenderers();

            if (material == null)
            {
                Clear();
                return;
            }

            for (int rendererIndex = 0; rendererIndex < targetRenderers.Length; rendererIndex++)
            {
                Renderer targetRenderer = targetRenderers[rendererIndex];
                if (targetRenderer == null || IsInteractionOutlineRenderer(targetRenderer)) continue;

                Material[] newMaterials = GetMaterialBuffer(rendererIndex);
                for (int i = 0; i < newMaterials.Length; i++)
                {
                    newMaterials[i] = material;
                }

                targetRenderer.materials = newMaterials;
                targetRenderer.SetPropertyBlock(null);
            }
        }

        public void Clear()
        {
            if (this == null) return;
            CacheRenderers();

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];
                if (targetRenderer == null || IsInteractionOutlineRenderer(targetRenderer)) continue;

                if (neutralMaterial != null)
                {
                    Material[] newMaterials = GetMaterialBuffer(i);
                    for (int materialIndex = 0; materialIndex < newMaterials.Length; materialIndex++)
                    {
                        newMaterials[materialIndex] = neutralMaterial;
                    }

                    targetRenderer.materials = newMaterials;
                }
                else if (authoredMaterials != null && i < authoredMaterials.Length)
                {
                    targetRenderer.materials = authoredMaterials[i];
                }

                targetRenderer.SetPropertyBlock(null);
            }
        }

        /// <summary>
        /// Scratch array matching a renderer's authored material-slot count, reused across
        /// calls so SetMaterial/Clear no longer allocate on every stack movement.
        /// </summary>
        private Material[] GetMaterialBuffer(int rendererIndex)
        {
            int slotCount = authoredMaterials != null && rendererIndex < authoredMaterials.Length
                ? authoredMaterials[rendererIndex].Length
                : 0;

            if (materialBuffers == null || materialBuffers.Length != targetRenderers.Length)
            {
                materialBuffers = new Material[targetRenderers.Length][];
            }

            Material[] buffer = materialBuffers[rendererIndex];
            if (buffer == null || buffer.Length != slotCount)
            {
                buffer = new Material[slotCount];
                materialBuffers[rendererIndex] = buffer;
            }

            return buffer;
        }

        private void CacheRenderers()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (authoredMaterials != null && authoredMaterials.Length == targetRenderers.Length) return;

            authoredMaterials = new Material[targetRenderers.Length][];
            materialBuffers = new Material[targetRenderers.Length][];
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                authoredMaterials[i] = targetRenderers[i] != null
                    ? targetRenderers[i].sharedMaterials
                    : System.Array.Empty<Material>();
            }
        }

        private bool IsInteractionOutlineRenderer(Renderer renderer)
        {
            Transform current = renderer.transform;
            while (current != null && current != transform)
            {
                if (current.name == BaseSlot.InteractionOutlineChildName) return true;
                current = current.parent;
            }

            return false;
        }
    }
}
