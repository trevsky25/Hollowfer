using System.Collections;
using Hollowfen.Audio;
using Hollowfen.Foraging;
using Hollowfen.Quests;
using Hollowfen.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.GameTime
{
    [DisallowMultipleComponent]
    public sealed class RestSpot : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _requiredQuestId = "findJournal";
        [SerializeField] private string _targetTextId = "rest.mill_hearth.name";
        [SerializeField] private float _dawnHour = 7f;
        [SerializeField] private float _duskHour = 19f;
        [SerializeField] private float _fadeSeconds = 0.45f;
        [SerializeField] private float _holdSeconds = 0.7f;

        private bool _resting;

        public string PromptVerb => "prompt.rest.verb";
        public string PromptTarget => Localization.Get(_targetTextId);

        public bool CanInteract(GameObject actor)
        {
            if (_resting || TimeManager.Instance == null) return false;
            return string.IsNullOrEmpty(_requiredQuestId) || QuestManager.IsCompleted(_requiredQuestId);
        }

        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            var destination = Destination(TimeManager.Instance);
            bool dusk = Mathf.Approximately(destination.hour, _duskHour);
            string title = Localization.Get(dusk ? "rest.confirm.dusk.title" : "rest.confirm.dawn.title");
            string body = Localization.Get(dusk ? "rest.confirm.dusk.body" : "rest.confirm.dawn.body");
            bool hasModal = ConfirmModal.Instance != null && UIManager.Instance != null;
            if (!hasModal || !ConfirmModal.Show(title, body,
                    () => StartCoroutine(RestRoutine(destination.day, destination.hour))))
                StartCoroutine(RestRoutine(destination.day, destination.hour));
        }

        private (int day, float hour) Destination(TimeManager time)
        {
            if (time.Hour < _dawnHour) return (time.Day, _dawnHour);
            if (time.Hour < _duskHour) return (time.Day, _duskHour);
            return (time.Day + 1, _dawnHour);
        }

        private IEnumerator RestRoutine(int targetDay, float targetHour)
        {
            if (_resting) yield break;
            _resting = true;
            float previousScale = Time.timeScale;
            Time.timeScale = 0f;
            PlayerInteractor.Suspended = true;
            PlayerInteractor.SetPlayerInputEnabled(false);

            var overlay = BuildOverlay(out var group, out var caption);
            caption.text = "";
            GameplaySfx.Rest();
            yield return Fade(group, 0f, 1f);

            var time = TimeManager.Instance;
            if (time != null) time.AdvanceTo(targetDay, targetHour, true);
            bool dusk = Mathf.Approximately(targetHour, _duskHour);
            caption.text = string.Format(Localization.Get(dusk ? "rest.transition.dusk" : "rest.transition.dawn"), targetDay);
            yield return new WaitForSecondsRealtime(_holdSeconds);
            yield return Fade(group, 1f, 0f);

            Destroy(overlay);
            Time.timeScale = previousScale;
            PlayerInteractor.Suspended = false;
            PlayerInteractor.SetPlayerInputEnabled(true);
            _resting = false;
        }

        private GameObject BuildOverlay(out CanvasGroup group, out TMP_Text caption)
        {
            var root = new GameObject("_RestTransition", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 980;
            root.GetComponent<CanvasScaler>().Init1080();
            group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = true;

            var black = UICanvasUtil.NewImage("Black", root.transform, Color.black, true);
            UICanvasUtil.Stretch((RectTransform)black.transform);
            caption = UICanvasUtil.NewHeading("Caption", root.transform, "", 34f,
                new Color(0.83f, 0.72f, 0.46f, 1f), FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(caption.rectTransform);
            return root;
        }

        private IEnumerator Fade(CanvasGroup group, float from, float to)
        {
            float duration = Mathf.Max(0.05f, _fadeSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }
            group.alpha = to;
        }
    }
}
