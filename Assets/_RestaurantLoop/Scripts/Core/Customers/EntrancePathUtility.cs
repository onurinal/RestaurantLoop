using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Pure mathematical utility for conveyor gap calculation and organic CatmullRom path generation.
    /// </summary>
    public static class EntrancePathUtility
    {
        public static Vector3 GetConveyorGapCenter(ConveyorManager conveyor, Vector3 fallbackPos)
        {
            if (conveyor != null && conveyor.Path != null)
            {
                Vector3 entrancePos = conveyor.Path.GetPosition(conveyor.EntranceDistance);
                Vector3 exitPos = conveyor.Path.GetPosition(conveyor.ExitDistance);
                return (entrancePos + exitPos) * 0.5f;
            }

            return fallbackPos;
        }

        public static Vector3 GetOuterSpawnPosition(Vector3 gapPos, Vector3 outerOffset)
        {
            return gapPos + outerOffset;
        }

        public static Vector3[] BuildOrganicPath(Vector3 startPos, Vector3 targetPos, Vector3 gapCenter, Vector3 roomCenter, float jitterAmount)
        {
            Vector3 inboundDirection = (roomCenter - gapCenter).normalized;
            Vector3 perpendicularDirection = Vector3.Cross(inboundDirection, Vector3.up);

            float jitter = Random.Range(-jitterAmount, jitterAmount);
            Vector3 intermediateLandingPos = gapCenter + (inboundDirection * 2.0f) + (perpendicularDirection * jitter);

            return new Vector3[] { startPos, gapCenter, intermediateLandingPos, targetPos };
        }
    }
}