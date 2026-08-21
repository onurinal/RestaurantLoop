using UnityEngine;
using TMPro; 
using DG.Tweening;
using System.Collections.Generic;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Controls stack movement along the spline, jump tweens, O(1) grid service detection,
    /// visual stacking of items, dynamic UI counting (Billboard) and service cooldowns.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class StackItem : MonoBehaviour, IInteractable
    {
        [SerializeField] private int remainingCount = 10;
        [SerializeField] private float jumpPower = 1.5f;
        [SerializeField] private float jumpDuration = 0.5f;

        [Header("Data")]
        [SerializeField] private ItemDataSO itemData;

        [Header("Visual Stack Settings")]
        [SerializeField] private GameObject visualPrefab;
        [SerializeField] private Transform visualContainer;
        [SerializeField] private float yOffset = 0.3f;

        [Header("UI Settings")]
        [SerializeField] private TMP_Text countText; 
        [SerializeField] private float textHeightOffset = 3.5f; 

        private float currentDistance;
        private float traveledDistance;
        private float targetTravelDistance;
        
        // Makinalı tüfek (peş peşe fırlatma) hatasını önleyecek bekleme süresi
        private float serviceCooldown = 0f; 
        
        private Stack<GameObject> visualItems = new Stack<GameObject>();
        private Camera mainCamera;

        public int RemainingItemCount => remainingCount;
        public bool IsJumping { get; private set; }
        public ItemDataSO Data => itemData;

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (countText != null && mainCamera != null)
            {
                countText.transform.rotation = mainCamera.transform.rotation;
            }
        }

        public void InitializeData(ItemDataSO data)
        {
            if (data != null)
            {
                itemData = data;
                GenerateVisualStack();
            }
        }

        public void SetItemCount(int newCount)
        {
            remainingCount = newCount;
            GenerateVisualStack();
        }

        private void GenerateVisualStack()
        {
            foreach (var item in visualItems)
            {
                Destroy(item);
            }
            visualItems.Clear();

            for (int i = 0; i < remainingCount; i++)
            {
                GameObject newItem = Instantiate(visualPrefab, visualContainer);
                newItem.transform.localPosition = new Vector3(0, i * yOffset, 0);
                visualItems.Push(newItem);
            }

            BoxCollider boxCol = GetComponent<BoxCollider>();
            if (boxCol == null)
            {
                boxCol = gameObject.AddComponent<BoxCollider>();
            }

            float totalHeight = remainingCount > 0 ? (remainingCount * yOffset) : 0.5f;
            boxCol.size = new Vector3(1f, totalHeight, 1f); 
            boxCol.center = new Vector3(0f, (totalHeight / 2f) - (yOffset / 2f), 0f);

            UpdateCountText();
        }

        private void UpdateCountText()
        {
            if (countText != null)
            {
                if (remainingCount > 0)
                {
                    countText.text = remainingCount.ToString();
                    float currentStackHeight = remainingCount * yOffset;
                    countText.transform.position = transform.position + new Vector3(0, currentStackHeight + textHeightOffset, 0);
                }
                else
                {
                    countText.text = "";
                }
            }
        }

        public GameObject PopTopVisualItem()
        {
            if (visualItems.Count > 0)
            {
                remainingCount--;
                GameObject topItem = visualItems.Pop();
                topItem.transform.SetParent(null); 
                
                GenerateVisualStack(); 
                return topItem;
            }
            return null;
        }

        public void OnTap()
        {
            if (IsJumping) return;
            GetComponentInParent<BaseSlot>()?.OnStackTapped(this);
        }

        public void InitializeOnBelt(SplineConveyorPath path, float startDistance, float totalDistanceToExit)
        {
            currentDistance = startDistance;
            traveledDistance = 0f;
            targetTravelDistance = totalDistanceToExit;
            IsJumping = false;
            serviceCooldown = 0f; // Bantta ilk başladığında süre sıfırlansın
            UpdateTransform(path, true);
        }

        public void MoveAlongBelt(SplineConveyorPath path, float speed, bool isClockwise, float deltaTime)
        {
            if (this == null) return;
            if (IsJumping) return;

            // Bekleme süresi varsa süreyi azalt
            if (serviceCooldown > 0f)
            {
                serviceCooldown -= deltaTime;
            }

            float moveDelta = (isClockwise ? speed : -speed) * deltaTime;
            currentDistance = (currentDistance + moveDelta) % path.Length;
            if (currentDistance < 0f) currentDistance += path.Length;

            traveledDistance += Mathf.Abs(moveDelta);
            UpdateTransform(path, isClockwise);
            
            // Eğer bekleme süresi bittiyse yeni müşteri ara
            if (serviceCooldown <= 0f)
            {
                CheckForNearbyCustomer();
            }

            if (this == null) return;

            if (traveledDistance >= targetTravelDistance)
            {
                OnExitReached();
            }
        }

        public void JumpToConveyor(Vector3 targetPosition, System.Action onComplete)
        {
            IsJumping = true;
            transform.DOKill();
            transform.DOJump(targetPosition, jumpPower, 1, jumpDuration)
                .OnComplete(() =>
                {
                    IsJumping = false;
                    onComplete?.Invoke();
                });
        }

        public void JumpToSlot(Transform slotTransform, System.Action onComplete = null)
        {
            IsJumping = true;
            transform.DOKill();
            transform.DOJump(slotTransform.position, jumpPower, 1, jumpDuration)
                .OnComplete(() =>
                {
                    IsJumping = false;
                    transform.SetParent(slotTransform);
                    transform.localPosition = Vector3.zero;
                    onComplete?.Invoke();
                });
        }

        public void Shake()
        {
            if (IsJumping || DOTween.IsTweening(visualContainer)) return;

            visualContainer.DOKill();
            visualContainer.localPosition = Vector3.zero;

            visualContainer.DOShakePosition(0.2f, 0.12f, 10, 90f)
                .OnComplete(() => { visualContainer.localPosition = Vector3.zero; });
        }

        private void CheckForNearbyCustomer()
        {
            if (IsJumping || itemData == null || CrowdManager.Instance == null || remainingCount <= 0) return;

            Customer targetCustomer = CrowdManager.Instance.CheckServiceForBeltItem(currentDistance, itemData);

            if (targetCustomer != null)
            {
                // YENİ: Başka bir tabak fırlatmadan önce 0.5 saniye bekle
                serviceCooldown = 0.5f; 

                GameObject thrownItem = PopTopVisualItem();

                if (thrownItem != null)
                {
                    Vector3 targetPos = targetCustomer.transform.position;
                    
                    thrownItem.transform.DOJump(targetPos, 2f, 1, 0.35f)
                        .OnComplete(() => 
                        {
                            if (thrownItem != null) Destroy(thrownItem);
                        });
                }

                targetCustomer.ReceiveItem(this, () => { CrowdManager.Instance.OnCustomerServed(targetCustomer); });

                if (remainingCount <= 0)
                {
                    IsJumping = true; 
                    ConveyorManager.Instance.RemoveStackFromBelt(this);
                    ConveyorManager.Instance.ReleaseCapacity();
                    Destroy(gameObject); 
                }
            }
        }

        private void UpdateTransform(SplineConveyorPath path, bool isClockwise)
        {
            transform.position = path.GetPosition(currentDistance);
            Vector3 direction = path.GetDirection(currentDistance, isClockwise);
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        private void OnExitReached()
        {
            if (remainingCount > 0)
            {
                IsJumping = true;
                RackManager.Instance.TryAddStackToRack(this);
            }
        }
    }
}