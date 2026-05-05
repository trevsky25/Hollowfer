using System;
using Hollowfen.Data;
using UnityEngine;

namespace Hollowfen.UI
{
    public class StoryCardCell : MonoBehaviour
    {
        public StoryCardData Card { get; private set; }
        public Action<StoryCardData> OnClick;

        public void Bind(StoryCardData card, Action<StoryCardData> onClick)
        {
            Card = card;
            OnClick = onClick;
        }

        public void HandleClick()
        {
            if (OnClick != null && Card != null) OnClick.Invoke(Card);
        }
    }
}
