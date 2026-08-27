using UnityEngine;

namespace RestaurantLoop.Core
{
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

                Material[] newMaterials = new Material[targetRenderer.sharedMaterials.Length];
                for (int i = 0; i < newMaterials.Length; i++)
                {
                    newMaterials[i] = material;
                }

                targetRenderer.materials = newMaterials;
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
                    Material[] newMaterials = new Material[targetRenderer.sharedMaterials.Length];
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