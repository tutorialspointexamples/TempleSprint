using UnityEngine;

namespace TempleSprint
{
    public class GameSettings
    {
        const string SensKey = "TempleSprint.SwipeSensitivity";
        const string TiltKey = "TempleSprint.TiltEnabled";
        const string QualityKey = "TempleSprint.Quality";
        const string TextKey = "TempleSprint.TextScale";
        const string SfxKey = "TempleSprint.SfxEnabled";

        public static bool SfxEnabled
        {
            get => PlayerPrefs.GetInt(SfxKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(SfxKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static float SwipeSensitivity
        {
            get => PlayerPrefs.GetFloat(SensKey, 1f);
            set
            {
                PlayerPrefs.SetFloat(SensKey, Mathf.Clamp(value, 0.5f, 2f));
                PlayerPrefs.Save();
                if (SwipeInput.Instance != null) SwipeInput.Instance.Sensitivity = value;
            }
        }

        public static bool TiltEnabled
        {
            get => PlayerPrefs.GetInt(TiltKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(TiltKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (SwipeInput.Instance != null) SwipeInput.Instance.TiltEnabled = value;
            }
        }

        public static int GraphicsQuality
        {
            get => PlayerPrefs.GetInt(QualityKey, 1);
            set
            {
                PlayerPrefs.SetInt(QualityKey, Mathf.Clamp(value, 0, 2));
                PlayerPrefs.Save();
                QualitySettings.SetQualityLevel(Mathf.Clamp(value, 0, QualitySettings.names.Length - 1));
            }
        }

        public static float TextScale
        {
            get => PlayerPrefs.GetFloat(TextKey, 1f);
            set
            {
                PlayerPrefs.SetFloat(TextKey, Mathf.Clamp(value, 1f, 1.5f));
                PlayerPrefs.Save();
            }
        }

        public static void ApplyToInput()
        {
            if (SwipeInput.Instance == null) return;
            SwipeInput.Instance.Sensitivity = SwipeSensitivity;
            SwipeInput.Instance.TiltEnabled = TiltEnabled;
        }
    }
}
