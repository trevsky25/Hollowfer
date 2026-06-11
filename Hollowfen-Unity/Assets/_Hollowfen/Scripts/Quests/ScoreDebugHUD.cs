using System.Text;
using Hollowfen.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowfen.Quests
{
    // Dev overlay: F3 toggles a live readout of the ending meters — Village Hope, Knowledge,
    // relationships, and set flags. New Input System polling; hidden by default; never ships
    // visible (toggle only, no scene reference needed beyond the host object).
    public class ScoreDebugHUD : MonoBehaviour
    {
        private Canvas _canvas;
        private TMP_Text _text;
        private bool _visible;
        private bool _built;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f3Key.wasPressedThisFrame)
            {
                _visible = !_visible;
                BuildIfNeeded();
                _canvas.gameObject.SetActive(_visible);
                if (_visible) Refresh();
            }
            if (_visible && Time.frameCount % 30 == 0) Refresh();
        }

        private void Refresh()
        {
            var sb = new StringBuilder();
            sb.Append("<b>HOPE</b> ").Append(GameScores.VillageHope)
              .Append("    <b>KNOWLEDGE</b> ").Append(GameScores.Knowledge).AppendLine();
            foreach (var kv in GameScores.Relationships)
                sb.Append(kv.Key).Append(' ').Append(kv.Value >= 0 ? "+" : "").Append(kv.Value).Append("   ");
            sb.AppendLine();
            sb.Append("<color=#9a7b2f>");
            foreach (var f in GameScores.Flags) sb.Append(f).Append("  ");
            sb.Append("</color>");
            _text.text = sb.ToString();
        }

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            var canvasGo = new GameObject("ScoreDebugCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 95;
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>().Init1080();

            var pill = UICanvasUtil.NewImage("Bg", canvasGo.transform, new Color(0f, 0f, 0f, 0.72f), false);
            var img = pill.GetComponent<UnityEngine.UI.Image>();
            img.sprite = UICanvasUtil.RoundedRect(10);
            img.type = UnityEngine.UI.Image.Type.Sliced;
            var rt = (RectTransform)pill.transform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(720f, 110f);
            rt.anchoredPosition = new Vector2(16f, 84f);

            _text = UICanvasUtil.NewBody("Text", rt, "", 14f, new Color(0.93f, 0.90f, 0.82f, 1f),
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _text.richText = true;
            UICanvasUtil.Stretch(_text.rectTransform);
            _text.rectTransform.offsetMin = new Vector2(12f, 8f);
            _text.rectTransform.offsetMax = new Vector2(-12f, -8f);

            _canvas.gameObject.SetActive(false);
        }
    }
}
