using System.Collections.Generic;
using UnityEngine;

namespace PaperHouse
{
    /// <summary>
    /// Отдельный стол, куда складывают готовые листы.
    /// Лист кладётся сверху тем же видом, что и на коврике, а толщина честная:
    /// обычный лист 0.2 мм, каждый сгиб удваивает — 0.2 / 0.4 / 0.8 мм.
    /// Стопка растёт в высоту на толщину положенного листа.
    /// </summary>
    public class PaperPileInteractable : InteractableBase
    {
        public Transform pileParent;
        public Material paperMaterial;

        [Header("Лист")]
        public float sheetWidth = 0.42f;
        public float sheetLength = 0.58f;

        /// <summary>Толщина обычного, несогнутого листа — 0.2 мм.</summary>
        public float sheetThickness = 0.0002f;

        [Header("Звук")]
        public AudioSource audioSource;
        public AudioClip placeClip;
        public AudioClip takeClip;

        private readonly List<GameObject> sheets = new List<GameObject>();
        private readonly List<int> folds = new List<int>();
        private readonly List<float> thicknesses = new List<float>();
        private float pileHeight;

        public int Count { get { return sheets.Count; } }
        public float TotalHeight { get { return pileHeight; } }
        public float TotalHeightMm { get { return pileHeight * 1000f; } }

        public override string GetPrompt(PlayerInteractor player)
        {
            if (player != null && player.HasPaper)
                return "E — положить лист в стопку · сейчас " + Count + " (" + HeightText() + ")";

            if (Count == 0)
                return "Стол для готовой бумаги — сложите лист и положите сюда";

            return "E — взять верхний лист · в стопке " + Count + " (" + HeightText() + ")";
        }

        public override void Interact(PlayerInteractor player)
        {
            if (player == null) return;

            if (player.HasPaper)
            {
                AddSheet(player.CarriedFolds);
                player.ConsumePaper();
                PlayClip(placeClip, 0.8f);
            }
            else if (Count > 0)
            {
                int sheetFolds = TakeTopSheet();
                player.TakePaper(sheetFolds);
                PlayClip(takeClip, 0.7f);
            }

            player.RefreshHud();
        }

        public void ClearPile()
        {
            for (int i = 0; i < sheets.Count; i++)
            {
                if (sheets[i] != null) Destroy(sheets[i]);
            }

            sheets.Clear();
            folds.Clear();
            thicknesses.Clear();
            pileHeight = 0f;
        }

        // ------------------------------------------------------------------ стопка

        private void AddSheet(int sheetFolds)
        {
            int layers = Mathf.Clamp(sheetFolds, 0, 2);
            float thickness = sheetThickness * Mathf.Pow(2f, layers);

            // Держатель стоит на своём месте в стопке, а лист внутри центрируется сам.
            var holder = new GameObject("Stacked Sheet " + (sheets.Count + 1));
            Transform parent = pileParent != null ? pileParent : transform;

            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = new Vector3(
                Random.Range(-0.003f, 0.003f),
                pileHeight + thickness * 0.5f,
                Random.Range(-0.003f, 0.003f));
            holder.transform.localRotation = Quaternion.Euler(0f, Random.Range(-3f, 3f), 0f);

            var paper = new GameObject("Paper");
            paper.transform.SetParent(holder.transform, false);

            var foldable = paper.AddComponent<FoldablePaper>();
            foldable.material = paperMaterial;
            foldable.width = sheetWidth;
            foldable.length = sheetLength;
            foldable.thickness = sheetThickness;
            foldable.Build();
            foldable.FoldInstant(layers, Random.Range(-2f, 2f));

            sheets.Add(holder);
            folds.Add(layers);
            thicknesses.Add(thickness);
            pileHeight += thickness;
        }

        private int TakeTopSheet()
        {
            int last = sheets.Count - 1;
            GameObject holder = sheets[last];
            int sheetFolds = folds[last];
            float thickness = thicknesses[last];

            sheets.RemoveAt(last);
            folds.RemoveAt(last);
            thicknesses.RemoveAt(last);
            pileHeight = Mathf.Max(0f, pileHeight - thickness);

            if (holder != null) Destroy(holder);
            return sheetFolds;
        }

        private string HeightText()
        {
            float mm = TotalHeightMm;
            if (mm < 0.95f) return "<1 мм";
            return mm.ToString("0.0") + " мм";
        }

        private void PlayClip(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null)
                AudioService.PlaySfx(audioSource, clip, volume);
        }
    }
}
