using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RiftForge
{
    /// <summary>Shared colour language: cyan = player/rift, gold = forge/reward, red = danger.</summary>
    public static class Palette
    {
        public static readonly Color Cyan = new Color(0.32f, 0.85f, 0.66f);
        public static readonly Color Gold = new Color(1f, 0.64f, 0.32f);
        public static readonly Color Violet = new Color(0.66f, 0.48f, 1f);
        public static readonly Color Pink = new Color(1f, 0.38f, 0.72f);
        public static readonly Color Danger = new Color(1f, 0.27f, 0.2f);
        public static readonly Color White = new Color(0.94f, 0.97f, 1f);
    }

    public sealed partial class RiftGame
    {
        public const float ArenaRadius = 8.35f;
        private const float PlatformRadius = 9.6f;

        private readonly List<Material> runtimeMaterials = new List<Material>();
        private readonly List<Transform> floaters = new List<Transform>();
        private readonly List<Vector3> floaterBases = new List<Vector3>();
        private readonly List<Transform> flames = new List<Transform>();
        private readonly List<Light> flameLights = new List<Light>();
        private readonly List<Transform> runeCrystals = new List<Transform>();
        private Material baseMaterial;
        private Material glowBaseMaterial;
        private Material floorMaterial;
        private Material floorInnerMaterial;
        private Material edgeMaterial;
        private Material crackMaterial;
        private Material cyanMaterial;
        private Material goldMaterial;
        private Material pinkMaterial;
        private Material violetGlowMaterial;
        private Material goldTrimMaterial;
        private Material portalMaterial;
        private Material enemyMaterial;
        private Material enemyDarkMaterial;
        private Material lancerMaterial;
        private Material bossMaterial;
        private Material warningMaterial;
        private Material warningFillMaterial;
        private Material darkMaterial;
        private Material armorMaterial;
        private Material capeMaterial;
        private Material eyeMaterial;
        private Material flashMaterial;
        private Material hurtMaterial;
        private Material fireMaterial;
        private Material voidMaterial;
        private Transform worldRoot;
        private Transform cameraAnchor;
        private Transform player;
        private Transform playerRig;
        private RiftCharacter playerCharacter;
        private Transform swordPivot;
        private Transform blade;
        private Transform chestPlate;
        private Transform cape;
        private Transform legLeft;
        private Transform legRight;
        private Transform[] boots;
        private Renderer[] playerRenderers;
        private Material[] playerMaterials;
        private Transform portalRing;
        private Transform portalCore;
        private Light portalLight;
        private Volume postVolume;
        private VolumeProfile postProfile;
        private Vignette vignette;
        private ChromaticAberration chromatic;
        private float chromaticKick;
        private float walkPhase;
        private Vector3 lastPlayerPosition;
        private StrikeKind swingKind;
        private float swingClock = 10f;
        private float swingWindup = 0.06f;
        private float titleYaw;

        private void CreateMaterials()
        {
            // Resources materials keep their URP shader variants in native players.
            baseMaterial = Resources.Load<Material>("RiftURPLit");
            if (baseMaterial == null || baseMaterial.shader == null)
                throw new System.InvalidOperationException("RiftURPLit material resource is missing.");
            glowBaseMaterial = Resources.Load<Material>("RiftURPLitGlow");
            if (glowBaseMaterial == null) glowBaseMaterial = baseMaterial;

            floorMaterial = NewMaterial(new Color(0.11f, 0.13f, 0.19f), 0f, 0.25f);
            floorInnerMaterial = NewMaterial(new Color(0.16f, 0.19f, 0.26f), 0f, 0.4f);
            edgeMaterial = NewMaterial(new Color(0.22f, 0.26f, 0.33f), 0f, 0.3f);
            crackMaterial = NewMaterial(new Color(0.15f, 0.75f, 0.95f), 1.3f);
            cyanMaterial = NewMaterial(Palette.Cyan, 1.6f);
            goldMaterial = NewMaterial(Palette.Gold, 1.5f);
            goldTrimMaterial = NewMaterial(Palette.Gold * 0.85f, 0.75f, 0.5f, 0.6f);
            pinkMaterial = NewMaterial(Palette.Pink, 1.6f);
            violetGlowMaterial = NewMaterial(Palette.Violet, 1.3f);
            portalMaterial = NewMaterial(new Color(0.42f, 0.25f, 0.9f), 0.9f);
            enemyMaterial = NewMaterial(new Color(0.42f, 0.24f, 0.6f), 0f, 0.45f);
            enemyDarkMaterial = NewMaterial(new Color(0.12f, 0.08f, 0.18f), 0f, 0.3f);
            lancerMaterial = NewMaterial(new Color(0.58f, 0.2f, 0.45f), 0f, 0.5f);
            bossMaterial = NewMaterial(new Color(0.6f, 0.15f, 0.22f), 0f, 0.45f);
            warningMaterial = NewMaterial(Palette.Danger, 1.9f);
            warningFillMaterial = NewMaterial(new Color(0.3f, 0.03f, 0.03f), 0.6f);
            darkMaterial = NewMaterial(new Color(0.05f, 0.07f, 0.11f), 0f, 0.3f);
            armorMaterial = NewMaterial(new Color(0.58f, 0.65f, 0.76f), 0f, 0.7f, 0.55f);
            capeMaterial = NewMaterial(new Color(0.14f, 0.36f, 0.62f), 0f, 0.2f);
            eyeMaterial = NewMaterial(new Color(1f, 0.32f, 0.22f), 2.4f);
            flashMaterial = NewMaterial(Color.white, 1.6f);
            hurtMaterial = NewMaterial(Palette.Danger, 1.8f);
            fireMaterial = NewMaterial(new Color(1f, 0.45f, 0.12f), 2.2f);
            voidMaterial = NewMaterial(new Color(0.04f, 0.03f, 0.08f), 0.15f);
        }

        private Material NewMaterial(Color color, float glow = 0f, float smoothness = 0.35f, float metallic = 0f)
        {
            Material material = new Material(glow > 0f ? glowBaseMaterial : baseMaterial);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_EmissionColor"))
            {
                if (glow > 0f)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * glow);
                }
                else
                {
                    material.DisableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.black);
                }
            }
            runtimeMaterials.Add(material);
            return material;
        }

        private void CreateWorld()
        {
            if (Application.isMobilePlatform)
            {
                Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = true;
                Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
                Screen.orientation = ScreenOrientation.AutoRotation;
            }
            Application.targetFrameRate = 60;
            worldRoot = new GameObject("Arena").transform;
            worldRoot.SetParent(transform, false);

            // Abyss under the floating platform gives depth instead of a flat background.
            MakePrimitive(PrimitiveType.Cylinder, "Abyss", worldRoot, new Vector3(0f, -7f, 4f),
                new Vector3(70f, 0.1f, 70f), voidMaterial);
            // Two-tier basalt platform. A cylinder primitive is 1 unit wide, so scale is the diameter.
            MakePrimitive(PrimitiveType.Cylinder, "Basalt Platform", worldRoot,
                new Vector3(0f, -0.62f, 0f), new Vector3(PlatformRadius * 2f, 0.6f, PlatformRadius * 2f), floorMaterial);
            MakePrimitive(PrimitiveType.Cylinder, "Platform Underside", worldRoot,
                new Vector3(0f, -1.6f, 0f), new Vector3(PlatformRadius * 1.7f, 0.9f, PlatformRadius * 1.7f), darkMaterial);
            MakePrimitive(PrimitiveType.Cylinder, "Rift Seam", worldRoot,
                new Vector3(0f, -0.3f, 0f), new Vector3(11.6f, 0.31f, 11.6f), crackMaterial);
            MakePrimitive(PrimitiveType.Cylinder, "Duel Circle", worldRoot,
                new Vector3(0f, -0.28f, 0f), new Vector3(11.2f, 0.3f, 11.2f), floorInnerMaterial);
            MakePrimitive(PrimitiveType.Cylinder, "Forge Sigil", worldRoot,
                new Vector3(0f, -0.27f, 0f), new Vector3(3.2f, 0.3f, 3.2f), floorMaterial);
            for (int i = 0; i < 6; i++)
            {
                GameObject spoke = MakePrimitive(PrimitiveType.Cube, "Sigil Spoke", worldRoot,
                    new Vector3(0f, 0.035f, 0f), new Vector3(0.06f, 0.02f, 3.1f), goldTrimMaterial);
                spoke.transform.localRotation = Quaternion.Euler(0f, i * 30f, 0f);
                NoShadow(spoke);
            }
            CreateCracks();

            for (int i = 0; i < 28; i++)
            {
                float angle = i * Mathf.PI * 2f / 28;
                Vector3 p = new Vector3(Mathf.Cos(angle) * (PlatformRadius - 0.15f), 0.05f, Mathf.Sin(angle) * (PlatformRadius - 0.15f));
                bool rune = i % 4 == 0;
                GameObject segment = MakePrimitive(PrimitiveType.Cube, "Arena Rim", worldRoot,
                    p, new Vector3(2.05f, rune ? 0.42f : 0.32f, 0.42f), rune ? goldTrimMaterial : edgeMaterial);
                segment.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg - 90f, 0f);
            }
            for (int i = 0; i < 8; i++)
            {
                float angle = (i + 0.5f) * Mathf.PI * 2f / 8;
                Vector3 p = new Vector3(Mathf.Cos(angle) * 9.05f, 0f, Mathf.Sin(angle) * 9.05f);
                if (i % 2 == 0) CreateBrazier(p);
                else
                {
                    GameObject spire = MakePrimitive(PrimitiveType.Cube, "Rune Spire", worldRoot,
                        p + Vector3.up * 1.05f, new Vector3(0.42f, 2.1f, 0.42f), edgeMaterial);
                    spire.transform.rotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
                    GameObject crystal = MakePrimitive(PrimitiveType.Cube, "Rune Crystal", worldRoot,
                        p + Vector3.up * 2.45f, new Vector3(0.3f, 0.3f, 0.3f), cyanMaterial);
                    crystal.transform.rotation = Quaternion.Euler(45f, 0f, 45f);
                    NoShadow(crystal);
                    runeCrystals.Add(crystal.transform);
                }
            }
            CreateFloatingRocks();
            CreatePortal();
            CreatePlayer();
            CreateCameraAndLight();
        }

        private void CreateCracks()
        {
            var random = new System.Random(4242);
            for (int i = 0; i < 18; i++)
            {
                float angle = i * 360f / 18f + (float)random.NextDouble() * 8f;
                float radius = 5.9f;
                for (int s = 0; s < 3; s++)
                {
                    float length = 0.7f + (float)random.NextDouble() * 0.9f;
                    float bend = angle + ((float)random.NextDouble() - 0.5f) * 30f;
                    Vector3 direction = Quaternion.Euler(0f, bend, 0f) * Vector3.forward;
                    Vector3 start = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                    if (radius + length > PlatformRadius - 0.6f) break;
                    GameObject crack = MakePrimitive(PrimitiveType.Cube, "Rift Crack", worldRoot,
                        new Vector3(start.x, 0.012f, start.z) + direction * (length * 0.5f),
                        new Vector3(0.07f - s * 0.015f, 0.02f, length), crackMaterial);
                    crack.transform.localRotation = Quaternion.LookRotation(direction);
                    NoShadow(crack);
                    radius += length * 0.9f;
                    angle = bend;
                }
            }
        }

        private void CreateBrazier(Vector3 p)
        {
            MakePrimitive(PrimitiveType.Cylinder, "Brazier Stand", worldRoot, p + Vector3.up * 0.45f,
                new Vector3(0.38f, 0.45f, 0.38f), edgeMaterial);
            MakePrimitive(PrimitiveType.Cylinder, "Brazier Bowl", worldRoot, p + Vector3.up * 0.95f,
                new Vector3(0.8f, 0.1f, 0.8f), goldTrimMaterial);
            GameObject flame = MakePrimitive(PrimitiveType.Sphere, "Forge Flame", worldRoot, p + Vector3.up * 1.2f,
                new Vector3(0.45f, 0.6f, 0.45f), fireMaterial);
            NoShadow(flame);
            flames.Add(flame.transform);
            GameObject lightObject = new GameObject("Brazier Light");
            lightObject.transform.SetParent(worldRoot, false);
            lightObject.transform.position = p + Vector3.up * 1.6f;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.58f, 0.25f);
            light.range = 6.5f;
            light.intensity = 2.2f;
            light.shadows = LightShadows.None;
            flameLights.Add(light);
        }

        private void CreateFloatingRocks()
        {
            var random = new System.Random(77);
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16 + (float)random.NextDouble() * 0.3f;
                float radius = 12.5f + (float)random.NextDouble() * 5f;
                Vector3 p = new Vector3(Mathf.Cos(angle) * radius, -2.5f + (float)random.NextDouble() * 3f,
                    Mathf.Sin(angle) * radius + 2f);
                float size = 0.7f + (float)random.NextDouble() * 1.6f;
                GameObject rock = MakePrimitive(PrimitiveType.Cube, "Floating Rock", worldRoot, p,
                    new Vector3(size, size * 0.7f, size * 0.9f), i % 5 == 0 ? edgeMaterial : floorMaterial);
                rock.transform.rotation = Quaternion.Euler((float)random.NextDouble() * 40f, (float)random.NextDouble() * 360f,
                    (float)random.NextDouble() * 40f);
                if (i % 3 == 0)
                {
                    GameObject vein = MakePrimitive(PrimitiveType.Cube, "Rock Vein", rock.transform,
                        new Vector3(0f, 0.51f, 0f), new Vector3(0.7f, 0.04f, 0.08f), crackMaterial);
                    NoShadow(vein);
                }
                floaters.Add(rock.transform);
                floaterBases.Add(p);
            }
        }

        private void CreatePortal()
        {
            GameObject portal = new GameObject("Rift Gate");
            portal.transform.SetParent(worldRoot, false);
            portal.transform.localPosition = new Vector3(0f, 0f, 11.2f);
            MakePrimitive(PrimitiveType.Cube, "Gate Pillar L", portal.transform, new Vector3(-2.2f, 2.2f, 0f),
                new Vector3(0.7f, 4.6f, 0.7f), edgeMaterial);
            MakePrimitive(PrimitiveType.Cube, "Gate Pillar R", portal.transform, new Vector3(2.2f, 2.2f, 0f),
                new Vector3(0.7f, 4.6f, 0.7f), edgeMaterial);
            MakePrimitive(PrimitiveType.Cube, "Gate Lintel", portal.transform, new Vector3(0f, 4.65f, 0f),
                new Vector3(5.4f, 0.6f, 0.8f), edgeMaterial);
            MakePrimitive(PrimitiveType.Cube, "Gate Rune", portal.transform, new Vector3(0f, 4.65f, -0.42f),
                new Vector3(1.4f, 0.18f, 0.05f), goldMaterial);
            GameObject core = MakePrimitive(PrimitiveType.Cylinder, "Rift Core", portal.transform,
                new Vector3(0f, 2.3f, 0f), new Vector3(3.1f, 0.04f, 3.9f), portalMaterial);
            core.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            NoShadow(core);
            portalCore = core.transform;
            portalRing = new GameObject("Rift Ring").transform;
            portalRing.SetParent(portal.transform, false);
            portalRing.localPosition = new Vector3(0f, 2.3f, -0.1f);
            for (int i = 0; i < 14; i++)
            {
                float angle = i * Mathf.PI * 2f / 14;
                GameObject shard = MakePrimitive(PrimitiveType.Cube, "Orbiting Rift Shard", portalRing,
                    new Vector3(Mathf.Cos(angle) * 1.75f, Mathf.Sin(angle) * 2.05f, 0f),
                    new Vector3(0.16f, 0.62f, 0.2f), i % 3 == 0 ? goldMaterial : cyanMaterial);
                shard.transform.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
                NoShadow(shard);
            }
            GameObject lightObject = new GameObject("Rift Light");
            lightObject.transform.SetParent(portal.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 2.4f, -1.5f);
            portalLight = lightObject.AddComponent<Light>();
            portalLight.type = LightType.Point;
            portalLight.color = new Color(0.55f, 0.45f, 1f);
            portalLight.range = 12f;
            portalLight.intensity = 3f;
            portalLight.shadows = LightShadows.None;
        }

        private void CreatePlayer()
        {
            GameObject root = new GameObject("Forged Sentinel");
            root.transform.SetParent(worldRoot, false);
            root.transform.position = new Vector3(0f, 1f, -2.4f);
            characterController = root.AddComponent<CharacterController>();
            playerController = root.AddComponent<PlayerController>();
            playerController.MoveSpeed = 5.6f;
            playerController.ArenaRadius = ArenaRadius;
            player = root.transform;
            lastPlayerPosition = player.position;
            playerRig = new GameObject("Rig").transform;
            playerRig.SetParent(player, false);

            // Hero ring keeps the player readable in crowds.
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16;
                GameObject dash = MakePrimitive(PrimitiveType.Cube, "Hero Ring", player,
                    new Vector3(Mathf.Cos(angle) * 0.7f, -0.97f, Mathf.Sin(angle) * 0.7f),
                    new Vector3(0.2f, 0.02f, 0.05f), cyanMaterial);
                dash.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg - 90f, 0f);
                NoShadow(dash);
            }

            legLeft = Pivot("Leg L", playerRig, Vector3.zero);
            legRight = Pivot("Leg R", playerRig, Vector3.zero);
            boots = new Transform[0];
            chestPlate = Pivot("Chest Sigil", playerRig, Vector3.zero);
            playerCharacter = CreateCharacter("Sentinel", playerRig, new Vector3(0f, -1f, 0f), .62f);
            swordPivot = Pivot("Weapon Pivot", playerRig, new Vector3(.5f, .2f, .1f));
            blade = MakePrimitive(PrimitiveType.Cube, "Forge Blade", swordPivot, new Vector3(0f, .02f, 1.18f),
                new Vector3(.13f, .05f, 1.5f), cyanMaterial).transform;
            NoShadow(blade.gameObject);

            playerRenderers = playerRig.GetComponentsInChildren<Renderer>(true);
            playerMaterials = new Material[playerRenderers.Length];
            for (int i = 0; i < playerRenderers.Length; i++) playerMaterials[i] = playerRenderers[i].sharedMaterial;
        }

        private RiftCharacter CreateCharacter(string resource, Transform parent, Vector3 at, float scale)
        {
            var prefab = Resources.Load<GameObject>("Characters/" + resource);
            if (prefab == null) throw new System.InvalidOperationException("Missing licensed character: " + resource);
            var visual = Instantiate(prefab, parent); visual.name = resource + " Skeletal Mesh";
            visual.transform.localPosition = at; visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * scale;
            foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    Color color = original != null && original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") :
                        original != null && original.HasProperty("_Color") ? original.GetColor("_Color") : Palette.Gold;
                    materials[i] = NewMaterial(color, 0, .45f, .3f);
                }
                renderer.sharedMaterials = materials;
            }
            var character = visual.AddComponent<RiftCharacter>(); character.Initialize(); return character;
        }

        private static Transform Pivot(string name, Transform parent, Vector3 localPosition)
        {
            Transform pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = localPosition;
            return pivot;
        }

        private void RefreshPlayerUpgradeLook()
        {
            if (blade == null) return;
            int bladeLevel = run.BladeLevel;
            blade.localScale = new Vector3(0.13f + 0.02f * bladeLevel, 0.05f, 1.5f + 0.22f * bladeLevel);
            blade.localPosition = new Vector3(0f, 0.02f, 0.43f + blade.localScale.z * 0.5f);
            Material bladeMaterial = bladeLevel >= 3 ? goldMaterial : cyanMaterial;
            blade.GetComponent<Renderer>().sharedMaterial = bladeMaterial;
            chestPlate.localScale = new Vector3(0.26f + 0.06f * run.VitalityLevel, 0.34f + 0.05f * run.VitalityLevel, 0.08f);
            for (int i = 0; i < boots.Length; i++)
                boots[i].GetComponent<Renderer>().sharedMaterial = run.DodgeLevel > 0 ? cyanMaterial : armorMaterial;
            playerMaterials = new Material[playerRenderers.Length];
            for (int i = 0; i < playerRenderers.Length; i++) playerMaterials[i] = playerRenderers[i].sharedMaterial;
        }

        private void CreateCameraAndLight()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.018f, 0.022f, 0.05f);
            mainCamera.fieldOfView = 52f;
            mainCamera.farClipPlane = 120f;
            UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = true;
                cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            }
            cameraAnchor = new GameObject("Camera Anchor").transform;
            cameraAnchor.SetParent(transform, false);
            rig = mainCamera.GetComponent<CameraRig>();
            if (rig == null) rig = mainCamera.gameObject.AddComponent<CameraRig>();
            rig.SetTarget(cameraAnchor);
            rig.Distance = 17f;
            rig.Height = 11f;
            rig.Damping = 4f;
            mainCamera.transform.position = new Vector3(0f, 11f, -17f);
            mainCamera.transform.LookAt(Vector3.zero);
            rig.SnapTo(mainCamera.transform.position, mainCamera.transform.rotation);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.24f, 0.28f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.14f, 0.12f, 0.2f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.04f, 0.07f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.03f, 0.035f, 0.08f);
            RenderSettings.fogStartDistance = 26f;
            RenderSettings.fogEndDistance = 52f;

            Light sun = null;
            foreach (Light existing in FindObjectsByType<Light>())
                if (existing.type == LightType.Directional) { sun = existing; break; }
            if (sun == null)
            {
                GameObject lightObject = new GameObject("Arena Sun");
                sun = lightObject.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.color = new Color(1f, 0.88f, 0.76f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(52f, -32f, 0f);

            GameObject rimObject = new GameObject("Rift Rim Light");
            rimObject.transform.SetParent(transform, false);
            Light rim = rimObject.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(0.4f, 0.62f, 1f);
            rim.intensity = 0.55f;
            rim.shadows = LightShadows.None;
            rimObject.transform.rotation = Quaternion.Euler(28f, 160f, 0f);

            CreatePostProcessing();
        }

        private void CreatePostProcessing()
        {
            GameObject volumeObject = new GameObject("Rift Post FX");
            volumeObject.transform.SetParent(transform, false);
            postVolume = volumeObject.AddComponent<Volume>();
            postVolume.isGlobal = true;
            postVolume.priority = 10f;
            postProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            postVolume.sharedProfile = postProfile;
            Bloom bloom = postProfile.Add<Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.8f);
            bloom.scatter.Override(0.62f);
            Tonemapping tonemapping = postProfile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);
            ColorAdjustments color = postProfile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.45f);
            color.contrast.Override(12f);
            color.saturation.Override(16f);
            vignette = postProfile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.02f, 0.02f, 0.06f));
            chromatic = postProfile.Add<ChromaticAberration>(true);
            chromatic.intensity.Override(0f);
        }

        private void PulseChromatic(float amount)
        {
            chromaticKick = Mathf.Max(chromaticKick, Mathf.Clamp01(amount));
        }

        public void CreateEnemy(Vector3 position, EnemyKind kind, float emergeDelay)
        {
            bool boss = kind == EnemyKind.Warden;
            GameObject root = new GameObject(boss ? "Rift Warden" : kind == EnemyKind.Lancer ? "Rift Lancer" : "Rift Stalker");
            root.transform.SetParent(worldRoot, false);
            root.transform.position = position;
            float s = boss ? 1.8f : kind == EnemyKind.Lancer ? 1.05f : 1f;
            Material skin = boss ? bossMaterial : kind == EnemyKind.Lancer ? lancerMaterial : enemyMaterial;
            Transform body = Pivot("Body", root.transform, new Vector3(0f, 0.6f * s, 0f));

            Pivot("Leg L", root.transform, Vector3.zero);
            Pivot("Leg R", root.transform, Vector3.zero);
            CreateCharacter(boss ? "Warden" : kind == EnemyKind.Lancer ? "Lancer" : "Stalker",
                body, new Vector3(0f, -.6f * s, 0f), boss ? 1.05f : .6f);

            if (!boss)
            {
                Transform bar = Pivot("Health Bar", root.transform, new Vector3(0f, 2.15f * s, 0f));
                GameObject back = MakePrimitive(PrimitiveType.Cube, "Back", bar, Vector3.zero, new Vector3(1.04f, 0.12f, 0.02f), darkMaterial);
                NoShadow(back);
                Transform fill = Pivot("Fill", bar, Vector3.zero);
                GameObject fillCube = MakePrimitive(PrimitiveType.Cube, "Fill Cube", fill, new Vector3(0f, 0f, -0.02f),
                    new Vector3(1f, 0.08f, 0.02f), warningMaterial);
                NoShadow(fillCube);
            }

            RiftEnemy enemy = root.AddComponent<RiftEnemy>();
            enemy.Configure(this, run.Wave, kind, emergeDelay);
            enemies.Add(enemy);
        }

        public GameObject CreateThreatRing(float radius, bool boss)
        {
            GameObject ring = new GameObject(boss ? "Boss Warning" : "Strike Warning");
            ring.transform.SetParent(worldRoot, false);
            int count = boss ? 24 : 16;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                GameObject segment = MakePrimitive(PrimitiveType.Cube, "Warning Segment", ring.transform,
                    new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius),
                    new Vector3(2f * Mathf.PI * radius / count * 0.75f, 0.05f, 0.12f), warningMaterial);
                segment.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg - 90f, 0f);
                NoShadow(segment);
            }
            GameObject fill = MakePrimitive(PrimitiveType.Cylinder, "Warning Fill", ring.transform,
                new Vector3(0f, -0.015f, 0f), new Vector3(radius * 2f, 0.004f, radius * 2f), warningFillMaterial);
            NoShadow(fill);
            Transform progress = Pivot("Progress", ring.transform, Vector3.zero);
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                GameObject segment = MakePrimitive(PrimitiveType.Cube, "Progress Segment", progress,
                    new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius),
                    new Vector3(2f * Mathf.PI * radius / count * 0.6f, 0.03f, 0.07f), warningMaterial);
                segment.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg - 90f, 0f);
                NoShadow(segment);
            }
            progress.localScale = Vector3.zero;
            return ring;
        }

        public GameObject CreateThreatLine(float length, float halfWidth)
        {
            GameObject line = new GameObject("Charge Warning");
            line.transform.SetParent(worldRoot, false);
            for (int side = -1; side <= 1; side += 2)
                NoShadow(MakePrimitive(PrimitiveType.Cube, "Lane Edge", line.transform,
                    new Vector3(side * halfWidth, 0f, length * 0.5f), new Vector3(0.1f, 0.05f, length), warningMaterial));
            NoShadow(MakePrimitive(PrimitiveType.Cube, "Lane Tip", line.transform,
                new Vector3(0f, 0f, length), new Vector3(halfWidth * 2f, 0.05f, 0.12f), warningMaterial));
            NoShadow(MakePrimitive(PrimitiveType.Cube, "Warning Fill", line.transform,
                new Vector3(0f, -0.015f, length * 0.5f), new Vector3(halfWidth * 2f, 0.004f, length), warningFillMaterial));
            Transform progress = Pivot("Progress", line.transform, Vector3.zero);
            NoShadow(MakePrimitive(PrimitiveType.Cube, "Progress Bar", progress,
                new Vector3(0f, 0f, length * 0.5f), new Vector3(halfWidth * 1.2f, 0.03f, length), warningMaterial));
            progress.localScale = new Vector3(1f, 1f, 0f);
            return line;
        }

        public void SetBossEnragedLook(RiftEnemy boss)
        {
            if (boss != null) boss.Recolor("Core", warningMaterial);
            if (boss != null) boss.Recolor("Torso", hurtMaterial);
        }

        private void BeginSwingAnimation(StrikeKind kind, float windupSeconds)
        {
            swingKind = kind;
            swingClock = 0f;
            swingWindup = windupSeconds;
        }

        private void CreateSlash(StrikeKind kind)
        {
            if (kind == StrikeKind.Burst) return;
            int count = kind == StrikeKind.Finisher ? 18 : kind == StrikeKind.Counter ? 6 : 9;
            float spread = kind == StrikeKind.Finisher ? 180f : kind == StrikeKind.Counter ? 25f : 68f;
            float radius = kind == StrikeKind.Finisher ? 2.3f : kind == StrikeKind.Counter ? 2.4f : 1.8f;
            Material material = kind == StrikeKind.Finisher ? goldMaterial : kind == StrikeKind.Counter ? pinkMaterial : cyanMaterial;
            float lift = kind == StrikeKind.Cross ? 0.55f : 0.3f;
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (count - 1f);
                float angle = Mathf.Lerp(-spread, spread, kind == StrikeKind.Cross ? 1f - t : t);
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * facing;
                float thickness = Mathf.Sin(t * Mathf.PI) * 0.16f + 0.05f;
                GameObject arc = MakePrimitive(PrimitiveType.Cube, "Blade Arc", transform,
                    player.position + direction * radius + Vector3.up * (lift + t * 0.15f - 0.6f),
                    new Vector3(2f * Mathf.PI * radius * (2f * spread / 360f) / count * 1.1f, thickness * 0.5f, thickness), material, true);
                arc.transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 90f, 0f);
                NoShadow(arc);
                effects.Add(new ShortEffect { Visual = arc, Remaining = 0.16f + t * 0.04f });
            }
        }

        private void AnimateWorld(float unscaled)
        {
            float time = Time.unscaledTime;
            if (portalRing != null) portalRing.Rotate(0f, 0f, 22f * unscaled, Space.Self);
            if (portalCore != null)
            {
                float pulse = 1f + Mathf.Sin(time * 2.2f) * 0.04f;
                portalCore.localScale = new Vector3(3.1f * pulse, 0.04f, 3.9f * pulse);
            }
            if (portalLight != null) portalLight.intensity = 2.6f + Mathf.Sin(time * 2.2f) * 0.6f;
            for (int i = 0; i < flames.Count; i++)
            {
                float flicker = Mathf.PerlinNoise(time * 4f, i * 3.1f);
                flames[i].localScale = new Vector3(0.42f + flicker * 0.08f, 0.5f + flicker * 0.25f, 0.42f + flicker * 0.08f);
                flameLights[i].intensity = 1.7f + flicker * 1.1f;
            }
            for (int i = 0; i < floaters.Count; i++)
            {
                Vector3 b = floaterBases[i];
                floaters[i].position = b + Vector3.up * (Mathf.Sin(time * 0.5f + i * 1.7f) * 0.35f);
                floaters[i].Rotate(0f, 6f * unscaled, 0f, Space.World);
            }
            for (int i = 0; i < runeCrystals.Count; i++) runeCrystals[i].Rotate(0f, 50f * unscaled, 0f, Space.World);

            chromaticKick = Mathf.MoveTowards(chromaticKick, 0f, unscaled * 2.5f);
            if (chromatic != null) chromatic.intensity.value = chromaticKick;
            if (vignette != null)
            {
                bool danger = run.Phase == RunPhase.Combat && run.Health <= run.MaximumHealth * 0.3f;
                float beat = danger ? 0.5f + 0.5f * Mathf.Sin(time * 6f) : 0f;
                vignette.intensity.value = Mathf.Lerp(0.3f, 0.48f, beat);
                vignette.color.value = Color.Lerp(new Color(0.02f, 0.02f, 0.06f), new Color(0.45f, 0.02f, 0.03f), danger ? 0.8f : 0f);
            }
        }

        private void AnimatePlayer(float unscaled)
        {
            if (player == null || playerRig == null) return;
            float dt = Time.deltaTime;
            Vector3 moved = player.position - lastPlayerPosition;
            moved.y = 0f;
            lastPlayerPosition = player.position;
            float speed = dt > 0f ? moved.magnitude / dt : 0f;
            float moving = Mathf.Clamp01(speed / 5f);
            walkPhase += moved.magnitude * 3.4f;
            float legSwing = Mathf.Sin(walkPhase) * 38f * moving;
            legLeft.localRotation = Quaternion.Euler(legSwing, 0f, 0f);
            legRight.localRotation = Quaternion.Euler(-legSwing, 0f, 0f);
            float breathe = Mathf.Sin(Time.time * 2.4f) * 0.025f;
            float bob = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.06f * moving;
            if (cape != null) cape.localRotation = Quaternion.Euler(14f + moving * 28f + Mathf.Sin(Time.time * 3f) * 4f, 0f, 0f);

            // Strike pose: anticipation (wind-up) -> impact sweep -> recovery.
            swingClock += dt;
            float yaw = 0f, rigYaw = 0f, hop = 0f, lunge = 0f, pitch = 0f;
            float impactEnd = swingWindup + 0.09f;
            float recoveryEnd = impactEnd + 0.22f;
            float startYaw = 105f, endYaw = -80f;
            if (swingKind == StrikeKind.Cross) { startYaw = -85f; endYaw = 95f; }
            if (swingClock < swingWindup)
            {
                float t = swingClock / Mathf.Max(0.01f, swingWindup);
                float e = t * t;
                yaw = Mathf.Lerp(0f, startYaw, e);
                pitch = -20f * e;
                if (swingKind == StrikeKind.Burst || swingKind == StrikeKind.Finisher) hop = -0.12f * e;
            }
            else if (swingClock < impactEnd)
            {
                float t = (swingClock - swingWindup) / 0.09f;
                float e = 1f - (1f - t) * (1f - t);
                switch (swingKind)
                {
                    case StrikeKind.Finisher:
                        rigYaw = Mathf.Lerp(0f, 360f, e); yaw = -30f; hop = Mathf.Sin(t * Mathf.PI) * 0.28f; break;
                    case StrikeKind.Burst:
                        rigYaw = Mathf.Lerp(0f, 720f, e); yaw = -10f; hop = Mathf.Sin(t * Mathf.PI) * 0.5f; break;
                    case StrikeKind.Counter:
                        yaw = 0f; lunge = Mathf.Sin(t * Mathf.PI) * 0.6f; pitch = 10f; break;
                    default:
                        yaw = Mathf.Lerp(startYaw, endYaw, e); pitch = 12f * Mathf.Sin(t * Mathf.PI); break;
                }
            }
            else if (swingClock < recoveryEnd)
            {
                float t = (swingClock - impactEnd) / 0.22f;
                float e = t * t * (3f - 2f * t);
                bool spun = swingKind == StrikeKind.Finisher || swingKind == StrikeKind.Burst || swingKind == StrikeKind.Counter;
                yaw = Mathf.Lerp(spun ? (swingKind == StrikeKind.Counter ? 0f : -30f) : endYaw, 0f, e);
            }

            if (playerCharacter != null && !paused)
            {
                if (run.Phase == RunPhase.Defeat) playerCharacter.Pose("Death", dt, 1.8f, false);
                else if (hurtFlash > 0) playerCharacter.Pose("RecieveHit", dt, .22f, false);
                else if (dodgeTime > 0) playerCharacter.Pose("Roll", dt, .25f, false);
                else if (swingClock < recoveryEnd)
                    playerCharacter.SampleAt("SwordSlash", swingClock < swingWindup ? .4f * swingClock / swingWindup : .4f + .6f * (swingClock - swingWindup) / .31f);
                else playerCharacter.Pose(moving > .1f ? "Run" : "Idle", dt);
            }
            float dodgeLean = dodgeTime > 0f ? 25f : 0f;
            playerRig.localPosition = new Vector3(0f, bob + hop, lunge);
            playerRig.localRotation = Quaternion.Euler(dodgeLean + moving * 8f, rigYaw, 0f);
            float squash = dodgeTime > 0f ? 0.85f : 1f;
            playerRig.localScale = new Vector3(1f + (1f - squash) * 0.5f, (1f + breathe) * squash, 1f);
            swordPivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);

            if (hurtFlash > 0f)
            {
                hurtFlash -= unscaled;
                for (int i = 0; i < playerRenderers.Length; i++)
                    playerRenderers[i].sharedMaterial = hurtFlash > 0f ? hurtMaterial : playerMaterials[i];
            }
        }

        private void UpdateCamera(float unscaled)
        {
            if (cameraAnchor == null || player == null || rig == null) return;
            RunPhase phase = run.Phase;
            if (phase == RunPhase.Combat)
            {
                cameraAnchor.position = new Vector3(player.position.x * 0.55f, 0f, player.position.z * 0.5f);
                cameraAnchor.rotation = Quaternion.Slerp(cameraAnchor.rotation, Quaternion.identity, 1f - Mathf.Exp(-3.5f * unscaled));
                titleYaw = cameraAnchor.eulerAngles.y;
                rig.Distance = run.IsBossWave ? 13.5f : 12.5f;
                rig.Height = run.IsBossWave ? 14f : 13f;
            }
            else if (phase == RunPhase.Forge)
            {
                cameraAnchor.position = Vector3.Lerp(cameraAnchor.position, player.position + Vector3.down, 1f - Mathf.Exp(-3f * unscaled));
                cameraAnchor.rotation = Quaternion.Slerp(cameraAnchor.rotation, Quaternion.Euler(0f, 20f, 0f), 1f - Mathf.Exp(-2f * unscaled));
                rig.Distance = 7.5f;
                rig.Height = 5.5f;
            }
            else
            {
                titleYaw += unscaled * 7f;
                cameraAnchor.position = Vector3.Lerp(cameraAnchor.position, new Vector3(0f, 0f, 1.5f), 1f - Mathf.Exp(-2f * unscaled));
                cameraAnchor.rotation = Quaternion.Euler(0f, titleYaw, 0f);
                rig.Distance = 16f;
                rig.Height = 9f;
            }
        }

        public static GameObject MakePrimitive(PrimitiveType type, string name, Transform parent,
            Vector3 position, Vector3 scale, Material material, bool worldPosition = false)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            Collider collider = item.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            item.transform.SetParent(parent, false);
            if (worldPosition) item.transform.position = position;
            else item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        private static GameObject NoShadow(GameObject item)
        {
            Renderer renderer = item.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return item;
        }

        private void DestroyWorldResources()
        {
            foreach (Material material in runtimeMaterials) if (material != null) Destroy(material);
            runtimeMaterials.Clear();
            if (postProfile != null) Destroy(postProfile);
        }
    }
}
