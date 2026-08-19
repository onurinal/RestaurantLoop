using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Controls conveyor simulation settings including capacity, speed, direction, and spawn entrance.
    /// </summary>
    public class ConveyorController : MonoBehaviour
    {
        public static ConveyorController Instance { get; private set; }

        [SerializeField] private SplineConveyorPath path;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private int maxCapacity = 5;
        [SerializeField] private bool isClockwise = true;
        [Range(0f, 1f)] [SerializeField] private float entranceRatio = 0f;

        private readonly List<StackItem> activeStacks = new List<StackItem>();

        public bool CanAcceptStack => activeStacks.Count < maxCapacity;
        public bool IsClockwise => isClockwise;
        public float EntranceDistance => entranceRatio * path.Length;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            for (int i = activeStacks.Count - 1; i >= 0; i--)
            {
                activeStacks[i].MoveAlongBelt(path, moveSpeed, isClockwise, Time.deltaTime);
            }
        }

        /// <summary>
        /// Attempts to add a stack to the conveyor and initializes its position at the configured entrance point.
        /// </summary>
        public bool TryAddStack(StackItem stack)
        {
            if (!CanAcceptStack)
            {
                return false;
            }

            activeStacks.Add(stack);
            stack.InitializeOnBelt(path, EntranceDistance);
            return true;
        }

        public void RemoveStack(StackItem stack)
        {
            activeStacks.Remove(stack);
        }

        private void OnDrawGizmosSelected()
        {
            if (path == null)
            {
                return;
            }

            // Visualizes the entrance point in the scene view
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(path.GetPosition(EntranceDistance), 0.3f);
        }
    }
}