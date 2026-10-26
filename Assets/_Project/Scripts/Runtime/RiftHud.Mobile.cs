using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RiftForge
{
    /// <summary>Relayout also runs during headless WorldSpace canvas reachability tests.</summary>
    public sealed class RiftHudResize : MonoBehaviour
    {
        public RiftHud Owner;
        private void OnRectTransformDimensionsChange() => Owner?.ApplyMobileLayout();
    }

    public sealed partial class RiftHud
    {
        private sealed class Placement { public RectTransform Rect; public Vector4 Portrait, Landscape; }
        private readonly List<Placement> placements = new List<Placement>();
        private RectTransform content;
        private Vector2 layoutSize;
        private Text routeName, routeBody, progressText, dailyText, startLabel, storyText;
        private string bannerKey = "";
        private RectTransform padBase;
        private bool applyingLayout;
        private Button resumeRun;
        private Text initiationLabel;

        private Image AddCooldownSweep(Transform button)
        {
            var rect=AddImage(button,"Cooldown",new Color(0,.02f,.05f,.55f),circle,false);
            Stretch(rect);
            var image=rect.GetComponent<Image>(); image.type=Image.Type.Filled;
            image.fillMethod=Image.FillMethod.Radial360; image.fillOrigin=(int)Image.Origin360.Top;
            image.fillClockwise=false; image.fillAmount=0;
            var label=button.GetComponentInChildren<Text>(); if(label!=null) label.transform.SetAsLastSibling();
            return image;
        }

        private static string LocalizeKey(string value)
        {
            if (value == "THE WARDEN ENRAGES") return "守門者狂暴";
            if (value.StartsWith("RIFT ")) return "裂隙 " + value.Substring(5);
            if (value == "TAP STRIKE 3x IN A ROW FOR A FORGE FINISHER") return "左手拖曳移動，右手長按斬擊；三連擊觸發終結技";
            return value;
        }

        public void ApplyMobileLayout()
        {
            if (content == null || safeRoot == null || applyingLayout) return;
            Vector2 size = safeRoot.rect.size;
            if (size.x <= 0 || size.y <= 0 || size == layoutSize) return;
            applyingLayout = true;
            bool rotated = layoutSize.x > 0 && (size.x < size.y) != (layoutSize.x < layoutSize.y);
            layoutSize = size;
            bool portrait = size.x < size.y;
            Vector2 design = portrait ? new Vector2(390, 844) : new Vector2(844, 390);
            if (portrait && size.y / size.x < 2f) design.y = Mathf.Max(740f, size.y / size.x * 390f);
            float scale = Mathf.Min(size.x / design.x, size.y / design.y);
            content.sizeDelta = design;
            content.localScale = Vector3.one * scale;
            if (rotated)
            {
                MoveSource?.ResetTouch();
                foreach(var hold in content.GetComponentsInChildren<HoldStrikeButton>(true)) hold.Cancel();
                foreach(var dodge in content.GetComponentsInChildren<DodgeTouchButton>(true)) dodge.Cancel();
            }
            foreach (var placement in placements)
            {
                Vector4 v = portrait ? placement.Portrait : placement.Landscape;
                if (portrait && design.y < 844f)
                {
                    v.y *= design.y / 844f;
                    if (v.w > 80f && (placement.Rect.GetComponent<Button>() != null || placement.Rect.GetComponent<TouchMoveArea>() != null))
                        v.w *= design.y / 844f;
                }
                if (placement.Rect.GetComponent<Button>() != null)
                {
                    v.z = Mathf.Max(v.z, 46f / scale);
                    v.w = Mathf.Max(v.w, 46f / scale);
                }
                Place(placement.Rect, new Vector2(0, 1), new Vector2(v.z, v.w), new Vector2(0, 1), new Vector2(v.x, -v.y));
            }
            applyingLayout = false;
        }

        private void At(RectTransform rect, Vector4 portrait, Vector4 landscape)
        {
            placements.Add(new Placement { Rect = rect, Portrait = portrait, Landscape = landscape });
        }
        private Text MobileText(Transform parent, string value, int size, Color color, Vector4 p, Vector4 l)
        {
            Text text = AddText(parent, value, Vector2.zero, Vector2.one, size, color);
            text.fontStyle = FontStyle.Normal;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            At(text.rectTransform, p, l);
            return text;
        }
        private Button MobileButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction action, Vector4 p, Vector4 l, out Text text)
        {
            Button button = AddButton(parent, label, Vector2.zero, new Vector2(p.z, p.w), color, action, false, out text);
            text.fontSize = 18;
            text.fontStyle = FontStyle.Normal;
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(6, 4);
            text.rectTransform.offsetMax = new Vector2(-6, -4);
            text.verticalOverflow = VerticalWrapMode.Truncate;
            At((RectTransform)button.transform, p, l);
            return button;
        }
        private GameObject MobilePanel(string name, bool solid = true)
        {
            RectTransform rect = solid ? AddImage(content, name, Ink, null, false) : NewRect(name, content);
            Stretch(rect);
            if (solid) rect.GetComponent<Image>().raycastTarget = true;
            return rect.gameObject;
        }
        private void CreatePanels(PlayerController player)
        {
            content = NewRect("Responsive Content", safeRoot);
            content.anchorMin = content.anchorMax = content.pivot = Vector2.one * 0.5f;
            safeRoot.gameObject.AddComponent<RiftHudResize>().Owner = this;
            RectTransform overlay = AddImage(safeRoot, "Hurt Overlay", new Color(0.85f,0.05f,0.05f,0), edgeGlow, false);
            Stretch(overlay); overlay.SetAsFirstSibling(); hurtOverlay = overlay.GetComponent<Image>();
            CreateTitle(); CreateCombat(player); CreateForge(); CreatePause();
            victoryPanel = OutcomePanel(0, "Victory", "裂隙已封印", "爐火記得你這次的選擇。", Gold);
            defeatPanel = OutcomePanel(1, "Defeat", "核心破碎", "保留印記，換種鍛造再挑戰。", Cyan);
            floatLayer = NewRect("Floating Text", safeRoot); Stretch(floatLayer);
            CreateBanner();
            layoutSize = Vector2.zero;
            ApplyMobileLayout();
        }
        private void CreateTitle()
        {
            titlePanel = MobilePanel("Title");
            titleLogo = MobileText(titlePanel.transform, "裂隙鍛造", 38, Cyan, new Vector4(20,90,350,68), new Vector4(20,24,360,58));
            titleBest = MobileText(titlePanel.transform, "", 16, Gold, new Vector4(20,165,350,34), new Vector4(20,88,360,32));
            MobileText(titlePanel.transform, "左手拖曳移動，右手長按斬擊\n紅圈落點前閃避，接兩倍反擊", 17, White,
                new Vector4(20,210,350,60), new Vector4(20,132,360,56));
            MobileButton(titlePanel.transform, "‹", Panel, () => game.SelectRoute(-1), new Vector4(16,310,54,60), new Vector4(420,32,54,60), out var left); left.color=White;
            MobileButton(titlePanel.transform, "›", Panel, () => game.SelectRoute(1), new Vector4(320,310,54,60), new Vector4(772,32,54,60), out var right); right.color=White;
            routeName = MobileText(titlePanel.transform, "", 24, Gold, new Vector4(76,314,238,52), new Vector4(480,36,290,52));
            routeBody = MobileText(titlePanel.transform, "", 17, White, new Vector4(24,390,342,64), new Vector4(426,110,398,60));
            progressText = MobileText(titlePanel.transform, "", 14, Muted, new Vector4(14,465,362,68), new Vector4(20,210,380,68));
            dailyText = MobileText(titlePanel.transform, "", 15, Gold, new Vector4(14,555,362,50), new Vector4(426,188,398,50));
            var start = MobileButton(titlePanel.transform, "進入裂隙", Gold, () => game.StartSelectedRun(),
                new Vector4(30,650,330,60), new Vector4(450,270,350,58), out startLabel);
            titleStart = (RectTransform)start.transform;
            MobileButton(titlePanel.transform, "初鍛引導", Cyan, () => game.StartInitiation(),
                new Vector4(20,728,170,54), new Vector4(20,292,180,54), out initiationLabel);
            resumeRun = MobileButton(titlePanel.transform, "波間續玩", Panel, () => game.ResumeCheckpoint(),
                new Vector4(200,728,170,54), new Vector4(212,292,188,54), out var resumeLabel); resumeLabel.color = White;
            MobileText(titlePanel.transform, "版本 " + Application.version, 12, Muted,
                new Vector4(20,805,350,24), new Vector4(20,360,804,22));
        }
        private void CreateCombat(PlayerController player)
        {
            combatPanel = MobilePanel("Combat HUD", false);
            healthText = MobileText(combatPanel.transform, "生命", 16, White, new Vector4(16,16,210,26), new Vector4(18,12,220,26));
            var track = AddImage(combatPanel.transform, "Health Track", Panel, rounded, true);
            At(track, new Vector4(16,46,210,12), new Vector4(18,42,220,10));
            healthTrail = AddImage(track,"Core Trail",White,rounded,true); Stretch(healthTrail);
            healthFill = AddImage(track,"Core Fill",Cyan,rounded,true); Stretch(healthFill);
            waveText = MobileText(combatPanel.transform,"",16,White,new Vector4(16,70,290,28),new Vector4(260,14,280,28));
            materialText = MobileText(combatPanel.transform,"0",16,Gold,new Vector4(244,18,70,28),new Vector4(640,14,80,28));
            var icon = AddImage(combatPanel.transform,"Shard",Gold,ring,false);
            At(icon,new Vector4(246,49,14,14),new Vector4(640,46,14,14)); materialIcon=icon;
            var pause = MobileButton(combatPanel.transform,"暫停",Panel,()=>game.SetPaused(true),new Vector4(322,14,52,54),new Vector4(772,12,54,54),out var pauseLabel);
            pause.name="Pause Button"; pauseLabel.color=White; pauseLabel.fontSize=15;
            bossBar=NewRect("Boss Bar",combatPanel.transform).gameObject;
            At((RectTransform)bossBar.transform,new Vector4(40,110,310,44),new Vector4(270,52,310,44));
            bossName=AddText(bossBar.transform,"守門者",new Vector2(.5f,.8f),new Vector2(310,24),16,White);
            var bossTrack=AddImage(bossBar.transform,"Track",Panel,rounded,true);
            Place(bossTrack,new Vector2(.5f,0),new Vector2(310,8),new Vector2(.5f,0),Vector2.zero);
            bossFill=AddImage(bossTrack,"Fill",Palette.Danger,rounded,true); Stretch(bossFill);
            comboText=MobileText(combatPanel.transform,"",20,Gold,new Vector4(220,280,150,36),new Vector4(600,104,224,36));
            chargeText=MobileText(combatPanel.transform,"",15,Gold,new Vector4(150,616,224,48),new Vector4(610,194,218,46));
            hintBack=AddImage(combatPanel.transform,"Hint",Glass,rounded,true).GetComponent<Image>();
            At((RectTransform)hintBack.transform,new Vector4(16,174,358,72),new Vector4(260,108,326,64));
            hintText=AddText(hintBack.transform,"",Vector2.one*.5f,new Vector2(306,64),16,Gold);
            Stretch(hintText.rectTransform); hintText.rectTransform.offsetMin=new Vector2(8,4); hintText.rectTransform.offsetMax=new Vector2(-8,-4);
            hintText.verticalOverflow=VerticalWrapMode.Truncate;

            var pad=AddImage(combatPanel.transform,"Movement Pad",new Color(.2f,.45f,.6f,.12f),circle,false);
            At(pad,new Vector4(16,660,140,140),new Vector4(18,220,140,140));
            pad.GetComponent<Image>().raycastTarget=true;
            padBase=AddImage(pad,"Pad Ring",new Color(.55f,.9f,1,.7f),ring,false);
            Place(padBase,Vector2.one*.5f,new Vector2(92,92));
            moveKnob=AddImage(pad,"Pad Knob",new Color(.75f,.95f,1,.8f),circle,false);
            Place(moveKnob,Vector2.one*.5f,new Vector2(36,36));
            touchMove=pad.gameObject.AddComponent<TouchMoveArea>(); touchMove.TravelRadius=32; touchMove.BindTo(player);
            var moveLabel=AddText(pad,"移動",new Vector2(.5f,0),new Vector2(100,24),14,White);
            Place(moveLabel.rectTransform,new Vector2(.5f,0),new Vector2(100,24),new Vector2(.5f,1),new Vector2(0,-2));
            var attack=MobileButton(combatPanel.transform,"斬擊",Cyan,null,new Vector4(270,662,104,104),new Vector4(724,240,104,104),out attackText);
            attack.name="Strike Button"; attackFace=attack.GetComponent<Image>(); attackCooldown=AddCooldownSweep(attack.transform);
            var hold=attack.gameObject.AddComponent<HoldStrikeButton>(); hold.Bind(game); attack.onClick.AddListener(hold.OnClick);
            chargeRing=AddImage(attack.transform,"Charge Ring",Gold,ring,false).GetComponent<Image>(); Stretch(chargeRing.rectTransform);
            chargeRing.type=Image.Type.Filled; chargeRing.fillMethod=Image.FillMethod.Radial360;
            var dodge=MobileButton(combatPanel.transform,"閃避",Gold,null,new Vector4(180,726,72,72),new Vector4(630,282,72,72),out dodgeText);
            dodge.name="Dodge Button"; dodgeCooldown=AddCooldownSweep(dodge.transform);
            var press=dodge.gameObject.AddComponent<DodgeTouchButton>(); press.Bind(game); dodge.onClick.AddListener(press.OnClick);
        }
        private void CreateForge()
        {
            forgePanel=MobilePanel("The Forge");
            MobileText(forgePanel.transform,"裂隙鍛爐",28,Gold,new Vector4(20,42,350,50),new Vector4(20,16,804,44));
            forgeSummary=MobileText(forgePanel.transform,"",17,White,new Vector4(20,100,350,36),new Vector4(20,62,804,30));
            storyText=MobileText(forgePanel.transform,"",15,Muted,new Vector4(20,136,350,44),new Vector4(444,90,380,52));
            contractButton=MobileButton(forgePanel.transform,"下波委託：不接委託",Cyan,()=>game.CycleContract(),
                new Vector4(24,184,342,86),new Vector4(20,94,400,48),out contractLabel);
            contractButton.name="Wave Contract Button"; contractLabel.fontSize=15; contractLabel.color=Ink;
            upgradeTitles=new Text[3]; upgradeBodies=new Text[3]; upgradeCosts=new Text[3];
            upgradePips=new Image[3][]; upgradeButtons=new Button[3]; upgradeGroups=new CanvasGroup[3];
            for(int i=0;i<3;i++)
            {
                var upgrade=(ForgeUpgrade)i;
                var card=MobileButton(forgePanel.transform,"",Panel,()=>game.Purchase(upgrade),
                    new Vector4(24,286+i*116,342,104),new Vector4(20+i*272,150,260,128),out var unused);
                Destroy(unused.gameObject); card.name="Forge Upgrade "+i; upgradeButtons[i]=card;
                upgradeGroups[i]=card.gameObject.AddComponent<CanvasGroup>();
                upgradeTitles[i]=AddText(card.transform,"",new Vector2(.5f,.81f),new Vector2(240,28),20,i==0?Cyan:i==1?Gold:Palette.Violet);
                upgradeBodies[i]=AddText(card.transform,"",new Vector2(.5f,.48f),new Vector2(248,48),15,White);
                upgradeCosts[i]=AddText(card.transform,"",new Vector2(.5f,.14f),new Vector2(240,26),16,Gold);
                upgradePips[i]=new Image[3];
                for(int p=0;p<3;p++) { var pip=AddImage(card.transform,"Pip",White,rounded,true); Place(pip,new Vector2(.5f,.98f),new Vector2(30,3),new Vector2(.5f,1),new Vector2((p-1)*36,0)); upgradePips[i][p]=pip.GetComponent<Image>(); }
            }
            MobileButton(forgePanel.transform,"開啟下一波",Gold,()=>game.ContinueFromForge(),new Vector4(24,662,342,58),new Vector4(444,308,380,54),out _);
            MobileButton(forgePanel.transform,"返回標題",Panel,()=>game.ReturnToTitle(),new Vector4(24,740,342,54),new Vector4(20,308,380,54),out var back); back.color=White;
        }
        private void CreatePause()
        {
            pausePanel=MobilePanel("Pause");
            MobileText(pausePanel.transform,"裂隙已暫停",32,White,new Vector4(20,170,350,64),new Vector4(20,36,804,54));
            pauseStats=MobileText(pausePanel.transform,"",17,Muted,new Vector4(20,260,350,48),new Vector4(20,100,804,40));
            MobileButton(pausePanel.transform,"繼續戰鬥",Cyan,()=>game.SetPaused(false),new Vector4(30,380,330,60),new Vector4(242,166,360,52),out _);
            MobileButton(pausePanel.transform,"重新挑戰",Gold,()=>game.StartSelectedRun(),new Vector4(30,470,330,60),new Vector4(242,234,360,52),out _);
            MobileButton(pausePanel.transform,"返回標題",Panel,()=>game.ReturnToTitle(),new Vector4(30,560,330,60),new Vector4(242,302,360,52),out var back); back.color=White;
        }
        private GameObject OutcomePanel(int index,string name,string headline,string subtitle,Color accent)
        {
            var panel=MobilePanel(name);
            MobileText(panel.transform,headline,34,accent,new Vector4(20,110,350,64),new Vector4(20,24,804,56));
            MobileText(panel.transform,subtitle,17,White,new Vector4(20,190,350,42),new Vector4(20,84,804,34));
            outcomeGrade[index]=MobileText(panel.transform,"",64,Gold,new Vector4(20,264,350,100),new Vector4(30,140,150,100));
            outcomeStats[index]=MobileText(panel.transform,"",16,White,new Vector4(20,396,350,100),new Vector4(198,146,626,72));
            outcomeBest[index]=MobileText(panel.transform,"",17,Gold,new Vector4(20,520,350,64),new Vector4(198,224,626,40));
            MobileButton(panel.transform,"再挑戰一次",accent,()=>game.StartSelectedRun(),new Vector4(30,634,330,60),new Vector4(444,302,380,54),out _);
            MobileButton(panel.transform,"返回標題",Panel,()=>game.ReturnToTitle(),new Vector4(30,724,330,52),new Vector4(20,302,380,54),out var back); back.color=White;
            return panel;
        }
        private void CreateBanner()
        {
            var root=NewRect("Banner",content);
            At(root,new Vector4(20,344,350,110),new Vector4(200,180,440,100));
            bannerGroup=root.gameObject.AddComponent<CanvasGroup>(); bannerGroup.blocksRaycasts=false;
            var back=AddImage(root,"Banner Back",Glass,rounded,true); Stretch(back);
            bannerTitle=AddText(root,"",new Vector2(.5f,.75f),new Vector2(340,48),28,Cyan);
            bannerSub=AddText(root,"",new Vector2(.5f,.27f),new Vector2(340,52),16,White);
            root.gameObject.SetActive(false);
        }
    }
}
