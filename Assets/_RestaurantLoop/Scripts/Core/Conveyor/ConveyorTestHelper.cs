using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// spawning an object to conveyor
    /// </summary>
    public class ConveyorTestHelper : MonoBehaviour
    {
        [SerializeField] private StackItem stackPrefab;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SpawnTestStack();
            }
        }

        private void SpawnTestStack()
        {
            if (stackPrefab == null) return;

            StackItem newStack = Instantiate(stackPrefab);
            bool isAdded = ConveyorController.Instance.TryAddStack(newStack);

            if (!isAdded)
            {
                Destroy(newStack.gameObject);
            }
        }
    }
}