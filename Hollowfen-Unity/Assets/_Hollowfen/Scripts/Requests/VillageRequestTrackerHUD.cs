using Hollowfen.Foraging;
using Hollowfen.Apothecary;
using Hollowfen.GameTime;
using Hollowfen.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.Requests
{
    /// <summary>Compact objective companion beneath the main story quest HUD.</summary>
    public sealed class VillageRequestTrackerHUD : MonoBehaviour
    {
        public static VillageRequestTrackerHUD Instance { get; private set; }

        private CanvasGroup _group;
        private TMP_Text _eyebrow;
        private TMP_Text _title;
        private TMP_Text _progress;

        public static VillageRequestTrackerHUD Ensure()
        {
            if (Instance != null) return Instance;
            var parent = GameObject.Find("_HUDCanvas")?.transform;
            var go = new GameObject("VillageRequestTracker", typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            else
            {
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 11;
                go.AddComponent<CanvasScaler>().Init1080();
            }
            return go.AddComponent<VillageRequestTrackerHUD>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Build();
        }

        private void OnEnable()
        {
            VillageRequests.OnChanged += Refresh;
            InventoryRuntime.OnChanged += HandleInventory;
            ApothecaryRuntime.OnChanged += Refresh;
            TimeManager.OnDayChanged += HandleDay;
            Refresh();
        }

        private void OnDisable()
        {
            VillageRequests.OnChanged -= Refresh;
            InventoryRuntime.OnChanged -= HandleInventory;
            ApothecaryRuntime.OnChanged -= Refresh;
            TimeManager.OnDayChanged -= HandleDay;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void HandleInventory(string id, int count) => Refresh();
        private void HandleDay(int day) => Refresh();

        private void Refresh()
        {
            var request = VillageRequests.TrackedRequest;
            if (request == null)
            {
                _group.alpha = 0f;
                return;
            }
            _group.alpha = 1f;
            int day = TimeManager.Instance != null ? TimeManager.Instance.Day : 1;
            _eyebrow.text = request.OneShot
                ? Localization.Get("request.tracker.story")
                : string.Format(Localization.Get("request.tracker.daily"), day);
            _title.text = Localization.Get(request.TitleId);
            _progress.text = VillageRequests.RequirementProgress(request);
            _progress.color = VillageRequests.CanDeliver(request)
                ? new Color(0.70f, 0.83f, 0.56f, 1f)
                : HollowfenPalette.Parchment;
        }

        private void Build()
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(420f, 94f);
            rt.anchoredPosition = new Vector2(24f, -116f);
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            var bg = gameObject.AddComponent<Image>();
            bg.sprite = UICanvasUtil.RoundedRect(14);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.07f, 0.06f, 0.045f, 0.66f);
            bg.raycastTarget = false;
            var stroke = UICanvasUtil.NewImage("Hairline", rt,
                new Color(HollowfenPalette.Moss.r, HollowfenPalette.Moss.g, HollowfenPalette.Moss.b, 0.40f), false);
            stroke.GetComponent<Image>().sprite = UICanvasUtil.RoundedOutline(14, 1.6f);
            stroke.GetComponent<Image>().type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)stroke.transform);
            var bar = UICanvasUtil.NewImage("Accent", rt, HollowfenPalette.Moss, false);
            bar.GetComponent<Image>().sprite = UICanvasUtil.RoundedRect(2);
            UICanvasUtil.SetRect((RectTransform)bar.transform, new Vector2(0f, 0.1f), new Vector2(0f, 0.9f),
                new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(6f, 0f));

            _eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", rt, "", 12.5f,
                new Color(0.70f, 0.83f, 0.56f, 1f), TextAlignmentOptions.TopLeft);
            SetTextRect(_eyebrow.rectTransform, -8f, 15f);
            _title = UICanvasUtil.NewHeading("Title", rt, "", 17f,
                HollowfenPalette.Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            SetTextRect(_title.rectTransform, -26f, 24f);
            _progress = UICanvasUtil.NewBody("Progress", rt, "", 12.5f,
                HollowfenPalette.Parchment, FontStyles.Italic, TextAlignmentOptions.TopLeft);
            SetTextRect(_progress.rectTransform, -54f, 26f);
        }

        private static void SetTextRect(RectTransform rt, float y, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(-30f, height);
            rt.anchoredPosition = new Vector2(14f, y);
        }
    }
}
