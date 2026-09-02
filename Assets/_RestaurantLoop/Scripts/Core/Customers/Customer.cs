using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using RestaurantLoop.Infrastructure;
using RestaurantLoop.Audio;

namespace RestaurantLoop.Core
{
    public class Customer : MonoBehaviour
    {
        [Header("Animation & Visuals")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualContainer;
        [SerializeField] private GameObject balloonObject;
        [SerializeField] private Renderer customerRenderer;

        [Header("Data")]
        private ItemDataSO requiredData;
        private MaterialPropertyBlock propBlock;
        private static readonly int DesatProperty = Shader.PropertyToID("_Desaturation");
        private float currentDesat = 1f;

        private static readonly Dictionary<Material, Material> DesaturatedVariants = new Dictionary<Material, Material>();
        private static readonly Dictionary<Material, Material> SaturatedVariants = new Dictionary<Material, Material>();
        private Material baseSharedMaterial;
        private Tween desatTween;

        private TweenCallback<float> desatUpdateCallback;
        private TweenCallback desatCompleteCallback;

        public bool IsServed { get; private set; }
        public bool IsEdgeCustomer { get; private set; }
        public ItemDataSO RequiredData => requiredData;

        private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
        private static readonly int EatHash = Animator.StringToHash("Eat");
        private static readonly int JumpHash = Animator.StringToHash("Jump");

        private Vector3 authoredLocalScale;
        private OrderBalloon orderBalloon;
        private TimedCustomerAgent timedCustomerAgent;
        private Transform ModelTransform => visualContainer != null ? visualContainer : (animator != null ? animator.transform : transform);

        private void Awake()
        {
            authoredLocalScale = transform.localScale;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (customerRenderer == null) customerRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            if (balloonObject != null) orderBalloon = balloonObject.GetComponent<OrderBalloon>();
            timedCustomerAgent = GetComponent<TimedCustomerAgent>();

            if (customerRenderer != null) baseSharedMaterial = customerRenderer.sharedMaterial;

            propBlock = new MaterialPropertyBlock();
            desatUpdateCallback = OnDesaturationUpdate;
            desatCompleteCallback = OnDesaturationComplete;
        }

        private void OnDisable()
        {
            KillTransientTweens();

            if (customerRenderer != null) customerRenderer.SetPropertyBlock(null);
        }

        private void OnDestroy()
        {
            KillTransientTweens();
        }

        private void KillTransientTweens()
        {
            desatTween?.Kill();
            desatTween = null;
            transform.DOKill();
            ModelTransform.DOKill();
        }

        public void Initialize(ItemDataSO data, bool isTimed = false, float timeLimitDuration = 0f)
        {
            StopAllCoroutines();
            transform.DOKill();
            ModelTransform.DOKill();

            requiredData = data;
            IsServed = false;
            IsEdgeCustomer = false;

            transform.localScale = authoredLocalScale;
            transform.rotation = Quaternion.identity;
            ModelTransform.localRotation = Quaternion.identity;

            SetBalloonActive(false, animate: false);
            SetDesaturation(1f, 0f);
            timedCustomerAgent?.Configure(isTimed, timeLimitDuration);
        }

        public void SetDesaturation(float targetValue, float duration = 0.5f)
        {
            if (customerRenderer == null) return;

            desatTween?.Kill();
            desatTween = null;

            if (duration <= 0f)
            {
                currentDesat = targetValue;
                ApplySettledDesaturation(targetValue);
                return;
            }

            desatTween = DOVirtual.Float(currentDesat, targetValue, duration, desatUpdateCallback)
                .SetTarget(this)
                .OnComplete(desatCompleteCallback);
        }

        private void OnDesaturationUpdate(float value)
        {
            currentDesat = value;
            if (customerRenderer != null)
            {
                customerRenderer.GetPropertyBlock(propBlock);
                propBlock.SetFloat(DesatProperty, value);
                customerRenderer.SetPropertyBlock(propBlock);
            }
        }

        private void OnDesaturationComplete()
        {
            desatTween = null;
            ApplySettledDesaturation(currentDesat);
        }

        private void ApplySettledDesaturation(float value)
        {
            if (customerRenderer == null) return;

            Material variant = GetDesaturationVariant(value);
            if (variant == null)
            {
                customerRenderer.GetPropertyBlock(propBlock);
                propBlock.SetFloat(DesatProperty, value);
                customerRenderer.SetPropertyBlock(propBlock);
                return;
            }

            customerRenderer.SetPropertyBlock(null);
            customerRenderer.sharedMaterial = variant;
        }

        private Material GetDesaturationVariant(float value)
        {
            if (baseSharedMaterial == null) return null;

            Dictionary<Material, Material> cache;
            if (Mathf.Approximately(value, 1f)) cache = DesaturatedVariants;
            else if (Mathf.Approximately(value, 0f)) cache = SaturatedVariants;
            else return null;

            if (!cache.TryGetValue(baseSharedMaterial, out Material variant) || variant == null)
            {
                variant = new Material(baseSharedMaterial)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                variant.SetFloat(DesatProperty, value);
                cache[baseSharedMaterial] = variant;
            }

            return variant;
        }

        public void SetBalloonActive(bool active, bool animate = true)
        {
            if (balloonObject == null) return;

            if (active)
            {
                if (balloonObject.activeSelf)
                {
                    orderBalloon?.PlayIn();
                }
                else
                {
                    balloonObject.SetActive(true);
                }

                return;
            }

            if (!balloonObject.activeSelf) return;

            if (!animate || orderBalloon == null)
            {
                balloonObject.SetActive(false);
                return;
            }

            orderBalloon.PlayOut(() =>
            {
                if (balloonObject != null)
                {
                    balloonObject.SetActive(false);
                }
            });
        }

        public void SetModelRotation(float yAngle)
        {
            ModelTransform.localRotation = Quaternion.Euler(0f, yAngle, 0f);
        }

        public void SetEdgeStatus(bool isEdge)
        {
            IsEdgeCustomer = isEdge;
        }

        public void MoveAlongPath(Vector3[] waypoints, float duration, bool setAsEdge, float targetYRotation = 0f, Vector3 roomCenter = default,
            Action onComplete = null)
        {
            SetEdgeStatus(false);
            SetBalloonActive(false);

            if (setAsEdge)
            {
                SetDesaturation(0f, 0.4f);
            }

            transform.DOKill();
            ModelTransform.DOKill();

            if (animator != null) animator.SetBool(IsWalkingHash, true);

            ModelTransform.DOLookAt(waypoints[waypoints.Length - 1], duration, AxisConstraint.Y);

            transform.DOPath(waypoints, duration, PathType.CatmullRom)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    ModelTransform.DOLocalRotate(new Vector3(0f, targetYRotation, 0f), 0.2f).OnComplete(() =>
                    {
                        if (animator != null) animator.SetBool(IsWalkingHash, false);
                        if (setAsEdge)
                        {
                            SetEdgeStatus(true);
                            AlignBalloonToCenter(roomCenter);
                            SetBalloonActive(true);
                        }

                        onComplete?.Invoke();
                    });
                });
        }

        public void MoveToEdgeSlot(Vector3 targetPosition, float targetYRotation = 0f, Vector3 roomCenter = default, Action onComplete = null)
        {
            SetEdgeStatus(false);
            SetBalloonActive(false);

            SetDesaturation(0f, 0.4f);

            transform.DOKill();
            ModelTransform.DOKill();

            if (animator != null) animator.SetBool(IsWalkingHash, true);

            Vector3 moveDir = targetPosition - transform.position;
            moveDir.y = 0f;

            if (moveDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                ModelTransform.DORotateQuaternion(targetRotation, 0.15f);
            }

            transform.DOMove(targetPosition, 0.6f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    ModelTransform.DOLocalRotate(new Vector3(0f, targetYRotation, 0f), 0.2f).OnComplete(() =>
                    {
                        if (animator != null) animator.SetBool(IsWalkingHash, false);
                        SetEdgeStatus(true);
                        AlignBalloonToCenter(roomCenter);
                        SetBalloonActive(true);
                        onComplete?.Invoke();
                    });
                });
        }

        public void AlignBalloonToCenter(Vector3 roomCenter)
        {
            // Position and scale inversions removed. Balloon maintains its exact authored prefab transform.
        }

        public void ReceiveItem(StackItem stack, Action<Customer> onComplete)
        {
            timedCustomerAgent?.MarkServed();
            IsServed = true;
            SetEdgeStatus(false);
            SetBalloonActive(false);

            transform.DOKill();
            ModelTransform.DOKill();

            StartCoroutine(EatAndLeaveRoutine(onComplete));
        }

        public void ResolveByClearColor()
        {
            StopAllCoroutines();
            timedCustomerAgent?.MarkServed();
            IsServed = true;
            SetEdgeStatus(false);
            SetBalloonActive(false);

            transform.DOKill();
            ModelTransform.DOKill();
            StartCoroutine(ClearColorExitRoutine());
        }

        public void PlayTimedFailureReaction()
        {
            if (IsServed) return;

            StopAllCoroutines();
            transform.DOKill();
            ModelTransform.DOKill();
            if (animator != null) animator.SetBool(IsWalkingHash, false);

            ModelTransform.DOShakeRotation(0.45f, new Vector3(0f, 0f, 14f), 18, 70f)
                .SetUpdate(true)
                .SetTarget(this);
        }

        private IEnumerator EatAndLeaveRoutine(Action<Customer> onComplete)
        {
            yield return new WaitForSeconds(0.35f);

            if (AudioManager.Instance != null && AudioManager.Instance.popSound != null)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.popSound);

            if (animator != null) animator.SetTrigger(EatHash);

            if (AudioManager.Instance != null && AudioManager.Instance.nomNomSound != null)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.nomNomSound);

            yield return new WaitForSeconds(0.5f);

            if (animator != null) animator.SetTrigger(JumpHash);

            if (AudioManager.Instance != null && AudioManager.Instance.happyJumpSound != null)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.happyJumpSound);

            transform.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InBack);
            transform.DOJump(transform.position, 0.5f, 1, 0.4f)
                .OnComplete(() =>
                {
                    onComplete?.Invoke(this);
                    PoolManager.Instance.Despawn(gameObject);
                });
        }

        private IEnumerator ClearColorExitRoutine()
        {
            yield return new WaitForSecondsRealtime(0.35f);

            if (AudioManager.Instance != null && AudioManager.Instance.popSound != null)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.popSound);

            if (animator != null) animator.SetTrigger(EatHash);

            if (AudioManager.Instance != null && AudioManager.Instance.nomNomSound != null)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.nomNomSound);

            yield return new WaitForSecondsRealtime(0.5f);

            if (animator != null) animator.SetTrigger(JumpHash);

            if (AudioManager.Instance != null && AudioManager.Instance.happyJumpSound != null)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.happyJumpSound);

            transform.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InBack).SetUpdate(true);
            transform.DOJump(transform.position, 0.5f, 1, 0.4f)
                .SetUpdate(true)
                .OnComplete(() => PoolManager.Instance.Despawn(gameObject));
        }
    }
}