using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;
using RestaurantLoop.Infrastructure;
using RestaurantLoop.Audio; // Added to access AudioManager

namespace RestaurantLoop.Core
{
    public class Customer : MonoBehaviour
    {
        [Header("Animation & Visuals")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualContainer;
        [Tooltip("Child GameObject containing the balloon sprite (preset at 40 degrees camera pitch).")]
        [SerializeField] private GameObject balloonObject;

        [Header("Data")]
        private ItemDataSO requiredData;

        public bool IsServed { get; private set; }
        public bool IsEdgeCustomer { get; private set; }
        public ItemDataSO RequiredData => requiredData;

        private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
        private static readonly int EatHash = Animator.StringToHash("Eat");
        private static readonly int JumpHash = Animator.StringToHash("Jump");

        private Vector3 authoredLocalScale;
        private Transform ModelTransform => visualContainer != null ? visualContainer : (animator != null ? animator.transform : transform);

        private void Awake()
        {
            authoredLocalScale = transform.localScale;
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void OnDestroy()
        {
            transform.DOKill();
            ModelTransform.DOKill();
        }

        public void Initialize(ItemDataSO data)
        {
            transform.DOKill();
            ModelTransform.DOKill();

            requiredData = data;
            IsServed = false;
            IsEdgeCustomer = false;

            transform.localScale = authoredLocalScale;
            transform.rotation = Quaternion.identity;
            ModelTransform.localRotation = Quaternion.identity;

            SetBalloonActive(false);
        }

        public void SetBalloonActive(bool active)
        {
            if (balloonObject != null)
            {
                balloonObject.SetActive(active);
            }
        }

        public void SetModelRotation(float yAngle)
        {
            ModelTransform.localRotation = Quaternion.Euler(0f, yAngle, 0f);
        }

        public void SetEdgeStatus(bool isEdge)
        {
            IsEdgeCustomer = isEdge;
        }

        public void MoveAlongPath(Vector3[] waypoints, float duration, bool setAsEdge, float targetYRotation = 0f, Action onComplete = null)
        {
            SetEdgeStatus(setAsEdge);
            SetBalloonActive(false);

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

                        if (IsEdgeCustomer)
                        {
                            SetBalloonActive(true);
                        }

                        onComplete?.Invoke();
                    });
                });
        }

        public void MoveToEdgeSlot(Vector3 targetPosition, float targetYRotation = 0f, Action onComplete = null)
        {
            SetEdgeStatus(true);
            SetBalloonActive(false);

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
                        SetBalloonActive(true);
                        onComplete?.Invoke();
                    });
                });
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

        private IEnumerator EatAndLeaveRoutine(Action onComplete)
        {
            // Wait for the plate flight duration (0.35s)
            yield return new WaitForSeconds(0.35f);

            // 1. Plate is caught by the customer -> Play "Pop" sound
            if (AudioManager.Instance != null && AudioManager.Instance.popSound != null)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.popSound);

            if (animator != null) animator.SetTrigger(EatHash);
            
            // 2. Customer starts eating -> Play "Nom-nom" sound
            if (AudioManager.Instance != null && AudioManager.Instance.nomNomSound != null)
                AudioManager.Instance.PlaySFX(AudioManager.Instance.nomNomSound);

            // Wait for the eating animation to finish (0.5s)
            yield return new WaitForSeconds(0.5f);

            if (animator != null) animator.SetTrigger(JumpHash);

            // 3. Customer finishes eating and jumps happily -> Play "Happy Jump" sound
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
    }
}