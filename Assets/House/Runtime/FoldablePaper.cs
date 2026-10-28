using System.Collections;
using UnityEngine;

namespace PaperHouse
{
    /// <summary>
    /// Лист бумаги, который складывается по-настоящему, как оригами:
    /// сначала пополам по длине, потом ещё пополам по ширине.
    ///
    /// Геометрия: четыре панели на двух шарнирах.
    ///   Fold 1 — обе половины (левая и правая) загибаются внутрь по оси Z-края,
    ///   Fold 2 — вся правая половина перекидывается на левую по оси X-края.
    /// В результате лист 0.42 × 0.58 превращается в стопку 0.21 × 0.29 из 4 слоёв.
    /// </summary>
    public class FoldablePaper : MonoBehaviour
    {
        public Material material;
        public float width = 0.42f;
        public float length = 0.58f;

        /// <summary>Толщина одного слоя: обычный лист — 0.2 мм.</summary>
        public float thickness = 0.0002f;

        private Transform hingeFold1Left;
        private Transform hingeFold1Right;
        private Transform hingeFold2;

        private int folds;
        private bool animating;

        public int Folds { get { return folds; } }
        public int MaxFolds { get { return 2; } }
        public bool IsAnimating { get { return animating; } }

        /// <summary>Создаёт четыре панели и два шарнира.</summary>
        public void Build()
        {
            float w = Mathf.Max(0.05f, width);
            float l = Mathf.Max(0.05f, length);
            float t = Mathf.Max(0.0001f, thickness);

            // Левая половина: остаётся на месте при обеих складках.
            Transform left = CreateNode(transform, "Half Left", Vector3.zero);
            CreatePanel(left, "Base Left",
                new Vector3(-w * 0.25f, t * 0.5f, -l * 0.25f),
                new Vector3(w * 0.5f, t, l * 0.5f));

            hingeFold1Left = CreateNode(left, "Hinge 1 Left", new Vector3(-w * 0.25f, t * 1.02f, 0f));
            CreatePanel(hingeFold1Left, "Flap Left",
                new Vector3(0f, t * 0.5f, l * 0.25f),
                new Vector3(w * 0.5f, t, l * 0.5f));

            // Правая половина висит на шарнире второго сгиба.
            hingeFold2 = CreateNode(transform, "Hinge 2", new Vector3(0f, t * 2.04f, 0f));
            Transform right = CreateNode(hingeFold2, "Half Right", new Vector3(0f, -t * 2.04f, 0f));
            CreatePanel(right, "Base Right",
                new Vector3(w * 0.25f, t * 0.5f, -l * 0.25f),
                new Vector3(w * 0.5f, t, l * 0.5f));

            hingeFold1Right = CreateNode(right, "Hinge 1 Right", new Vector3(w * 0.25f, t * 1.02f, 0f));
            CreatePanel(hingeFold1Right, "Flap Right",
                new Vector3(0f, t * 0.5f, l * 0.25f),
                new Vector3(w * 0.5f, t, l * 0.5f));

            folds = 0;
        }

        /// <summary>
        /// Следующий сгиб. crookDegrees — насколько криво (сила рук игрока),
        /// от 0° (идеально) до ~12° (совсем криво).
        /// </summary>
        public void FoldNext(float crookDegrees, float duration = 0.5f)
        {
            if (animating || folds >= MaxFolds) return;
            StartCoroutine(FoldRoutine(folds + 1, Mathf.Clamp(crookDegrees, 0f, 20f), duration));
        }

        private IEnumerator FoldRoutine(int stage, float crook, float duration)        {
            animating = true;

            bool firstFold = stage == 1;
            Transform hingeA = firstFold ? hingeFold1Left : hingeFold2;
            Transform hingeB = firstFold ? hingeFold1Right : null;

            Quaternion fromA = hingeA.localRotation;
            Quaternion fromB = hingeB != null ? hingeB.localRotation : Quaternion.identity;

            Vector3 axis = firstFold ? Vector3.right : Vector3.forward;

            // Полный сгиб плюс лёгкий перекос: при малой силе лист выходит кривым.
            Quaternion toA = Quaternion.AngleAxis(crook, Vector3.up) * Quaternion.AngleAxis(180f, axis);
            Quaternion toB = toA;

            Vector3 fromPosition = transform.localPosition;
            Vector3 toPosition = firstFold
                ? fromPosition + new Vector3(0f, 0f, length * 0.25f)
                : fromPosition + new Vector3(width * 0.25f, 0f, 0f);

            float elapsed = 0f;
            duration = Mathf.Max(0.05f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / duration);
                float eased = k * k * (3f - 2f * k);

                hingeA.localRotation = Quaternion.Slerp(fromA, toA, eased);
                if (hingeB != null)
                    hingeB.localRotation = Quaternion.Slerp(fromB, toB, eased);

                transform.localPosition = Vector3.Lerp(fromPosition, toPosition, eased);
                yield return null;
            }

            hingeA.localRotation = toA;
            if (hingeB != null)
                hingeB.localRotation = toB;
            transform.localPosition = toPosition;

            folds = stage;
            animating = false;
        }

        /// <summary>
        /// Мгновенно применить сгибы (для листов, уже лежащих в стопке):
        /// 0 — плоский, 1 — пополам, 2 — вчетверо.
        /// </summary>
        public void FoldInstant(int foldCount, float crookDegrees = 0f)
        {
            foldCount = Mathf.Clamp(foldCount, 0, MaxFolds);
            float crook = Mathf.Clamp(crookDegrees, 0f, 20f);

            if (hingeFold1Left != null) hingeFold1Left.localRotation = Quaternion.identity;
            if (hingeFold1Right != null) hingeFold1Right.localRotation = Quaternion.identity;
            if (hingeFold2 != null) hingeFold2.localRotation = Quaternion.identity;

            transform.localPosition = Vector3.zero;
            folds = 0;

            for (int stage = 1; stage <= foldCount; stage++)
            {
                bool firstFold = stage == 1;
                Quaternion rotation = Quaternion.AngleAxis(crook, Vector3.up) *
                                      Quaternion.AngleAxis(180f, firstFold ? Vector3.right : Vector3.forward);

                if (firstFold)
                {
                    if (hingeFold1Left != null) hingeFold1Left.localRotation = rotation;
                    if (hingeFold1Right != null) hingeFold1Right.localRotation = rotation;
                    transform.localPosition += new Vector3(0f, 0f, length * 0.25f);
                }
                else
                {
                    if (hingeFold2 != null) hingeFold2.localRotation = rotation;
                    transform.localPosition += new Vector3(width * 0.25f, 0f, 0f);
                }

                folds = stage;
            }
        }

        // ------------------------------------------------------------------ утилиты

        private static Transform CreateNode(Transform parent, string name, Vector3 localPosition)
        {
            var node = new GameObject(name);
            node.transform.SetParent(parent, false);
            node.transform.localPosition = localPosition;
            node.transform.localRotation = Quaternion.identity;
            node.transform.localScale = Vector3.one;
            return node.transform;
        }

        private void CreatePanel(Transform parent, string name, Vector3 localPosition, Vector3 scale)
        {
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = name;

            Collider panelCollider = panel.GetComponent<Collider>();
            if (panelCollider != null)
            {
                panelCollider.enabled = false;
                Destroy(panelCollider);
            }

            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = localPosition;
            panel.transform.localRotation = Quaternion.identity;
            panel.transform.localScale = scale;

            var renderer = panel.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;
        }
    }
}
