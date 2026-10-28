using UnityEngine;

namespace PaperHouse
{
    /// <summary>
    /// Коврик на столе. Работает в три шага:
    ///   1. E с листом в руках — лист ложится ровно на коврик;
    ///   2. E по коврику — лист складывается пополам (анимация);
    ///   3. E ещё раз — сложенный пополам лист складывается ещё раз, вчетверо.
    /// После этого E забирает готовый лист в руки: продать можно только из рук.
    /// </summary>
    public class PaperMatInteractable : InteractableBase
    {
        public Transform sheetParent;
        public Material paperMaterial;

        [Header("Размер листа")]
        public float sheetWidth = 0.42f;
        public float sheetLength = 0.58f;

        [Header("Звук")]
        public AudioSource audioSource;
        public AudioClip placeClip;
        public AudioClip foldClip;

        private FoldablePaper sheet;

        public int PlacedCount { get { return sheet != null ? 1 : 0; } }
        public int Folds { get { return sheet != null ? sheet.Folds : 0; } }

        public override string GetPrompt(PlayerInteractor player)
        {
            if (sheet == null)
            {
                if (player != null && player.HasPaper)
                    return "E — положить лист на коврик";

                return "Возьмите 1 лист со стопки слева";
            }

            if (sheet.IsAnimating)
                return "Складываем…";

            switch (sheet.Folds)
            {
                case 0:
                    return "E — сложить лист пополам";
                case 1:
                    return "E — сложить ещё раз пополам (вчетверо)";
                default:
                    return "E — взять сложенный лист · стоит $" +
                           (PlayerStats.PaperValue + sheet.Folds * 3);
            }
        }

        public override void Interact(PlayerInteractor player)
        {
            if (sheet == null)
            {
                if (player == null || !player.HasPaper) return;
                PlaceSheet(player);
                return;
            }

            if (sheet.IsAnimating) return;

            if (sheet.Folds < sheet.MaxFolds)
            {
                sheet.FoldNext(CrookForStrength());
                AudioClip clip = foldClip != null ? foldClip : placeClip;
                PlayClip(clip, 0.7f);
            }
            else
            {
                // Готовый лист забираем в руки — сдавать его можно только из рук.
                if (player == null || player.HasPaper) return;

                int folds = sheet.Folds;
                Destroy(sheet.gameObject);
                sheet = null;

                player.TakePaper(folds);
                PlayClip(placeClip, 0.6f);
            }

            if (player != null) player.RefreshHud();
        }

        public void ClearPlaced()
        {
            if (sheet == null) return;

            Destroy(sheet.gameObject);
            sheet = null;
        }

        // ------------------------------------------------------------------ детали

        private void PlaceSheet(PlayerInteractor player)
        {
            // Лист помнит, сколько раз его сложили: вернули на коврик — он
            // остаётся сложенным, а не «расправляется».
            int folds = player.CarriedFolds;
            player.ConsumePaper();

            var sheetObject = new GameObject("Sheet");
            if (sheetParent != null)
                sheetObject.transform.SetParent(sheetParent, false);

            sheetObject.transform.localPosition = Vector3.zero;
            sheetObject.transform.localRotation = Quaternion.identity;

            sheet = sheetObject.AddComponent<FoldablePaper>();
            sheet.material = paperMaterial;
            sheet.width = sheetWidth;
            sheet.length = sheetLength;
            sheet.Build();

            if (folds > 0)
                sheet.FoldInstant(folds);

            PlayClip(placeClip, 0.8f);
            player.RefreshHud();
        }

        /// <summary>
        /// Чем больше силы, тем ровнее сгиб: 1 — криво, 2 — заметно, 3+ — почти идеально.
        /// </summary>
        private static float CrookForStrength()
        {
            switch (PlayerStats.StrengthLevel)
            {
                case 1: return 12f;
                case 2: return 6f;
                case 3: return 2f;
                default: return 0f;
            }
        }

        private void PlayClip(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null)
                AudioService.PlaySfx(audioSource, clip, volume);
        }
    }
}
