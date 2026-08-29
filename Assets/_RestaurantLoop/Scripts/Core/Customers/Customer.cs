using System;
using System.Collections;
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
        [Tooltip("Child GameObject containing the balloon sprite.")]
        [SerializeField] private GameObject balloonObject;
        [SerializeField] private Renderer customerRenderer;

        [Header("Data")]
        private ItemDataSO requiredData;
        private MaterialPropertyBlock propBlock;
        private static readonly int DesatProperty = Shader.PropertyToID("_Desaturation");
        private float currentDesat = 1f;

        public bool IsServed { get; private set; }
        public bool IsEdgeCustomer { get; private set; }
        public ItemDataSO RequiredData => requiredData;

        private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
        private static readonly int EatHash = Animator.StringToHash("Eat");
        private static readonly int JumpHash = Animator.StringToHash("Jump");

        private Vector3 authoredLocalScale;
        private OrderBalloon orderBalloon;
        private Transform ModelTransform => visualContainer != null ? visualContainer : (animator != null ? animator.transform : transform);

        private void Awake()
        {
            authoredLocalScale = transform.localScale;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (customerRenderer == null) customerRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            if (balloonObject != null) orderBalloon = balloonObject.GetComponent<OrderBalloon>();

            propBlock = new MaterialPropertyBlock();
        }

        private void OnDestroy()
        {
            transform.DOKill();
            ModelTransform.DOKill();
        }

        public void Initialize(ItemDataSO data)
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
        }

        public void SetDesaturation(float targetValue, float duration = 0.5f)
        {
            if (customerRenderer == null) return;

            if (duration <= 0f)
            {
                currentDesat = targetValue;
                customerRenderer.GetPropertyBlock(propBlock);
                propBlock.SetFloat(DesatProperty, targetValue);
                customerRenderer.SetPropertyBlock(propBlock);
                return;
            }

            DOVirtual.Float(currentDesat, targetValue, duration, v =>
            {
                currentDesat = v;
                customerRenderer.GetPropertyBlock(propBlock);
                propBlock.SetFloat(DesatProperty, v);
                customerRenderer.SetPropertyBlock(propBlock);
            });
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
            if (balloonObject == null) return;

            bool isRightSide = transform.position.x > roomCenter.x;

            Vector3 localPos = balloonObject.transform.localPosition;
            Vector3 localScale = balloonObject.transform.localScale;

            float absX = Mathf.Abs(localPos.x != 0 ? localPos.x : 1.4f);
            float absScaleX = Mathf.Abs(localScale.x != 0 ? localScale.x : 1.0f);

            if (isRightSide)
            {
                localPos.x = -absX;
                localScale.x = -absScaleX;
            }
            else
            {
                localPos.x = absX;
                localScale.x = absScaleX;
            }

            balloonObject.transform.localPosition = localPos;
            balloonObject.transform.localScale = localScale;
        }

        public void ReceiveItem(StackItem stack, Action onComplete)
        {
            IsServed = true;
            SetEdgeStatus(false);
            SetBalloonActive(false);

            transform.DOKill();
            ModelTransform.DOKill();

            StartCoroutine(EatAndLeaveRoutine(onComplete));
        }

        /// <summary>
        /// Plays the normal happy departure presentation without invoking normal edge-slot service logic.
        /// Clear Color removes those slots and demands atomically before this visual completes.
        /// </summary>
        public void ResolveByClearColor()
        {
            StopAllCoroutines();
            IsServed = true;
            SetEdgeStatus(false);
            SetBalloonActive(false);

            transform.DOKill();
            ModelTransform.DOKill();
            StartCoroutine(ClearColorExitRoutine());
        }

        private IEnumerator EatAndLeaveRoutine(Action onComplete)
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
                    onComplete?.Invoke();
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
