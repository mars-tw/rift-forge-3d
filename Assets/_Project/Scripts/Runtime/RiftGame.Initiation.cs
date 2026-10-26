using System.Collections.Generic;
using UnityEngine;

namespace RiftForge
{
    public enum InitiationStep { None, Dodge, Counter, Burst, Complete }

    public sealed partial class RiftGame
    {
        public PhaseImprints Imprints { get; } = new PhaseImprints();
        public InitiationStep Initiation { get; private set; }
        public bool IsInitiation => Initiation != InitiationStep.None;
        private readonly List<GameObject> imprintVisuals = new List<GameObject>();
        public string InitiationHint => Initiation == InitiationStep.Dodge ? "初鍛 1/3｜紅圈收攏時閃避，留下鍛印" :
            Initiation == InitiationStep.Counter ? "初鍛 2/3｜靠近敵人，按斬擊打出相位反擊" :
            Initiation == InitiationStep.Burst ? "初鍛 3/3｜再閃避蓄能，爆發引燃地上的鍛印" :
            Initiation == InitiationStep.Complete ? "初鍛完成｜鍛印已引爆，可從暫停返回標題" : "";

        public void StartInitiation()
        {
            StartRun(RiftRoute.Campaign, true);
            Initiation = InitiationStep.Dodge;
            // The normal combat rules and touch controls are used. A durable partner keeps all
            // three lessons available even if the player attacks before reading the objective.
            foreach (var enemy in enemies) enemy.ConfigureInitiationPartner();
            hud.Refresh();
        }
        private void AddImprint()
        {
            Imprints.Add(PlayerPosition);
            RebuildImprintVisuals();
            if (Initiation == InitiationStep.Dodge) Initiation = InitiationStep.Counter;
        }
        private void RebuildImprintVisuals()
        {
            foreach (var visual in imprintVisuals) if (visual != null) Destroy(visual);
            imprintVisuals.Clear();
            foreach (Vector3 p in Imprints.Points)
            {
                var marker = new GameObject("相位鍛印"); marker.transform.SetParent(worldRoot, false);
                var line = marker.AddComponent<LineRenderer>(); line.sharedMaterial = goldMaterial;
                line.useWorldSpace = true; line.loop = true; line.widthMultiplier = .065f; line.positionCount = 6;
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3f;
                    line.SetPosition(i, p + new Vector3(Mathf.Cos(a) * .7f, .075f, Mathf.Sin(a) * .7f));
                }
                imprintVisuals.Add(marker);
            }
        }
        private int DetonateImprints()
        {
            var points = Imprints.Consume(); RebuildImprintVisuals();
            // One enemy can be hit by one remote imprint per burst, avoiding stacked instant kills.
            var hit = new HashSet<RiftEnemy>();
            int acceptedRemoteHits = 0;
            foreach (var p in points)
            {
                fx.Shockwave(p, .3f, PhaseImprints.Radius, .5f);
                foreach (var enemy in new List<RiftEnemy>(enemies))
                {
                    if (enemy == null || hit.Contains(enemy)) continue;
                    Vector3 offset = enemy.transform.position - p; offset.y = 0;
                    if (offset.sqrMagnitude > PhaseImprints.Radius * PhaseImprints.Radius) continue;
                    hit.Add(enemy);
                    int healthBefore = enemy.Health;
                    enemy.TakeHit(run.AttackDamage, offset.normalized * 4f, true);
                    if (enemy.Health < healthBefore) acceptedRemoteHits++;
                }
            }
            if (Initiation == InitiationStep.Burst && points.Length > 0)
            {
                Initiation = InitiationStep.Complete;
                Progress.CompleteInitiation();
                hud.ShowBanner("初鍛完成", "你留下的鍛印，成了下一次爆發的落點", Palette.Gold);
            }
            return acceptedRemoteHits;
        }
    }
}
