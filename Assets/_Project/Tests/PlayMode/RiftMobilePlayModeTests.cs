using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RiftForge.Tests.PlayMode
{
    public sealed class RiftMobilePlayModeTests
    {
        private GameObject root;
        private RiftGame game;
        [UnitySetUp] public IEnumerator Setup()
        {
            RiftGame.ProgressOverride=new RiftProgress();
            root=new GameObject("R5 Mobile Tests"); game=root.AddComponent<RiftGame>(); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale=1; RiftGame.ProgressOverride=null;
            Object.Destroy(game.Hud.gameObject); Object.Destroy(root); yield return null;
        }
        private T Control<T>() where T:Component => game.Hud.GetComponentInChildren<T>(true);
        private PointerEventData Touch(int id) => new PointerEventData(EventSystem.current){pointerId=id,button=PointerEventData.InputButton.Left};

        [UnityTest] public IEnumerator DodgePressUsesCurrentJoystickAndDoesNotWaitForReleaseOrClick()
        {
            game.StartNewRun(); var move=Control<TouchMoveArea>(); var dodge=Control<DodgeTouchButton>();
            Assert.That(move.Source.TryBeginPointer(1),Is.True); move.Source.TryMovePointer(1,Vector2.left);
            dodge.OnPointerDown(Touch(2));
            Assert.That(game.DodgeCooldownRemaining,Is.GreaterThan(1.9f));
            Assert.That(game.LastDodgeDirection,Is.EqualTo(Vector3.left));
            Assert.That(move.Source.ActivePointerId,Is.EqualTo(1));
            dodge.OnPointerUp(Touch(2)); dodge.OnClick();
            Assert.That(game.TryDamagePlayer(20),Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator EarlyDodgeQueuesOnceAndOtherFingerCannotCancelButExitCan()
        {
            game.StartNewRun(); var dodge=Control<DodgeTouchButton>(); game.Dodge();
            while(game.DodgeCooldownRemaining>.12f) yield return null;
            dodge.OnPointerDown(Touch(4)); Assert.That(dodge.IsQueued,Is.True);
            dodge.OnPointerUp(Touch(5)); Assert.That(dodge.IsQueued,Is.True);
            float deadline=Time.realtimeSinceStartup+1;
            while(game.DodgeCooldownRemaining<1 && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(game.DodgeCooldownRemaining,Is.GreaterThan(1)); Assert.That(dodge.IsQueued,Is.False);
            dodge.OnPointerUp(Touch(4));
            while(game.DodgeCooldownRemaining>.12f) yield return null;
            dodge.OnPointerDown(Touch(6)); Assert.That(dodge.IsQueued,Is.True);
            dodge.OnPointerExit(Touch(6)); Assert.That(dodge.IsQueued,Is.False);
            yield return new WaitForSeconds(.3f); Assert.That(game.DodgeCooldownRemaining,Is.Zero);
        }
        [UnityTest] public IEnumerator RotationAndPauseReleaseMovementAndCombatTouches()
        {
            game.StartNewRun(); var move=Control<TouchMoveArea>(); var strike=Control<HoldStrikeButton>();
            var canvas=(RectTransform)game.Hud.Canvas.transform; game.Hud.Canvas.renderMode=RenderMode.WorldSpace;
            canvas.sizeDelta=new Vector2(390,844); Canvas.ForceUpdateCanvases(); game.Hud.ApplyMobileLayout();
            move.Source.TryBeginPointer(1); strike.OnPointerDown(Touch(2)); Assert.That(strike.IsHeld,Is.True);
            canvas.sizeDelta=new Vector2(844,390); Canvas.ForceUpdateCanvases(); game.Hud.ApplyMobileLayout();
            Assert.That(move.Source.ActivePointerId,Is.Null); Assert.That(strike.IsHeld,Is.False);
            move.Source.TryBeginPointer(3); strike.OnPointerDown(Touch(4)); game.SetPaused(true);
            Assert.That(move.Source.ActivePointerId,Is.Null); Assert.That(strike.IsHeld,Is.False);
            game.Hud.Canvas.renderMode=RenderMode.ScreenSpaceOverlay; yield return null;
        }
        [UnityTest] public IEnumerator TrialSelectorEnforcesLocksAndAllThreeTrialsReachVisibleVictory()
        {
            game.SelectRoute(2); Assert.That(game.SelectedRoute,Is.EqualTo(RiftRoute.Hunt));
            Assert.That(game.StartSelectedRun(),Is.False); Assert.That(game.Run.Phase,Is.EqualTo(RunPhase.Title));
            for(int i=0;i<3;i++) game.Progress.RegisterVictory();
            foreach(var route in new[]{RiftRoute.Echo,RiftRoute.Hunt,RiftRoute.Crucible})
            {
                game.ReturnToTitle(); game.SelectRoute((int)route-(int)game.SelectedRoute);
                var start=game.Hud.GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<Text>().text=="進入裂隙");
                start.onClick.Invoke();
                Assert.That(game.Run.Route,Is.EqualTo(route));
                for(int wave=1;wave<=3;wave++)
                {
                    Assert.That(game.ActiveEnemyCount,Is.EqualTo(RiftRoutes.Count(route,wave)));
                    if(route==RiftRoute.Echo && wave<3) Assert.That(game.Enemies.All(e=>e.EchoSlam),Is.True);
                    foreach(var e in new List<RiftEnemy>(game.Enemies)) e.TakeHit(9999);
                    yield return WaitPhase(wave==3?RunPhase.Victory:RunPhase.Forge);
                    if(wave<3)
                    {
                        int before=game.Progress.dailyForges;
                        Assert.That(game.Purchase(ForgeUpgrade.Blade),Is.True);
                        // Two purchases in one forge still count as one daily action.
                        game.Purchase(ForgeUpgrade.Dodge);
                        Assert.That(game.Progress.dailyForges,Is.LessThanOrEqualTo(before+1));
                        var next=game.Hud.GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<Text>().text=="開啟下一波");
                        next.onClick.Invoke();
                    }
                }
                Assert.That(game.Hud.GetComponentsInChildren<Text>().Any(t=>t.text=="裂隙已封印"),Is.True);
            }
        }
        [UnityTest] public IEnumerator EchoFollowupIsTelegraphedAndDoesNotDamageBeforeImpact()
        {
            game.SelectRoute(1); game.StartSelectedRun();
            var enemy=game.Enemies[0];
            while(enemy.IsEmerging) yield return null;
            var p=game.PlayerPosition; enemy.transform.position=new Vector3(p.x,0,p.z+1);
            enemy.Tick(.01f); Assert.That(enemy.IsTelegraphing,Is.True);
            int health=game.Run.Health;
            enemy.Tick(.2f); Assert.That(game.Run.Health,Is.EqualTo(health));
            enemy.Tick(1); Assert.That(enemy.IsTelegraphing,Is.True,"Echo must show a second warning");
            Assert.That(enemy.SecondsUntilStrike,Is.EqualTo(.8f).Within(.01f));
            yield return null;
        }

        [UnityTest] public IEnumerator TouchHoldMovementDodgeAndForgeCanCompleteEchoTrialWithoutInjectedDamage()
        {
            game.SelectRoute(1); Assert.That(game.StartSelectedRun(),Is.True);
            var move=Control<TouchMoveArea>(); var strike=Control<HoldStrikeButton>(); var dodge=Control<DodgeTouchButton>();
            for(int wave=1;wave<=3;wave++)
            {
                int pointer=100+wave; Assert.That(move.Source.TryBeginPointer(pointer),Is.True);
                strike.OnPointerDown(Touch(200+wave));
                float deadline=Time.realtimeSinceStartup+40;
                while(game.CanSimulate && Time.realtimeSinceStartup<deadline)
                {
                    RiftEnemy nearest=null; float distance=float.MaxValue;
                    foreach(var enemy in game.Enemies)
                    {
                        Vector3 offset=enemy.transform.position-game.PlayerPosition; offset.y=0;
                        if(offset.sqrMagnitude<distance) { nearest=enemy; distance=offset.sqrMagnitude; }
                    }
                    if(nearest!=null && distance>2.1f*2.1f)
                    {
                        Vector3 offset=nearest.transform.position-game.PlayerPosition;
                        move.Source.TryMovePointer(pointer,new Vector2(offset.x,offset.z).normalized);
                    }
                    else move.Source.TryMovePointer(pointer,Vector2.zero);
                    if(game.DodgeCooldownRemaining<=0 && game.Enemies.Any(e=>e.ThreatContains(game.PlayerPosition) && e.SecondsUntilStrike<=.25f))
                    { dodge.OnPointerDown(Touch(300)); dodge.OnPointerUp(Touch(300)); }
                    yield return null;
                }
                strike.OnPointerUp(Touch(200+wave)); move.Source.CancelPointer(pointer);
                Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));
                Assert.That(game.Run.Phase,Is.EqualTo(wave==3?RunPhase.Victory:RunPhase.Forge),$"Trial wave {wave}, health {game.Run.Health}");
                if(wave<3)
                {
                    Assert.That(game.Purchase(ForgeUpgrade.Blade),Is.True);
                    game.Purchase(ForgeUpgrade.Vitality); game.Purchase(ForgeUpgrade.Vitality);
                    game.ContinueFromForge();
                }
            }
            Assert.That(game.Progress.Seals,Is.GreaterThanOrEqualTo(1));
            Assert.That(game.Progress.IsUnlocked(RiftRoute.Hunt),Is.True);
        }

        [UnityTest] public IEnumerator MobileViewportSafeAreaTextGlyphAndRaycastGate()
        {
            yield return CheckViewports("title");
            game.StartNewRun(); yield return null; yield return CheckViewports("combat");
            game.SetPaused(true); yield return CheckViewports("pause"); game.SetPaused(false);
            foreach(var e in new List<RiftEnemy>(game.Enemies)) e.TakeHit(9999);
            yield return WaitPhase(RunPhase.Forge); yield return CheckViewports("forge");
            game.ContinueFromForge(); game.TryDamagePlayer(9999); yield return null; yield return CheckViewports("defeat");
            game.ReturnToTitle(); game.SelectRoute(1); game.StartSelectedRun();
            for(int wave=1;wave<=3;wave++)
            {
                foreach(var e in new List<RiftEnemy>(game.Enemies)) e.TakeHit(9999);
                yield return WaitPhase(wave==3?RunPhase.Victory:RunPhase.Forge);
                if(wave<3) game.ContinueFromForge();
            }
            yield return CheckViewports("victory");
        }
        private IEnumerator CheckViewports(string phase)
        {
            Canvas canvas=game.Hud.Canvas; var rect=(RectTransform)canvas.transform;
            var camObj=new GameObject("UI Gate Camera"); var cam=camObj.AddComponent<Camera>();
            cam.orthographic=true; cam.transform.position=new Vector3(0,0,-100); cam.nearClipPlane=.1f; cam.farClipPlane=200;
            canvas.renderMode=RenderMode.WorldSpace; canvas.worldCamera=cam; rect.position=Vector3.zero; rect.rotation=Quaternion.identity; rect.localScale=Vector3.one;
            foreach(var size in new[]{new Vector2Int(390,844),new Vector2Int(844,390),new Vector2Int(1366,600),new Vector2Int(1280,640)})
            foreach(bool notch in new[]{false,true})
            {
                // World units represent viewport pixels. Project into the host screen for real UI raycasts;
                // a pixelRect larger than a -nographics host screen would clip test coordinates.
                rect.sizeDelta=size; cam.rect=new Rect(0,0,1,1); cam.aspect=(float)size.x/size.y; cam.orthographicSize=size.y/2f;
                game.Hud.SafeRoot.anchorMin=notch?new Vector2(20f/size.x,16f/size.y):Vector2.zero;
                game.Hud.SafeRoot.anchorMax=notch?new Vector2(1-20f/size.x,1-24f/size.y):Vector2.one;
                Canvas.ForceUpdateCanvases(); game.Hud.ApplyMobileLayout(); Canvas.ForceUpdateCanvases();
                yield return null;
                var targets=new List<RectTransform>();
                targets.AddRange(canvas.GetComponentsInChildren<Button>().Select(b=>(RectTransform)b.transform));
                targets.AddRange(canvas.GetComponentsInChildren<TouchMoveArea>().Select(b=>(RectTransform)b.transform));
                var boxes=new List<Rect>();
                foreach(var target in targets)
                {
                    Rect box=Bounds(rect,target); boxes.Add(box);
                    Assert.That(Mathf.Min(box.width,box.height),Is.GreaterThanOrEqualTo(44),$"{phase} {size} {target.name}");
                    Rect safe=Bounds(rect,game.Hud.SafeRoot);
                    Assert.That(box.xMin,Is.GreaterThanOrEqualTo(safe.xMin-.1f)); Assert.That(box.xMax,Is.LessThanOrEqualTo(safe.xMax+.1f));
                    Assert.That(box.yMin,Is.GreaterThanOrEqualTo(safe.yMin-.1f)); Assert.That(box.yMax,Is.LessThanOrEqualTo(safe.yMax+.1f));
                    var pointer=Touch(99); pointer.position=cam.WorldToScreenPoint(target.TransformPoint(target.rect.center));
                    var hits=new List<RaycastResult>(); canvas.GetComponent<GraphicRaycaster>().Raycast(pointer,hits);
                    Assert.That(hits.Count,Is.GreaterThan(0),$"{phase} {size} {target.name} raycast missed at {pointer.position}, depth {target.GetComponent<Graphic>().depth}, cull {target.GetComponent<Graphic>().canvasRenderer.cull}");
                    Assert.That(hits[0].gameObject==target.gameObject || hits[0].gameObject.transform.IsChildOf(target),Is.True,$"{phase} {target.name} blocked by {hits[0].gameObject.name}");
                }
                for(int i=0;i<boxes.Count;i++) for(int j=i+1;j<boxes.Count;j++) Assert.That(boxes[i].Overlaps(boxes[j]),Is.False,$"{phase} control overlap");
                foreach(var text in canvas.GetComponentsInChildren<Text>())
                {
                    if(string.IsNullOrEmpty(text.text) || text.transform.IsChildOf(canvas.transform.Find("Safe Area/Floating Text"))) continue;
                    Assert.That(text.font.name,Does.Contain("NotoSans"));
                    foreach(char c in text.text) if(!char.IsWhiteSpace(c)) Assert.That(text.font.HasCharacter(c),Is.True,$"Missing glyph {c}");
                    Assert.That(text.preferredHeight,Is.LessThanOrEqualTo(text.rectTransform.rect.height+2),$"{phase} {size} text clipped: {text.text}");
                }
            }
            game.Hud.SafeRoot.anchorMin=Vector2.zero; game.Hud.SafeRoot.anchorMax=Vector2.one;
            canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null; Object.Destroy(camObj); yield return null;
        }
        private static Rect Bounds(RectTransform root,RectTransform target)
        {
            var c=new Vector3[4]; target.GetWorldCorners(c); Vector2 min=Vector2.one*float.MaxValue,max=Vector2.one*float.MinValue;
            foreach(var point in c) { Vector2 p=root.InverseTransformPoint(point); min=Vector2.Min(min,p); max=Vector2.Max(max,p); }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        private IEnumerator WaitPhase(RunPhase expected)
        {
            float until=Time.realtimeSinceStartup+8;
            while(game.Run.Phase!=expected && Time.realtimeSinceStartup<until) yield return null;
            Assert.That(game.Run.Phase,Is.EqualTo(expected));
        }
    }
}
