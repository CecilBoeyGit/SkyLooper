using UnityEngine;
using UnityEngine.UI;

namespace MiniPlanes
{
    /// <summary>
    /// Animates the Level Creator's mini tutorial on a loop. Right card: the hand pinches and a new point
    /// pops onto the end of a little path. Left card: the hand pinches a point, which turns red, gets
    /// struck out and shrinks away. Lives on the LevelCreatorPanel prefab; restyle the images freely.
    /// </summary>
    public class CreatorTutorialMotion : MonoBehaviour
    {
        [Header("Add a point")]
        public RectTransform addHand;
        public RectTransform addLink;
        public RectTransform addDot;
        [Header("Delete a point")]
        public RectTransform deleteHand;
        public Graphic deleteDot;
        public RectTransform deleteCross;

        [Tooltip("Seconds for one loop of both animations.")]
        public float period = 2.6f;
        public Color dotColor = new Color(0.98f, 0.93f, 0.8f, 1f);
        public Color doomedColor = new Color(0.9f, 0.16f, 0.14f, 1f);

        void Update()
        {
            float t = Mathf.Repeat(Time.unscaledTime / Mathf.Max(0.2f, period), 1f);

            // Add: pinch at 0.15-0.3, the link draws out, then the dot pops in and stays until the loop ends.
            float squeeze = Bump(t, 0.12f, 0.32f);
            Scale(addHand, 1f - 0.12f * squeeze);
            if (addLink != null)
            {
                addLink.localScale = new Vector3(Mathf.Max(MinScale, Ease(Mathf.InverseLerp(0.2f, 0.36f, t))), 1f, 1f);
                Dirty(addLink);
            }
            Scale(addDot, Pop(Mathf.InverseLerp(0.3f, 0.48f, t)) * (t > 0.92f ? 1f - Mathf.InverseLerp(0.92f, 1f, t) : 1f));

            // Delete: pinch at 0.3-0.5, the dot turns red and is struck out, then shrinks away; back at the end.
            Scale(deleteHand, 1f - 0.12f * Bump(t, 0.28f, 0.5f));
            bool doomed = t > 0.36f;
            float shrink = 1f - Ease(Mathf.InverseLerp(0.55f, 0.72f, t));
            float back = Pop(Mathf.InverseLerp(0.86f, 1f, t));
            if (deleteDot != null)
            {
                deleteDot.color = doomed && t < 0.86f ? doomedColor : dotColor;
                Scale(deleteDot.rectTransform, t < 0.86f ? (doomed ? 1.2f : 1f) * shrink : back);
            }
            Scale(deleteCross, t > 0.38f && t < 0.72f ? Pop(Mathf.InverseLerp(0.38f, 0.5f, t)) * shrink : 0f);
        }

        // Never exactly 0: bending a curved graphic inverts its transform.
        const float MinScale = 0.001f;

        static float Bump(float t, float from, float to)
        {
            float k = Mathf.InverseLerp(from, to, t);
            return k <= 0f || k >= 1f ? 0f : Mathf.Sin(k * Mathf.PI);
        }

        static float Ease(float k) => k * k * (3f - 2f * k);

        /// <summary>0 to 1 with a small overshoot.</summary>
        static float Pop(float k)
        {
            if (k <= 0f) return 0f;
            if (k >= 1f) return 1f;
            float u = k - 1f;
            return 1f + 2.2f * u * u * u + 1.2f * u * u;
        }

        static void Scale(RectTransform r, float s)
        {
            if (r == null) return;
            r.localScale = Vector3.one * Mathf.Max(MinScale, s);
            Dirty(r);
        }

        /// <summary>A moved or scaled part of a curved canvas has to be bent again.</summary>
        static void Dirty(RectTransform r)
        {
            CurvedCanvas.MarkDirty(r);
        }
    }
}
