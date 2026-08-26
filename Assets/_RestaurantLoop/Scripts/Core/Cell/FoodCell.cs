using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Presentation-only floor cell shared by customer positions, the food queue, and the rack.
    /// Assign a neutral material plus the renderers supplied by the final art prefab.
    /// </summary>
    public class FoodCell : MonoBehaviour
    {
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private Material neutralMaterial;

        private Material[][] authoredMaterials;

        private void Awake()
        {
            CacheRenderers();
        }

        public void SetFood(ItemDataSO itemData)
        {
            SetMaterial(itemData != null ? itemData.CellMaterial : null);
        }

        public void SetMaterial(Material material)
        {
            CacheRenderers();

            if (material == null)
            {
                Clear();
                return;
            }

            foreach (Renderer targetRenderer in targetRenderers)
            {
                if (targetRenderer == null) continue;

                Material[] materials = targetRenderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }

                targetRenderer.sharedMaterials = materials;
            }
        }

        public void Clear()
        {
            CacheRenderers();

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];
                if (targetRenderer == null) continue;

                if (neutralMaterial != null)
                {
                    Material[] materials = targetRenderer.sharedMaterials;
                    for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    {
                        materials[materialIndex] = neutralMaterial;
                    }

                    targetRenderer.sharedMaterials = materials;
                }
                else if (authoredMaterials != null && i < authoredMaterials.Length)
                {
                    targetRenderer.sharedMaterials = authoredMaterials[i];
                }
            }
        }

        private void CacheRenderers()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (authoredMaterials != null && authoredMaterials.Length == targetRenderers.Length) return;

            authoredMaterials = new Material[targetRenderers.Length][];
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                authoredMaterials[i] = targetRenderers[i] != null
                    ? targetRenderers[i].sharedMaterials
                    : System.Array.Empty<Material>();
            }
        }
    }
}
