using UnityEngine;

namespace RestaurantLoop.Core
{
    [CreateAssetMenu(fileName = "NewItemData", menuName = "RestaurantLoop/Item Data")]
    public class ItemDataSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string itemName = "Item";

        [Header("Visuals")]
        [SerializeField] private Color uiColor = Color.red;
        [SerializeField] private Sprite balloonIcon;
        [SerializeField] private GameObject stackPrefab;
        [Tooltip("Low-saturation material used by the customer and food cell surfaces for this item type.")]
        [SerializeField] private Material cellMaterial;

        [Header("Customer")]
        [Tooltip("The dedicated customer archetype for this food. Leave empty to use CrowdManager's generic fallback.")]
        [SerializeField] private Customer customerPrefab;

        public string ItemName => itemName;
        public Color UIColor => uiColor;
        public Sprite BalloonIcon => balloonIcon;
        public GameObject StackPrefab => stackPrefab;
        public Material CellMaterial => cellMaterial;
        public Customer CustomerPrefab => customerPrefab;
    }
}
