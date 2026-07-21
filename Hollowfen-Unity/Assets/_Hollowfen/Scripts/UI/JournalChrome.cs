using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    public static class JournalChrome
    {
        public static Button BuildCloseButton(Transform parent, Action onClick)
        {
            var rt = UICanvasUtil.NewRect("CloseButton", parent);
            UICanvasUtil.SetRect(rt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(58f, 58f), new Vector2(-26f, -26f));
            var image = rt.gameObject.AddComponent<Image>();
            image.color = new Color(HollowfenPalette.InkDeep.r, HollowfenPalette.InkDeep.g, HollowfenPalette.InkDeep.b, 0.18f);
            UICanvasUtil.Roundify(image, 20);
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            var label = UICanvasUtil.NewBody("Glyph", rt, "<sprite name=\"ui_x\">", 26f,
                new Color(HollowfenPalette.Cream.r, HollowfenPalette.Cream.g, HollowfenPalette.Cream.b, 0.86f),
                FontStyles.Normal, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(label.rectTransform);
            var focus = rt.gameObject.AddComponent<FocusHighlight>();
            focus.Configure(image, rt,
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.10f),
                1.04f, true, true);
            return button;
        }

        // Shared controller/mouse focus treatment for menu cards and rows. Resting
        // surfaces stay quiet; selection is communicated by a slim rail plus a
        // restrained wash instead of another persistent outline.
        public static FocusHighlight AddSurfaceFocus(GameObject root, int radius = 14, float focusedScale = 1.012f)
        {
            if (root == null) return null;
            var washGo = UICanvasUtil.NewImage("FocusWash", root.transform,
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0f), false);
            var wash = washGo.GetComponent<Image>();
            UICanvasUtil.Roundify(wash, radius);
            UICanvasUtil.Stretch((RectTransform)washGo.transform);

            var railGo = UICanvasUtil.NewImage("FocusRail", root.transform, HollowfenPalette.FocusRail, false);
            var rail = railGo.GetComponent<Image>();
            UICanvasUtil.Roundify(rail, 2);
            UICanvasUtil.SetRect(railGo.GetComponent<RectTransform>(),
                new Vector2(0f, 0.12f), new Vector2(0f, 0.88f), new Vector2(0f, 0.5f),
                new Vector2(3f, 0f), new Vector2(0f, 0f));

            var focus = root.AddComponent<FocusHighlight>();
            focus.Configure(wash, root.transform as RectTransform, HollowfenPalette.FocusWash,
                focusedScale, true, focusedScale > 1.0001f, false, rail);
            return focus;
        }

        public static Image AddStructuralBorder(RectTransform target, int radius, float alpha = 0.10f)
        {
            if (target == null) return null;
            var border = UICanvasUtil.NewImage("StructuralLine", target,
                new Color(HollowfenPalette.Cream.r, HollowfenPalette.Cream.g, HollowfenPalette.Cream.b, alpha), false);
            var image = border.GetComponent<Image>();
            UICanvasUtil.RoundifyOutline(image, radius, 1.25f);
            UICanvasUtil.Stretch(border.GetComponent<RectTransform>());
            return image;
        }

        public static Image AddSpecimenHalo(RectTransform parent, Vector2 size, Vector2 anchoredPosition)
        {
            var haloGo = UICanvasUtil.NewImage("SpecimenHalo", parent,
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.12f), false);
            var halo = haloGo.GetComponent<Image>();
            halo.sprite = UICanvasUtil.RadialGlow();
            UICanvasUtil.SetRect(haloGo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), size, anchoredPosition);
            return halo;
        }

        public static RectTransform BuildIndexHeader(
            Transform parent,
            string title,
            string summary,
            out TMP_Text summaryText)
        {
            var header = UICanvasUtil.NewRect("Header", parent);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 170f);

            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", header, Localization.Get("journal.eyebrow"), 18f, HollowfenPalette.Gold);
            UICanvasUtil.SetRect(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 24f), Vector2.zero);

            var heading = UICanvasUtil.NewHeading("Title", header, title, 76f, HollowfenPalette.Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(heading.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 86f), new Vector2(0f, -28f));

            summaryText = UICanvasUtil.NewBody("Summary", header, summary, 20f, new Color(HollowfenPalette.Cream.r, HollowfenPalette.Cream.g, HollowfenPalette.Cream.b, 0.72f), FontStyles.Italic);
            UICanvasUtil.SetRect(summaryText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 28f), new Vector2(0f, -116f));

            var rule = UICanvasUtil.NewImage("Rule", header, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.22f), false);
            UICanvasUtil.SetRect(rule.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(0f, -160f));
            return header;
        }

        public static TMP_Text BuildBottomHint(Transform parent, string localizationId)
        {
            var hint = UICanvasUtil.NewBody("JournalHint", parent, Localization.Get(localizationId), 17.5f,
                new Color(HollowfenPalette.Cream.r, HollowfenPalette.Cream.g, HollowfenPalette.Cream.b, 0.72f),
                FontStyles.Normal, TextAlignmentOptions.Center);
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            UICanvasUtil.SetRect(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(0f, 15f));
            return hint;
        }

        public static void FitText(TMP_Text text, float minHeight)
        {
            if (text == null) return;
            var layout = text.gameObject.GetComponent<LayoutElement>();
            if (layout == null) layout = text.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = minHeight;
            layout.flexibleWidth = 1f;
            var fitter = text.gameObject.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = text.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public static void SetNavigation(
            Selectable selectable,
            Selectable up,
            Selectable down,
            Selectable left = null,
            Selectable right = null)
        {
            if (selectable == null) return;
            var nav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = up,
                selectOnDown = down,
                selectOnLeft = left,
                selectOnRight = right
            };
            selectable.navigation = nav;
        }
    }
}
