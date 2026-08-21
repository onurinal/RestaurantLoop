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

        public string ItemName => itemName;
        public Color UIColor => uiColor;
        public Sprite BalloonIcon => balloonIcon;
        public GameObject StackPrefab => stackPrefab;
    }
}