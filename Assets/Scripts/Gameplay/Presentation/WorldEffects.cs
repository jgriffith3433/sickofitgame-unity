using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Core;
using SickOfIt.Core.Input;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The camera (static, follow, scroll, or travel, with shake), screen effects, and level audio for a scripted level.
    /// The camera's position is simulation state (saved for rewind); shake, flash, and slow motion are
    /// presentation and run on real time.
    /// </summary>
    public sealed class WorldEffects : MonoBehaviour
    {
        GameplayWorld world;
        AudioSource music;
        StageMusic stageMusic;
        WorldDef worldDef;
        Transform cameraTransform;
        float cameraZ;

        Vector2 travelOrigin;
        StageTrack track;
        FloorBackdrop backdrop;

        // A floor's path (world.path), expanded (generated sections filled from the Circle's chunks).
        readonly List<PathSectionDef> path = new List<PathSectionDef>();
        Vector2[] sectionStart = new Vector2[0];
        float[] sectionAt = new float[0];

        // The Static camera's starting view, where cameraReset goes back to.
        Vector2 homeBase;
        float homeSize;

        // ─── Saved state ──────────────────────────────────────────────
        Vector2 cameraBase;
        bool travelLocked;
        float cameraSize;
        bool cameraMoving;
        Vector2 moveFrom, moveTo;
        float sizeFrom, sizeTo;
        float moveStart, moveDuration;
        int moveEase;

        // Path (saved): the section the camera is in, how far along the whole path it is, an Arena released by
        // nextSection, and the last section whose spawns are out.
        int section;
        float pathDistance;
        bool sectionReleased;
        int spawnedThrough = -1;

        float shakeIntensity;
        float shakeEndsAt;
        float slowMotionEndsAt = -1f;
        bool movesCamera;

        public Vector2 CameraBase => cameraBase;
        public CameraMode Mode => worldDef != null ? worldDef.camera : CameraMode.Static;
        public StageTrack Track => track;
        public FloorBackdrop Backdrop => backdrop;

        /// <summary>The level follows a path of sections (world.path).</summary>
        public bool HasPath => Mode == CameraMode.Travel && path.Count > 0;
        /// <summary>The path's sections after generation (empty without a path).</summary>
        public IReadOnlyList<PathSectionDef> Path => path;
        /// <summary>Index of the section the camera is in (-1 without a path).</summary>
        public int SectionIndex => HasPath ? section : -1;
        public PathSectionDef CurrentSection => HasPath ? path[section] : null;
        /// <summary>Where the path starts (the camera's first position).</summary>
        public Vector2 PathOrigin => travelOrigin;
        /// <summary>Where a section starts, in world coordinates.</summary>
        public Vector2 SectionStart(int index) => index >= 0 && index < sectionStart.Length ? sectionStart[index] : travelOrigin;

        Vector2 TravelAxis => worldDef.travelDirection == TravelDirection.Up ? Vector2.up : Vector2.right;

        /// <summary>Travel stages: how far the camera has gone from where it started.</summary>
        public float TravelDistance => Mode != CameraMode.Travel ? 0f
            : HasPath ? pathDistance : Mathf.Max(0f, Vector2.Dot(cameraBase - travelOrigin, TravelAxis));

        /// <summary>Travel stages: the camera doesn't move forward (lockTravel / unlockTravel).</summary>
        public bool TravelLocked
        {
            get => travelLocked;
            set => travelLocked = value;
        }

        public void Init(GameplayWorld owner, AudioSource musicSource, WorldDef def, Transform backgroundRoot)
        {
            world = owner;
            music = musicSource;
            worldDef = def;
            if (music != null)
            {
                stageMusic = gameObject.AddComponent<StageMusic>();
                stageMusic.Init(owner, music);
            }
            if (world.Camera != null)
            {
                cameraTransform = world.Camera.transform;
                cameraBase = cameraTransform.position;
                cameraZ = cameraTransform.position.z;
            }
            travelOrigin = cameraBase;
            homeBase = cameraBase;
            homeSize = cameraSize = world.Camera != null && world.Camera.orthographic ? world.Camera.orthographicSize : 0f;

            if (Mode != CameraMode.Static && cameraTransform != null)
            {
                // Backgrounds are full-screen videos; keep them on screen as the camera moves.
                if (backgroundRoot != null)
                    backgroundRoot.SetParent(cameraTransform, true);
                BuildMovingBarriers();
            }
        }

        /// <summary>Travel stages: builds the streamed ground, platforms and scenery from the stage's circle.</summary>
        public void BuildTrack(LevelScript script, CircleDef circle)
        {
            if (Mode != CameraMode.Travel || cameraTransform == null)
                return;
            if (worldDef.HasPath)
            {
                BuildPath(circle, script.floors.start);
                if (circle != null && circle.sky != null && circle.sky.enabled)
                    backdrop = FloorBackdrop.Create(world, circle.sky, travelOrigin);
                // The first two sections are there before the level starts: the hero lands on the opening ledge.
                SpawnThrough(Mathf.Min(1, path.Count - 1), false);
                if (path.Count > 0)
                    SpawnSection(0, onEnter: true);
                return;
            }
            track = StageTrack.Create(world, script, circle, travelOrigin);
            track.Refresh(cameraBase);
        }

        // ─── Path ─────────────────────────────────────────────────────

        /// <summary>Expands generated sections from the Circle's chunks (seeded) and lays the sections end to end.</summary>
        void BuildPath(CircleDef circle, int floor)
        {
            path.Clear();
            for (int i = 0; i < worldDef.path.Count; i++)
            {
                var def = worldDef.path[i];
                if (def == null)
                    continue;
                if (def.generate <= 0)
                {
                    path.Add(def);
                    continue;
                }
                var random = DeterministicRandom.Derive(world.Seed ^ (uint)Mathf.Max(0, floor), 0xF100Du + (uint)i);
                for (int n = 0; n < def.generate; n++)
                {
                    var chunk = PickChunk(circle, floor, ref random);
                    if (chunk == null)
                    {
                        Debug.LogWarning($"[WorldEffects] Path section {i} asks for generated chunks, but the Circle has none for floor {floor}.");
                        break;
                    }
                    var copy = chunk.section.Copy();
                    if (string.IsNullOrEmpty(copy.name))
                        copy.name = string.IsNullOrEmpty(chunk.name) ? "chunk" : chunk.name;
                    copy.generate = 0;
                    path.Add(copy);
                    world.Telemetry.Record("chunk", copy.name, path.Count - 1);
                }
            }

            sectionStart = new Vector2[path.Count];
            sectionAt = new float[path.Count];
            Vector2 at = travelOrigin;
            float distance = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                sectionStart[i] = at;
                sectionAt[i] = distance;
                at += path[i].Axis * path[i].Length;
                distance += path[i].Length;
            }
            section = 0;
            pathDistance = 0f;
            sectionReleased = false;
            spawnedThrough = -1;
            if (path.Count > 0)
                world.Telemetry.Record("section", path[0].name, 0, path[0].direction.ToString());
        }

        static PathChunkDef PickChunk(CircleDef circle, int floor, ref DeterministicRandom random)
        {
            if (circle == null || circle.chunks == null)
                return null;
            float total = 0f;
            foreach (var c in circle.chunks)
            {
                if (c != null && c.section != null && c.weight > 0f && (c.fromFloor <= 0 || floor <= c.fromFloor))
                    total += c.weight;
            }
            if (total <= 0f)
                return null;
            float roll = random.Value() * total;
            foreach (var c in circle.chunks)
            {
                if (c == null || c.section == null || c.weight <= 0f || (c.fromFloor > 0 && floor > c.fromFloor))
                    continue;
                roll -= c.weight;
                if (roll <= 0f)
                    return c;
            }
            return circle.chunks[circle.chunks.Count - 1];
        }

        /// <summary>The camera has reached the named section (or one after it).</summary>
        public bool SectionReached(string name)
        {
            if (!HasPath || string.IsNullOrEmpty(name))
                return false;
            for (int i = 0; i <= section && i < path.Count; i++)
            {
                if (path[i].name == name)
                    return true;
            }
            return false;
        }

        /// <summary>nextSection: an Arena section lets the camera go on.</summary>
        public void ReleaseSection()
        {
            if (HasPath)
                sectionReleased = true;
        }

        /// <summary>Puts out the spawns of every section up to 'last' (each section once).</summary>
        void SpawnThrough(int last, bool entered)
        {
            for (int i = spawnedThrough + 1; i <= last && i < path.Count; i++)
            {
                SpawnSection(i, onEnter: false);
                spawnedThrough = i;
            }
            if (entered)
                SpawnSection(section, onEnter: true);
        }

        void SpawnSection(int index, bool onEnter)
        {
            var def = path[index];
            if (def.spawns == null)
                return;
            foreach (var s in def.spawns)
            {
                if (s == null || s.onEnter != onEnter)
                    continue;
                Vector2 at = s.anchor == PathAnchor.World ? s.at
                    : s.anchor == PathAnchor.Section ? sectionStart[index] + s.at
                    : travelOrigin + s.at;
                var options = new SpawnOptions { InstanceId = s.id ?? "", Facing = s.facing, FacePlayer = s.facing == 0f };
                if (s.tags != null)
                    options.Tags.AddRange(s.tags);
                var entity = world.Spawn(s.entity, at, options);
                if (entity != null && s.scale != Vector2.one && s.scale.x > 0f && s.scale.y > 0f)
                {
                    var t = entity.transform;
                    t.localScale = new Vector3(t.localScale.x * s.scale.x, t.localScale.y * s.scale.y, t.localScale.z);
                    // Abilities that remember where they started read it at spawn; the scale only changes the size.
                    Physics2D.SyncTransforms();
                }
            }
        }

        /// <summary>The camera's point at 'distance' along the path, with the turns between sections rounded off.</summary>
        Vector2 PathPoint(float distance)
        {
            for (int c = 1; c < path.Count; c++)
            {
                float r = Mathf.Min(Mathf.Max(0f, path[c - 1].turn), path[c - 1].Length * 0.5f, path[c].Length * 0.5f);
                float corner = sectionAt[c];
                if (r > 0.01f && Mathf.Abs(distance - corner) < r && path[c - 1].Axis != path[c].Axis)
                {
                    Vector2 a = StraightPoint(corner - r);
                    Vector2 b = StraightPoint(corner + r);
                    Vector2 k = sectionStart[c];
                    float t = (distance - (corner - r)) / (2f * r);
                    float u = 1f - t;
                    return u * u * a + 2f * u * t * k + t * t * b;
                }
            }
            return StraightPoint(distance);
        }

        Vector2 StraightPoint(float distance)
        {
            int k = path.Count - 1;
            for (int i = 0; i < path.Count; i++)
            {
                if (distance < sectionAt[i] + path[i].Length || i == path.Count - 1)
                {
                    k = i;
                    break;
                }
            }
            return sectionStart[k] + path[k].Axis * Mathf.Clamp(distance - sectionAt[k], 0f, path[k].Length);
        }

        /// <summary>Forward only along the current section: follow the hero with a look-ahead, at least at autoSpeed, unless held.</summary>
        Vector2 PathStep(float dt)
        {
            var def = path[section];
            float start = sectionAt[section];
            float end = start + def.Length;
            bool arena = def.direction == PathDirection.Arena;
            bool held = travelLocked || (worldDef.holdForPushes && world.Director != null && world.Director.GeneratorHoldsCamera)
                        || (arena && !sectionReleased);
            if (!held && !arena)
            {
                float target = pathDistance;
                if (world.Player != null)
                {
                    float lead = def.lead >= 0f ? def.lead : worldDef.travelLead;
                    float wanted = start + Vector2.Dot(world.Player.Position - sectionStart[section], def.Axis) + lead;
                    float t = 1f - Mathf.Exp(-Mathf.Max(0.1f, worldDef.followSharpness) * dt);
                    target = Mathf.Max(target, Mathf.Lerp(pathDistance, Mathf.Min(wanted, end), t));
                }
                target = Mathf.Max(target, pathDistance + Mathf.Max(0f, def.autoSpeed) * dt);
                pathDistance = Mathf.Min(target, end);
            }

            bool done = arena ? sectionReleased : pathDistance >= end - 0.0001f;
            if (done && section < path.Count - 1)
            {
                section++;
                sectionReleased = false;
                var next = path[section];
                world.Emit("section:" + next.name);
                world.Telemetry.Record("section", next.name, section, next.direction.ToString());
                SpawnThrough(Mathf.Min(section + 1, path.Count - 1), true);
            }
            return PathPoint(pathDistance);
        }

        const float PitDepth = 4f;

        /// <summary>
        /// Below the screen of a path floor is the lava: a hero who has dropped out of the bottom of the view is burned and
        /// thrown back up, like the lava sea does where it is. Stateless (the hit invincibility spaces the burns), so a rewind
        /// needs nothing saved.
        /// </summary>
        void PitCatch()
        {
            var player = world.Player;
            if (player == null || !player.IsAlive || player.Body == null || world.Camera == null || !world.Camera.orthographic)
                return;
            var feet = world.PlayerController != null ? world.PlayerController.BodyCollider : null;
            if (feet == null || world.PlayerController.IsHeld)
                return;
            float bottom = world.CameraCenter.y - world.Camera.orthographicSize;
            if (feet.bounds.max.y > bottom)
                return;
            var v = player.Body.linearVelocity;
            v.y = Mathf.Max(v.y, 15f);
            player.Body.linearVelocity = v;
            if (player.DamageAs(1, "pit"))
                world.Telemetry.Record("pit", "lava", 1);
        }

        void BuildMovingBarriers()
        {
            if (world.Camera == null || !world.Camera.orthographic)
                return;
            float h = world.Camera.orthographicSize;
            float w = h * world.Camera.aspect;
            int layer = LayerMask.NameToLayer("Boundary");
            if (Mode == CameraMode.Travel)
            {
                // Edges ride with the camera: no going back, no running ahead of it. A climb also gets a floor that
                // rises with the camera, so falling off the bottom of the screen just lands you on its edge.
                Barrier(cameraTransform, "TravelEdge_Left", new Vector2(-w - 0.5f, 0f), new Vector2(1f, h * 4f), layer);
                Barrier(cameraTransform, "TravelEdge_Right", new Vector2(w + 0.5f, 0f), new Vector2(1f, h * 4f), layer);
                // A path floor has no floor under the screen: what drops off the bottom falls into the lava (PitCatch burns
                // him and throws him back up), so its safety net sits well below the screen, never a hidden ledge on it.
                if (worldDef.HasPath)
                    Barrier(cameraTransform, "TravelEdge_Bottom", new Vector2(0f, -h - PitDepth - 0.5f), new Vector2(w * 4f, 1f), layer);
                else if (worldDef.travelDirection == TravelDirection.Up)
                    Barrier(cameraTransform, "TravelEdge_Bottom", new Vector2(0f, -h - 0.5f), new Vector2(w * 4f, 1f), layer);
            }
            else if (Mode == CameraMode.Scroll)
            {
                // Edges travel with the camera, so the player can't fall behind or run ahead of the screen.
                Barrier(cameraTransform, "ScrollEdge_Left", new Vector2(-w - 0.5f, 0f), new Vector2(1f, h * 4f), layer);
                Barrier(cameraTransform, "ScrollEdge_Right", new Vector2(w + 0.5f, 0f), new Vector2(1f, h * 4f), layer);
            }
            else if (worldDef.boundsSize.x > 0f && worldDef.boundsSize.y > 0f)
            {
                var root = new GameObject("WorldBounds").transform;
                root.SetParent(transform, false);
                Vector2 c = worldDef.boundsCenter;
                Vector2 s = worldDef.boundsSize;
                Barrier(root, "Bound_Left", c + new Vector2(-s.x * 0.5f - 0.5f, 0f), new Vector2(1f, s.y + 2f), layer);
                Barrier(root, "Bound_Right", c + new Vector2(s.x * 0.5f + 0.5f, 0f), new Vector2(1f, s.y + 2f), layer);
                Barrier(root, "Bound_Top", c + new Vector2(0f, s.y * 0.5f + 0.5f), new Vector2(s.x + 2f, 1f), layer);
                Barrier(root, "Bound_Bottom", c + new Vector2(0f, -s.y * 0.5f - 0.5f), new Vector2(s.x + 2f, 1f), layer);
            }
        }

        static void Barrier(Transform parent, string name, Vector2 localPosition, Vector2 size, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            if (layer >= 0)
                go.layer = layer;
            go.AddComponent<BoxCollider2D>().size = size;
        }

        // ─── Simulation ───────────────────────────────────────────────

        public void Tick(float now, float dt)
        {
            if (cameraTransform != null && cameraMoving)
                TickCameraMove(now);
            if (cameraTransform == null || Mode == CameraMode.Static)
                return;

            Vector2 next = cameraBase;
            if (Mode == CameraMode.Travel)
            {
                if (HasPath)
                {
                    cameraBase = PathStep(dt);
                    movesCamera = true;
                    PitCatch();
                    return;
                }
                cameraBase = TravelStep(dt);
                movesCamera = true;
                track?.Refresh(cameraBase);
                return;
            }
            if (Mode == CameraMode.Scroll)
            {
                next += worldDef.scrollSpeed * dt;
            }
            else if (world.Player != null)
            {
                float t = 1f - Mathf.Exp(-Mathf.Max(0.1f, worldDef.followSharpness) * dt);
                next = Vector2.Lerp(cameraBase, world.Player.Position, t);
            }
            cameraBase = ClampToBounds(next);
            movesCamera = true;
        }

        /// <summary>Forward only: follow the player (with a lead), at least at travelSpeed, unless held.</summary>
        Vector2 TravelStep(float dt)
        {
            Vector2 axis = TravelAxis;
            float current = Vector2.Dot(cameraBase - travelOrigin, axis);
            float target = current;
            if (world.Player != null)
            {
                float wanted = Vector2.Dot(world.Player.Position - travelOrigin, axis) + worldDef.travelLead;
                float t = 1f - Mathf.Exp(-Mathf.Max(0.1f, worldDef.followSharpness) * dt);
                target = Mathf.Max(current, Mathf.Lerp(current, wanted, t));
            }
            target = Mathf.Max(target, current + Mathf.Max(0f, worldDef.travelSpeed) * dt);
            bool held = travelLocked || (worldDef.holdForPushes && world.Director != null && world.Director.GeneratorHoldsCamera);
            if (held)
                target = current;
            if (worldDef.travelLength > 0f)
                target = Mathf.Min(target, Mathf.Max(current, worldDef.travelLength));
            return travelOrigin + axis * target;
        }

        // ─── Scripted camera (Static camera only): cameraTo, cameraZoom, cameraReset ───

        public float CameraSize => cameraSize;
        public float HomeSize => homeSize;
        public bool CameraMoving => cameraMoving;

        /// <summary>
        /// Moves the Static camera to 'center' and zooms it to 'size' (orthographic half-height; 0 keeps the current
        /// one) over 'duration' seconds. Returns false (and does nothing) for the other camera modes.
        /// </summary>
        public bool MoveCamera(Vector2 center, float size, float duration, Ease ease, float now)
        {
            if (cameraTransform == null || Mode != CameraMode.Static)
                return false;
            moveFrom = cameraBase;
            moveTo = center;
            sizeFrom = cameraSize;
            sizeTo = size > 0f ? size : cameraSize;
            moveStart = now;
            moveDuration = Mathf.Max(0f, duration);
            moveEase = (int)ease;
            cameraMoving = true;
            world.Telemetry.Record("cutscene", "camera", Mathf.RoundToInt(sizeTo * 100f), $"{center.x:0.##},{center.y:0.##}");
            TickCameraMove(now);
            return true;
        }

        /// <summary>Back to the level's starting view.</summary>
        public bool ResetCamera(float duration, Ease ease, float now) => MoveCamera(homeBase, homeSize, duration, ease, now);

        void TickCameraMove(float now)
        {
            float t = moveDuration > 0f ? Mathf.Clamp01((now - moveStart) / moveDuration) : 1f;
            float k = Easing.Apply((Ease)moveEase, t);
            cameraBase = Vector2.Lerp(moveFrom, moveTo, k);
            cameraSize = Mathf.Lerp(sizeFrom, sizeTo, k);
            ApplyCameraSize();
            movesCamera = true;
            if (t >= 1f)
                cameraMoving = false;
        }

        void ApplyCameraSize()
        {
            if (world.Camera != null && world.Camera.orthographic && cameraSize > 0f)
                world.Camera.orthographicSize = cameraSize;
        }

        Vector2 ClampToBounds(Vector2 center)
        {
            if (worldDef.boundsSize.x <= 0f || worldDef.boundsSize.y <= 0f || world.Camera == null)
                return center;
            float h = world.Camera.orthographicSize;
            float w = h * world.Camera.aspect;
            Vector2 min = worldDef.boundsCenter - worldDef.boundsSize * 0.5f + new Vector2(w, h);
            Vector2 max = worldDef.boundsCenter + worldDef.boundsSize * 0.5f - new Vector2(w, h);
            return new Vector2(
                min.x > max.x ? worldDef.boundsCenter.x : Mathf.Clamp(center.x, min.x, max.x),
                min.y > max.y ? worldDef.boundsCenter.y : Mathf.Clamp(center.y, min.y, max.y));
        }

        void LateUpdate()
        {
            if (slowMotionEndsAt >= 0f && Time.unscaledTime >= slowMotionEndsAt)
            {
                slowMotionEndsAt = -1f;
                var clock = GameClock.Instance;
                if (clock != null && !clock.IsRewinding)
                    clock.SetTimeScale(1f);
            }

            if (cameraTransform == null)
                return;
            bool shaking = Time.unscaledTime < shakeEndsAt;
            if (!shaking && !movesCamera)
                return;
            Vector2 offset = shaking ? Random.insideUnitCircle * shakeIntensity : Vector2.zero;
            Vector2 p = cameraBase + offset;
            cameraTransform.position = new Vector3(p.x, p.y, cameraZ);
            if (!shaking && Mode == CameraMode.Static)
                movesCamera = false;
        }

        // ─── Effects ──────────────────────────────────────────────────

        public void Shake(float intensity, float duration)
        {
            if (!SickOfIt.Data.GameSettings.ScreenShake)
                return;
            shakeIntensity = SickOfIt.Data.GameSettings.ReducedShake ? intensity * 0.3f : intensity;
            shakeEndsAt = Time.unscaledTime + duration;
            movesCamera = true;
            LastShakeIntensity = shakeIntensity;
        }

        /// <summary>The strength of the last shake started, after the accessibility setting (the playtest reads it).</summary>
        public float LastShakeIntensity { get; private set; }

        public void Flash(Color color, float duration) => world.Hud.Flash(color, duration);

        /// <summary>Floats a short text up from a world point and fades it (cosmetic, real time).</summary>
        public void PopText(Vector2 at, string text, Color color, float size = 1f)
        {
            if (string.IsNullOrEmpty(text))
                return;
            var theme = SickOfIt.Data.GameDefinition.Instance != null ? SickOfIt.Data.GameDefinition.Instance.menuTheme : null;
            var go = new GameObject("PopText");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, at.y, -0.5f);
            var mesh = go.AddComponent<TMPro.TextMeshPro>();
            // Same font and ink outline as the menus and HUD.
            mesh.font = SickOfIt.Core.UI.Menus.MenuGraphics.FontAsset(theme != null ? theme.font : null);
            mesh.text = text;
            mesh.alignment = TMPro.TextAlignmentOptions.Center;
            mesh.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            mesh.fontSize = 3.2f * Mathf.Max(0.2f, size);
            mesh.fontStyle = TMPro.FontStyles.Bold | TMPro.FontStyles.UpperCase;
            mesh.color = color;
            mesh.outlineWidth = 0.28f;
            mesh.outlineColor = theme != null ? (Color32)theme.title.outlineColor : new Color32(30, 18, 32, 255);
            mesh.rectTransform.sizeDelta = new Vector2(8f, 2f);
            mesh.sortingLayerID = SortingLayer.NameToID(SickOfIt.Data.SpriteLayer.Player.ToString());
            mesh.sortingOrder = 500;
            go.AddComponent<PopTextMotion>().Begin(mesh);
        }

        sealed class PopTextMotion : MonoBehaviour
        {
            const float Duration = 0.9f;
            TMPro.TextMeshPro mesh;
            Color start;
            float age;
            float tilt;

            public void Begin(TMPro.TextMeshPro text)
            {
                mesh = text;
                start = text.color;
                tilt = (Random.value - 0.5f) * 16f;
            }

            void Update()
            {
                age += Time.unscaledDeltaTime;
                transform.position += Vector3.up * (0.9f * Time.unscaledDeltaTime);
                // Pop in big, settle, then fade.
                float pop = age < 0.12f ? Mathf.Lerp(0.4f, 1.25f, age / 0.12f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((age - 0.12f) / 0.15f));
                transform.localScale = Vector3.one * pop;
                transform.localRotation = Quaternion.Euler(0f, 0f, tilt * Mathf.Clamp01(1f - age * 2f));
                var c = start;
                c.a = start.a * Mathf.Clamp01((Duration - age) / 0.35f);
                mesh.color = c;
                if (age >= Duration)
                    Destroy(gameObject);
            }
        }

        public void SlowMotion(float scale, float duration)
        {
            var clock = GameClock.Instance;
            if (clock == null || clock.IsRewinding)
                return;
            clock.SetTimeScale(Mathf.Clamp(scale, 0.05f, 4f));
            slowMotionEndsAt = Time.unscaledTime + duration;
        }

        public void PlaySound(AudioClip clip, float volume)
        {
            if (clip == null)
                return;
            Vector3 at = cameraTransform != null ? cameraTransform.position : Vector3.zero;
            AudioSource.PlayClipAtPoint(clip, at, volume);
        }

        public void PlayMusic(AudioClip clip, float volume)
        {
            if (music == null || clip == null)
                return;
            stageMusic.Play(clip, volume);
        }

        public void MusicVolume(float volume)
        {
            if (stageMusic != null)
                stageMusic.SetVolume(volume);
        }

        // ─── Rewind ───────────────────────────────────────────────────

        public void SaveState(StateWriter w)
        {
            w.Float(cameraBase.x);
            w.Float(cameraBase.y);
            w.Bool(travelLocked);
            w.Int(section);
            w.Float(pathDistance);
            w.Bool(sectionReleased);
            w.Int(spawnedThrough);
            w.Float(cameraSize);
            w.Bool(cameraMoving);
            w.Float(moveFrom.x); w.Float(moveFrom.y);
            w.Float(moveTo.x); w.Float(moveTo.y);
            w.Float(sizeFrom); w.Float(sizeTo);
            w.Float(moveStart); w.Float(moveDuration);
            w.Int(moveEase);
            if (stageMusic != null)
                stageMusic.SaveState(w);
        }

        public void LoadState(StateReader r)
        {
            cameraBase = new Vector2(r.Float(), r.Float());
            travelLocked = r.Bool();
            section = r.Int();
            pathDistance = r.Float();
            sectionReleased = r.Bool();
            spawnedThrough = r.Int();
            cameraSize = r.Float();
            cameraMoving = r.Bool();
            moveFrom = new Vector2(r.Float(), r.Float());
            moveTo = new Vector2(r.Float(), r.Float());
            sizeFrom = r.Float(); sizeTo = r.Float();
            moveStart = r.Float(); moveDuration = r.Float();
            moveEase = r.Int();
            if (stageMusic != null)
                stageMusic.LoadState(r);
            ApplyCameraSize();
            // The Static camera only moves for a script (cameraTo); put it back wherever that left it.
            if (cameraTransform != null)
                cameraTransform.position = new Vector3(cameraBase.x, cameraBase.y, cameraZ);
            track?.Refresh(cameraBase);
        }
    }
}
