using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RiftForge
{
    /// <summary>Small press feedback for any uGUI button: scales down while held.</summary>
    public sealed class UiPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private float target = 1f;
        private float scale = 1f;
        public void OnPointerDown(PointerEventData eventData) => target = 0.96f;
        public void OnPointerUp(PointerEventData eventData) => target = 1f;
        public void OnPointerExit(PointerEventData eventData) => target = 1f;
        private void OnDisable() { target = scale = 1f; transform.localScale = Vector3.one; }
        private void Update()
        {
            scale = Mathf.MoveTowards(scale, target, Time.unscaledDeltaTime * 2.5f);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    /// <summary>Native-safe, multi-touch uGUI for every run phase. All sprites are generated at runtime.</summary>
    public sealed partial class RiftHud : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.055f, 0.045f, 0.035f, 0.92f);
        private static readonly Color Glass = new Color(0.08f, 0.075f, 0.055f, 0.72f);
        private static readonly Color Panel = new Color(0.13f, 0.12f, 0.08f, 0.95f);
        private static readonly Color Cyan = Palette.Cyan;
        private static readonly Color Gold = Palette.Gold;
        private static readonly Color White = Palette.White;
        private static readonly Color Muted = new Color(0.62f, 0.7f, 0.8f);

        private sealed class FloatingText
        {
            public Text Label;
            public Vector3 World;
            public float Age;
            public float Life;
            public float Scale;
        }

        private readonly List<FloatingText> floating = new List<FloatingText>();
        private readonly Stack<Text> freeLabels = new Stack<Text>();
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private readonly List<Sprite> sprites = new List<Sprite>();
        private RiftGame game;
        private Canvas canvas;
        private RectTransform safeRoot;
        private RectTransform floatLayer;
        private Rect lastSafeArea;
        private Font font;
        private Sprite circle;
        private Sprite ring;
        private Sprite rounded;
        private Sprite edgeGlow;
        private GameObject titlePanel;
        private GameObject combatPanel;
        private GameObject forgePanel;
        private GameObject pausePanel;
        private GameObject victoryPanel;
        private GameObject defeatPanel;
        private TouchMoveArea touchMove;
        private RectTransform moveKnob;
        private Text healthText;
        private Text waveText;
        private Text materialText;
        private RectTransform materialIcon;
        private Text attackText;
        private Text dodgeText;
        private Image attackFace;
        private Image attackCooldown;
        private Image chargeRing;
        private Image dodgeCooldown;
        private Text comboText;
        private Text chargeText;
        private Text hintText;
        private Image hintBack;
        private Text bannerTitle;
        private Text bannerSub;
        private CanvasGroup bannerGroup;
        private float bannerTime = -1f;
        private Image hurtOverlay;
        private float hurtPulse;
        private float shardPulse;
        private RectTransform healthFill;
        private RectTransform healthTrail;
        private float trailFraction = 1f;
        private GameObject bossBar;
        private RectTransform bossFill;
        private Text bossName;
        private Text forgeSummary;
        private Text[] upgradeTitles;
        private Text[] upgradeBodies;
        private Text[] upgradeCosts;
        private Image[][] upgradePips;
        private CanvasGroup[] upgradeGroups;
        private Button[] upgradeButtons;
        private Text titleBest;
        private Text titleLogo;
        private RectTransform titleStart;
        private Text pauseStats;
        private Text[] outcomeGrade = new Text[2];
        private Text[] outcomeStats = new Text[2];
        private Text[] outcomeBest = new Text[2];
        private int lastComboShown;
        private float comboPunch;

        public TouchPlayerInputSource MoveSource => touchMove == null ? null : touchMove.Source;
        public Canvas Canvas => canvas;
        public RectTransform SafeRoot => safeRoot;
        public string BannerText => bannerTitle != null && bannerTime >= 0f ? bannerKey : "";
        public string HintText => hintText != null && hintBack.gameObject.activeSelf ? hintText.text : "";
        public int FloatingCount => floating.Count;

        public void Initialize(RiftGame owner, PlayerController player)
        {
            game = owner;
            font = Resources.Load<Font>("Fonts/NotoSansTC-Regular");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 24);
            circle = CreateRadialSprite(64, (d) => Mathf.Clamp01((1f - d) * 32f));
            ring = CreateRadialSprite(128, (d) => Mathf.Clamp01((1f - d) * 64f) * Mathf.Clamp01((d - 0.82f) * 64f));
            edgeGlow = CreateRadialSprite(128, (d) => Mathf.Pow(Mathf.Clamp01((d - 0.45f) / 0.75f), 1.6f));
            rounded = CreateRoundedSprite();
            CreateCanvas();
            Canvas.ForceUpdateCanvases();
            CreatePanels(player);
            Refresh();
        }

        // ---------------------------------------------------------------- refresh

        public void Refresh()
        {
            if (game == null || game.Run == null || safeRoot == null) return;
            UpdateSafeArea();
            ApplyMobileLayout();
            RiftRunState run = game.Run;
            RunPhase phase = run.Phase;
            Show(titlePanel, phase == RunPhase.Title);
            Show(combatPanel, phase == RunPhase.Combat && !game.IsPaused);
            Show(pausePanel, phase == RunPhase.Combat && game.IsPaused);
            Show(forgePanel, phase == RunPhase.Forge);
            Show(victoryPanel, phase == RunPhase.Victory);
            Show(defeatPanel, phase == RunPhase.Defeat);
            bool combatVisible = phase == RunPhase.Combat && !game.IsPaused;
            if (floatLayer != null) Show(floatLayer.gameObject, combatVisible);
            if (!combatVisible && bannerTime >= 0f)
            {
                bannerTime = -1f;
                bannerGroup.gameObject.SetActive(false);
            }

            if (phase == RunPhase.Title)
            {
                IRiftRecordStore records = game.Records;
                titleBest.text = records != null && records.BestPoints > 0
                    ? $"最佳 {records.BestGrade}　{records.BestPoints:N0} 分" : "看紅圈，抓時機，閃避後反擊";
                routeName.text = RiftRoutes.Name(game.SelectedRoute);
                routeBody.text = RiftRoutes.Description(game.SelectedRoute);
                progressText.text = $"封印印記 {game.Progress.Seals}　成就 {game.Progress.AchievementCount}/3\n{game.Progress.AchievementText}";
                dailyText.text = game.Progress.DailyText;
                resumeRun.interactable = game.HasCheckpoint;
                initiationLabel.text = game.Progress.initiationComplete ? "重練初鍛" : "初鍛引導";
                startLabel.text = game.Progress.IsUnlocked(game.SelectedRoute) ? "進入裂隙" : $"需要 {RiftRoutes.RequiredSeals(game.SelectedRoute)} 枚印記";

            }
            else if (phase == RunPhase.Combat)
            {
                RefreshCombat(run);
                pauseStats.text = $"第 {run.Wave}/{run.WaveLimit} 波　擊殺 {game.Kills}　{FormatTime(game.RunSeconds)}";
            }
            else if (phase == RunPhase.Forge)
            {
                storyText.text = game.DailyRewardThisRun ? "今日鍛造完成！封印印記 +1" : RiftRoutes.Story(run.Route, run.Wave);
                forgeSummary.text = $"第 {run.Wave} 波完成　可用碎片 {run.Material}";
                SetUpgrade(0, ForgeUpgrade.Blade, "餘燼之刃", $"斬擊  {run.AttackDamage} → {run.AttackDamage + 9}",
                    "劍刃延伸，命中更重");
                SetUpgrade(1, ForgeUpgrade.Vitality, "活性鎧甲", $"生命  {run.MaximumHealth} → {run.MaximumHealth + 25}",
                    "立即恢復 35 生命");
                SetUpgrade(2, ForgeUpgrade.Dodge, "相位步伐", $"閃避  {run.DodgeCooldown:0.00}s → {System.Math.Max(0.55f, run.DodgeCooldown - 0.32f):0.00}s",
                    "冷卻縮短，反擊更頻繁");
            }
            else if (phase == RunPhase.Victory || phase == RunPhase.Defeat)
            {
                RefreshOutcome(phase == RunPhase.Victory ? 0 : 1);
            }
        }

        private void RefreshCombat(RiftRunState run)
        {
            healthText.text = game.IsInitiation ? "初鍛護盾・練習不扣生命" : $"生命  {run.Health} / {run.MaximumHealth}";
            int hostiles = run.EnemiesRemaining;
            waveText.text = run.IsBossWave ? $"最後一波　敵人 {hostiles}" :
                $"第 {run.Wave}/{run.WaveLimit} 波　敵人 {hostiles}";
            materialText.text = run.Material.ToString();
            float healthFraction = Mathf.Clamp01((float)run.Health / run.MaximumHealth);
            healthFill.anchorMax = new Vector2(healthFraction, 1f);
            healthTrail.anchorMax = new Vector2(Mathf.Max(healthFraction, trailFraction), 1f);
            Image fillImage = healthFill.GetComponent<Image>();
            fillImage.color = healthFraction <= 0.3f ? Color.Lerp(Palette.Danger, White, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f) * 0.4f) : Cyan;

            CombatRhythm rhythm = game.Rhythm;
            float attackFraction = Mathf.Clamp01(game.AttackCooldownRemaining / RiftGame.AttackCooldownSeconds);
            attackCooldown.fillAmount = attackFraction;
            dodgeCooldown.fillAmount = Mathf.Clamp01(game.DodgeCooldownRemaining / Mathf.Max(0.01f, game.DodgeCooldownTotal));
            dodgeText.text = game.DodgeCooldownRemaining > 0.05f ? $"閃避\n{game.DodgeCooldownRemaining:0.0}" : "閃避";
            chargeRing.fillAmount = rhythm.Charge / CombatRhythm.ChargeMax;
            if (rhythm.BurstReady)
            {
                attackText.text = "爆發";
                attackFace.color = Color.Lerp(Gold, White, 0.25f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f));
                chargeRing.color = White;
                chargeText.text = "鍛造爆發就緒";
                chargeText.color = Gold;
            }
            else if (rhythm.CounterReady)
            {
                attackText.text = "反擊";
                attackFace.color = Palette.Pink;
                chargeRing.color = Gold;
                chargeText.text = "相位反擊 ×2";
                chargeText.color = Palette.Pink;
            }
            else
            {
                attackText.text = rhythm.ComboStep == 2 ? "終結！" : "斬擊";
                attackFace.color = rhythm.ComboStep == 2 ? Color.Lerp(Cyan, Gold, 0.6f) : Cyan;
                chargeRing.color = Gold;
                chargeText.text = $"能量 {Mathf.FloorToInt(rhythm.Charge)}%　鍛印 {game.Imprints.Count}/3";
                chargeText.color = new Color(Gold.r, Gold.g, Gold.b, 0.85f);
            }

            int streak = rhythm.HitStreak;
            if (streak != lastComboShown)
            {
                if (streak > lastComboShown) comboPunch = 1f;
                lastComboShown = streak;
            }
            comboText.gameObject.SetActive(streak >= 2);
            if (streak >= 2) comboText.text = $"{streak} 連擊";

            RiftEnemy boss = game.Boss;
            bossBar.SetActive(boss != null && !boss.IsEmerging);
            if (boss != null)
            {
                bossFill.anchorMax = new Vector2(Mathf.Clamp01((float)boss.Health / Mathf.Max(1, boss.MaxHealth)), 1f);
                bossName.text = boss.Enraged ? "守門者・狂暴" : "守門者";
                bossName.color = boss.Enraged ? Palette.Danger : White;
            }

            string hint = game.CurrentHint;
            hintBack.gameObject.SetActive(!string.IsNullOrEmpty(hint));
            if (!string.IsNullOrEmpty(hint)) hintText.text = LocalizeKey(hint);

            if (moveKnob != null && MoveSource != null)
            {
                Vector2 move = MoveSource.ActivePointerId.HasValue ? MoveSource.ReadMove() : Vector2.zero;
                Vector2 origin = touchMove.VisualOrigin;
                padBase.anchoredPosition = origin;
                moveKnob.anchoredPosition = Vector2.Lerp(moveKnob.anchoredPosition, origin + move * 29f, 0.5f);
            }
        }

        private void RefreshOutcome(int index)
        {
            RunScore? maybe = game.LastScore;
            if (!maybe.HasValue) return;
            RunScore score = maybe.Value;
            outcomeGrade[index].text = score.Grade;
            outcomeGrade[index].color = score.Grade == "S" ? Gold : score.Grade == "A" ? Cyan : score.Grade == "B" ? Palette.Violet : Muted;
            outcomeStats[index].text =
                $"時間 {FormatTime(score.Seconds)}　擊殺 {score.Kills}　最高連擊 {score.BestStreak}\n" +
                $"完美閃避 {score.PerfectDodges}　受傷 {score.DamageTaken}　{score.Points:N0} 分";
            outcomeBest[index].text = (game.LastScoreIsBest ? "刷新個人紀錄！　" : "") +
                $"印記 {game.Progress.Seals}　成就 {game.Progress.AchievementCount}/3";

            outcomeBest[index].color = game.LastScoreIsBest ? Gold : Muted;
        }

        private void SetUpgrade(int index, ForgeUpgrade upgrade, string name, string stat, string effect)
        {
            RiftRunState run = game.Run;
            int level = run.UpgradeLevel(upgrade);
            int cost = run.UpgradeCost(upgrade);
            bool maxed = level >= 3;
            bool available = !maxed && run.Material >= cost;
            upgradeButtons[index].interactable = available;
            upgradeGroups[index].alpha = available || maxed ? 1f : 0.55f;
            upgradeTitles[index].text = name;
            upgradeBodies[index].text = maxed ? $"{effect}\n已鍛造至上限" : $"{stat}\n{effect}";
            upgradeCosts[index].text = maxed ? "已滿級" : available ? $"鍛造　{cost} 碎片" : $"還差 {cost - run.Material} 碎片";
            for (int i = 0; i < 3; i++)
                upgradePips[index][i].color = i < level ? (index == 0 ? Cyan : index == 1 ? Gold : Palette.Violet) : new Color(1f, 1f, 1f, 0.12f);
        }

        // ---------------------------------------------------------------- feedback API

        public void ShowBanner(string title, string subtitle, Color color)
        {
            if (bannerTitle == null) return;
            bannerKey = title;
            bannerTitle.text = LocalizeKey(title);
            bannerTitle.color = color;
            bannerSub.text = LocalizeKey(subtitle);
            bannerTime = 0f;
            bannerGroup.gameObject.SetActive(true);
        }

        public void ShowDamageNumber(Vector3 world, int amount, StrikeKind kind)
        {
            Color color = kind == StrikeKind.Counter ? Palette.Pink : kind == StrikeKind.Burst || kind == StrikeKind.Finisher ? Gold : White;
            float scale = kind == StrikeKind.Slash || kind == StrikeKind.Cross ? 1f : 1.45f;
            Spawn(world, amount.ToString(), color, scale, 0.75f);
        }

        public void ShowPopup(Vector3 world, string text, Color color) => Spawn(world, text, color, 1.15f, 0.9f);

        public void PulseHurt() => hurtPulse = 1f;
        public void PulseShards() => shardPulse = 1f;

        private void Spawn(Vector3 world, string text, Color color, float scale, float life)
        {
            if (floatLayer == null) return;
            FloatingText item = null;
            if (floating.Count >= 24)
            {
                item = floating[0];
                floating.RemoveAt(0);
            }
            if (item == null && freeLabels.Count > 0) item = new FloatingText { Label = freeLabels.Pop() };
            if (item == null)
            {
                Text label = AddText(floatLayer, text, new Vector2(0.5f, 0.5f), new Vector2(420f, 90f), 54, color,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                AddShadow(label.gameObject, 3f);
                item = new FloatingText { Label = label };
            }
            item.Label.text = text;
            item.Label.color = color;
            item.World = world + new Vector3(Random.Range(-0.3f, 0.3f), 0f, 0f);
            item.Age = 0f;
            item.Life = life;
            item.Scale = scale;
            item.Label.gameObject.SetActive(true);
            floating.Add(item);
        }

        private void Update()
        {
            ApplyMobileLayout();
            float dt = Time.unscaledDeltaTime;
            float time = Time.unscaledTime;
            Camera cam = Camera.main;
            for (int i = floating.Count - 1; i >= 0; i--)
            {
                FloatingText item = floating[i];
                item.Age += dt;
                float t = item.Age / item.Life;
                if (t >= 1f || cam == null)
                {
                    item.Label.gameObject.SetActive(false);
                    floating.RemoveAt(i);
                    freeLabels.Push(item.Label);
                    continue;
                }
                Vector3 screen = cam.WorldToScreenPoint(item.World + Vector3.up * (t * 1.1f));
                if (screen.z < 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(floatLayer, screen,
                        canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out Vector2 local))
                    continue;
                RectTransform rect = item.Label.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = local;
                float pop = t < 0.15f ? Mathf.Lerp(1.6f, 1f, t / 0.15f) : 1f;
                rect.localScale = Vector3.one * (item.Scale * pop);
                Color c = item.Label.color;
                c.a = t < 0.65f ? 1f : 1f - (t - 0.65f) / 0.35f;
                item.Label.color = c;
            }

            if (bannerTime >= 0f && bannerGroup != null)
            {
                bannerTime += dt;
                const float life = 2.1f;
                float a = bannerTime < 0.18f ? bannerTime / 0.18f : bannerTime > life - 0.4f ? Mathf.Clamp01((life - bannerTime) / 0.4f) : 1f;
                bannerGroup.alpha = a;
                float s = 1f; // Fade without enlarging text outside the narrow portrait safe area.
                bannerGroup.transform.localScale = new Vector3(s, s, 1f);
                if (bannerTime >= life)
                {
                    bannerTime = -1f;
                    bannerGroup.gameObject.SetActive(false);
                }
            }

            hurtPulse = Mathf.MoveTowards(hurtPulse, 0f, dt * 2.6f);
            if (hurtOverlay != null)
            {
                bool lowHealth = game != null && game.Run != null && game.Run.Phase == RunPhase.Combat &&
                                 game.Run.Health <= game.Run.MaximumHealth * 0.3f;
                float low = lowHealth ? 0.25f + 0.15f * Mathf.Sin(time * 6f) : 0f;
                Color c = hurtOverlay.color;
                c.a = Mathf.Max(hurtPulse * 0.5f, low);
                hurtOverlay.color = c;
            }
            if (game != null && game.Run != null && game.Run.Phase == RunPhase.Combat)
                trailFraction = Mathf.MoveTowards(trailFraction, Mathf.Clamp01((float)game.Run.Health / game.Run.MaximumHealth), dt * 0.45f);
            else trailFraction = 1f;

            shardPulse = Mathf.MoveTowards(shardPulse, 0f, dt * 4f);
            if (materialIcon != null)
            {
                materialIcon.localScale = Vector3.one * (1f + shardPulse * 0.45f);
                materialIcon.localRotation = Quaternion.Euler(0f, 0f, 45f + shardPulse * 90f);
            }
            comboPunch = Mathf.MoveTowards(comboPunch, 0f, dt * 5f);
            if (comboText != null) comboText.rectTransform.localScale = Vector3.one * (1f + comboPunch * 0.06f);
            if (titleStart != null && titlePanel.activeSelf)
            {
                float pulse = 1f + Mathf.Sin(time * 3.2f) * 0.035f;
                titleStart.localScale = new Vector3(pulse, pulse, 1f);
                titleLogo.color = Color.Lerp(Cyan, White, 0.15f + 0.15f * Mathf.Sin(time * 1.7f));
            }
        }

        // ---------------------------------------------------------------- construction

        private void CreateCanvas()
        {
            GameObject canvasObject = new GameObject("Game Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Bake dynamic glyphs at mobile pixel density before the design root scales down.
            scaler.referenceResolution = new Vector2(844f, 390f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            safeRoot = NewRect("Safe Area", canvasObject.transform);
            Stretch(safeRoot);
            UpdateSafeArea();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject events = new GameObject("UI Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                InputSystemUIInputModule module = events.GetComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
                module.pointerBehavior = UIPointerBehavior.AllPointersAsIs;
            }
        }

        // ---------------------------------------------------------------- helpers

        private GameObject FullPanel(string name, Color color)
        {
            RectTransform rect = AddImage(safeRoot, name, color, null, false);
            Stretch(rect);
            rect.GetComponent<Image>().raycastTarget = true; // Blocks touches from reaching panels below.
            return rect.gameObject;
        }

        private GameObject EmptyPanel(string name)
        {
            RectTransform rect = NewRect(name, safeRoot);
            Stretch(rect);
            return rect.gameObject;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject item = new GameObject(name, typeof(RectTransform));
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 pivot, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private RectTransform AddImage(Transform parent, string name, Color color, Sprite sprite, bool sliced)
        {
            RectTransform rect = NewRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            if (sliced && sprite != null) image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            return rect;
        }

        private static void AddShadow(GameObject target, float distance)
        {
            Shadow shadow = target.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(distance, -distance);
        }

        private Text AddText(Transform parent, string value, Vector2 anchor, Vector2 size,
            int fontSize, Color color, TextAnchor alignment = TextAnchor.MiddleCenter,
            FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = NewRect(string.IsNullOrEmpty(value) ? "Label" : value, parent);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = style;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = value;
            return label;
        }

        private Button AddButton(Transform parent, string label, Vector2 anchor, Vector2 size,
            Color color, UnityEngine.Events.UnityAction onClick, bool round = false)
        {
            return AddButton(parent, label, anchor, size, color, onClick, round, out _);
        }

        private Button AddButton(Transform parent, string label, Vector2 anchor, Vector2 size,
            Color color, UnityEngine.Events.UnityAction onClick, bool round, out Text text)
        {
            RectTransform rect = AddImage(parent, label + " Button", color, round ? circle : rounded, !round);
            Place(rect, anchor, size);
            Image image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(onClick);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            rect.gameObject.AddComponent<UiPress>();
            if (!round)
            {
                Shadow drop = rect.gameObject.AddComponent<Shadow>();
                drop.effectColor = new Color(0f, 0f, 0f, 0.5f);
                drop.effectDistance = new Vector2(0f, -6f);
            }
            text = AddText(rect, label, new Vector2(0.5f, 0.5f), new Vector2(size.x - 20f, size.y - 14f),
                size.y > 220f ? 30 : 38, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.color = new Color(0.03f, 0.05f, 0.09f);
            return button;
        }

        private void UpdateSafeArea()
        {
            Rect area = Screen.safeArea;
            if (area == lastSafeArea || Screen.width <= 0 || Screen.height <= 0) return;
            lastSafeArea = area;
            safeRoot.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeRoot.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        }

        private Sprite CreateRadialSprite(int size, System.Func<float, float> alphaAtDistance)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alphaAtDistance(d)));
            }
            texture.Apply();
            textures.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            sprites.Add(sprite);
            return sprite;
        }

        private Sprite CreateRoundedSprite()
        {
            const int size = 64;
            const float radius = 20f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                // Cut corners evoke stamped bronze plates rather than rounded app cards.
                float d = Mathf.Abs(x + .5f - cx) + Mathf.Abs(y + .5f - cy);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d)));
            }
            texture.Apply();
            textures.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius + 2f, radius + 2f, radius + 2f, radius + 2f));
            sprites.Add(sprite);
            return sprite;
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        private static void Show(GameObject panel, bool visible)
        {
            if (panel != null && panel.activeSelf != visible) panel.SetActive(visible);
        }

        private void OnDestroy()
        {
            foreach (Sprite sprite in sprites) if (sprite != null) Destroy(sprite);
            foreach (Texture2D texture in textures) if (texture != null) Destroy(texture);
        }
    }
}
