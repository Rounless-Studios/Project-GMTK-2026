using NUnit.Framework;
using UnityEngine;
using Gmtk2026.GameBalance;

namespace Gmtk2026.GameBalance.Tests
{
    public class DurabilityStateTests
    {
        /// <summary>
        /// A 0-100 scale spelled out on purpose: these tests cover the durability rule, not the
        /// shipped catalog, so re-scaling the presets (0-200 since 2026-07-26) must not rewrite
        /// every expected number here. The asset's own values are pinned by
        /// GameBalanceSettingsTests instead.
        /// </summary>
        private DamageSettings Settings() => new DamageSettings
        {
            maximumDurability = 100f,
            damagedThreshold = 60f,
            criticalThreshold = 30f,
            wreckedThreshold = 0f,
            wreckDurationSeconds = 2f,
            recoveryDurability = 50f,
            recoveryProtectionSeconds = 2f,
        };

        [Test]
        public void StartsPristineAtMaxDurability()
        {
            var d = new DurabilityState(Settings());
            Assert.AreEqual(100f, d.Durability);
            Assert.AreEqual(DamageStage.Pristine, d.Stage);
            Assert.IsFalse(d.IsWrecked);
            Assert.IsFalse(d.IsProtected);
        }

        [Test]
        public void DamageCrossesStageThresholds()
        {
            var d = new DurabilityState(Settings());

            d.ApplyDamage(40f);            // 100 -> 60 (<=60)
            Assert.AreEqual(60f, d.Durability);
            Assert.AreEqual(DamageStage.Damaged, d.Stage);

            d.ApplyDamage(30f);            // 60 -> 30 (<=30)
            Assert.AreEqual(30f, d.Durability);
            Assert.AreEqual(DamageStage.Critical, d.Stage);
        }

        [Test]
        public void StageChangedFiresOnlyOnTransition()
        {
            var d = new DurabilityState(Settings());
            int fired = 0;
            DamageStage last = DamageStage.Pristine;
            d.StageChanged += s => { fired++; last = s; };

            d.ApplyDamage(5f);             // 95, still Pristine -> no event
            Assert.AreEqual(0, fired);
            d.ApplyDamage(40f);            // 55 -> Damaged
            Assert.AreEqual(1, fired);
            Assert.AreEqual(DamageStage.Damaged, last);
            d.ApplyDamage(5f);             // 50, still Damaged -> no event
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void ZeroDurabilityWrecksAndFiresWrecked()
        {
            var d = new DurabilityState(Settings());
            bool wrecked = false;
            d.Wrecked += () => wrecked = true;

            d.ApplyDamage(100f);
            Assert.AreEqual(0f, d.Durability);
            Assert.AreEqual(DamageStage.Wrecked, d.Stage);
            Assert.IsTrue(d.IsWrecked);
            Assert.IsTrue(wrecked);
            Assert.AreEqual(2f, d.WreckTimeRemaining);
        }

        [Test]
        public void OverkillDamageClampsAtZero()
        {
            var d = new DurabilityState(Settings());
            d.ApplyDamage(9999f);
            Assert.AreEqual(0f, d.Durability);
            Assert.AreEqual(DamageStage.Wrecked, d.Stage);
        }

        [Test]
        public void DamageIsIgnoredWhileWrecked()
        {
            var d = new DurabilityState(Settings());
            d.ApplyDamage(100f);           // wrecked at 0
            d.ApplyDamage(50f);            // ignored
            Assert.AreEqual(0f, d.Durability);
            Assert.AreEqual(DamageStage.Wrecked, d.Stage);
        }

        [Test]
        public void WreckTimerRecoversToRecoveryDurabilityWithProtection()
        {
            var d = new DurabilityState(Settings());
            bool recovered = false;
            d.Recovered += () => recovered = true;

            d.ApplyDamage(100f);           // wrecked
            d.Tick(1f);
            Assert.IsTrue(d.IsWrecked, "still wrecked after 1s of 2s");
            d.Tick(1.5f);                  // total 2.5s > 2s wreck duration
            Assert.IsTrue(recovered);
            Assert.AreEqual(50f, d.Durability);
            Assert.AreEqual(DamageStage.Damaged, d.Stage, "50 durability is within the damaged band");
            Assert.IsTrue(d.IsProtected);
            Assert.AreEqual(2f, d.ProtectionTimeRemaining, 0.001f);
        }

        [Test]
        public void DamageIsIgnoredDuringProtectionThenAppliesAfter()
        {
            var d = new DurabilityState(Settings());
            d.ApplyDamage(100f);
            d.Tick(2f);                    // wreck elapses -> recover to 50 + 2s protection

            d.ApplyDamage(40f);            // protected -> ignored
            Assert.AreEqual(50f, d.Durability);

            d.Tick(2f);                    // protection elapses
            Assert.IsFalse(d.IsProtected);
            d.ApplyDamage(40f);            // now applies: 50 -> 10 (Critical)
            Assert.AreEqual(10f, d.Durability);
            Assert.AreEqual(DamageStage.Critical, d.Stage);
        }

        [Test]
        public void NonPositiveDamageIsNoOp()
        {
            var d = new DurabilityState(Settings());
            d.ApplyDamage(0f);
            d.ApplyDamage(-10f);
            Assert.AreEqual(100f, d.Durability);
            Assert.AreEqual(DamageStage.Pristine, d.Stage);
        }

        [Test]
        public void CustomThresholdsAreRespected()
        {
            var s = new DamageSettings
            {
                maximumDurability = 200f,
                damagedThreshold = 120f,
                criticalThreshold = 40f,
                wreckedThreshold = 0f,
                recoveryDurability = 120f,
            };
            var d = new DurabilityState(s);
            Assert.AreEqual(200f, d.Durability);
            d.ApplyDamage(90f);            // 110 -> Damaged (<=120)
            Assert.AreEqual(DamageStage.Damaged, d.Stage);
            d.ApplyDamage(80f);            // 30 -> Critical (<=40)
            Assert.AreEqual(DamageStage.Critical, d.Stage);
        }
    }
}
