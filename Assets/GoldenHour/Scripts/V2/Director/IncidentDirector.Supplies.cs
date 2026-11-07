using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace GoldenHour
{
    public enum BagItem
    {
        PressureDressing,
        Blanket,
        Gloves,
        Splint,
        ColdPack,
        Sling
    }

    /// <summary>
    /// The supply bag (pick an item to use on the casualty; limited counts; feedback for items that are not part of
    /// START triage) and the live per-casualty task list (<see cref="PatientTasks"/>).
    /// </summary>
    public sealed partial class IncidentDirector
    {
        public const string BagTitlePrefix = "SUPPLY BAG - ";
        public const int MaxVisibleTasks = 3;
        private const float DoneTaskSeconds = 4f;

        private bool bagOpen;
        private string bagFeedback = string.Empty;
        private readonly Dictionary<string, float> taskDoneTimes = new Dictionary<string, float>();
        private List<PatientTask> patientTasks = new List<PatientTask>();

        /// <summary>Times the player picked an item that is not used in START triage (telemetry and tests).</summary>
        public int WrongItemPicks { get; private set; }

        /// <summary>Current task list (rebuilt every HUD refresh; read-only use).</summary>
        public List<PatientTask> PatientTaskList => patientTasks;

        /// <summary>Items in the bag that are never consumed (they are not used during triage).</summary>
        public static int DecoyCount(BagItem item) => item == BagItem.Splint ? 1 : 2;

        public int BagItemCount(BagItem item)
        {
            switch (item)
            {
                case BagItem.PressureDressing: return Dressings;
                case BagItem.Blanket: return Blankets;
                case BagItem.Gloves: return Gloves;
                default: return KitCollected ? DecoyCount(item) : 0;
            }
        }

        private static string BagItemName(BagItem item)
        {
            switch (item)
            {
                case BagItem.PressureDressing: return "Pressure dressing";
                case BagItem.Blanket: return "Foil blanket";
                case BagItem.Gloves: return "Gloves (pair)";
                case BagItem.Splint: return "Splint";
                case BagItem.ColdPack: return "Cold pack";
                default: return "Triangular sling";
            }
        }

        private static string WrongItemFeedback(BagItem item)
        {
            switch (item)
            {
                case BagItem.Splint:
                    return "Not now: START triage only allows quick life-saving steps (open the airway, control severe bleeding, position). Splinting comes later, in treatment.";
                case BagItem.ColdPack:
                    return "Not now: a cold pack does not change anyone's priority. Spend your time on life-saving steps and on reaching everyone.";
                default:
                    return "Not now: slings are for later care. During triage, only control severe bleeding, open airways and position casualties.";
            }
        }

        /// <summary>Switches the docked examination panel to the bag view for <paramref name="casualty"/>.</summary>
        public void OpenBag(Casualty casualty)
        {
            if (casualty == null || Finished || ActionRunning) return;
            if (examTarget != casualty) OpenExamination(casualty);
            if (examTarget != casualty) return;
            if (!KitCollected)
            {
                hud.SetStatus("Collect the first-aid bag first.");
                return;
            }

            bagOpen = true;
            bagFeedback = string.Empty;
            BuildExamPanel();
        }

        public void CloseBag()
        {
            bagOpen = false;
            if (examTarget != null && modal.IsOpen) BuildExamPanel();
        }

        /// <summary>Uses one item from the bag on the casualty being examined.</summary>
        public void PickBagItem(BagItem item)
        {
            Casualty casualty = examTarget;
            if (casualty == null || !bagOpen || Finished || ActionRunning) return;
            switch (item)
            {
                case BagItem.PressureDressing:
                case BagItem.Blanket:
                    if (BagItemCount(item) <= 0)
                    {
                        bagFeedback = "No " + BagItemName(item).ToLowerInvariant() + "s left in the bag.";
                        BuildExamPanel();
                        return;
                    }

                    bagOpen = false;
                    RequestTreatment(casualty, item == BagItem.PressureDressing ? Treatment.PressureDressing : Treatment.Blanket);
                    if (examTarget == casualty && modal.IsOpen) BuildExamPanel();
                    return;
                case BagItem.Gloves:
                    if (GlovesOn)
                    {
                        bagFeedback = "Your gloves are already on.";
                        BuildExamPanel();
                        return;
                    }

                    bagOpen = false;
                    PutOnGloves();
                    if (examTarget == casualty && modal.IsOpen) BuildExamPanel();
                    return;
                default:
                    WrongItemPicks++;
                    bagFeedback = WrongItemFeedback(item);
                    hud.SetStatus(BagItemName(item) + ": " + bagFeedback, 7f);
                    Log("wrong-item", casualty.Id, BagItemName(item) + " picked during triage (no score change)");
                    BuildExamPanel();
                    return;
            }
        }

        private void BuildBagPanel()
        {
            Casualty casualty = examTarget;
            StringBuilder body = new StringBuilder();
            body.Append("Pick an item to use on <b>").Append(casualty.Id).Append("</b> (").Append(casualty.Profile.displayName).Append(").\n");
            body.Append("<color=#CDD3D8>The numbers show what is left in the bag. Dressings and blankets are used up.</color>\n");
            if (casualty.Findings.TryGetValue(ExamAction.LookForBleeding, out Finding bleeding)) body.Append("\n- ").Append(bleeding.text);
            body.Append("\n<b>AID GIVEN</b>  ").Append(TreatmentNames.Describe(casualty.Sim.Applied));
            if (bagFeedback.Length > 0) body.Append("\n\n<color=#F6D171>").Append(bagFeedback).Append("</color>");

            OpenDockedPanel(BagTitlePrefix + casualty.Id, body.ToString(), 3);
            Transform panel = modal.Panel;
            bool idle = !ActionRunning && !Finished;
            bool alive = !casualty.Sim.State.deceased;
            BagItem[] items = { BagItem.PressureDressing, BagItem.Blanket, BagItem.Gloves, BagItem.Splint, BagItem.ColdPack, BagItem.Sling };
            for (int i = 0; i < items.Length; i++)
            {
                BagItem item = items[i];
                int count = BagItemCount(item);
                bool decoy = i >= 3;
                string label = BagItemName(item) + " <size=80%><color=#D8DEE4>x" + count + "</color></size>";
                Color color = decoy ? new Color32(70, 76, 86, 255) : new Color32(52, 92, 120, 255);
                Button button = DockButton(panel, label, new Vector2((i % 3 - 1) * 170f, modal.RowY(i < 3 ? 2 : 1)), new Vector2(162f, 40f),
                    () => PickBagItem(item), color);
                button.interactable = idle && count > 0 && (alive || item == BagItem.Gloves);
            }

            DockButton(panel, "BACK <size=70%>to the examination</size>", new Vector2(0f, modal.RowY(0)), new Vector2(332f, 40f), CloseBag, Palette.Charcoal);
        }

        // ------------------------------------------------------------- tasks

        private void UpdatePatientTasks()
        {
            List<PatientTaskInput> inputs = new List<PatientTaskInput>(casualties.Count);
            foreach (Casualty casualty in casualties)
            {
                float worsenedAt = -1f;
                foreach (SimEvent simEvent in casualty.Sim.Events)
                {
                    if (simEvent.kind == SimEventKind.Worsened) worsenedAt = simEvent.minute;
                }

                inputs.Add(new PatientTaskInput
                {
                    casualtyId = casualty.Id,
                    careNeeded = CareNeeded(casualty),
                    applied = casualty.Sim.Applied,
                    active = !casualty.Sim.State.deceased && !casualty.Sim.Transported && !casualty.InGreenZone,
                    examined = casualty.Findings.Count > 0,
                    tagged = casualty.Record.HasTag,
                    breathingChecked = casualty.Findings.ContainsKey(ExamAction.CheckBreathing),
                    bleedingChecked = casualty.Findings.ContainsKey(ExamAction.LookForBleeding),
                    commandsChecked = casualty.Findings.ContainsKey(ExamAction.GiveCommand),
                    worsenedSinceExam = worsenedAt >= 0f && worsenedAt > casualty.Record.LastExamMinute
                });
            }

            patientTasks = PatientTasks.Build(inputs);
            foreach (PatientTask task in patientTasks)
            {
                if (task.done && !taskDoneTimes.ContainsKey(task.Key)) taskDoneTimes[task.Key] = Time.time;
            }
        }

        /// <summary>Up to <see cref="MaxVisibleTasks"/> pending tasks, then tasks completed in the last few seconds (ticked).</summary>
        private void AppendPatientTasks(StringBuilder objectives)
        {
            UpdatePatientTasks();
            StringBuilder lines = new StringBuilder();
            int shown = 0;
            foreach (PatientTask task in patientTasks)
            {
                if (shown >= MaxVisibleTasks) break;
                if (task.done && (!taskDoneTimes.TryGetValue(task.Key, out float doneAt) || Time.time - doneAt > DoneTaskSeconds)) continue;
                lines.Append("\n").Append(Check(task.done)).Append(task.Text);
                shown++;
            }

            if (shown == 0) return;
            objectives.Append("\n<size=85%><color=#F6D171><b>PATIENT TASKS</b></color>").Append(lines).Append("</size>");
        }
    }
}
