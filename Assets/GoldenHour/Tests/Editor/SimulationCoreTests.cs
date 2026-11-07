using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GoldenHour.Tests
{
    public sealed class StartTriageTests
    {
        private static PhysiologyState Stable() => new PhysiologyState
        {
            canWalk = false, respiratoryRate = 18, capillaryRefill = 1.5f, radialPulse = true, followsCommands = true
        };

        [Test]
        public void WalkingIsGreenEvenWithRedSigns()
        {
            PhysiologyState state = Stable();
            state.canWalk = true;
            state.respiratoryRate = 36;
            Assert.AreEqual(TriageTag.Green, StartTriage.Evaluate(state, out StartStep step));
            Assert.AreEqual(StartStep.Walking, step);
        }

        [Test]
        public void NoBreathingAfterAirwayIsBlack()
        {
            PhysiologyState state = Stable();
            state.respiratoryRate = 0;
            Assert.AreEqual(TriageTag.Black, StartTriage.Evaluate(state, out StartStep step));
            Assert.AreEqual(StartStep.NoBreathingAfterAirwayOpening, step);
        }

        [Test]
        public void BreathingOnlyAfterAirwayOpeningIsRed()
        {
            PhysiologyState state = Stable();
            state.airwayObstructed = true;
            Assert.IsFalse(state.Breathing);
            Assert.AreEqual(TriageTag.Red, StartTriage.Evaluate(state, out StartStep step));
            Assert.AreEqual(StartStep.BreathesOnlyAfterAirwayOpening, step);
            state.airwayOpened = true;
            Assert.IsTrue(state.Breathing);
            Assert.AreEqual(TriageTag.Red, StartTriage.Evaluate(state));
        }

        [TestCase(30, TriageTag.Yellow)]
        [TestCase(31, TriageTag.Red)]
        public void RespiratoryRateThreshold(int rate, TriageTag expected)
        {
            PhysiologyState state = Stable();
            state.respiratoryRate = rate;
            Assert.AreEqual(expected, StartTriage.Evaluate(state));
        }

        [TestCase(2.0f, true, TriageTag.Yellow)]
        [TestCase(2.5f, true, TriageTag.Red)]
        [TestCase(1.5f, false, TriageTag.Red)]
        public void CirculationThreshold(float refill, bool radial, TriageTag expected)
        {
            PhysiologyState state = Stable();
            state.capillaryRefill = refill;
            state.radialPulse = radial;
            Assert.AreEqual(expected, StartTriage.Evaluate(state));
        }

        [Test]
        public void CannotFollowCommandsIsRedOtherwiseYellow()
        {
            PhysiologyState state = Stable();
            Assert.AreEqual(TriageTag.Yellow, StartTriage.Evaluate(state, out StartStep yellowStep));
            Assert.AreEqual(StartStep.AllChecksPassed, yellowStep);
            state.followsCommands = false;
            Assert.AreEqual(TriageTag.Red, StartTriage.Evaluate(state, out StartStep step));
            Assert.AreEqual(StartStep.CannotFollowCommands, step);
        }

        [Test]
        public void DeceasedIsBlack()
        {
            PhysiologyState state = Stable();
            state.deceased = true;
            Assert.AreEqual(TriageTag.Black, StartTriage.Evaluate(state));
        }

        [Test]
        public void PriorityOrder()
        {
            Assert.Less(StartTriage.Priority(TriageTag.Red), StartTriage.Priority(TriageTag.Yellow));
            Assert.Less(StartTriage.Priority(TriageTag.Yellow), StartTriage.Priority(TriageTag.Green));
            Assert.Less(StartTriage.Priority(TriageTag.Green), StartTriage.Priority(TriageTag.Black));
        }
    }

    public sealed class CasualtySimTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (Object item in created) Object.DestroyImmediate(item);
            created.Clear();
        }

        private CasualtyProfile Profile(System.Action<CasualtyProfile> configure)
        {
            CasualtyProfile profile = ScriptableObject.CreateInstance<CasualtyProfile>();
            profile.initial = new PhysiologyState { respiratoryRate = 18, capillaryRefill = 1.5f };
            configure(profile);
            created.Add(profile);
            return profile;
        }

        [Test]
        public void HaemorrhageDeterioratesAndDiesUntreated()
        {
            CasualtyProfile profile = Profile(p =>
            {
                p.initial.bleeding = BleedingLevel.Severe;
                p.condition = CriticalCondition.Haemorrhage;
                p.capillaryRefillRisePerMinute = 0.3f;
                p.deathMinute = 12f;
                p.stabilisedBy = Treatment.PressureDressing;
                p.indicatedTreatments = Treatment.PressureDressing;
            });
            CasualtySim sim = new CasualtySim(profile, 7);
            Assert.AreEqual(TriageTag.Yellow, sim.TrueTag);
            sim.Advance(3f);
            Assert.AreEqual(TriageTag.Red, sim.TrueTag, "capillary refill should exceed 2 s after 3 min");
            float death = sim.EffectiveDeathMinute;
            Assert.That(death, Is.InRange(12f * 0.95f, 12f * 1.05f));
            sim.Advance(death);
            Assert.IsTrue(sim.State.deceased);
            Assert.IsTrue(sim.PreventableDeath);
            Assert.AreEqual(TriageTag.Black, sim.TrueTag);
        }

        [Test]
        public void PressureDressingStabilises()
        {
            CasualtyProfile profile = Profile(p =>
            {
                p.initial.bleeding = BleedingLevel.Severe;
                p.condition = CriticalCondition.Haemorrhage;
                p.capillaryRefillRisePerMinute = 0.3f;
                p.deathMinute = 12f;
                p.stabilisedBy = Treatment.PressureDressing;
                p.indicatedTreatments = Treatment.PressureDressing;
            });
            CasualtySim sim = new CasualtySim(profile, 7);
            sim.Advance(2f);
            Assert.AreEqual(TreatmentResult.Effective, sim.Treat(Treatment.PressureDressing));
            Assert.IsTrue(sim.Stabilised);
            Assert.AreEqual(TreatmentResult.AlreadyApplied, sim.Treat(Treatment.PressureDressing));
            sim.Advance(60f);
            Assert.IsFalse(sim.State.deceased);
        }

        [Test]
        public void YellowWorsensToRedUnlessStabilised()
        {
            CasualtyProfile profile = Profile(p =>
            {
                p.worsenMinute = 10f;
                p.worsenedRespiratoryRate = 34;
                p.stabilisedBy = Treatment.Blanket;
                p.indicatedTreatments = Treatment.Blanket;
                p.condition = CriticalCondition.Shock;
            });
            CasualtySim untreated = new CasualtySim(profile, 1);
            untreated.Advance(9.9f);
            Assert.AreEqual(TriageTag.Yellow, untreated.TrueTag);
            untreated.Advance(0.2f);
            Assert.AreEqual(TriageTag.Red, untreated.TrueTag);
            Assert.IsTrue(untreated.Worsened);

            CasualtySim treated = new CasualtySim(profile, 1);
            treated.Treat(Treatment.Blanket);
            treated.Advance(20f);
            Assert.AreEqual(TriageTag.Yellow, treated.TrueTag);
        }

        [Test]
        public void AirwayOpeningDelaysDeathAndRecoveryPositionStabilises()
        {
            CasualtyProfile profile = Profile(p =>
            {
                p.initial.airwayObstructed = true;
                p.initial.followsCommands = false;
                p.condition = CriticalCondition.Airway;
                p.deathMinute = 5f;
                p.airwayOpeningDelayMinutes = 6f;
                p.stabilisedBy = Treatment.RecoveryPosition;
                p.indicatedTreatments = Treatment.AirwayOpening | Treatment.RecoveryPosition;
            });
            CasualtySim sim = new CasualtySim(profile, 3);
            float before = sim.EffectiveDeathMinute;
            Assert.AreEqual(TreatmentResult.Effective, sim.Treat(Treatment.AirwayOpening));
            Assert.AreEqual(before + 6f, sim.EffectiveDeathMinute, 0.001f);
            Assert.IsTrue(sim.State.Breathing);
            Assert.AreEqual(TriageTag.Red, sim.TrueTag);
            Assert.AreEqual(TreatmentResult.Effective, sim.Treat(Treatment.RecoveryPosition));
            sim.Advance(30f);
            Assert.IsFalse(sim.State.deceased);
        }

        [Test]
        public void CprNeverChangesOutcomeAndExpectantIsNotPreventable()
        {
            CasualtyProfile profile = Profile(p => { p.initial.respiratoryRate = 0; });
            CasualtySim sim = new CasualtySim(profile, 5);
            Assert.IsTrue(sim.InitiallyExpectant);
            Assert.AreEqual(TreatmentResult.NotIndicated, sim.Treat(Treatment.Cpr));
            Assert.AreEqual(TriageTag.Black, sim.TrueTag);
            Assert.IsFalse(sim.PreventableDeath);
        }

        [Test]
        public void DeterminismAndMeasuredRateRespectsThreshold()
        {
            CasualtyProfile profile = Profile(p => { p.initial.respiratoryRate = 30; p.deathMinute = 10f; });
            CasualtySim a = new CasualtySim(profile, 42);
            CasualtySim b = new CasualtySim(profile, 42);
            Assert.AreEqual(a.EffectiveDeathMinute, b.EffectiveDeathMinute);
            for (int i = 0; i < 50; i++)
            {
                int measured = a.MeasureRespiratoryRate();
                Assert.AreEqual(measured, b.MeasureRespiratoryRate());
                Assert.LessOrEqual(measured, 30);
            }
        }

        [Test]
        public void TransportStopsDeterioration()
        {
            CasualtyProfile profile = Profile(p => { p.deathMinute = 5f; p.initial.respiratoryRate = 34; });
            CasualtySim sim = new CasualtySim(profile, 9);
            sim.Advance(2f);
            sim.Transport();
            sim.Advance(30f);
            Assert.IsFalse(sim.State.deceased);
            Assert.IsTrue(sim.Transported);
        }
    }

    public sealed class ScoringTests
    {
        [Test]
        public void FirstTagScores()
        {
            Assert.AreEqual(100, ScoreRules.FirstTag(TriageTag.Red, TriageTag.Red));
            Assert.AreEqual(-20, ScoreRules.FirstTag(TriageTag.Red, TriageTag.Yellow));
            Assert.AreEqual(-60, ScoreRules.FirstTag(TriageTag.Green, TriageTag.Red));
            Assert.AreEqual(-100, ScoreRules.FirstTag(TriageTag.Black, TriageTag.Red));
            Assert.AreEqual(-20, ScoreRules.FirstTag(TriageTag.Red, TriageTag.Black));
        }

        [Test]
        public void TagNeedsExaminationAndRetagNeedsReexamination()
        {
            TriageRecord record = new TriageRecord();
            Assert.AreEqual(0, record.ApplyTag(1f, TriageTag.Yellow, TriageTag.Yellow, StartStep.AllChecksPassed,
                out TagRequestResult rejected));
            Assert.AreEqual(TagRequestResult.RejectedNoExamination, rejected);

            record.RecordExamination(1f, "check breathing");
            Assert.AreEqual(100, record.ApplyTag(1.5f, TriageTag.Yellow, TriageTag.Yellow, StartStep.AllChecksPassed,
                out TagRequestResult first));
            Assert.AreEqual(TagRequestResult.FirstTag, first);
            Assert.AreEqual(0.5f, record.DecisionMinutes, 0.0001f);

            Assert.AreEqual(0, record.ApplyTag(2f, TriageTag.Red, TriageTag.Red, StartStep.RespiratoryRateOver30,
                out TagRequestResult blocked));
            Assert.AreEqual(TagRequestResult.RejectedNeedsReexamination, blocked);

            record.RecordExamination(8f, "check breathing");
            Assert.AreEqual(40, record.ApplyTag(8.5f, TriageTag.Red, TriageTag.Red, StartStep.RespiratoryRateOver30,
                out TagRequestResult justified));
            Assert.AreEqual(TagRequestResult.Retag, justified);
            Assert.AreEqual(TriageTag.Yellow, record.FirstTag);
            Assert.AreEqual(1, record.Retags);
        }

        [Test]
        public void RetagWithoutConditionChangeEarnsNothing()
        {
            TriageRecord record = new TriageRecord();
            record.RecordExamination(1f, "check");
            Assert.AreEqual(-60, record.ApplyTag(1f, TriageTag.Green, TriageTag.Red, StartStep.PoorCirculation, out _));
            record.RecordExamination(2f, "check circulation");
            Assert.AreEqual(0, record.ApplyTag(2.5f, TriageTag.Red, TriageTag.Red, StartStep.PoorCirculation,
                out TagRequestResult result));
            Assert.AreEqual(TagRequestResult.Retag, result);
            Assert.AreEqual(TriageTag.Red, record.CurrentTag);
        }

        [Test]
        public void HandoverPriority()
        {
            Assert.IsTrue(HandoverRules.IsCorrect(TriageTag.Red, new[] { TriageTag.Yellow, TriageTag.Red, TriageTag.Black }));
            Assert.IsFalse(HandoverRules.IsCorrect(TriageTag.Yellow, new[] { TriageTag.Red }));
            Assert.IsFalse(HandoverRules.IsCorrect(TriageTag.Black, new TriageTag[0]));
            Assert.IsTrue(HandoverRules.IsCorrect(TriageTag.Green, new[] { TriageTag.Green, TriageTag.Black }));
        }

        [Test]
        public void DispatchFields()
        {
            DispatchReport report = new DispatchReport
            {
                locationIndex = 1, casualtyCount = 6, hazardMask = 3, emergencyNumber = "112"
            };
            Assert.AreEqual(4, report.CorrectFields(1, 5, 3));
            report.emergencyNumber = "911";
            report.casualtyCount = 9;
            Assert.AreEqual(2, report.CorrectFields(1, 5, 3));
        }

        [Test]
        public void TimeBonusStarsAndLedger()
        {
            Assert.AreEqual(100, ScoreRules.TimeBonus(80f));
            Assert.AreEqual(20, ScoreRules.TimeBonus(10.9f));
            Assert.AreEqual(0, ScoreRules.TimeBonus(-3f));
            Assert.AreEqual(3, ScoreRules.Stars(90, 100, 0));
            Assert.AreEqual(2, ScoreRules.Stars(90, 100, 1));
            Assert.AreEqual(1, ScoreRules.Stars(10, 100, 0));
            ScoreLedger ledger = new ScoreLedger();
            ledger.Add(1f, "triage", 100, "a");
            ledger.Add(2f, "triage", -60, "b");
            ledger.Add(3f, "aid", 50, "c");
            Assert.AreEqual(90, ledger.Total);
            Assert.AreEqual(40, ledger.Sum("triage"));
            Assert.AreEqual(1, ledger.Count("triage", true));
        }

        [Test]
        public void AdaptiveDifficultyBands()
        {
            Assert.AreEqual(1f, AdaptiveDifficulty.DeteriorationScale(new List<float>(), out string none));
            Assert.AreEqual("Standard", none);
            Assert.AreEqual(1.2f, AdaptiveDifficulty.DeteriorationScale(new List<float> { 0.1f, 0.9f, 0.85f, 0.95f }, out string high));
            Assert.AreEqual("Challenge", high);
            Assert.AreEqual(0.85f, AdaptiveDifficulty.DeteriorationScale(new List<float> { 0.3f, 0.4f }, out string low));
            Assert.AreEqual("Support", low);
        }

        [Test]
        public void ProfileUnlocksNextScenario()
        {
            PlayerProfileData profile = new PlayerProfileData();
            profile.RecordRun("tutorial", 500, 600, 2, "highway");
            Assert.IsTrue(profile.Get("highway").unlocked);
            Assert.AreEqual(2, profile.Get("tutorial").bestStars);
            Assert.AreEqual(1, profile.recentRatios.Count);
            string json = JsonUtility.ToJson(profile);
            PlayerProfileData loaded = JsonUtility.FromJson<PlayerProfileData>(json);
            Assert.AreEqual(500, loaded.Get("tutorial").bestScore);
        }

        [Test]
        public void TreatmentNamesAreReadable()
        {
            Assert.AreEqual("None", TreatmentNames.Describe(Treatment.None));
            Assert.AreEqual("Pressure dressing", TreatmentNames.Describe(Treatment.PressureDressing));
            Assert.AreEqual("Airway opened, Recovery position", TreatmentNames.Describe(Treatment.RecoveryPosition | Treatment.AirwayOpening));
            Assert.AreEqual("Pressure dressing, Blanket, Reassurance", TreatmentNames.Describe(Treatment.Reassure | Treatment.Blanket | Treatment.PressureDressing));
            foreach (Treatment single in new[] { Treatment.PressureDressing, Treatment.AirwayOpening, Treatment.RecoveryPosition, Treatment.Blanket, Treatment.Cpr, Treatment.Reassure })
            {
                StringAssert.DoesNotMatch("[a-z][A-Z]", TreatmentNames.Name(single), "no CamelCase identifiers in player-facing names");
            }
        }

        [Test]
        public void PatientTasksAppearOnlyAfterTheRevealingFinding()
        {
            PatientTaskInput bleeder = new PatientTaskInput
            {
                casualtyId = "P03", careNeeded = Treatment.PressureDressing | Treatment.Blanket, active = true, examined = true
            };
            List<PatientTask> tasks = PatientTasks.Build(new[] { bleeder });
            Assert.AreEqual(1, tasks.Count, "only the tag task before the bleeding check");
            Assert.AreEqual("Tag P03", tasks[0].Text);

            bleeder.bleedingChecked = true;
            tasks = PatientTasks.Build(new[] { bleeder });
            Assert.AreEqual("Control bleeding on P03", tasks[0].Text, "bleeding is the most urgent revealed need");
            Assert.IsFalse(tasks[0].done);

            bleeder.applied = Treatment.PressureDressing;
            bleeder.tagged = true;
            tasks = PatientTasks.Build(new[] { bleeder });
            Assert.AreEqual(PatientTaskKind.KeepWarm, tasks[0].kind, "pending tasks come before done ones");
            Assert.IsTrue(tasks.Exists(t => t.kind == PatientTaskKind.ControlBleeding && t.done));
            Assert.IsTrue(tasks.Exists(t => t.kind == PatientTaskKind.Tag && t.done));
        }

        [Test]
        public void PatientTasksNeverAskForCareThatIsNotIndicated()
        {
            PatientTaskInput walker = new PatientTaskInput
            {
                casualtyId = "P01", careNeeded = Treatment.None, active = true, examined = true, tagged = true,
                breathingChecked = true, bleedingChecked = true, commandsChecked = true
            };
            PatientTaskInput gone = new PatientTaskInput
            {
                casualtyId = "P02", careNeeded = Treatment.PressureDressing, active = false, examined = true, bleedingChecked = true
            };
            PatientTaskInput airway = new PatientTaskInput
            {
                casualtyId = "P04", careNeeded = Treatment.AirwayOpening | Treatment.RecoveryPosition, active = true, examined = true,
                breathingChecked = true, tagged = true, worsenedSinceExam = true
            };
            List<PatientTask> tasks = PatientTasks.Build(new[] { walker, gone, airway });
            Assert.IsFalse(tasks.Exists(t => t.casualtyId == "P02"), "transported, green-zone or deceased casualties have no tasks");
            Assert.IsFalse(tasks.Exists(t => t.casualtyId == "P01" && t.kind != PatientTaskKind.Tag), "no aid tasks without indicated care");
            Assert.AreEqual("Open the airway of P04", tasks[0].Text);
            Assert.AreEqual("Re-check P04 (condition changed)", tasks[1].Text);
            Assert.IsFalse(tasks.Exists(t => t.kind == PatientTaskKind.RecoveryPosition), "recovery position appears only after the commands check");
        }    }
}
