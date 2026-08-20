using System.Collections.Generic;
using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Spawns queue layout columns and slots dynamically with scene Gizmo preview.
    /// </summary>
    public class QueueSpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private QueueColumn columnPrefab;
        [SerializeField] private QueueSlot slotPrefab;

        [Header("Positioning & Offset")]
        [SerializeField] private RackManager rackManager;
        [SerializeField] private Vector3 offsetFromRack = new Vector3(0f, 0f, -1.8f);

        [Header("Layout Settings")]
        [SerializeField] private float columnSpacing = 1.1f;
        [SerializeField] private float rowSpacing = 1.1f;

        public List<QueueColumn> SpawnQueueLayout(int columnCount, int rowCountPerColumn)
        {
            List<QueueColumn> spawnedColumns = new List<QueueColumn>();

            if (columnPrefab == null || slotPrefab == null)
            {
                return spawnedColumns;
            }

            Vector3 originPosition = GetCalculatedCenterPosition();
            float startX = originPosition.x - (((columnCount - 1) * columnSpacing) / 2f);

            for (int col = 0; col < columnCount; col++)
            {
                Vector3 columnPosition = new Vector3(startX + (col * columnSpacing), originPosition.y, originPosition.z);
                QueueColumn newColumn = Instantiate(columnPrefab, columnPosition, Quaternion.identity, transform);
                newColumn.gameObject.name = $"Column_{col + 1}";

                for (int row = 0; row < rowCountPerColumn; row++)
                {
                    Vector3 slotPosition = columnPosition + new Vector3(0f, 0f, -row * rowSpacing);
                    QueueSlot newSlot = Instantiate(slotPrefab, slotPosition, Quaternion.identity, newColumn.transform);
                    newSlot.gameObject.name = $"Slot_{row + 1}";
                }

                newColumn.InitializeChildSlots();
                spawnedColumns.Add(newColumn);
            }

            return spawnedColumns;
        }

        public Vector3 GetCalculatedCenterPosition()
        {
            if (rackManager == null)
            {
                rackManager = FindFirstObjectByType<RackManager>();
            }

            if (rackManager != null)
            {
                return rackManager.GetCalculatedCenterPosition() + offsetFromRack;
            }

            return transform.position;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = GetCalculatedCenterPosition();
            QueueManager queueManager = FindFirstObjectByType<QueueManager>();

            int cols = queueManager != null ? queueManager.InitialColumnCount : 3;
            int rows = queueManager != null ? queueManager.InitialRowCount : 3;

            Gizmos.color = Color.cyan;
            float startX = origin.x - (((cols - 1) * columnSpacing) / 2f);

            for (int col = 0; col < cols; col++)
            {
                for (int row = 0; row < rows; row++)
                {
                    Vector3 slotPos = origin + new Vector3((startX - origin.x) + (col * columnSpacing), 0f, -row * rowSpacing);
                    Gizmos.DrawWireSphere(slotPos, 0.4f);
                }
            }
        }
    }
}