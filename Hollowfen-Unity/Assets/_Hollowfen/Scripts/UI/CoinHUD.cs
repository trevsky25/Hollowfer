using Hollowfen.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Small parchment pill in the bottom-left HUD corner showing Wren's purse ("3s 2c").
    // Hidden until she's earned her first coin — coins don't exist in Hollowfen's story
    // until Marra's kitchen sells a bowl. Builds itself programmatically like the other HUD bits.
    public class CoinHUD : MonoBehaviour
    {
        [SerializeField] private Sprite _parchmentSprite;

        private GameObject _pill;
        private TMP_Text _label;
        private bool _built;

        private void OnEnable()
        {
            CoinPurse.OnChanged += HandleChanged;
        }

        private void OnDisable()
        {
            CoinPurse.OnChanged -= HandleChanged;
        }

        private void Start()
        {
            Refresh(CoinPurse.TotalCopper);
        }

        private void HandleChanged(int totalCopper) => Refresh(totalCopper);

        private void Refresh(int totalCopper)
        {
            if (totalCopper <= 0)
            {
                if (_pill != null) _pill.SetActive(false);
                return;
            }
            BuildIfNeeded();
            _pill.SetActive(true);
            _label.text = CoinPurse.Format(totalCopper);
        }

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            var canvasRT = (RectTransform)transform;
            _pill = new GameObject("CoinPill", typeof(RectTransform));
            _pill.transform.SetParent(canvasRT, false);
            var rt = (RectTransform)_pill.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(150f, 44f);
            rt.anchoredPosition = new Vector2(24f, 24f);

            var bg = _pill.AddComponent<Image>();
            bg.sprite = UICanvasUtil.RoundedRect(22);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.07f, 0.06f, 0.045f, 0.72f);
            bg.raycastTarget = false;

            var rim = UICanvasUtil.NewImage("Rim", rt, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.32f), false);
            var rimImg = rim.GetComponent<Image>();
            rimImg.sprite = UICanvasUtil.RoundedOutline(22, 1.6f);
            rimImg.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)rim.transform);

            var glyph = UICanvasUtil.NewHeading("Glyph", rt, "<sprite name=\"coin\">", 20f, HollowfenPalette.Gold, FontStyles.Normal, TextAlignmentOptions.Center); // batch-48: ◉ had no font glyph
            var gRT = glyph.rectTransform;
            gRT.anchorMin = new Vector2(0f, 0f); gRT.anchorMax = new Vector2(0f, 1f);
            gRT.pivot = new Vector2(0f, 0.5f);
            gRT.sizeDelta = new Vector2(36f, 0f);
            gRT.anchoredPosition = new Vector2(8f, 0f);

            _label = UICanvasUtil.NewHeading("Amount", rt, "", 24f, HollowfenPalette.Cream, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            var lRT = _label.rectTransform;
            lRT.anchorMin = new Vector2(0f, 0f); lRT.anchorMax = new Vector2(1f, 1f);
            lRT.pivot = new Vector2(0.5f, 0.5f);
            lRT.offsetMin = new Vector2(48f, 0f);
            lRT.offsetMax = new Vector2(-10f, 0f);
        }
    }
}
