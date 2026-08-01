using UnityEngine;

namespace TempleSprint
{
    /// <summary>Spawn / sample pose along the endless path (not locked to world +Z).</summary>
    public struct PathPose
    {
        public Vector3 position;
        public float yaw;
        public float pathDistance;
        /// <summary>Roll degrees — positive leans right; used for banked curve turns.</summary>
        public float bank;

        public PathPose(Vector3 position, float yaw, float pathDistance, float bank = 0f)
        {
            this.position = position;
            this.yaw = yaw;
            this.pathDistance = pathDistance;
            this.bank = bank;
        }

        public Quaternion Rotation => Quaternion.Euler(0f, yaw, bank);
        public Vector3 Forward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        public Vector3 Right => Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

        public PathPose AdvanceStraight(float distance)
        {
            return new PathPose(position + Forward * distance, yaw, pathDistance + distance, 0f);
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
