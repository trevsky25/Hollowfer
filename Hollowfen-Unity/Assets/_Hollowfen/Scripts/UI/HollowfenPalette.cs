using UnityEngine;

namespace Hollowfen.UI
{
    // Hollowfen menu palette. Centralised so screens stay in sync; tune here.
    public static class HollowfenPalette
    {
        // Backgrounds
        public static readonly Color InkDeep    = new Color(0.064f, 0.058f, 0.050f, 1f); // near-black walnut
        public static readonly Color InkSoft    = new Color(0.118f, 0.103f, 0.087f, 1f); // card / panel
        public static readonly Color Scrim      = new Color(0.030f, 0.027f, 0.024f, 0.55f);

        // Foreground / text
        public static readonly Color Cream      = new Color(0.965f, 0.929f, 0.847f, 1f); // headings
        public static readonly Color Parchment  = new Color(0.870f, 0.831f, 0.745f, 1f); // body
        public static readonly Color Moss       = new Color(0.624f, 0.620f, 0.541f, 1f); // muted hints
        public static readonly Color Sage       = new Color(0.486f, 0.580f, 0.443f, 1f); // edible / positive
        public static readonly Color Gold       = new Color(0.792f, 0.659f, 0.341f, 1f); // act head, eyebrow
        public static readonly Color GoldFaint  = new Color(0.792f, 0.659f, 0.341f, 0.45f);
        public static readonly Color GoldGlow   = new Color(0.965f, 0.812f, 0.475f, 1f); // focus highlight

        // Edibility (per handoff)
        public static readonly Color EdEdible    = new Color32(0x7E, 0xC3, 0x8A, 0xFF);
        public static readonly Color EdDeadly    = new Color32(0xD3, 0x6A, 0x5B, 0xFF);
        public static readonly Color EdMagic     = new Color32(0xA4, 0x7B, 0xD0, 0xFF);
        public static readonly Color EdMedicinal = new Color32(0x7D, 0xA7, 0xC8, 0xFF);
        public static readonly Color EdUnknown   = new Color32(0xBB, 0xB1, 0x90, 0xFF);

        public static Color Edibility(Hollowfen.Data.Edibility e)
        {
            switch (e)
            {
                case Hollowfen.Data.Edibility.Edible:        return EdEdible;
                case Hollowfen.Data.Edibility.Deadly:        return EdDeadly;
                case Hollowfen.Data.Edibility.Psychoactive:  return EdMagic;
                case Hollowfen.Data.Edibility.Medicinal:     return EdMedicinal;
                default:                                     return EdUnknown;
            }
        }
    }
}
