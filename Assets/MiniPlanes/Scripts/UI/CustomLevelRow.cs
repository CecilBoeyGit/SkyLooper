using UnityEngine;
using UnityEngine.UI;

namespace MiniPlanes
{
    /// <summary>
    /// One custom level in the Custom Paths list: poke the row to fly it, or Edit to open it in the Level
    /// Creator. The menu copies a hidden template row for each saved level and gives the copies their ids.
    /// </summary>
    public class CustomLevelRow : MonoBehaviour
    {
        public PokeButton play;
        public PokeButton edit;
        public Text levelName;
        [Tooltip("Points, flight time and features. {fighter} and the like draw as icons.")]
        public Text detail;
        public InlineIcons detailIcons;

        public RectTransform Rect => (RectTransform)transform;
    }
}
