using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MiniPlanes.Editor
{
    /// <summary>
    /// Creates the editable UI prefabs in Assets/MiniPlanes/Prefabs/UI: the menu (with Hangar and Custom
    /// Paths), the cockpit HUD, the pause panel, the Level Creator panel, the wrist button, balloon icons,
    /// the crosshair and the fuel gauge. A prefab that
    /// already exists is left exactly as it is, so your edits survive rebuilding the scene. Delete a prefab
    /// to get the generated version back.
    /// </summary>
    public static class MiniPlanesUiBuilder
    {
        public const string PrefabDir = "Assets/MiniPlanes/Prefabs/UI";
        const float CanvasScale = 0.0005f;   // metres per canvas unit

        static readonly Color Paper = new Color(0.98f, 0.95f, 0.89f, 0.97f);
        static readonly Color Ink = new Color(0.25f, 0.2f, 0.17f, 1f);
        static readonly Color Muted = new Color(0.56f, 0.52f, 0.47f, 1f);
        static readonly Color Teal = new Color(0.24f, 0.58f, 0.54f, 1f);
        static readonly Color TealDark = new Color(0.16f, 0.44f, 0.41f, 1f);
        static readonly Color Seafoam = new Color(0.56f, 0.82f, 0.68f, 1f);
        static readonly Color Coral = new Color(0.86f, 0.42f, 0.33f, 1f);
        static readonly Color CoralDark = new Color(0.68f, 0.3f, 0.23f, 1f);
        static readonly Color Slate = new Color(0.36f, 0.42f, 0.42f, 1f);
        static readonly Color SlateDark = new Color(0.24f, 0.3f, 0.3f, 1f);
        static readonly Color Locked = new Color(0.88f, 0.85f, 0.79f, 1f);
        static readonly Color Gold = new Color(0.86f, 0.6f, 0.12f, 1f);
        static readonly Color Violet = new Color(0.5f, 0.4f, 0.7f, 1f);
        static readonly Color VioletDark = new Color(0.36f, 0.28f, 0.54f, 1f);
        static readonly Color Card = new Color(0.93f, 0.9f, 0.83f, 1f);
        // The Level Creator's path points, matching LevelEditor's colours: start, middle, finish.
        static readonly Color PointStart = new Color(0.24f, 0.62f, 0.56f, 1f);
        static readonly Color PointMid = new Color(0.98f, 0.93f, 0.8f, 1f);
        static readonly Color PointEnd = new Color(0.9f, 0.42f, 0.32f, 1f);
        static readonly Color CardLight = new Color(1f, 0.98f, 0.94f, 1f);
        static readonly Color HudPlate = new Color(0.07f, 0.17f, 0.17f, 0.55f);
        static readonly Color HudCream = new Color(0.98f, 0.94f, 0.86f, 1f);
        static readonly Color HudAccent = new Color(0.36f, 0.85f, 0.72f, 1f);

        static Font _font, _heading;
        static Sprite _rounded, _white, _circle;

        public class Prefabs
        {
            public GameObject splash, menu, hud, pause, creator, wrist, shieldIcon, bombIcon, turretIcon, lifeIcon, jamIcon, noTouchIcon, cupIcon, crosshair, dragCursor, fuelGauge;
        }

        public static Prefabs EnsureAll(GameArt art)
        {
            MiniPlanesUiSprites.EnsureAll();
            if (!AssetDatabase.IsValidFolder("Assets/MiniPlanes/Prefabs")) AssetDatabase.CreateFolder("Assets/MiniPlanes", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/MiniPlanes/Prefabs", "UI");

            _font = art.hudFont != null ? art.hudFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _heading = art.headingFont != null ? art.headingFont : _font;
            _rounded = MiniPlanesUiSprites.Load("Rounded");
            _white = MiniPlanesUiSprites.Load("White");
            _circle = MiniPlanesUiSprites.Load("Circle");

            return new Prefabs
            {
                // Rebuilt once if it predates a level: the list needs a row for every level.
                menu = Ensure("MenuPanel", BuildMenu, existing =>
                {
                    var m = existing.GetComponent<MenuPanel>();
                    return m == null || m.levelRows == null || m.levelRows.Length < Progression.TutorialLevelCount || m.respawnButton == null || m.resultWallet == null
                        || m.planeRows == null || m.planeRows.Length < 4
                        || m.customButton == null || m.customPage == null || m.customRowTemplate == null || m.creatorButton == null;
                }),
                // Rebuilt once if it predates the plane knob on the progress bar.
                splash = Ensure("SplashPanel", BuildSplash),
                hud = Ensure("CockpitHUD", BuildHud, existing =>
                {
                    var h = existing.GetComponent<CurvedHud>();
                    return h == null || h.progressKnob == null || h.finishFlag == null;
                }),
                pause = Ensure("PausePanel", BuildPause),
                creator = Ensure("LevelCreatorPanel", BuildCreator, existing =>
                {
                    var c = existing.GetComponent<LevelCreatorPanel>();
                    return c == null || c.featureRows == null || c.featureRows.Length < CustomFeatures.Count || c.fuelToggle == null || c.pathToggle == null;
                }),
                wrist = Ensure("WristMenuButton", BuildWrist),
                shieldIcon = Ensure("ShieldIcon", () => BuildIcon("ShieldIcon", "ShieldIcon")),
                bombIcon = Ensure("BombIcon", () => BuildIcon("BombIcon", "BombIcon")),
                turretIcon = Ensure("TurretIcon", () => BuildIcon("TurretIcon", "TurretIcon")),
                lifeIcon = Ensure("LifeIcon", () => BuildIcon("LifeIcon", "LifeIcon")),
                jamIcon = Ensure("JamIcon", () => BuildIcon("JamIcon", "JamIcon")),
                noTouchIcon = Ensure("NoTouchIcon", () => BuildIcon("NoTouchIcon", "NoTouchIcon")),
                cupIcon = Ensure("CupIcon", BuildCupIcon),
                crosshair = Ensure("Crosshair", BuildCrosshair),
                dragCursor = Ensure("DragCursor", BuildDragCursor),
                fuelGauge = Ensure("FuelGauge", BuildFuelGauge)
            };
        }

        static GameObject Ensure(string name, Func<GameObject> build, Func<GameObject, bool> outdated = null)
        {
            string path = PrefabDir + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && (outdated == null || !outdated(existing))) return existing;
            if (existing != null) Debug.LogWarning("[MiniPlanes] Rebuilding outdated UI prefab " + path + " (it lacks parts the game now needs).");
            var go = build();
            foreach (var curved in go.GetComponentsInChildren<CurvedCanvas>(true)) curved.EnsureEffects();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log("[MiniPlanes] Created UI prefab " + path);
            return prefab;
        }

        // ---------------------------------------------------------------- shared pieces

        /// <summary>A follow root, and a curved poke canvas on it at the given distance and height.</summary>
        static RectTransform CurvedPanel(GameObject root, string name, float distance, float height, Vector2 size, int order,
            out LazyFollow follow, out PokeCanvas poke)
        {
            var followGo = new GameObject("Follow");
            followGo.transform.SetParent(root.transform, false);
            follow = followGo.AddComponent<LazyFollow>();

            var canvasGo = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)canvasGo.transform;
            rect.SetParent(followGo.transform, false);
            rect.localPosition = new Vector3(0f, height, distance);
            rect.sizeDelta = size;
            rect.localScale = Vector3.one * CanvasScale;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = order;
            canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2.5f;
            var curved = canvasGo.AddComponent<CurvedCanvas>();
            curved.radius = distance;
            poke = canvasGo.AddComponent<PokeCanvas>();
            return rect;
        }

        static RectTransform FlatCanvas(Transform parent, string name, Vector2 size, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.localScale = Vector3.one * CanvasScale;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = order;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2.5f;
            return rect;
        }

        static void AddCursors(RectTransform canvas, PokeCanvas poke)
        {
            var cursors = new Graphic[2];
            for (int i = 0; i < 2; i++)
            {
                var c = Img(canvas, "Cursor" + i, _circle, new Color(Teal.r, Teal.g, Teal.b, 0.8f), r =>
                {
                    r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                    r.sizeDelta = new Vector2(26f, 26f);
                });
                c.enabled = false;
                cursors[i] = c;
            }
            poke.cursors = cursors;
        }

        static RectTransform Page(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            Stretch(r);
            return r;
        }

        /// <summary>A page of a fixed width, centred on the panel, so a layout made for that width stays in the middle.</summary>
        static RectTransform CentredPage(RectTransform parent, string name, float width)
        {
            var r = Page(parent, name);
            r.anchorMin = new Vector2(0.5f, 0f);
            r.anchorMax = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(width, 0f);
            r.anchoredPosition = Vector2.zero;
            return r;
        }

        static Image Img(RectTransform parent, string name, Sprite sprite, Color color, Action<RectTransform> layout)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
            layout(r);
            return image;
        }

        static Text Txt(RectTransform parent, string name, string text, int size, Font font, Color color, TextAnchor anchor, Action<RectTransform> layout)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            layout(r);
            return t;
        }

        static PokeButton Btn(RectTransform parent, string id, string label, int size, Color normal, Color pressed, Action<RectTransform> layout, out Text labelText)
        {
            var bg = Img(parent, "Button_" + id, _rounded, normal, layout);
            var b = bg.gameObject.AddComponent<PokeButton>();
            b.id = id;
            b.background = bg;
            b.normalColor = normal;
            b.pressedColor = pressed;
            b.disabledColor = Locked;
            labelText = null;
            if (!string.IsNullOrEmpty(label))
                labelText = Txt(bg.rectTransform, "Label", label, size, _heading, Color.white, TextAnchor.MiddleCenter, Stretch);
            return b;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        /// <summary>Pins by distance from the top-left corner, in canvas units.</summary>
        static Action<RectTransform> At(float x, float y, float w, float h)
        {
            return r =>
            {
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 1f);
                r.anchoredPosition = new Vector2(x, -y);
                r.sizeDelta = new Vector2(w, h);
            };
        }

        /// <summary>Places by centre, relative to the parent's centre, in canvas units.</summary>
        static Action<RectTransform> Centre(float x, float y, float w, float h)
        {
            return r =>
            {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = new Vector2(x, y);
                r.sizeDelta = new Vector2(w, h);
            };
        }

        /// <summary>Places by centre, measured from the parent's top-left corner, in canvas units. Scales from the middle.</summary>
        static Action<RectTransform> AtCentre(float x, float y, float w, float h)
        {
            return r =>
            {
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = new Vector2(x, -y);
                r.sizeDelta = new Vector2(w, h);
            };
        }

        /// <summary>A horizontal bar from x0 to x1 at height y (from the top-left), growing from its left end.</summary>
        static Action<RectTransform> Bar(float x0, float x1, float y, float thickness)
        {
            return r =>
            {
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 0.5f);
                r.anchoredPosition = new Vector2(x0, -y);
                r.sizeDelta = new Vector2(x1 - x0, thickness);
            };
        }

        static void CoinSlot(RectTransform page, float x, float y, out Text text)
        {
            Img(page, "CoinIcon", MiniPlanesUiSprites.Load("Coin"), Color.white, At(x, y, 46f, 46f));
            text = Txt(page, "Coins", "0", 38, _heading, Gold, TextAnchor.MiddleLeft, At(x + 54f, y - 4f, 120f, 54f));
        }

        // ---------------------------------------------------------------- menu

        // The start and Custom Paths pages use the full width; the others keep a centred 1000-wide layout.
        const float MenuW = 1320f, MenuH = 660f, PageW = 1000f;

        static GameObject BuildMenu()
        {
            var root = new GameObject("MenuPanel");
            var menu = root.AddComponent<MenuPanel>();
            var canvas = CurvedPanel(root, "Canvas", 0.46f, -0.07f, new Vector2(MenuW, MenuH), 10, out var follow, out var poke);
            follow.deadzoneDeg = 18f;
            follow.followSpeed = 2f;
            menu.follow = follow;
            menu.poke = poke;

            Img(canvas, "Paper", _rounded, Paper, Stretch);

            BuildStartPage(menu, canvas);
            BuildIntroPage(menu, canvas);
            BuildResultPage(menu, canvas);
            BuildHangarPage(menu, canvas, follow.transform);
            BuildCustomPage(menu, canvas);
            AddCursors(canvas, poke);
            return root;
        }

        static void BuildStartPage(MenuPanel menu, RectTransform canvas)
        {
            var page = Page(canvas, "StartPage");
            menu.startPage = page.gameObject;
            Txt(page, "Title", "SKYLOOPER", 60, _heading, Ink, TextAnchor.MiddleCenter, At(0f, 18f, MenuW, 84f));
            CoinSlot(page, MenuW - 170f, 32f, out menu.startCoins);

            Txt(page, "TutorialHead", "TUTORIAL", 28, _heading, Muted, TextAnchor.MiddleLeft, At(40f, 108f, 300f, 40f));
            var viewport = Img(page, "LevelList", null, new Color(1f, 1f, 1f, 0.01f), At(40f, 152f, 520f, 470f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var area = viewport.gameObject.AddComponent<PokeScrollArea>();
            var content = new GameObject("Content", typeof(RectTransform));
            var contentRect = (RectTransform)content.transform;
            contentRect.SetParent(viewport.rectTransform, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            const float rowH = 92f, gap = 10f;
            contentRect.sizeDelta = new Vector2(0f, Progression.TutorialLevelCount * (rowH + gap) - gap);
            area.content = contentRect;

            var rows = new List<MenuPanel.LevelRow>();
            for (int i = 0; i < Progression.TutorialLevelCount; i++)
            {
                float y = i * (rowH + gap);
                var b = Btn(contentRect, "level" + i, "", 1, Seafoam, Teal, r =>
                {
                    r.anchorMin = new Vector2(0f, 1f);
                    r.anchorMax = new Vector2(1f, 1f);
                    r.pivot = new Vector2(0.5f, 1f);
                    r.anchoredPosition = new Vector2(0f, -y);
                    r.sizeDelta = new Vector2(0f, rowH);
                }, out _);
                var row = new MenuPanel.LevelRow { button = b };
                row.number = Txt(b.Rect, "Number", (i + 1).ToString(), 44, _heading, Ink, TextAnchor.MiddleCenter, r =>
                {
                    r.anchorMin = new Vector2(0f, 0f);
                    r.anchorMax = new Vector2(0f, 1f);
                    r.pivot = new Vector2(0f, 0.5f);
                    r.sizeDelta = new Vector2(84f, 0f);
                });
                row.name = Txt(b.Rect, "Name", "Level " + (i + 1), 32, _font, Ink, TextAnchor.MiddleLeft, r =>
                {
                    Stretch(r);
                    r.offsetMin = new Vector2(88f, 0f);
                    r.offsetMax = new Vector2(-126f, 0f);
                });
                row.status = Txt(b.Rect, "Status", "", 22, _font, Muted, TextAnchor.MiddleRight, r =>
                {
                    r.anchorMin = new Vector2(1f, 0f);
                    r.anchorMax = new Vector2(1f, 1f);
                    r.pivot = new Vector2(1f, 0.5f);
                    r.sizeDelta = new Vector2(120f, 0f);
                    r.anchoredPosition = new Vector2(-14f, 0f);
                });
                rows.Add(row);
            }
            menu.levelRows = rows.ToArray();
            menu.levelList = area;

            var track = Img(page, "ScrollTrack", _rounded, new Color(0.82f, 0.79f, 0.73f, 1f), At(572f, 152f, 12f, 470f));
            var thumb = Img(track.rectTransform, "ScrollThumb", _rounded, Teal, r =>
            {
                r.anchorMin = new Vector2(0f, 1f);
                r.anchorMax = new Vector2(1f, 1f);
                r.pivot = new Vector2(0.5f, 1f);
                r.sizeDelta = new Vector2(0f, 200f);
            });
            area.track = track.rectTransform;
            area.thumb = thumb.rectTransform;

            Txt(page, "InfiniteHead", "INFINITE", 28, _heading, Muted, TextAnchor.MiddleLeft, At(612f, 108f, 300f, 40f));
            menu.infiniteButton = Btn(page, "infinite", "PLAY", 46, Teal, TealDark, At(612f, 152f, 320f, 160f), out menu.infiniteLabel);
            menu.infiniteBest = Txt(page, "Best", "BEST  0", 30, _font, Ink, TextAnchor.MiddleLeft, At(612f, 318f, 320f, 44f));
            menu.infiniteLock = Txt(page, "Lock", "", 22, _font, Muted, TextAnchor.UpperLeft, At(612f, 362f, 320f, 64f));

            Txt(page, "HangarHead", "HANGAR", 28, _heading, Muted, TextAnchor.MiddleLeft, At(612f, 446f, 300f, 40f));
            menu.hangarButton = Btn(page, "hangar", "PLANES", 40, Coral, CoralDark, At(612f, 490f, 320f, 132f), out _);

            // Custom Paths, to the right of Infinite: the player's own levels and the Level Creator.
            const float cx = 964f, cw = 316f;
            Txt(page, "CustomHead", "CUSTOM PATHS", 28, _heading, Muted, TextAnchor.MiddleLeft, At(cx, 108f, cw, 40f));
            menu.customButton = Btn(page, "custom", "", 1, Violet, VioletDark, At(cx, 152f, cw, 160f), out _);
            Img(menu.customButton.Rect, "Icon", MiniPlanesUiSprites.Load("CreatorIcon"), Color.white, r =>
            {
                r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
                r.pivot = new Vector2(0f, 0.5f);
                r.anchoredPosition = new Vector2(22f, 0f);
                r.sizeDelta = new Vector2(112f, 112f);
            });
            Txt(menu.customButton.Rect, "Label", "OPEN", 44, _heading, Color.white, TextAnchor.MiddleCenter, r =>
            {
                Stretch(r);
                r.offsetMin = new Vector2(134f, 0f);
            });
            menu.customInfo = Txt(page, "CustomInfo", "No levels made yet.", 26, _font, Ink, TextAnchor.MiddleLeft, At(cx, 318f, cw, 44f));
            Txt(page, "CustomBlurb", "Pinch out a path, add enemies and powerups, then fly it.", 22, _font, Muted, TextAnchor.UpperLeft, At(cx, 362f, cw, 64f));
            Img(page, "CustomArt", MiniPlanesUiSprites.Load("CreatorIcon"), Color.white, AtCentre(cx + cw * 0.5f, 540f, 150f, 150f));
        }

        static void BuildCustomPage(MenuPanel menu, RectTransform canvas)
        {
            var page = Page(canvas, "CustomPage");
            menu.customPage = page.gameObject;
            Btn(page, "custom_back", "BACK", 30, Slate, SlateDark, At(30f, 26f, 160f, 68f), out _);
            Txt(page, "Title", "CUSTOM PATHS", 50, _heading, Ink, TextAnchor.MiddleCenter, At(0f, 20f, MenuW, 76f));

            // Left: the saved levels. A hidden template row is copied for each one.
            Txt(page, "LevelsHead", "YOUR LEVELS", 28, _heading, Muted, TextAnchor.MiddleLeft, At(40f, 108f, 400f, 40f));
            var viewport = Img(page, "CustomList", null, new Color(1f, 1f, 1f, 0.01f), At(40f, 152f, 720f, 470f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var area = viewport.gameObject.AddComponent<PokeScrollArea>();
            var content = new GameObject("Content", typeof(RectTransform));
            var contentRect = (RectTransform)content.transform;
            contentRect.SetParent(viewport.rectTransform, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 0f);
            area.content = contentRect;
            menu.customList = area;
            menu.customRowGap = 10f;

            var rowGo = new GameObject("CustomRowTemplate", typeof(RectTransform));
            var row = (RectTransform)rowGo.transform;
            row.SetParent(contentRect, false);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = Vector2.zero;
            row.sizeDelta = new Vector2(0f, 112f);
            var rowView = rowGo.AddComponent<CustomLevelRow>();
            rowView.play = Btn(row, "cplay", "", 1, Seafoam, Teal, r =>
            {
                Stretch(r);
                r.offsetMax = new Vector2(-150f, 0f);
            }, out _);
            rowView.levelName = Txt(rowView.play.Rect, "Name", "Path 1", 34, _heading, Ink, TextAnchor.MiddleLeft, r =>
            {
                Stretch(r);
                r.offsetMin = new Vector2(24f, 52f);
                r.offsetMax = new Vector2(-14f, -8f);
            });
            rowView.detail = Txt(rowView.play.Rect, "Detail", "", 22, _font, Ink, TextAnchor.MiddleLeft, r =>
            {
                Stretch(r);
                r.offsetMin = new Vector2(24f, 8f);
                r.offsetMax = new Vector2(-14f, -62f);
            });
            rowView.detail.horizontalOverflow = HorizontalWrapMode.Overflow;
            rowView.detailIcons = rowView.detail.gameObject.AddComponent<InlineIcons>();
            rowView.detailIcons.iconScale = 1.45f;
            rowView.edit = Btn(row, "cedit", "EDIT", 32, Coral, CoralDark, r =>
            {
                r.anchorMin = new Vector2(1f, 0f);
                r.anchorMax = new Vector2(1f, 1f);
                r.pivot = new Vector2(1f, 0.5f);
                r.anchoredPosition = Vector2.zero;
                r.sizeDelta = new Vector2(136f, 0f);
            }, out _);
            rowGo.SetActive(false);
            menu.customRowTemplate = rowView;

            var track = Img(page, "ScrollTrack", _rounded, new Color(0.82f, 0.79f, 0.73f, 1f), At(772f, 152f, 12f, 470f));
            var thumb = Img(track.rectTransform, "ScrollThumb", _rounded, Teal, r =>
            {
                r.anchorMin = new Vector2(0f, 1f);
                r.anchorMax = new Vector2(1f, 1f);
                r.pivot = new Vector2(0.5f, 1f);
                r.sizeDelta = new Vector2(0f, 200f);
            });
            area.track = track.rectTransform;
            area.thumb = thumb.rectTransform;
            menu.customEmpty = Txt(page, "Empty", "No custom paths yet.\nOpen the Level Creator to make one.", 28, _font, Muted, TextAnchor.MiddleCenter,
                At(60f, 300f, 680f, 120f));

            // Right: the Level Creator.
            const float rx = 820f, rw = 460f;
            Txt(page, "CreatorHead", "LEVEL CREATOR", 28, _heading, Muted, TextAnchor.MiddleLeft, At(rx, 108f, rw, 40f));
            var card = Img(page, "CreatorCard", _rounded, Card, At(rx, 152f, rw, 300f));
            Img(card.rectTransform, "Art", MiniPlanesUiSprites.Load("CreatorIcon"), Color.white, AtCentre(rw * 0.5f, 92f, 150f, 150f));
            Txt(card.rectTransform, "Blurb", "Pinch a flight path into the air, choose its enemies, powerups and islands, then fly it.",
                22, _font, Ink, TextAnchor.UpperCenter, At(24f, 180f, rw - 48f, 104f));
            menu.creatorButton = Btn(page, "creator_new", "NEW LEVEL", 44, Teal, TealDark, At(rx, 476f, rw, 146f), out _);
        }

        static void BuildIntroPage(MenuPanel menu, RectTransform canvas)
        {
            var page = CentredPage(canvas, "IntroPage", PageW);
            menu.introPage = page.gameObject;
            menu.introBack = Btn(page, "back", "BACK", 30, Slate, SlateDark, At(36f, 30f, 170f, 70f), out _);
            menu.introNumber = Txt(page, "Number", "LEVEL 1", 32, _heading, Muted, TextAnchor.MiddleCenter, At(0f, 40f, PageW, 50f));
            menu.introTitle = Txt(page, "Title", "First Flight", 64, _heading, Ink, TextAnchor.MiddleCenter, At(0f, 92f, PageW, 90f));
            menu.introCaption = Txt(page, "Caption", "", 30, _font, Muted, TextAnchor.UpperCenter, At(70f, 196f, PageW - 140f, 140f));
            Txt(page, "LivesLabel", "LIVES", 26, _heading, Muted, TextAnchor.MiddleRight, At(300f, 352f, 150f, 56f));
            var hearts = new List<Image>();
            for (int i = 0; i < 5; i++)
                hearts.Add(Img(page, "Life" + i, MiniPlanesUiSprites.Load("Heart"), Color.white, At(470f + i * 62f, 352f, 54f, 54f)));
            menu.introLives = hearts.ToArray();
            menu.introStart = Btn(page, "start", "START", 54, Teal, TealDark, At(300f, 448f, 400f, 150f), out _);
        }

        static void BuildResultPage(MenuPanel menu, RectTransform canvas)
        {
            var page = CentredPage(canvas, "ResultPage", PageW);
            menu.resultPage = page.gameObject;
            menu.resultTitle = Txt(page, "Title", "LEVEL PASSED", 66, _heading, Ink, TextAnchor.MiddleCenter, At(0f, 44f, PageW, 100f));
            menu.resultDetail = Txt(page, "Detail", "", 30, _font, Muted, TextAnchor.MiddleCenter, At(60f, 150f, PageW - 120f, 110f));
            menu.resultScore = Txt(page, "Score", "SCORE  0", 52, _heading, Ink, TextAnchor.MiddleCenter, At(0f, 266f, PageW, 80f));
            menu.resultCoins = Txt(page, "Coins", "", 34, _heading, Gold, TextAnchor.MiddleCenter, At(0f, 346f, PageW, 56f));
            CoinSlot(page, 830f, 32f, out menu.resultWallet);
            menu.nextButton = Btn(page, "next", "NEXT", 36, Teal, TealDark, At(44f, 448f, 276f, 150f), out menu.nextLabel);
            // Shares the Next slot: only Infinite's game-over page shows it.
            menu.respawnButton = Btn(page, "respawn", "RESPAWN\n50 COINS", 30, Gold, Teal, At(44f, 448f, 276f, 150f), out menu.respawnLabel);
            menu.respawnButton.gameObject.SetActive(false);
            Btn(page, "retry", "RETRY", 36, Seafoam, Teal, At(362f, 448f, 276f, 150f), out _);
            Btn(page, "menu", "MENU", 36, Slate, SlateDark, At(680f, 448f, 276f, 150f), out _);
        }

        static void BuildHangarPage(MenuPanel menu, RectTransform canvas, Transform follow)
        {
            var page = CentredPage(canvas, "HangarPage", PageW);
            menu.hangarPage = page.gameObject;
            Btn(page, "hangar_back", "BACK", 30, Slate, SlateDark, At(30f, 26f, 160f, 68f), out _);
            Txt(page, "Title", "HANGAR", 50, _heading, Ink, TextAnchor.MiddleCenter, At(0f, 20f, PageW, 76f));
            CoinSlot(page, 830f, 32f, out menu.hangarCoins);

            var rows = new List<MenuPanel.PlaneRow>();
            for (int i = 0; i < 4; i++)
            {
                var b = Btn(page, "plane" + i, "", 1, Seafoam, Teal, At(30f, 112f + i * 128f, 280f, 116f), out _);
                var row = new MenuPanel.PlaneRow { button = b };
                row.name = Txt(b.Rect, "Name", "Plane", 32, _heading, Ink, TextAnchor.MiddleLeft, r =>
                {
                    Stretch(r);
                    r.offsetMin = new Vector2(22f, 40f);
                    r.offsetMax = new Vector2(-10f, -8f);
                });
                row.status = Txt(b.Rect, "Status", "", 24, _font, Muted, TextAnchor.MiddleLeft, r =>
                {
                    Stretch(r);
                    r.offsetMin = new Vector2(22f, 8f);
                    r.offsetMax = new Vector2(-10f, -72f);
                });
                rows.Add(row);
            }
            menu.planeRows = rows.ToArray();

            menu.planeName = Txt(page, "PlaneName", "", 40, _heading, Ink, TextAnchor.MiddleCenter, At(330f, 104f, 340f, 60f));
            Img(page, "PreviewStage", _rounded, new Color(0.9f, 0.86f, 0.78f, 0.6f), At(340f, 170f, 320f, 270f));
            menu.planeInfo = Txt(page, "PlaneInfo", "", 22, _font, Muted, TextAnchor.UpperCenter, At(330f, 448f, 340f, 60f));
            menu.planeAction = Btn(page, "plane_action", "FLY THIS", 36, Teal, TealDark, At(350f, 516f, 300f, 110f), out menu.planeActionLabel);

            var palette = Img(page, "PaintPanel", _rounded, new Color(0.93f, 0.9f, 0.83f, 1f), At(690f, 112f, 280f, 514f));
            menu.palettePanel = palette.gameObject;
            Txt(palette.rectTransform, "Head", "PAINT", 32, _heading, Muted, TextAnchor.MiddleCenter, At(0f, 16f, 280f, 50f));
            var swatches = new List<PokeButton>();
            for (int i = 0; i < 4; i++)
            {
                float x = 30f + (i % 2) * 120f;
                float y = 90f + (i / 2) * 140f;
                var bg = Img(palette.rectTransform, "Swatch" + i, MiniPlanesUiSprites.Load("Circle"), Color.white, At(x, y, 100f, 100f));
                var b = bg.gameObject.AddComponent<PokeButton>();
                b.id = "palette" + i;
                b.background = bg;
                swatches.Add(b);
                Img(bg.rectTransform, "Rim", MiniPlanesUiSprites.Load("Ring"), new Color(Ink.r, Ink.g, Ink.b, 0.5f), Stretch);
            }
            menu.paletteButtons = swatches.ToArray();
            menu.paletteName = Txt(palette.rectTransform, "Name", "", 28, _font, Ink, TextAnchor.MiddleCenter, At(0f, 380f, 280f, 50f));

            // The spinning preview floats just in front of the middle of the panel.
            var anchor = new GameObject("PlanePreview").transform;
            anchor.SetParent(follow, false);
            float stageCentre = MenuH * 0.5f - (170f + 135f);
            anchor.localPosition = new Vector3(0f, -0.07f + stageCentre * CanvasScale, 0.46f - 0.07f);
            anchor.localRotation = Quaternion.Euler(-15f, 200f, 0f);
            menu.previewAnchor = anchor;
        }

        // ---------------------------------------------------------------- level creator

        const float CreatorW = 1240f, CreatorH = 846f;

        /// <summary>
        /// The Level Creator's panel: low in front of the player and tilted up to face them, leaving the space
        /// above it free for pinching the path. Left, the mini tutorial; right, the feature counts, the rules
        /// (fuel and the fly path) and the Complete Level and Cancel buttons.
        /// </summary>
        static GameObject BuildCreator()
        {
            var root = new GameObject("LevelCreatorPanel");
            var panel = root.AddComponent<LevelCreatorPanel>();
            var canvas = CurvedPanel(root, "Canvas", 0.46f, -0.35f, new Vector2(CreatorW, CreatorH), 12, out var follow, out var poke);
            canvas.localRotation = Quaternion.Euler(28f, 0f, 0f);
            follow.deadzoneDeg = 26f;
            follow.followSpeed = 1.6f;
            follow.pitchFollow = 0f;
            panel.follow = follow;
            panel.poke = poke;

            Img(canvas, "Paper", _rounded, Paper, Stretch);
            panel.title = Txt(canvas, "Title", "LEVEL CREATOR", 40, _heading, Ink, TextAnchor.MiddleCenter, At(0f, 14f, CreatorW, 64f));
            BuildCreatorTutorial(root, canvas);
            BuildCreatorFeatures(panel, canvas);
            BuildCreatorRules(panel, canvas);

            const float bottom = 706f;
            panel.pathInfo = Txt(canvas, "PathInfo", "NO POINTS YET", 26, _heading, Ink, TextAnchor.MiddleLeft, At(512f, bottom - 4f, 330f, 36f));
            panel.hint = Txt(canvas, "Hint", "Pinch the air with your right hand to start a path.", 20, _font, Muted, TextAnchor.UpperLeft, At(512f, bottom + 34f, 330f, 80f));
            panel.cancelButton = Btn(canvas, "cancel", "CANCEL", 28, Slate, SlateDark, At(852f, bottom, 150f, 110f), out _);
            panel.completeButton = Btn(canvas, "complete", "COMPLETE\nLEVEL", 30, Teal, TealDark, At(1014f, bottom, 202f, 110f), out panel.completeLabel);
            AddCursors(canvas, poke);
            return root;
        }

        static void BuildCreatorTutorial(GameObject root, RectTransform canvas)
        {
            var section = Img(canvas, "Tutorial", _rounded, Card, At(24f, 88f, 470f, 728f)).rectTransform;
            const float cardH = 278f, dy = 29f;   // card height, and how far its contents sit below the old 220-tall layout
            Txt(section, "Head", "HOW TO DRAW", 26, _heading, Muted, TextAnchor.MiddleCenter, At(0f, 12f, 470f, 40f));
            var motion = root.AddComponent<CreatorTutorialMotion>();
            var dot = MiniPlanesUiSprites.Load("Circle");
            var ring = MiniPlanesUiSprites.Load("Ring");
            var hand = MiniPlanesUiSprites.Load("PinchHand");
            Color line = new Color(0.36f, 0.72f, 0.62f, 1f);
            Color red = new Color(0.9f, 0.16f, 0.14f, 1f);

            Image Dot(RectTransform parent, string name, float x, Color color)
            {
                var d = Img(parent, name, dot, color, AtCentre(x, 158f + dy, 28f, 28f));
                Img(d.rectTransform, "Rim", ring, new Color(Ink.r, Ink.g, Ink.b, 0.6f), Stretch);
                return d;
            }

            // Right pinch adds a point to the end of the path.
            var add = Img(section, "AddCard", _rounded, CardLight, At(16f, 62f, 438f, cardH)).rectTransform;
            motion.addHand = Img(add, "Hand", hand, Color.white, AtCentre(86f, 110f + dy, 136f, 136f)).rectTransform;
            Txt(add, "Head", "RIGHT PINCH", 30, _heading, TealDark, TextAnchor.MiddleLeft, At(170f, 22f + dy, 260f, 40f));
            Txt(add, "Text", "adds a path point", 24, _font, Ink, TextAnchor.UpperLeft, At(170f, 64f + dy, 260f, 60f));
            Img(add, "Link", _white, line, Bar(196f, 266f, 158f + dy, 6f));
            motion.addLink = Img(add, "NewLink", _white, line, Bar(266f, 346f, 158f + dy, 6f)).rectTransform;
            Dot(add, "Dot0", 196f, PointStart);
            Dot(add, "Dot1", 266f, PointMid);
            motion.addDot = Dot(add, "NewDot", 346f, PointEnd).rectTransform;

            // Left pinch on a point deletes it.
            var del = Img(section, "DeleteCard", _rounded, CardLight, At(16f, 358f, 438f, cardH)).rectTransform;
            var leftHand = Img(del, "Hand", hand, Color.white, AtCentre(86f, 110f + dy, 136f, 136f)).rectTransform;
            leftHand.localScale = new Vector3(-1f, 1f, 1f);
            motion.deleteHand = leftHand;
            Txt(del, "Head", "LEFT PINCH", 30, _heading, CoralDark, TextAnchor.MiddleLeft, At(170f, 22f + dy, 260f, 40f));
            Txt(del, "Text", "on a point deletes it", 24, _font, Ink, TextAnchor.UpperLeft, At(170f, 64f + dy, 260f, 60f));
            Img(del, "Link", _white, line, Bar(196f, 346f, 158f + dy, 6f));
            Dot(del, "Dot0", 196f, PointStart);
            motion.deleteDot = Dot(del, "Doomed", 271f, PointMid);
            Dot(del, "Dot2", 346f, PointEnd);
            var cross = new GameObject("Cross", typeof(RectTransform));
            var crossRect = (RectTransform)cross.transform;
            crossRect.SetParent(del, false);
            AtCentre(271f, 158f + dy, 44f, 44f)(crossRect);
            for (int i = 0; i < 2; i++)
            {
                var bar = Img(crossRect, "Bar" + i, _white, red, Centre(0f, 0f, 50f, 8f));
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
            }
            motion.deleteCross = crossRect;
            motion.dotColor = PointMid;
            motion.doomedColor = red;

            // What the colours mean.
            var key = Img(section, "Key", _rounded, CardLight, At(16f, 654f, 438f, 56f)).rectTransform;
            Img(key, "Start", dot, PointStart, AtCentre(30f, 28f, 22f, 22f));
            Txt(key, "StartText", "START", 20, _heading, Ink, TextAnchor.MiddleLeft, At(48f, 10f, 90f, 36f));
            Img(key, "End", dot, PointEnd, AtCentre(142f, 28f, 22f, 22f));
            Txt(key, "EndText", "FINISH", 20, _heading, Ink, TextAnchor.MiddleLeft, At(160f, 10f, 100f, 36f));
            Txt(key, "Order", "Joined in order", 19, _font, Muted, TextAnchor.MiddleLeft, At(274f, 10f, 156f, 36f));
        }

        static void BuildCreatorFeatures(LevelCreatorPanel panel, RectTransform canvas)
        {
            var section = Img(canvas, "Features", _rounded, Card, At(512f, 88f, 704f, 470f)).rectTransform;
            Txt(section, "Head", "LEVEL FEATURES", 26, _heading, Muted, TextAnchor.MiddleCenter, At(0f, 12f, 704f, 40f));
            string[] icons = { "FighterIcon", "BomberIcon", "ShieldIcon", "TurretIcon", "LifeIcon", "TubeIcon", "IslandIcon" };
            var rows = new List<LevelCreatorPanel.FeatureRow>();
            const float rowH = 54f, gap = 4f, top = 56f;
            for (int i = 0; i < CustomFeatures.Count; i++)
            {
                var f = (CustomFeature)i;
                float y = top + i * (rowH + gap);
                var row = new LevelCreatorPanel.FeatureRow { feature = f };
                if (i % 2 == 0) Img(section, "Stripe" + i, _white, new Color(1f, 1f, 1f, 0.35f), At(10f, y - 2f, 684f, rowH + 4f));
                row.icon = Img(section, "Icon" + i, MiniPlanesUiSprites.Load(icons[i]), Color.white, At(20f, y + 2f, 50f, 50f));
                row.name = Txt(section, "Name" + i, CustomFeatures.Name(f), 26, _font, Ink, TextAnchor.MiddleLeft, At(84f, y, 330f, rowH));
                row.minus = Btn(section, "feat_minus" + i, "-", 38, Slate, SlateDark, At(424f, y + 2f, 78f, rowH - 4f), out _);
                row.count = Txt(section, "Count" + i, "0", 32, _heading, Muted, TextAnchor.MiddleCenter, At(506f, y, 90f, rowH));
                row.plus = Btn(section, "feat_plus" + i, "+", 38, Teal, TealDark, At(600f, y + 2f, 78f, rowH - 4f), out _);
                rows.Add(row);
            }
            panel.featureRows = rows.ToArray();
        }

        /// <summary>The level's rules under the features: a switch for fuel and one for the fly path being always on.</summary>
        static void BuildCreatorRules(LevelCreatorPanel panel, RectTransform canvas)
        {
            var section = Img(canvas, "Rules", _rounded, Card, At(512f, 570f, 704f, 122f)).rectTransform;

            PokeButton Rule(string id, string sprite, string head, float x, out Text state, out Text detail)
            {
                var card = Img(section, "Rule_" + id, _rounded, CardLight, At(x, 8f, 340f, 106f)).rectTransform;
                Img(card, "Icon", MiniPlanesUiSprites.Load(sprite), Color.white, At(8f, 19f, 68f, 68f));
                Txt(card, "Head", head, 26, _heading, Ink, TextAnchor.MiddleLeft, At(84f, 6f, 168f, 36f));
                detail = Txt(card, "Detail", "", 19, _font, Ink, TextAnchor.UpperLeft, At(84f, 42f, 168f, 60f));
                return Btn(card, id, "ON", 28, Teal, TealDark, At(256f, 16f, 76f, 74f), out state);
            }

            panel.fuelToggle = Rule("toggle_fuel", "FuelIcon", "FUEL", 8f, out panel.fuelState, out panel.fuelDetail);
            panel.pathToggle = Rule("toggle_path", "PathFadeIcon", "FLY PATH", 356f, out panel.pathState, out panel.pathDetail);
        }

        // ---------------------------------------------------------------- HUD

        static GameObject BuildHud()
        {
            var root = new GameObject("CockpitHUD");
            var hud = root.AddComponent<CurvedHud>();
            var canvas = CurvedPanel(root, "Canvas", 0.8f, -0.2f, new Vector2(2000f, 700f), 5, out var follow, out var poke);
            UnityEngine.Object.DestroyImmediate(poke);
            hud.follow = follow;

            var strip = Page(canvas, "Strip");
            hud.strip = strip.gameObject;
            Img(strip, "Plate", _rounded, HudPlate, Centre(0f, 0f, 1880f, 96f));
            var track = Img(strip, "ProgressTrack", _rounded, new Color(HudCream.r, HudCream.g, HudCream.b, 0.22f), Centre(0f, 0f, 960f, 20f));
            hud.progressTrack = track.rectTransform;
            var fill = Img(strip, "ProgressFill", _white, HudAccent, Centre(0f, 0f, 948f, 12f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            hud.progressFill = fill;
            hud.finishFlag = Img(strip, "FinishFlag", MiniPlanesUiSprites.Load("FlagBadge"), Color.white, Centre(512f, 0f, 62f, 62f)).rectTransform;
            hud.progressKnob = Img(strip, "PlaneKnob", MiniPlanesUiSprites.Load("PlaneBadge"), Color.white, Centre(-480f, 0f, 70f, 70f)).rectTransform;
            hud.score = Txt(strip, "Score", "0", 56, _heading, HudCream, TextAnchor.MiddleCenter, Centre(-800f, 0f, 200f, 90f));
            hud.level = Txt(strip, "Level", "LV 1", 52, _heading, HudCream, TextAnchor.MiddleCenter, Centre(800f, 0f, 220f, 90f));
            var hearts = new List<Image>();
            for (int i = 0; i < 5; i++)
                hearts.Add(Img(strip, "Life" + i, MiniPlanesUiSprites.Load("Heart"), Color.white, Centre(-660f + i * 52f, 0f, 46f, 46f)));
            hud.lives = hearts.ToArray();
            Img(strip, "CoinIcon", MiniPlanesUiSprites.Load("Coin"), Color.white, Centre(590f, 0f, 46f, 46f));
            hud.coins = Txt(strip, "Coins", "0", 44, _heading, new Color(1f, 0.82f, 0.36f, 1f), TextAnchor.MiddleLeft, Centre(680f, 0f, 120f, 80f));

            var caption = Img(canvas, "CaptionPlate", _rounded, HudPlate, Centre(0f, 236f, 1080f, 190f));
            hud.captionPlate = caption.gameObject;
            hud.caption = Txt(caption.rectTransform, "Caption", "", 38, _font, HudCream, TextAnchor.MiddleCenter, r =>
            {
                Stretch(r);
                r.offsetMin = new Vector2(30f, 10f);
                r.offsetMax = new Vector2(-30f, -10f);
            });
            return root;
        }

        // ---------------------------------------------------------------- pause

        /// <summary>The opening screen: the logo, and a Start button that fades in under it.</summary>
        static GameObject BuildSplash()
        {
            var root = new GameObject("SplashPanel");
            var splash = root.AddComponent<SplashPanel>();
            var canvas = CurvedPanel(root, "Canvas", 0.62f, 0f, new Vector2(1300f, 940f), 30, out var follow, out var poke);
            follow.unscaledTime = true;
            splash.follow = follow;
            splash.poke = poke;

            var logoGo = new GameObject("Logo", typeof(RectTransform));
            logoGo.transform.SetParent(canvas, false);
            Stretch((RectTransform)logoGo.transform);
            splash.logo = logoGo.AddComponent<CanvasGroup>();
            var logo = Img((RectTransform)logoGo.transform, "LogoImage", MiniPlanesUiSprites.Load("SkyLooperLogo"), Color.white, Centre(0f, 110f, 1240f, 620f));
            logo.preserveAspect = true;
            logo.raycastTarget = false;

            var start = Btn(canvas, SplashPanel.StartId, "START", 60, Teal, TealDark, Centre(0f, -330f, 440f, 150f), out _);
            splash.startButton = start.gameObject.AddComponent<CanvasGroup>();
            AddCursors(canvas, poke);
            return root;
        }

        static GameObject BuildPause()
        {
            var root = new GameObject("PausePanel");
            var pause = root.AddComponent<PausePanel>();
            var canvas = CurvedPanel(root, "Canvas", 0.46f, -0.05f, new Vector2(560f, 600f), 20, out var follow, out var poke);
            follow.unscaledTime = true;
            pause.follow = follow;
            pause.poke = poke;
            Img(canvas, "Plate", _rounded, new Color(0.07f, 0.17f, 0.17f, 0.85f), Stretch);
            Txt(canvas, "Title", "PAUSED", 44, _heading, HudCream, TextAnchor.MiddleCenter, At(0f, 36f, 560f, 64f));
            Btn(canvas, "resume", "RESUME", 40, Teal, TealDark, At(60f, 128f, 440f, 116f), out _);
            Btn(canvas, "restart", "RESTART", 40, Slate, SlateDark, At(60f, 264f, 440f, 116f), out _);
            Btn(canvas, "mainmenu", "MAIN MENU", 40, Coral, CoralDark, At(60f, 400f, 440f, 116f), out _);
            AddCursors(canvas, poke);
            return root;
        }

        // ---------------------------------------------------------------- small world UI

        static GameObject BuildWrist()
        {
            var root = new GameObject("WristMenuButton");
            var wrist = root.AddComponent<WristMenuButton>();
            var canvas = FlatCanvas(root.transform, "Button", new Vector2(140f, 56f), 30);
            wrist.button = canvas;
            wrist.fade = canvas.gameObject.AddComponent<CanvasGroup>();
            Img(canvas, "Background", _rounded, new Color(TealDark.r, TealDark.g, TealDark.b, 0.95f), Stretch);
            Txt(canvas, "Label", "MENU", 30, _heading, HudCream, TextAnchor.MiddleCenter, Stretch);
            return root;
        }

        static GameObject BuildIcon(string name, string sprite)
        {
            var root = new GameObject(name);
            root.AddComponent<Billboard>();
            var canvas = FlatCanvas(root.transform, "Canvas", new Vector2(56f, 56f), 6);
            Img(canvas, "Icon", MiniPlanesUiSprites.Load(sprite), Color.white, Stretch);
            return root;
        }

        static GameObject BuildCrosshair()
        {
            var root = new GameObject("Crosshair");
            root.AddComponent<Billboard>();
            var canvas = FlatCanvas(root.transform, "Canvas", new Vector2(110f, 110f), 6);
            canvas.gameObject.AddComponent<CanvasGroup>();
            Img(canvas, "Ring", MiniPlanesUiSprites.Load("Crosshair"), Color.white, Stretch);
            return root;
        }

        /// <summary>Two hands closing round a blinking bomb, animated by CupIconMotion.</summary>
        static GameObject BuildCupIcon()
        {
            var root = new GameObject("CupIcon");
            root.AddComponent<Billboard>();
            var canvas = FlatCanvas(root.transform, "Canvas", new Vector2(64f, 64f), 6);
            Img(canvas, "Badge", MiniPlanesUiSprites.Load("CupBadge"), Color.white, Stretch);
            var motion = root.AddComponent<CupIconMotion>();
            motion.bomb = Img(canvas, "Bomb", MiniPlanesUiSprites.Load("Circle"), motion.bombColor, Centre(0f, 0f, 16f, 16f));
            motion.leftHand = Img(canvas, "LeftHand", MiniPlanesUiSprites.Load("CupHand"), Color.white, Centre(-22f, 0f, 36f, 36f)).rectTransform;
            var right = Img(canvas, "RightHand", MiniPlanesUiSprites.Load("CupHand"), Color.white, Centre(22f, 0f, 36f, 36f)).rectTransform;
            right.localScale = new Vector3(-1f, 1f, 1f);
            motion.rightHand = right;
            return root;
        }

        static GameObject BuildDragCursor()
        {
            var root = new GameObject("DragCursor");
            root.AddComponent<Billboard>();
            var canvas = FlatCanvas(root.transform, "Canvas", new Vector2(90f, 90f), 6);
            canvas.gameObject.AddComponent<CanvasGroup>();
            Img(canvas, "Arrows", MiniPlanesUiSprites.Load("DragCursor"), Color.white, Stretch);
            return root;
        }

        static GameObject BuildFuelGauge()
        {
            var root = new GameObject("FuelGauge");
            var view = root.AddComponent<FuelGaugeView>();
            var canvas = FlatCanvas(root.transform, "Canvas", new Vector2(72f, 12f), 6);
            Img(canvas, "Track", _rounded, new Color(0.12f, 0.2f, 0.2f, 0.6f), Stretch);
            var fill = Img(canvas, "Fill", _white, new Color(0.36f, 0.85f, 0.72f, 1f), r =>
            {
                Stretch(r);
                r.offsetMin = new Vector2(3f, 3f);
                r.offsetMax = new Vector2(-3f, -3f);
            });
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            view.fill = fill;
            return root;
        }
    }
}
