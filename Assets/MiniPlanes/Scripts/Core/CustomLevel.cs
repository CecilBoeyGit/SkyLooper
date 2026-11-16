using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MiniPlanes
{
    /// <summary>
    /// What a player can put into a custom level, in the order the Level Creator lists them. Keep new kinds
    /// at the end: saved levels store their counts by this number.
    /// </summary>
    public enum CustomFeature { Fighter, Bomber, ShieldBalloon, TurretBalloon, LifeBalloon, BoosterTube, Island }

    /// <summary>Names, icons, limits and timing windows for the custom level features.</summary>
    public static class CustomFeatures
    {
        public const int Count = 7;

        static readonly string[] Names =
            { "Fighter jets", "Bombers", "Shield balloons", "Turret balloons", "Heart balloons", "Booster tubes", "Floating islands" };
        static readonly string[] IconKeys = { "fighter", "bomber", "shield", "turret", "life", "tube", "island" };
        static readonly int[] Max = { 20, 10, 10, 10, 5, 10, 15 };

        // When along the path (0 to 1) each kind may turn up. Enemies stop early enough to do something
        // before the plane lands; islands come early so they are part of the scenery for most of the flight.
        static readonly Vector2[] Windows =
        {
            new Vector2(0.04f, 0.8f), new Vector2(0.04f, 0.7f), new Vector2(0.03f, 0.75f), new Vector2(0.03f, 0.75f),
            new Vector2(0.03f, 0.75f), new Vector2(0.03f, 0.7f), new Vector2(0.02f, 0.7f)
        };

        public static string Name(CustomFeature f) => Names[(int)f];
        /// <summary>The {key} that draws this feature's icon in text (see <see cref="InlineIcons"/>).</summary>
        public static string IconKey(CustomFeature f) => IconKeys[(int)f];
        public static int MaxCount(CustomFeature f) => Max[(int)f];
        public static Vector2 Window(CustomFeature f) => Windows[(int)f];
    }

    /// <summary>One moment in a custom flight when some of a feature appear.</summary>
    public struct ScheduledSpawn
    {
        /// <summary>How far along the path (0 to 1) the plane is when they appear.</summary>
        public float at;
        public CustomFeature feature;
        public int count;
    }

    /// <summary>
    /// A level made in the Level Creator: one path the player pinched into the air and how many of each
    /// feature fly in it. Custom levels are one wave on one path. The points are stored relative to the
    /// player's head and the way it faced when the level was made (x right, y up, z ahead, in metres), so
    /// the path is laid out the same way around the player whenever it is flown or edited.
    /// </summary>
    [Serializable]
    public class CustomLevelData
    {
        public const int CurrentVersion = 2;
        public const int MinPoints = 2;
        /// <summary>Flight speed when GameManager has no custom level template, in metres per second.</summary>
        public const float DefaultPlaneSpeed = 0.25f;

        public int version = CurrentVersion;
        public string id = "";
        public string name = "";
        public long createdTicks;
        public long editedTicks;
        public List<Vector3> points = new List<Vector3>();
        public int[] counts = new int[CustomFeatures.Count];
        /// <summary>The plane burns fuel, and ring passes top it up, as in the fuel tutorial and Infinite.</summary>
        public bool fuel = false;
        /// <summary>
        /// The fly path shows for the whole flight. Off, it shows for the first few seconds only and comes back
        /// for a while after three ring passes in a row, as in Infinite. Levels saved before this was a choice
        /// load with it on, which is how they flew.
        /// </summary>
        public bool pathAlwaysOn = true;

        public bool IsPlayable => points != null && points.Count >= MinPoints;

        public int GetCount(CustomFeature f)
        {
            int i = (int)f;
            return counts != null && i < counts.Length ? Mathf.Max(0, counts[i]) : 0;
        }

        public void SetCount(CustomFeature f, int value)
        {
            if (counts == null || counts.Length < CustomFeatures.Count)
            {
                var grown = new int[CustomFeatures.Count];
                if (counts != null) Array.Copy(counts, grown, counts.Length);
                counts = grown;
            }
            counts[(int)f] = Mathf.Clamp(value, 0, CustomFeatures.MaxCount(f));
        }

        public int[] CountsCopy()
        {
            var copy = new int[CustomFeatures.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = GetCount((CustomFeature)i);
            return copy;
        }

        public int TotalFeatures
        {
            get
            {
                int n = 0;
                for (int i = 0; i < CustomFeatures.Count; i++) n += GetCount((CustomFeature)i);
                return n;
            }
        }

        /// <summary>
        /// What to expect from the level's rules, one short line for the intro caption: how fuel and the fly
        /// path behave, or a plain reminder when both are left at their easy settings.
        /// </summary>
        public string RulesLine()
        {
            if (fuel && !pathAlwaysOn) return "Rings refill fuel. 3 in a row bring the path back.";
            if (fuel) return "Watch the fuel. Every ring tops it up.";
            if (!pathAlwaysOn) return "Memorise the path. 3 rings in a row bring it back.";
            return "Fly the path you drew all the way to the end.";
        }

        /// <summary>The rules as icons for lists: {fuel} when the plane burns fuel, {fade} when the path fades. Empty if neither.</summary>
        public string RulesIcons(string separator = "  ")
        {
            var parts = new List<string>();
            if (fuel) parts.Add("{fuel}");
            if (!pathAlwaysOn) parts.Add("{fade}");
            return string.Join(separator, parts);
        }

        /// <summary>The features as text with icons, such as "10 {fighter}   3 {shield}". Empty if there are none.</summary>
        public string FeatureSummary(string separator = "   ")
        {
            var parts = new List<string>();
            for (int i = 0; i < CustomFeatures.Count; i++)
            {
                var f = (CustomFeature)i;
                int n = GetCount(f);
                if (n > 0) parts.Add(n + " {" + CustomFeatures.IconKey(f) + "}");
            }
            return string.Join(separator, parts);
        }

        // ---------------------------------------------------------------- the path

        /// <summary>The way the player faced, level with the floor.</summary>
        public static Quaternion Frame(Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        public static Vector3 ToLocal(Vector3 world, Vector3 head, Vector3 forward)
        {
            return Quaternion.Inverse(Frame(forward)) * (world - head);
        }

        public static Vector3 ToWorld(Vector3 local, Vector3 head, Vector3 forward)
        {
            return head + Frame(forward) * local;
        }

        /// <summary>The smooth path through the points, laid out around a head. Null with too few points.</summary>
        public CatmullRomPath BuildPath(Vector3 head, Vector3 forward)
        {
            if (!IsPlayable) return null;
            var world = new List<Vector3>(points.Count);
            foreach (var p in points) world.Add(ToWorld(p, head, forward));
            return new CatmullRomPath(world, 24);
        }

        /// <summary>Length of the smooth path through some points, in metres.</summary>
        public static float PathLength(IReadOnlyList<Vector3> pts)
        {
            if (pts == null || pts.Count < MinPoints) return 0f;
            return new CatmullRomPath(pts, 12).Length;
        }

        // ---------------------------------------------------------------- flying it

        /// <summary>
        /// The tuning for flying this level: a copy of the template (or the defaults), with the path's own
        /// rules on top. Features are spawned by schedule, so every timer-driven spawn is off.
        /// </summary>
        public LevelConfig CreateConfig(LevelConfig template)
        {
            var cfg = template != null ? UnityEngine.Object.Instantiate(template) : ScriptableObject.CreateInstance<LevelConfig>();
            cfg.hideFlags = HideFlags.DontSave;
            cfg.name = "Custom " + name;
            if (template == null) cfg.planeSpeed = DefaultPlaneSpeed;
            cfg.levelName = string.IsNullOrEmpty(name) ? "Custom Path" : name;
            cfg.levelNumber = 0;
            cfg.introCaption = "";
            cfg.requiredRingPasses = 0;
            cfg.lives = 3;
            cfg.hitsToCrash = 1;
            cfg.revealMode = pathAlwaysOn ? PathRevealMode.AlwaysVisible : PathRevealMode.TimedAtStart;
            cfg.useFuel = fuel;
            cfg.maxFighters = 0;
            cfg.maxBombers = 0;
            cfg.useBalloons = true;
            cfg.turretBalloonInterval = 0f;
            cfg.lifeBalloonInterval = 0f;
            cfg.boosterTubeInterval = 0f;
            cfg.islandCount = 0;
            cfg.jammingTowers = 0;
            cfg.SetBoss(0, false);
            float length = PathLength(points);
            cfg.pathDurationSeconds = cfg.planeSpeed > 0f ? length / cfg.planeSpeed : 0f;
            return cfg;
        }

        /// <summary>
        /// Spreads each feature over the flight: at random moments along the path, a random number at a
        /// time, until the level's count is reached. Sorted by when they appear.
        /// </summary>
        public static List<ScheduledSpawn> MakeSchedule(IReadOnlyList<int> counts, int seed)
        {
            var rng = new System.Random(seed);
            var list = new List<ScheduledSpawn>();
            if (counts == null) return list;
            for (int i = 0; i < CustomFeatures.Count && i < counts.Count; i++)
            {
                var f = (CustomFeature)i;
                int left = Mathf.Clamp(counts[i], 0, CustomFeatures.MaxCount(f));
                if (left <= 0) continue;
                int maxBatch = Mathf.Clamp(Mathf.CeilToInt(left / 3f), 1, 4);
                Vector2 window = CustomFeatures.Window(f);
                while (left > 0)
                {
                    int batch = rng.Next(1, Mathf.Min(maxBatch, left) + 1);
                    float at = Mathf.Lerp(window.x, window.y, (float)rng.NextDouble());
                    list.Add(new ScheduledSpawn { at = at, feature = f, count = batch });
                    left -= batch;
                }
            }
            list.Sort((a, b) => a.at.CompareTo(b.at));
            return list;
        }

        public CustomLevelData Clone()
        {
            return JsonUtility.FromJson<CustomLevelData>(JsonUtility.ToJson(this));
        }
    }

    /// <summary>
    /// Saves custom levels as JSON files, one per level, in the app's persistent data folder
    /// (<see cref="Folder"/>), so they survive updates and never go through PlayerPrefs.
    /// </summary>
    public static class CustomLevelStore
    {
        public const string NamePrefix = "Path ";

        public static string Folder => Path.Combine(Application.persistentDataPath, "CustomLevels");

        /// <summary>Every saved level that can be read, oldest first.</summary>
        public static List<CustomLevelData> LoadAll()
        {
            var levels = new List<CustomLevelData>();
            try
            {
                if (!Directory.Exists(Folder)) return levels;
                foreach (var file in Directory.GetFiles(Folder, "*.json"))
                {
                    try
                    {
                        var data = JsonUtility.FromJson<CustomLevelData>(File.ReadAllText(file));
                        if (data == null || string.IsNullOrEmpty(data.id)) continue;
                        if (data.points == null) data.points = new List<Vector3>();
                        if (data.counts == null || data.counts.Length < CustomFeatures.Count) data.counts = data.CountsCopy();
                        levels.Add(data);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[MiniPlanes] Skipped an unreadable custom level " + file + ": " + e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MiniPlanes] Couldn't read custom levels: " + e.Message);
            }
            levels.Sort((a, b) => a.createdTicks != b.createdTicks ? a.createdTicks.CompareTo(b.createdTicks) : string.CompareOrdinal(a.id, b.id));
            return levels;
        }

        /// <summary>Writes a level, giving it an id first if it is new. Returns false if it couldn't be written.</summary>
        public static bool Save(CustomLevelData data)
        {
            if (data == null) return false;
            long now = DateTime.UtcNow.Ticks;
            if (string.IsNullOrEmpty(data.id))
            {
                data.id = "path_" + now;
                data.createdTicks = now;
            }
            data.editedTicks = now;
            data.version = CustomLevelData.CurrentVersion;
            try
            {
                Directory.CreateDirectory(Folder);
                string file = FileFor(data.id);
                string temp = file + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(file)) File.Delete(file);
                File.Move(temp, file);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MiniPlanes] Couldn't save custom level " + data.id + ": " + e.Message);
                return false;
            }
        }

        public static bool Delete(string id)
        {
            try
            {
                string file = FileFor(id);
                if (!File.Exists(file)) return false;
                File.Delete(file);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MiniPlanes] Couldn't delete custom level " + id + ": " + e.Message);
                return false;
            }
        }

        /// <summary>"Path N", one higher than any level already named that way.</summary>
        public static string NextName(IEnumerable<CustomLevelData> existing)
        {
            int highest = 0;
            if (existing != null)
                foreach (var level in existing)
                    if (level != null && level.name != null && level.name.StartsWith(NamePrefix)
                        && int.TryParse(level.name.Substring(NamePrefix.Length), out int n))
                        highest = Mathf.Max(highest, n);
            return NamePrefix + (highest + 1);
        }

        static string FileFor(string id)
        {
            var safe = new System.Text.StringBuilder(id.Length);
            foreach (char c in id) safe.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return Path.Combine(Folder, safe + ".json");
        }
    }
}
