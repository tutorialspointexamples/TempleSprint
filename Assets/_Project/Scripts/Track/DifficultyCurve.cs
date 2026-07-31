using UnityEngine;

namespace TempleSprint
{
    /// <summary>Scriptable difficulty curve — create assets later; runtime defaults via CreateRuntimeDefault.</summary>
    [CreateAssetMenu(fileName = "DifficultyCurve", menuName = "Temple Sprint/Difficulty Curve")]
    public class DifficultyCurve : ScriptableObject
    {
        public AnimationCurve speedOverDistance = AnimationCurve.Linear(0f, 10f, 1000f, 28f);
        public AnimationCurve obstacleChanceOverDistance = AnimationCurve.Linear(0f, 0.25f, 800f, 0.75f);

        public float EvaluateSpeed(float distance) => speedOverDistance.Evaluate(distance);
        public float EvaluateObstacleChance(float distance) => obstacleChanceOverDistance.Evaluate(distance);

        public static DifficultyCurve CreateRuntimeDefault()
        {
            var c = CreateInstance<DifficultyCurve>();
            c.speedOverDistance = AnimationCurve.EaseInOut(0f, 10f, 1000f, 28f);
            c.obstacleChanceOverDistance = AnimationCurve.Linear(0f, 0.25f, 800f, 0.75f);
            return c;
        }
    }
}
