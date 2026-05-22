using Hollowfen.UI;
using TMPro;
using UnityEngine;

namespace Hollowfen.Quests
{
    // Small persistent HUD widget on the gameplay canvas. Top-left in the safe zone (under the
    // LocationDebugHUD when present). Shows the current quest's name + objective text. Hides itself
    // when no quest is active. Subscribes to QuestManager events; no per-frame polling.
    public class QuestHUD : MonoBehaviour
    {
        [SerializeField] private float _panelWidth = 360f;
        [SerializeField] private float _panelHeight = 80f;
        [SerializeField] private Vector2 _anchoredPosition = new Vector2(20f, -150f); // top-left offset

        private CanvasGroup _cg;
        private TMP_Text _eyebrow;
        private TMP_Text _name;
        private TMP_Text _objective;
        private bool _built;

        private void OnEnable()
        {
            BuildIfNeeded();
            QuestManager.QuestStarted += HandleQuestStarted;
            QuestManager.QuestCompleted += HandleQuestCompleted;
            Refresh(QuestManager.ActiveQuest);
        }

        private void OnDisable()
        {
            QuestManager.QuestStarted -= HandleQuestStarted;
            QuestManager.QuestCompleted -= HandleQuestCompleted;
        }

        private void HandleQuestStarted(QuestData q) => Refresh(q);
        private void HandleQuestCompleted(QuestData q) => Refresh(QuestManager.ActiveQuest);

        private void Refresh(QuestData q)
        {
            BuildIfNeeded();
            if (q == null) { _cg.alpha = 0f; return; }
            _cg.alpha = 1f;
            _eyebrow.text = "QUEST  ·  ACT " + RomanAct(q.Act);
            _name.text = Hollowfen.Localization.Get(q.DisplayNameId);
            _objective.text = Hollowfen.Localization.Get(q.ObjectiveTextId);
        }

        private static string RomanAct(int a)
        {
            switch (a) { case 1: return "I"; case 2: return "II"; case 3: return "III"; case 4: return "IV"; default: return a.ToString(); }
        }

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(_panelWidth, _panelHeight);
            rt.anchoredPosition = _anchoredPosition;

            _cg = gameObject.AddComponent<CanvasGroup>();
            _cg.alpha = 0f;
            _cg.interactable = false;
            _cg.blocksRaycasts = false;

            // Subtle dark pill background
            var bg = UICanvasUtil.NewImage("BG", rt, new Color(0.05f, 0.04f, 0.02f, 0.55f), true);
            UICanvasUtil.Stretch((RectTransform)bg.transform);

            // Gold left bar accent
            var bar = UICanvasUtil.NewImage("Accent", rt, HollowfenPalette.Gold, false);
            var barRT = (RectTransform)bar.transform;
            barRT.anchorMin = new Vector2(0f, 0f); barRT.anchorMax = new Vector2(0f, 1f);
            barRT.pivot = new Vector2(0f, 0.5f);
            barRT.sizeDelta = new Vector2(3f, 0f);
            barRT.anchoredPosition = Vector2.zero;

            _eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", rt, "QUEST  ·  ACT I", 10f, HollowfenPalette.Gold, TextAlignmentOptions.TopLeft);
            _eyebrow.fontStyle = FontStyles.Bold;
            var eRT = _eyebrow.rectTransform;
            eRT.anchorMin = new Vector2(0f, 1f); eRT.anchorMax = new Vector2(1f, 1f);
            eRT.pivot = new Vector2(0f, 1f);
            eRT.sizeDelta = new Vector2(-30f, 14f);
            eRT.anchoredPosition = new Vector2(14f, -8f);

            _name = UICanvasUtil.NewHeading("Name", rt, "", 18f, HollowfenPalette.Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            var nRT = _name.rectTransform;
            nRT.anchorMin = new Vector2(0f, 1f); nRT.anchorMax = new Vector2(1f, 1f);
            nRT.pivot = new Vector2(0f, 1f);
            nRT.sizeDelta = new Vector2(-30f, 24f);
            nRT.anchoredPosition = new Vector2(14f, -22f);

            _objective = UICanvasUtil.NewBody("Objective", rt, "", 13f, HollowfenPalette.Parchment, FontStyles.Italic, TextAlignmentOptions.TopLeft);
            _objective.textWrappingMode = TextWrappingModes.Normal;
            var oRT = _objective.rectTransform;
            oRT.anchorMin = new Vector2(0f, 1f); oRT.anchorMax = new Vector2(1f, 1f);
            oRT.pivot = new Vector2(0f, 1f);
            oRT.sizeDelta = new Vector2(-30f, _panelHeight - 50f);
            oRT.anchoredPosition = new Vector2(14f, -48f);
        }
    }
}
