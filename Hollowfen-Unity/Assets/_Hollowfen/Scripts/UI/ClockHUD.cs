using Hollowfen.GameTime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Small ink-glass pill under the mini-map: "Day 1  ·  Afternoon". Period names instead
    // of digits keep it diegetic; the full map screen can show precise time later if needed.
    public class ClockHUD : MonoBehaviour
    {
        private TMP_Text _label;
        private string _last;
        private bool _built;

        private void Update()
        {
            var tm = TimeManager.Instance;
            if (tm == null) return;
            string text = "Day " + tm.Day + "  ·  " + Period(tm.Hour);
            if (text == _last) return;
            BuildIfNeeded();
            _last = text;
            _label.text = text;
        }

        private static string Period(float h)
        {
            if (h < 5f) return "Night";
            if (h < 7f) return "Dawn";
            if (h < 11f) return "Morning";
            if (h < 14f) return "Midday";
            if (h < 17f) return "Afternoon";
            if (h < 19f) return "Evening";
            if (h < 21f) return "Dusk";
            return "Night";
        }

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            var canvasRT = (RectTransform)transform;
            var pill = UICanvasUtil.NewImage("ClockPill", canvasRT, new Color(0.07f, 0.06f, 0.045f, 0.66f), false);
            var img = pill.GetComponent<Image>();
            img.sprite = UICanvasUtil.RoundedRect(15);
            img.type = Image.Type.Sliced;
            var rt = (RectTransform)pill.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(220f, 38f);
            rt.anchoredPosition = new Vector2(-88f, -352f); // centred under the 320px mini-map

            var stroke = UICanvasUtil.NewImage("Hairline", rt, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.28f), false);
            var sImg = stroke.GetComponent<Image>();
            sImg.sprite = UICanvasUtil.RoundedOutline(15, 1.5f);
            sImg.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)stroke.transform);

            _label = UICanvasUtil.NewEyebrow("Label", rt, "", 15f, HollowfenPalette.Cream, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(_label.rectTransform);
        }
    }
}
