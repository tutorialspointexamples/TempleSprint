using UnityEngine;

namespace TempleSprint
{
    /// <summary>Spawn / sample pose along the endless path (not locked to world +Z).</summary>
    public struct PathPose
    {
        public Vector3 position;
        public float yaw;
        public float pathDistance;

        public PathPose(Vector3 position, float yaw, float pathDistance)
        {
            this.position = position;
            this.yaw = yaw;
            this.pathDistance = pathDistance;
        }

        public Quaternion Rotation => Quaternion.Euler(0f, yaw, 0f);
        public Vector3 Forward => Rotation * Vector3.forward;
        public Vector3 Right => Rotation * Vector3.right;

        public PathPose AdvanceStraight(float distance)
        {
            return new PathPose(position + Forward * distance, yaw, pathDistance + distance);
        }

        public static float NormalizeYaw(float yaw)
        {
            yaw %= 360f;
            if (yaw > 180f) yaw -= 360f;
            if (yaw < -180f) yaw += 360f;
            return yaw;
        }
    }
}
