using UnityEngine;

namespace PaperHouse
{
    /// <summary>Полёт бумажного самолётика к луне.</summary>
    public class PaperProjectile : MonoBehaviour
    {
        public Vector3 end;
        public float duration = 4.5f;
        public float arcHeight = 9f;
        public PaperHUD hud;
        public string message = "Бумага долетела до луны!";

        private Vector3 start;
        private float progress;

        private void Start()
        {
            start = transform.position;
        }

        private void Update()
        {
            progress += Time.deltaTime / Mathf.Max(0.1f, duration);
            float t = Mathf.Clamp01(progress);

            transform.position = Vector3.Lerp(start, end, t) +
                                 Vector3.up * Mathf.Sin(t * Mathf.PI) * arcHeight;
            transform.Rotate(220f * Time.deltaTime, 160f * Time.deltaTime, 90f * Time.deltaTime,
                Space.Self);

            if (t >= 1f)
            {
                if (hud != null)
                    hud.ShowToast(message, 4f);
                Destroy(gameObject);
            }
        }
    }
}
