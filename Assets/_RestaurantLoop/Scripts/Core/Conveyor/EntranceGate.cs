using System;
using UnityEngine;
using DG.Tweening;

namespace RestaurantLoop.Core
{
    public class EntranceGate : MonoBehaviour
    {
        [Header("Door References")]
        [SerializeField] private Transform leftDoor;
        [SerializeField] private Transform rightDoor;

        [Header("Animation Settings")]
        [SerializeField] private float openAngle = 100f;
        [SerializeField] private float animationDuration = 0.45f;
        [SerializeField] private Ease openEase = Ease.OutBack;
        [SerializeField] private Ease closeEase = Ease.InQuad;

        private Vector3 leftInitialRotation;
        private Vector3 rightInitialRotation;

        private void Awake()
        {
            if (leftDoor != null) leftInitialRotation = leftDoor.localEulerAngles;
            if (rightDoor != null) rightInitialRotation = rightDoor.localEulerAngles;
        }

        public void OpenGate(Action onComplete = null)
        {
            if (leftDoor != null)
            {
                leftDoor.DOKill();
                leftDoor.DOLocalRotate(leftInitialRotation + new Vector3(0f, -openAngle, 0f), animationDuration)
                    .SetEase(openEase);
            }

            if (rightDoor != null)
            {
                rightDoor.DOKill();
                rightDoor.DOLocalRotate(rightInitialRotation + new Vector3(0f, openAngle, 0f), animationDuration)
                    .SetEase(openEase)
                    .OnComplete(() => onComplete?.Invoke());
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        public void CloseGate(Action onComplete = null)
        {
            if (leftDoor != null)
            {
                leftDoor.DOKill();
                leftDoor.DOLocalRotate(leftInitialRotation, animationDuration).SetEase(closeEase);
            }

            if (rightDoor != null)
            {
                rightDoor.DOKill();
                rightDoor.DOLocalRotate(rightInitialRotation, animationDuration)
                    .SetEase(closeEase)
                    .OnComplete(() => onComplete?.Invoke());
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        public void ResetGateImmediate()
        {
            if (leftDoor != null)
            {
                leftDoor.DOKill();
                leftDoor.localEulerAngles = leftInitialRotation;
            }

            if (rightDoor != null)
            {
                rightDoor.DOKill();
                rightDoor.localEulerAngles = rightInitialRotation;
            }
        }
    }
}