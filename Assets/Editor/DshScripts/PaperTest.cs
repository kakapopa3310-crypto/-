using PaperHouse;
using UnityEditor;
using UnityEngine;

namespace DshScripts
{
    /// <summary>
    /// Проверка механик из консоли (Play Mode):
    ///   setup     — встать у стола и взять лист
    ///   place     — положить лист на коврик
    ///   fold      — сложить (первый/второй раз)
    ///   take      — забрать готовый лист с коврика в руки
    ///   pile      — положить лист из рук в стопку на отдельном столе
    ///   takepile  — взять верхний лист из стопки
    ///   sell      — сдать то, что в руках, в зелёный бак
    ///   state     — руки, коврик, стопка, деньги
    /// </summary>
    public static class PaperTest
    {
        public static string Run(string args)
        {
            string command = (args ?? string.Empty).Trim().ToLowerInvariant();

            switch (command)
            {
                case "setup": return Setup();
                case "place": return Place();
                case "fold": return Fold();
                case "take": return Take();
                case "pile": return PutOnPile();
                case "give0": return Give(0);
                case "give1": return Give(1);
                case "give2": return Give(2);
                case "takepile": return TakeFromPile();
                case "sell": return Sell();
                case "state": return State();
                case "camera": return CameraInfo();
                case "table": return LookAtTable();
                case "throw": return Throw();
                case "throwsetup": return ThrowSetup();
                case "strong": PlayerStats.StrengthLevel = 5; return "сила = 5, дальность = " + PlayerStats.ThrowDistance.ToString("0.0") + " м";
                case "weak": PlayerStats.StrengthLevel = 1; return "сила = 1, дальность = " + PlayerStats.ThrowDistance.ToString("0.0") + " м";
                case "money": return "деньги = $" + PlayerStats.Money;
                default: return "args: setup | place | fold | take | pile | takepile | sell | state | camera | throw | throwsetup | strong | weak | money";
            }
        }

        private static string Setup()
        {
            if (!Application.isPlaying) return "Нужен Play Mode";

            Application.runInBackground = true;

            GameObject player = GameObject.Find("Player");
            if (player == null) return "Не найден Player";

            player.transform.position = new Vector3(-0.75f, 0.30f, 1.62f);
            player.transform.rotation = Quaternion.identity;

            var camera = player.GetComponentInChildren<Camera>();
            if (camera != null)
                camera.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);

            Physics.SyncTransforms();

            var interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            if (interactor != null && !interactor.HasPaper)
                interactor.TakePaper();

            return "игрок у стола, лист в руках";
        }

        private static string Place()
        {
            if (!Application.isPlaying) return "Нужен Play Mode";

            var interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            var mat = Object.FindAnyObjectByType<PaperMatInteractable>();
            if (interactor == null || mat == null) return "Нет игрока или коврика";

            if (!interactor.HasPaper) interactor.TakePaper();
            mat.Interact(interactor);
            return "на коврике: лист, сгибов " + mat.Folds;
        }

        private static string Fold()
        {
            if (!Application.isPlaying) return "Нужен Play Mode";

            var interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            var mat = Object.FindAnyObjectByType<PaperMatInteractable>();
            if (interactor == null || mat == null) return "Нет игрока или коврика";
            if (mat.PlacedCount == 0) return "На коврике нет листа";

            mat.Interact(interactor);
            return "сгиб запущен: folds=" + mat.Folds;
        }

        private static string Take()
        {
            if (!Application.isPlaying) return "Нужен Play Mode";

            var interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            var mat = Object.FindAnyObjectByType<PaperMatInteractable>();
            if (interactor == null || mat == null) return "Нет игрока или коврика";
            if (mat.PlacedCount == 0) return "На коврике нет листа";

            mat.Interact(interactor);
            return "в руках: " + (interactor.HasPaper ? "лист, сгибов " + interactor.CarriedFolds + ", цена $" + interactor.CarriedValue : "пусто") +
                   "; на коврике: " + mat.PlacedCount;
        }

        /// <summary>Выдать в руки лист с нужным числом сгибов (для проверки толщины).</summary>
        private static string Give(int folds)
        {
            if (!Application.isPlaying) return "Нужен Play Mode";

            var interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            if (interactor == null) return "Нет игрока";

            if (interactor.HasPaper) interactor.ConsumePaper();
            interactor.TakePaper(folds);
            return "в руках лист со сгибами " + folds;
        }

        private static string PutOnPile()
        {
            if (!Application.isPlaying) return "Нужен Play Mode";

            var interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            var pile = Object.FindAnyObjectByType<PaperPileInteractable>();
            if (interactor == null || pile == null) return "Нет игрока или стола-стопки";
            if (!interactor.HasPaper) return "Руки пусты — сначала возьмите лист";

            pile.Interact(interactor);
            return "в стопке " + pile.Count + " лист(ов)";
        }

        private static string TakeFromPile()
        {
            if (!Application.isPlaying) return "Нужен Play Mode";

            var interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            var pile = Object.FindAnyObjectByType<PaperPileInteractable>();
            if (interactor == null || pile == null) return "Нет игрока или стола-стопки";
            if (pile.Count == 0) return "Стопка пуста";

            pile.Interact(interactor);
            return "в руках сгибов " + interactor.CarriedFolds + ", в стопке осталось " + pile.Count;
        }

        private static string Sell()
        {
            if (!Application.isPlaying) return "Нужен Play Mode";

            var interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            var bin = Object.FindAnyObjectByType<TrashBinInteractable>();
            if (interactor == null || bin == null) return "Нет игрока или бака";
            if (!interactor.HasPaper) return "Руки пусты — бак принимает только из рук";

            int before = PlayerStats.Money;
            bin.Interact(interactor);
            return "продано за $" + (PlayerStats.Money - before) + ", всего $" + PlayerStats.Money +
                   ", в руках: " + (interactor.HasPaper ? "есть" : "пусто");
        }

        private static string State()
        {
            if (!Application.isPlaying) return "Нужен Play Mode";

            var interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            var mat = Object.FindAnyObjectByType<PaperMatInteractable>();
            var pile = Object.FindAnyObjectByType<PaperPileInteractable>();

            string hands = interactor != null && interactor.HasPaper
                ? "лист(сгибов " + interactor.CarriedFolds + ", $" + interactor.CarriedValue + ")"
                : "пусто";

            string pileTop = "";
            if (pile != null && pile.Count > 0 && pile.pileParent != null &&
                pile.pileParent.childCount > 0)
            {
                Transform top = pile.pileParent.GetChild(pile.pileParent.childCount - 1);
                pileTop = " (верх на y=" + top.localPosition.y.ToString("F3") + ")";
            }

            return "руки=" + hands +
                   " | коврик=" + (mat != null ? mat.PlacedCount + " (сгибов " + mat.Folds + ")" : "-") +
                   " | стопка=" + (pile != null
                       ? pile.Count + " (" + pile.TotalHeightMm.ToString("0.0") + " мм)"
                       : "-") + pileTop +
                   " | деньги=$" + PlayerStats.Money +
                   " | сила=" + PlayerStats.StrengthLevel;
        }

        /// <summary>Встать у стола-стопки и посмотреть на него.</summary>
        private static string LookAtTable()
        {
            if (!Application.isPlaying) return "Нужен Play Mode";

            Application.runInBackground = true;

            GameObject player = GameObject.Find("Player");
            if (player == null) return "Не найден Player";

            player.transform.position = new Vector3(1.05f, 0.30f, 0.5f);
            player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            var camera = player.GetComponentInChildren<Camera>();
            if (camera != null)
                camera.transform.localRotation = Quaternion.Euler(33f, 0f, 0f);

            Physics.SyncTransforms();
            return "игрок у стола-стопки";
        }

        private static string CameraInfo()
        {
            if (!Application.isPlaying) return "Нужен Play Mode";

            var camera = Camera.main;
            if (camera == null) return "Нет камеры";

            string hitText = "промах";
            if (Physics.Raycast(camera.transform.position, camera.transform.forward,
                    out RaycastHit hit, 6f))
                hitText = hit.collider.name + " @" + hit.point.ToString("F2");

            return "cam=" + camera.transform.position.ToString("F2") +
                   " forward=" + camera.transform.forward.ToString("F2") +
                   " rayHit=" + hitText;
        }

        /// <summary>Встать на газоне лицом в открытое поле — чтобы бросок ни во что не упирался.</summary>
        private static string ThrowSetup()
        {
            if (!Application.isPlaying) return "Нужен Play Mode";

            Application.runInBackground = true;

            GameObject player = GameObject.Find("Player");
            if (player == null) return "Не найден Player";

            player.transform.position = new Vector3(0f, 0.30f, -9f);
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var camera = player.GetComponentInChildren<Camera>();
            if (camera != null)
                camera.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);

            Physics.SyncTransforms();

            var interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            if (interactor != null && !interactor.HasPaper)
                interactor.TakePaper();

            return "игрок на газоне, смотрит в открытое поле";
        }

        /// <summary>Бросок самолётика и замер: куда целились и сколько пролетел.</summary>
        private static string Throw()
        {
            if (!Application.isPlaying) return "Нужен Play Mode";

            var interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            var thrower = Object.FindAnyObjectByType<PaperThrower>();
            if (interactor == null || thrower == null) return "Нет игрока или метателя";

            if (!interactor.HasPaper) interactor.TakePaper();

            Vector3 from = interactor.view != null ? interactor.view.position : Vector3.zero;

            thrower.Throw();

            var plane = GameObject.Find("Paper Airplane");
            if (plane == null) return "Самолётик не создался";

            var projectile = plane.GetComponent<PaperProjectile>();
            if (projectile == null) return "Нет PaperProjectile";

            float travelled = Vector3.Distance(
                new Vector3(from.x, 0f, from.z),
                new Vector3(projectile.end.x, 0f, projectile.end.z));

            return "бросок: дальность=" + travelled.ToString("0.00") + " м" +
                   " сила=" + PlayerStats.StrengthLevel +
                   " дуга=" + projectile.arcHeight.ToString("0.00") +
                   " время=" + projectile.duration.ToString("0.00") + " с";
        }
    }
}
