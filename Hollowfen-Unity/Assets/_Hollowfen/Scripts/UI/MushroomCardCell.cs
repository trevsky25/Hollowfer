using System;
using Hollowfen.Data;
using UnityEngine;

namespace Hollowfen.UI
{
    public class MushroomCardCell : MonoBehaviour
    {
        public MushroomFieldGuideData Entry { get; private set; }
        public Action<MushroomFieldGuideData> OnClick;

        public void Bind(MushroomFieldGuideData entry, Action<MushroomFieldGuideData> onClick)
        {
            Entry = entry;
            OnClick = onClick;
        }

        public void HandleClick()
        {
            if (OnClick != null && Entry != null) OnClick.Invoke(Entry);
        }
    }
}
