using UnityEngine;

namespace PaperHouse
{
    /// <summary>Короткий полёт бумажного самолётика: дуга и посадка в точку end.</summary>
    public class PaperProjectile : MonoBehaviour
    {
        public Vector3 end;
        public float duration = 1f;
        public float arcHeight = 0.4f;
        public PaperHUD hud;
        public string message = "Бумажный самолётик улетел";

        private Vector3 start;
        private float progress;

        private void Start()
        {
            start = transform.position;
        }

        private void Update()
        {
            progress += Time.deltaTime / Mathf.Max(0.05f, duration);
            float t = Mathf.Clamp01(progress);

            transform.position = Vector3.Lerp(start, end, t) +
                                 Vector3.up * Mathf.Sin(t * Mathf.PI) * arcHeight;
            transform.Rotate(220f * Time.deltaTime, 160f * Time.deltaTime, 90f * Time.deltaTime,
                Space.Self);

            if (t >= 1f)
            {
                if (hud != null)
                    hud.ShowToast(message, 3f);
                Destroy(gameObject);
            }
        }
    }
}
