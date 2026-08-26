using UnityEngine;

namespace RestaurantLoop.Core
{
    public class InputHandler : MonoBehaviour
    {
        private Camera mainCamera;

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void Update()
        {
            // Block all input raycasts when level is won or lost
            if (LevelManager.Instance != null && !LevelManager.Instance.IsGameActive)
            {
                return;
            }

            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.TryGetComponent(out IInteractable interactable))
                {
                    interactable.OnTap();
                }
            }
        }
    }
}