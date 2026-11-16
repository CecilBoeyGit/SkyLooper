using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MiniPlanes
{
    /// <summary>
    /// The Level Creator's floating panel: a curved, lazily following canvas below the drawing space, used
    /// by poking. Its left section is a mini tutorial (right pinch adds a path point, left pinch on a point
    /// deletes it); its right section lists the level features (enemies, powerups, floating islands), each
    /// with how many the level will have, and under them the level's rules: fuel on or off, and whether the
    /// fly path shows all the way or fades after take-off. Complete Level saves; Cancel leaves without saving.
    /// Its look is in the LevelCreatorPanel prefab; buttons are found by their ids ("feat_minus0",
    /// "feat_plus0", ..., "toggle_fuel", "toggle_path", "complete", "cancel"), so keep them when you restyle.
    /// </summary>
    public class LevelCreatorPanel : MonoBehaviour
    {
        [Serializable]
        public class FeatureRow
        {
            public CustomFeature feature;
            public Image icon;
            public Text name;
            public Text count;
            public PokeButton minus;
            public PokeButton plus;
        }

        public LazyFollow follow;
        public PokeCanvas poke;

        [Header("Parts (set by the UI builder)")]
        public Text title;
        [Tooltip("Points placed and how long the flight will take.")]
        public Text pathInfo;
        [Tooltip("What to do next, under the Complete button.")]
        public Text hint;
        public FeatureRow[] featureRows = new FeatureRow[0];
        [Tooltip("Switches fuel on and off. Its label reads ON or OFF.")]
        public PokeButton fuelToggle;
        public Text fuelState;
        public Text fuelDetail;
        [Tooltip("Switches the fly path between always on and fading after take-off. Its label reads ON or OFF.")]
        public PokeButton pathToggle;
        public Text pathState;
        public Text pathDetail;
        public PokeButton completeButton;
        public Text completeLabel;
        public PokeButton cancelButton;

        [Header("Behaviour")]
        [Tooltip("Frames after opening before a press counts.")]
        public int inputDelayFrames = 24;
        [Tooltip("Pinches this close to the panel, in front or behind, are left alone so reaching for a button can't drop a point, in metres.")]
        public float pinchBlockDepth = 0.07f;
        [Tooltip("The same, beyond the panel's edges, in canvas units.")]
        public float pinchBlockMargin = 40f;

        [Header("Colours")]
        public Color countZero = new Color(0.56f, 0.52f, 0.47f, 1f);
        public Color countSet = new Color(0.25f, 0.2f, 0.17f, 1f);
        public Color completeUnavailableText = new Color(0.47f, 0.47f, 0.47f, 1f);
        public Color toggleOn = new Color(0.24f, 0.58f, 0.54f, 1f);
        public Color toggleOnPressed = new Color(0.16f, 0.44f, 0.41f, 1f);
        public Color toggleOff = new Color(0.36f, 0.42f, 0.42f, 1f);
        public Color toggleOffPressed = new Color(0.24f, 0.3f, 0.3f, 1f);

        public event Action CompleteChosen;
        public event Action CancelChosen;
        /// <summary>A feature count went up or down.</summary>
        public event Action CountsChanged;
        /// <summary>Fuel or the fly path was switched.</summary>
        public event Action RulesChanged;

        public bool IsOpen => poke != null && poke.gameObject.activeSelf;
        public IReadOnlyList<int> Counts => _counts;
        /// <summary>The level's plane burns fuel.</summary>
        public bool Fuel { get; private set; }
        /// <summary>The level's fly path shows for the whole flight rather than fading after take-off.</summary>
        public bool PathAlwaysOn { get; private set; } = true;

        readonly int[] _counts = new int[CustomFeatures.Count];

        void Awake()
        {
            if (poke != null) poke.Clicked += OnClicked;
            Close();
        }

        public void SetHead(Transform head)
        {
            if (follow != null) follow.head = head;
        }

        public void SetHands(HandSource[] hands)
        {
            if (poke != null) poke.hands = hands;
        }

        /// <summary>Shows the panel in front of the player with a heading, the level's feature counts and its rules.</summary>
        public void Open(string heading, IReadOnlyList<int> counts, bool fuel = false, bool pathAlwaysOn = true)
        {
            Fuel = fuel;
            PathAlwaysOn = pathAlwaysOn;
            if (poke == null) return;
            poke.gameObject.SetActive(true);
            if (follow != null)
            {
                follow.enabled = true;
                follow.Snap();
            }
            if (title != null) title.text = heading;
            for (int i = 0; i < _counts.Length; i++)
                _counts[i] = counts != null && i < counts.Count ? Mathf.Clamp(counts[i], 0, CustomFeatures.MaxCount((CustomFeature)i)) : 0;
            RefreshRows();
            RefreshRules();
            var curved = poke.GetComponent<CurvedCanvas>();
            if (curved != null) curved.MarkDirty();
            poke.BlockInput(inputDelayFrames);
        }

        public void Close()
        {
            if (poke != null) poke.gameObject.SetActive(false);
        }

        public int GetCount(CustomFeature feature) => _counts[(int)feature];

        /// <summary>Shows the path's point count and flight time, and opens Complete Level once the path can be flown.</summary>
        public void SetPathInfo(int points, float flightSeconds)
        {
            bool ready = points >= CustomLevelData.MinPoints;
            if (pathInfo != null)
                pathInfo.text = points == 0 ? "NO POINTS YET"
                    : points + (points == 1 ? " POINT" : " POINTS") + (ready ? "   ·   " + Mathf.Max(1, Mathf.RoundToInt(flightSeconds)) + " S FLIGHT" : "");
            if (hint != null)
                hint.text = ready ? "Add features, then Complete Level to save."
                    : points == 1 ? "Pinch one more point to finish the path." : "Pinch the air with your right hand to start a path.";
            if (completeButton != null)
            {
                completeButton.Interactable = ready;
                if (completeLabel != null) completeLabel.color = ready ? Color.white : completeUnavailableText;
            }
        }

        /// <summary>Replaces the hint under the Complete button, for example when saving failed.</summary>
        public void ShowHint(string text)
        {
            if (hint != null) hint.text = text;
        }

        /// <summary>Whether a point in the room is on or right next to the open panel.</summary>
        public bool Covers(Vector3 world)
        {
            if (!IsOpen) return false;
            var curved = poke.GetComponent<CurvedCanvas>();
            if (curved == null) return false;
            Vector3 flat = curved.ToFlat(world);
            Rect r = curved.Rect.rect;
            r.xMin -= pinchBlockMargin;
            r.xMax += pinchBlockMargin;
            r.yMin -= pinchBlockMargin;
            r.yMax += pinchBlockMargin;
            return r.Contains(new Vector2(flat.x, flat.y)) && Mathf.Abs(flat.z) < pinchBlockDepth;
        }

        /// <summary>World position of a visible button's centre, for automated tests. Null if there is none.</summary>
        public Vector3? ButtonPosition(string id)
        {
            if (poke == null) return null;
            foreach (var b in poke.GetComponentsInChildren<PokeButton>(false))
                if (b.id == id) return poke.WorldCentre(b);
            return null;
        }

        public bool IsButtonInteractable(string id)
        {
            if (poke == null) return false;
            foreach (var b in poke.GetComponentsInChildren<PokeButton>(true))
                if (b.id == id) return b.Interactable;
            return false;
        }

        void LateUpdate()
        {
            if (follow != null && poke != null) follow.Hold = poke.IsPressing;
        }

        // ---------------------------------------------------------------- rows

        void RefreshRows()
        {
            foreach (var row in featureRows)
            {
                if (row == null) continue;
                int n = _counts[(int)row.feature];
                int max = CustomFeatures.MaxCount(row.feature);
                if (row.count != null)
                {
                    row.count.text = n.ToString();
                    row.count.color = n > 0 ? countSet : countZero;
                }
                if (row.minus != null) row.minus.Interactable = n > 0;
                if (row.plus != null) row.plus.Interactable = n < max;
            }
        }

        void RefreshRules()
        {
            PaintToggle(fuelToggle, fuelState, Fuel);
            if (fuelDetail != null) fuelDetail.text = Fuel ? "Burns fuel. Rings refill it." : "No fuel to manage.";
            PaintToggle(pathToggle, pathState, PathAlwaysOn);
            if (pathDetail != null) pathDetail.text = PathAlwaysOn ? "Shows all flight long." : "Fades. 3 rings in a row bring it back.";
        }

        void PaintToggle(PokeButton button, Text state, bool on)
        {
            if (state != null) state.text = on ? "ON" : "OFF";
            if (button == null) return;
            button.pressedColor = on ? toggleOnPressed : toggleOffPressed;
            button.SetNormalColor(on ? toggleOn : toggleOff);
        }

        /// <summary>Switches fuel on or off.</summary>
        public void SetFuel(bool on)
        {
            if (Fuel == on) return;
            Fuel = on;
            RefreshRules();
            RulesChanged?.Invoke();
        }

        /// <summary>Switches the fly path between always on and fading after take-off.</summary>
        public void SetPathAlwaysOn(bool on)
        {
            if (PathAlwaysOn == on) return;
            PathAlwaysOn = on;
            RefreshRules();
            RulesChanged?.Invoke();
        }

        void Change(int rowIndex, int delta)
        {
            if (rowIndex < 0 || rowIndex >= featureRows.Length || featureRows[rowIndex] == null) return;
            var f = featureRows[rowIndex].feature;
            int i = (int)f;
            int next = Mathf.Clamp(_counts[i] + delta, 0, CustomFeatures.MaxCount(f));
            if (next == _counts[i]) return;
            _counts[i] = next;
            RefreshRows();
            CountsChanged?.Invoke();
        }

        // ---------------------------------------------------------------- clicks

        /// <summary>Acts on a button by id, as if it had been poked. Used by the poke input and by tests.</summary>
        public void Press(string id)
        {
            switch (id)
            {
                case "complete":
                    if (completeButton == null || completeButton.Interactable) CompleteChosen?.Invoke();
                    return;
                case "cancel": CancelChosen?.Invoke(); return;
                case "toggle_fuel": SetFuel(!Fuel); return;
                case "toggle_path": SetPathAlwaysOn(!PathAlwaysOn); return;
            }
            if (TryIndex(id, "feat_minus", out int minus)) Change(minus, -1);
            else if (TryIndex(id, "feat_plus", out int plus)) Change(plus, 1);
        }

        void OnClicked(PokeButton button)
        {
            Press(button.id);
        }

        static bool TryIndex(string id, string prefix, out int index)
        {
            index = 0;
            return id != null && id.StartsWith(prefix) && int.TryParse(id.Substring(prefix.Length), out index);
        }
    }
}
