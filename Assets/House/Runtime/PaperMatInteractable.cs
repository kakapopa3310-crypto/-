using System.Collections.Generic;
using UnityEngine;

namespace PaperHouse
{
    /// <summary>Коврик на столе. Принимает ровно один лист за нажатие.</summary>
    public class PaperMatInteractable : InteractableBase
    {
        public Transform sheetParent;
        public Material paperMaterial;

        [Header("Звук")]
        public AudioSource audioSource;
        public AudioClip placeClip;

        private readonly List<GameObject> placedSheets = new List<GameObject>();

        public int PlacedCount { get; private set; }
        public int PlacedValue { get; private set; }

        public override string GetPrompt(PlayerInteractor player)
        {
            if (PlacedCount >= 1)
                return "На коврике уже лежит 1 лист — сдайте его в мусорку";

            if (player.HasPaper)
            {
                return "E — положить 1 лист · складка " +
                       Mathf.RoundToInt(PlayerStats.FoldAmount * 100f) + "%";
            }

            return "Возьмите 1 лист со стопки слева";
        }

        public override void Interact(PlayerInteractor player)
        {
            if (PlacedCount >= 1 || !player.HasPaper) return;

            player.ConsumePaper();
            PlacedCount = 1;
            PlacedValue = PlayerStats.PaperValue;

            if (audioSource != null && placeClip != null)
                AudioService.PlaySfx(audioSource, placeClip, 0.8f);

            if (sheetParent != null)
                AddFoldedSheetVisual();

            player.RefreshHud();
        }

        public void ClearPlaced()
        {
            foreach (GameObject sheet in placedSheets)
            {
                if (sheet != null)
                    DestroyImmediate(sheet);
            }

            placedSheets.Clear();
            PlacedCount = 0;
            PlacedValue = 0;
        }

        /// <summary>
        /// Складка зависит от силы: 1 — половина криво, 2 — ровнее,
        /// 3 — идеально, 4 — второй раз пополам с лёгкой кривизной.
        /// </summary>
        private void AddFoldedSheetVisual()
        {
            int strength = PlayerStats.StrengthLevel;
            bool doubleFold = strength >= 4;
            int layers = doubleFold ? 4 : 2;
            float firstCrook = strength == 1 ? 11f : strength == 2 ? 4f : 0f;
            float secondCrook = doubleFold ? 7f : 0f;
            float width = doubleFold ? 0.21f : 0.42f;

            var root = new GameObject("Folded Sheet");
            root.transform.SetParent(sheetParent, false);
            root.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            root.transform.localRotation = Quaternion.identity;

            for (int i = 0; i < layers; i++)
            {
                var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "Fold " + (i + 1);
                Collider panelCollider = panel.GetComponent<Collider>();
                if (panelCollider != null)
                    Destroy(panelCollider);

                panel.transform.SetParent(root.transform, false);
                panel.transform.localPosition = new Vector3(0f, i * 0.003f, 0f);
                panel.transform.localScale = new Vector3(width, 0.0025f, 0.29f);

                float angle = i == 1 ? firstCrook : 0f;
                if (doubleFold && i >= 2) angle += secondCrook;
                panel.transform.localRotation = Quaternion.Euler(0f, angle, 0f);

                var renderer = panel.GetComponent<MeshRenderer>();
                if (paperMaterial != null)
                    renderer.sharedMaterial = paperMaterial;
            }

            placedSheets.Add(root);
        }
    }
}
