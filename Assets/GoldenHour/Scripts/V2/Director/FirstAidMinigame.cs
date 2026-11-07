using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoldenHour
{
    public enum MinigameKind
    {
        None,
        RecoveryPosition,
        Cpr
    }

    /// <summary>
    /// Interactive first-aid procedures shown on the overlay canvas while the clock keeps running.
    /// Recovery position: roll the casualty onto their side with the Rotate axis, stop in the
    /// "on side" band, then tilt the head to keep the airway open (Primary).
    /// CPR: press Primary in rhythm; the rate is measured against 100-120 compressions per minute.
    /// Simplified for education; not a substitute for a first-aid course.
    /// </summary>
    public static class FirstAidMinigame
    {
        public const float RecoveryBandMin = 0.88f;
        public const float RecoveryBandMax = 1.04f;
        public const float RecoveryOverRoll = 1.14f;
        public const float RollSpeed = 0.55f;
        public const int CprCompressions = 30;
        public const float CprMinRate = 100f;
        public const float CprMaxRate = 120f;

        private static IncidentDirector director;
        private static Casualty target;
        private static Action<bool> finished;
        private static GameObject root;
        private static TMP_Text titleText;
        private static TMP_Text bodyText;
        private static Image barFill;
        private static RectTransform marker;
        private static float roll;
        private static float startTime;
        private static float lastPress = -1f;
        private static float nextBeat;
        private static readonly List<float> intervals = new List<float>();
        private static bool primaryLatch;

        public static bool Active => Kind != MinigameKind.None;
        public static MinigameKind Kind { get; private set; }
        public static int Compressions { get; private set; }
        public static int RolledTooFar { get; private set; }

        /// <summary>Test hook: when set, replaces the Rotate axis.</summary>
        public static float? TestRotate { get; set; }

        /// <summary>Test hook: set true for one press of the Primary input.</summary>
        public static bool TestPress { get; set; }

        public static float LastAverageRate { get; private set; }
        public static float LastInRangeFraction { get; private set; }

        public static void Begin(IncidentDirector owner, Casualty casualty, MinigameKind kind, Transform overlay, Action<bool> done)
        {
            Cancel(false);
            director = owner;
            target = casualty;
            finished = done;
            Kind = kind;
            roll = casualty.RecoveryRoll;
            Compressions = 0;
            RolledTooFar = 0;
            intervals.Clear();
            lastPress = -1f;
            startTime = Time.time;
            nextBeat = Time.time;
            primaryLatch = true;
            BuildUi(overlay);
            if (kind == MinigameKind.RecoveryPosition) casualty.SetRecoveryRoll(roll);
            Refresh();
        }

        public static void Tick()
        {
            if (!Active) return;
            if (target == null || director == null || director.Finished)
            {
                Cancel(false);
                return;
            }

            if (GameInput.Pressed(GameInput.Pause))
            {
                if (Kind == MinigameKind.RecoveryPosition) target.SetRecoveryRoll(0f);
                Complete(false);
                return;
            }

            bool press = TestPress || (GameInput.Pressed(GameInput.Primary) && !primaryLatch);
            TestPress = false;
            if (!GameInput.Held(GameInput.Primary)) primaryLatch = false;
            if (Kind == MinigameKind.RecoveryPosition) TickRecovery(press);
            else TickCpr(press);
            if (Active) Refresh();
        }

        private static void TickRecovery(bool press)
        {
            float axis = TestRotate ?? GameInput.Rotate.ReadValue<float>();
            roll = Mathf.Clamp(roll + axis * RollSpeed * Time.deltaTime, 0f, 1.2f);
            if (roll > RecoveryOverRoll)
            {
                RolledTooFar++;
                roll = 0.62f;
                director.Hud.SetStatus("Too far - they were rolling onto their front. Ease them back onto their side.", 4f);
            }

            target.SetRecoveryRoll(Mathf.Min(1f, roll));
            if (!press) return;
            if (roll >= RecoveryBandMin && roll <= RecoveryBandMax)
            {
                target.SetRecoveryRoll(1f);
                Complete(true);
            }
            else
            {
                director.Hud.SetStatus(roll < RecoveryBandMin ? "Not on their side yet - keep rolling." : "Too far over - ease back a little.", 3f);
            }
        }

        private static void TickCpr(bool press)
        {
            if (Time.time >= nextBeat)
            {
                nextBeat = Time.time + 60f / 110f;
                AudioOf()?.PlayMetronome();
            }

            if (!press) return;
            AudioOf()?.PlayCompression();
            if (lastPress >= 0f) intervals.Add(Time.time - lastPress);
            lastPress = Time.time;
            Compressions++;
            if (Compressions >= CprCompressions)
            {
                ComputeRate();
                Complete(true);
            }
        }

        private static void ComputeRate()
        {
            if (intervals.Count == 0)
            {
                LastAverageRate = 0f;
                LastInRangeFraction = 0f;
                return;
            }

            float sum = 0f;
            int inRange = 0;
            foreach (float interval in intervals)
            {
                sum += interval;
                float rate = 60f / Mathf.Max(0.05f, interval);
                if (rate >= CprMinRate - 5f && rate <= CprMaxRate + 5f) inRange++;
            }

            LastAverageRate = 60f / Mathf.Max(0.05f, sum / intervals.Count);
            LastInRangeFraction = inRange / (float)intervals.Count;
        }

        /// <summary>Rate of the last few compressions (per minute).</summary>
        public static float CurrentRate
        {
            get
            {
                if (intervals.Count == 0) return 0f;
                float sum = 0f;
                int count = 0;
                for (int i = intervals.Count - 1; i >= 0 && count < 5; i--, count++) sum += intervals[i];
                return 60f / Mathf.Max(0.05f, sum / count);
            }
        }

        private static GoldenHourAudio AudioOf() => UnityEngine.Object.FindAnyObjectByType<GoldenHourAudio>();

        private static void Complete(bool success)
        {
            Action<bool> callback = finished;
            Cancel(false);
            callback?.Invoke(success);
        }

        public static void Cancel(bool notify)
        {
            Action<bool> callback = finished;
            Kind = MinigameKind.None;
            finished = null;
            target = null;
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
            TestRotate = null;
            TestPress = false;
            if (notify) callback?.Invoke(false);
        }

        private static void BuildUi(Transform overlay)
        {
            root = UiFactory.Panel(overlay, "First Aid Minigame", new Vector2(760f, 220f), new Vector2(0f, -330f), new Color32(18, 24, 31, 235));
            Outline outline = root.AddComponent<Outline>();
            outline.effectColor = Palette.EmergencyRed;
            outline.effectDistance = new Vector2(2f, -2f);
            titleText = UiFactory.Text(root.transform, string.Empty, 24, new Vector2(720f, 34f), new Vector2(0f, 80f), TextAlignmentOptions.Center);
            titleText.fontStyle = FontStyles.Bold;
            bodyText = UiFactory.Text(root.transform, string.Empty, 17, new Vector2(720f, 84f), new Vector2(0f, -62f), TextAlignmentOptions.Top);
            GameObject bar = UiFactory.Panel(root.transform, "Bar", new Vector2(640f, 26f), new Vector2(0f, 36f), new Color32(52, 60, 70, 255));
            GameObject band = UiFactory.Panel(bar.transform, "Band", Vector2.zero, Vector2.zero, new Color32(70, 160, 105, 255));
            RectTransform bandRect = band.GetComponent<RectTransform>();
            GameObject fill = UiFactory.Panel(bar.transform, "Fill", Vector2.zero, Vector2.zero, new Color32(232, 236, 240, 90));
            barFill = fill.GetComponent<Image>();
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            GameObject markerObject = UiFactory.Panel(bar.transform, "Marker", new Vector2(6f, 38f), Vector2.zero, Palette.White);
            marker = markerObject.GetComponent<RectTransform>();
            marker.anchorMin = marker.anchorMax = new Vector2(0f, 0.5f);
            float scaleMax = Kind == MinigameKind.Cpr ? 160f : 1.2f;
            float bandMin = Kind == MinigameKind.Cpr ? CprMinRate : RecoveryBandMin;
            float bandMax = Kind == MinigameKind.Cpr ? CprMaxRate : RecoveryBandMax;
            bandRect.anchorMin = new Vector2(bandMin / scaleMax, 0f);
            bandRect.anchorMax = new Vector2(bandMax / scaleMax, 1f);
            bandRect.offsetMin = Vector2.zero;
            bandRect.offsetMax = Vector2.zero;
        }

        private static void Refresh()
        {
            if (root == null) return;
            string primary = GameInput.KeyName(GameInput.Primary);
            if (Kind == MinigameKind.RecoveryPosition)
            {
                titleText.text = "RECOVERY POSITION - " + target.Id;
                bodyText.text = "Roll them towards you with <b>" + GameInput.KeyName(GameInput.Rotate) + "</b>. Stop when they are on their side (green band),\n" +
                                "then press <b>" + primary + "</b> to tilt the head back and keep the airway open.  <color=#9AA4AD>Esc cancels.</color>";
                SetMarker(roll / 1.2f);
                barFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(roll / 1.2f), 1f);
            }
            else
            {
                float rate = CurrentRate;
                titleText.text = "CPR RHYTHM - " + target.Id + "   " + Compressions + " / " + CprCompressions;
                string verdict = intervals.Count == 0 ? "Press " + primary + " with the metronome" :
                    rate < CprMinRate ? "<color=#F6D171>faster</color>" : rate > CprMaxRate ? "<color=#F6D171>slower</color>" : "<color=#7FE0A6>good rate</color>";
                bodyText.text = "Press <b>" + primary + "</b> for each compression: 100-120 per minute.  Rate <b>" + Mathf.RoundToInt(rate) + "</b> - " + verdict +
                                "\n<size=85%><color=#9AA4AD>Simplified: real CPR also needs depth, full recoil and rescue breaths (30:2). Learn it on a course. " +
                                "In mass-casualty triage, CPR is normally deprioritised.</color></size>";
                SetMarker(Mathf.Clamp01(rate / 160f));
                barFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(Compressions / (float)CprCompressions), 1f);
            }
        }

        private static void SetMarker(float normalized)
        {
            RectTransform bar = (RectTransform)marker.parent;
            marker.anchoredPosition = new Vector2(normalized * bar.rect.width, 0f);
        }
    }
}
