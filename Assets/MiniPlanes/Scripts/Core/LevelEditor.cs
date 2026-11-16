using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniPlanes
{
    /// <summary>
    /// The Level Creator's world side. A right-hand pinch drops a path point where the thumb and index meet;
    /// a left-hand pinch on a point deletes it. Points join up in the order they were added, into the same
    /// smooth spline the plane will fly, drawn as the path line. The first point is the start (teal), the
    /// last the finish (coral). While the right hand is about to pinch, a small ghost shows where its point
    /// will go; a point within reach of a left pinch turns red.
    /// </summary>
    public class LevelEditor : MonoBehaviour
    {
        public const int MaxPoints = 64;

        [Header("Pinching (metres)")]
        [Tooltip("A left pinch deletes the nearest point within this distance.")]
        public float deleteReach = 0.035f;
        [Tooltip("A new point closer than this to the last one is ignored, so a shaky pinch can't stack points.")]
        public float minSpacing = 0.02f;
        [Tooltip("The ghost point shows once thumb and index are this close.")]
        public float ghostPinchGap = 0.045f;

        [Header("Look")]
        public float markerSize = 0.016f;
        public float lineWidth = 0.003f;
        [Range(0f, 1f)] public float lineAlpha = 0.9f;
        public Color startColor = new Color(0.24f, 0.62f, 0.56f, 1f);
        public Color endColor = new Color(0.9f, 0.42f, 0.32f, 1f);
        public Color pointColor = new Color(0.98f, 0.93f, 0.8f, 1f);
        public Color deleteColor = new Color(0.9f, 0.16f, 0.14f, 1f);
        public Color ghostColor = new Color(0.56f, 0.82f, 0.68f, 1f);

        public bool IsEditing { get; private set; }
        public int PointCount => _points.Count;
        /// <summary>The points in the room, in path order.</summary>
        public IReadOnlyList<Vector3> Points => _points;
        public float PathLength { get; private set; }
        /// <summary>The point a left pinch would delete right now, or -1.</summary>
        public int HoveredPoint => _hover;

        /// <summary>A point was added or deleted.</summary>
        public event Action Changed;
        /// <summary>Pinches where this returns true are ignored, for example right next to the creator panel.</summary>
        public Func<Vector3, bool> IsBlocked;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int VertexColorId = Shader.PropertyToID("_UseVertexColor");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        GameArt _art;
        HandSource[] _hands;
        Vector3 _head, _forward = Vector3.forward;
        readonly List<Vector3> _points = new List<Vector3>();
        readonly List<Renderer> _markers = new List<Renderer>();
        Renderer _ghost;
        LineRenderer _line;
        MaterialPropertyBlock _block;
        Material _markerMaterial;
        bool _rightWasPinching, _leftWasPinching;
        int _hover = -1;

        public void Init(GameArt art, HandSource[] hands)
        {
            _art = art;
            _hands = hands;
            _block = new MaterialPropertyBlock();

            var lineGo = new GameObject("PathLine");
            lineGo.transform.SetParent(transform, false);
            _line = lineGo.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.textureMode = LineTextureMode.Stretch;
            _line.widthMultiplier = lineWidth;
            _line.numCapVertices = 2;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            if (art != null) _line.sharedMaterial = art.pathLine;
            _line.enabled = false;

            if (art != null && art.planeMatte != null)
            {
                _markerMaterial = new Material(art.planeMatte) { name = "PathPointMatte" };
                _markerMaterial.SetFloat(VertexColorId, 0f);
            }
            _ghost = MakeMarker("GhostPoint");
            _ghost.enabled = false;
        }

        /// <summary>Swaps the hands that pinch, for example for simulated ones.</summary>
        public void SetHands(HandSource[] hands)
        {
            _hands = hands;
        }

        void OnDestroy()
        {
            if (_markerMaterial != null) Destroy(_markerMaterial);
        }

        /// <summary>
        /// Opens editing around a head, with a level's points (stored relative to the head and its facing),
        /// or with none for a new level.
        /// </summary>
        public void Begin(IReadOnlyList<Vector3> localPoints, Vector3 head, Vector3 forward)
        {
            _head = head;
            _forward = forward;
            _points.Clear();
            if (localPoints != null)
                foreach (var p in localPoints)
                    if (_points.Count < MaxPoints) _points.Add(CustomLevelData.ToWorld(p, head, forward));
            IsEditing = true;
            // A pinch already held when the editor opens is old: it must let go before it counts.
            _rightWasPinching = true;
            _leftWasPinching = true;
            _hover = -1;
            Rebuild();
        }

        public void End()
        {
            IsEditing = false;
            _points.Clear();
            _hover = -1;
            Rebuild();
            if (_ghost != null) _ghost.enabled = false;
        }

        /// <summary>The points relative to the head and facing the editor opened with, ready to save.</summary>
        public List<Vector3> LocalPoints()
        {
            var local = new List<Vector3>(_points.Count);
            foreach (var p in _points) local.Add(CustomLevelData.ToLocal(p, _head, _forward));
            return local;
        }

        /// <summary>Adds a point at the end of the path. False if it is too close to the last one or the path is full.</summary>
        public bool AddPoint(Vector3 world)
        {
            if (!IsEditing || _points.Count >= MaxPoints) return false;
            if (_points.Count > 0 && Vector3.Distance(_points[_points.Count - 1], world) < minSpacing) return false;
            _points.Add(world);
            Rebuild();
            Sfx.Play(SoundKind.UiClick, world);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Deletes the point nearest a position, if one is within reach. False if none was.</summary>
        public bool RemovePointNear(Vector3 world)
        {
            int i = NearestPoint(world, deleteReach);
            if (i < 0) return false;
            RemoveAt(i);
            return true;
        }

        public void RemoveAt(int index)
        {
            if (!IsEditing || index < 0 || index >= _points.Count) return;
            Vector3 at = _points[index];
            _points.RemoveAt(index);
            _hover = -1;
            Rebuild();
            ComicVfx.Play(ComicKind.Poof, at, markerSize * 3f);
            Sfx.Play(SoundKind.BalloonPop, at);
            Changed?.Invoke();
        }

        /// <summary>The point nearest a position within a distance, or -1.</summary>
        public int NearestPoint(Vector3 world, float reach)
        {
            int best = -1;
            float bestDistance = reach;
            for (int i = 0; i < _points.Count; i++)
            {
                float d = Vector3.Distance(_points[i], world);
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- input

        void Update()
        {
            if (!IsEditing) return;
            HandSource right = Hand(HandSide.Right), left = Hand(HandSide.Left);

            bool rightPinching = Pinch(right, out Vector3 rightPoint, out float rightGap);
            if (rightPinching && !_rightWasPinching && !Blocked(rightPoint)) AddPoint(rightPoint);
            _rightWasPinching = rightPinching;

            bool leftPinching = Pinch(left, out Vector3 leftPoint, out _);
            int hover = left != null && left.IsTracked && !Blocked(leftPoint) ? NearestPoint(leftPoint, deleteReach) : -1;
            if (leftPinching && !_leftWasPinching && hover >= 0)
            {
                RemoveAt(hover);
                hover = -1;
            }
            _leftWasPinching = leftPinching;
            if (hover != _hover)
            {
                _hover = hover;
                Rebuild();
            }

            // The ghost: where the next point will go, while the right hand is closing to pinch.
            bool ghost = right != null && right.IsTracked && !rightPinching && rightGap < ghostPinchGap && !Blocked(rightPoint)
                         && _points.Count < MaxPoints;
            if (_ghost != null)
            {
                _ghost.enabled = ghost;
                if (ghost)
                {
                    _ghost.transform.position = rightPoint;
                    float k = Mathf.InverseLerp(ghostPinchGap, 0.01f, rightGap);
                    _ghost.transform.localScale = Vector3.one * markerSize * Mathf.Lerp(0.4f, 0.8f, k);
                    Paint(_ghost, ghostColor);
                }
            }
        }

        HandSource Hand(HandSide side)
        {
            if (_hands == null) return null;
            foreach (var h in _hands)
                if (h != null && h.side == side) return h;
            return null;
        }

        /// <summary>Whether a hand is pinching, where its thumb and index meet, and how far apart they are.</summary>
        static bool Pinch(HandSource hand, out Vector3 point, out float gap)
        {
            point = Vector3.zero;
            gap = float.PositiveInfinity;
            if (hand == null || !hand.IsTracked) return false;
            if (!hand.TryGetJoint(HandJoint.ThumbTip, out var thumb) || !hand.TryGetJoint(HandJoint.IndexTip, out var index)) return false;
            point = (thumb + index) * 0.5f;
            gap = Vector3.Distance(thumb, index);
            return hand.IsIndexPinching && !GameManager.IsPaused;
        }

        bool Blocked(Vector3 point)
        {
            return IsBlocked != null && IsBlocked(point);
        }

        // ---------------------------------------------------------------- visuals

        void Rebuild()
        {
            while (_markers.Count < _points.Count) _markers.Add(MakeMarker("PathPoint" + _markers.Count));
            for (int i = 0; i < _markers.Count; i++)
            {
                var m = _markers[i];
                bool used = i < _points.Count;
                m.enabled = used;
                if (!used) continue;
                bool first = i == 0, last = i == _points.Count - 1 && _points.Count > 1;
                m.transform.position = _points[i];
                float size = markerSize * (first || last ? 1.25f : 1f) * (i == _hover ? 1.35f : 1f);
                m.transform.localScale = Vector3.one * size;
                Paint(m, i == _hover ? deleteColor : first ? startColor : last ? endColor : pointColor);
            }

            PathLength = CustomLevelData.PathLength(_points);
            if (_line == null) return;
            if (_points.Count < CustomLevelData.MinPoints || !IsEditing)
            {
                _line.enabled = false;
                return;
            }
            var path = new CatmullRomPath(_points, 24);
            int count = Mathf.Clamp(Mathf.CeilToInt(path.Length / 0.01f) + 1, 2, 1200);
            _line.positionCount = count;
            for (int i = 0; i < count; i++) _line.SetPosition(i, path.PositionAt(path.Length * i / (count - 1)));
            _line.widthMultiplier = lineWidth;
            _block.Clear();
            _block.SetFloat(AlphaId, lineAlpha);
            _line.SetPropertyBlock(_block);
            _line.enabled = true;
        }

        Renderer MakeMarker(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * markerSize;
            var r = go.GetComponent<MeshRenderer>();
            if (_markerMaterial != null) r.sharedMaterial = _markerMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            return r;
        }

        void Paint(Renderer r, Color color)
        {
            r.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetFloat(VertexColorId, 0f);
            r.SetPropertyBlock(_block);
        }
    }
}
