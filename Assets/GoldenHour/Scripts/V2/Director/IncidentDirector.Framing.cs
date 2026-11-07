using UnityEngine;

namespace GoldenHour
{
    /// <summary>
    /// In-world examination: the player kneels beside the casualty and the camera frames them left of the docked
    /// panel; timed checks and aid show a world-space marker on the body part involved.
    /// </summary>
    public sealed partial class IncidentDirector
    {
        /// <summary>Viewport x of the casualty while the docked examination panel is open (panel on the right).</summary>
        public const float ExamFramingX = 0.34f;

        private readonly BodyMarker bodyMarker = new BodyMarker();

        public BodyMarker Marker => bodyMarker;

        /// <summary>Moves the player to a free kneeling spot beside the casualty and frames them at <paramref name="viewportX"/>.</summary>
        private void FrameCasualty(Casualty casualty, float viewportX)
        {
            if (player == null || casualty == null) return;
            Vector3 feet = TryKneelSpot(casualty, out Vector3 spot) ? spot : player.transform.position;
            player.FrameTarget(feet, casualty.FocusPoint, viewportX, GameSettings.ReduceMotion);
        }

        /// <summary>Finds a clear spot about an arm's length from the casualty: beside the torso if lying, in front otherwise.</summary>
        private bool TryKneelSpot(Casualty casualty, out Vector3 spot)
        {
            Vector3 playerFeet = player.transform.position;
            Vector3 focus = casualty.FocusPoint;
            Vector3 centre = new Vector3(focus.x, playerFeet.y, focus.z);
            Vector3 toPlayer = playerFeet - centre;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.01f) toPlayer = -casualty.transform.forward;
            toPlayer.y = 0f;
            toPlayer.Normalize();
            Vector3 right = casualty.transform.right;
            right.y = 0f;
            right = right.sqrMagnitude > 0.01f ? right.normalized : Vector3.right;
            float side = Vector3.Dot(toPlayer, right) >= 0f ? 1f : -1f;
            Vector3[] directions = casualty.Lying
                ? new[] { right * side, -right * side, toPlayer }
                : new[] { toPlayer, right * side, -right * side };
            float distance = casualty.Lying ? 1.35f : 1.4f;

            foreach (Vector3 direction in directions)
            {
                Vector3 candidate = centre + direction * distance;
                if (!GroundAt(candidate, out float ground) || Mathf.Abs(ground - playerFeet.y) > 0.6f) continue;
                candidate.y = ground;
                if (Blocked(candidate)) continue;
                spot = candidate;
                return true;
            }

            spot = playerFeet;
            return false;
        }

        private static bool IgnoredForFraming(Collider collider)
        {
            return collider.isTrigger || collider.GetComponentInParent<Casualty>() != null ||
                   collider.GetComponentInParent<FirstPersonPlayer>() != null;
        }

        private static bool GroundAt(Vector3 point, out float height)
        {
            RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 1.6f, Vector3.down, 3.2f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (IgnoredForFraming(hit.collider)) continue;
                if (hit.point.y > best) best = hit.point.y;
            }

            height = best;
            return !float.IsNegativeInfinity(best);
        }

        private static bool Blocked(Vector3 feet)
        {
            Collider[] overlaps = Physics.OverlapCapsule(feet + Vector3.up * 0.5f, feet + Vector3.up * 1.3f, 0.28f, ~0,
                QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in overlaps)
            {
                if (!IgnoredForFraming(overlap)) return true;
            }

            return false;
        }

        /// <summary>The body point a check or treatment concerns (chest for breathing, forearm for circulation ...).</summary>
        public Vector3 BodyPoint(Casualty casualty, string part)
        {
            HumanoidBody body = casualty.Body;
            Transform joint = body != null ? body.Joint(part) : null;
            if (joint == null) return casualty.FocusPoint;
            if (part.StartsWith("LowerArm") || part.StartsWith("LowerLeg"))
            {
                // Mid-forearm / mid-shin: halfway to the child joint or along the segment.
                Vector3 along = joint.childCount > 0 ? joint.GetChild(0).position - joint.position : -joint.up * 0.25f;
                if (along.magnitude > 0.5f || along.magnitude < 0.05f) along = -joint.up * 0.22f;
                return joint.position + along * 0.6f;
            }

            return joint.position;
        }

        /// <summary>Picks the left or right limb nearer the player, so the marker is not hidden behind the body.</summary>
        private string NearerSide(Casualty casualty, string part)
        {
            string stem = part.Substring(0, part.Length - 2);
            Transform left = casualty.Body != null ? casualty.Body.Joint(stem + ".L") : null;
            Transform right = casualty.Body != null ? casualty.Body.Joint(stem + ".R") : null;
            if (left == null || right == null || player == null) return part;
            Vector3 eye = player.PlayerCamera != null ? player.PlayerCamera.transform.position : player.transform.position;
            return (left.position - eye).sqrMagnitude < (right.position - eye).sqrMagnitude ? stem + ".L" : stem + ".R";
        }

        private static string ExamPart(Casualty casualty, ExamAction action)
        {
            switch (action)
            {
                case ExamAction.CheckBreathing: return "Chest";
                case ExamAction.OpenAirway:
                case ExamAction.GiveCommand: return "Head";
                case ExamAction.CheckCirculation: return "LowerArm.R";
                case ExamAction.AskToWalk: return "LowerLeg.L";
                default: return BleedingPart(casualty);
            }
        }

        private static string BleedingPart(Casualty casualty)
        {
            switch (BleedingSite(casualty))
            {
                case "leg": return "LowerLeg.L";
                case "arm": return "LowerArm.R";
                case "head": return "Head";
                default: return "Spine";
            }
        }

        private static string TreatmentPart(Casualty casualty, Treatment treatment)
        {
            switch (treatment)
            {
                case Treatment.PressureDressing: return BleedingPart(casualty);
                case Treatment.Reassure: return "Head";
                default: return "Chest";
            }
        }

        private void ShowMarker(Casualty casualty, string part, string label)
        {
            if (InstantActions || casualty == null) return;
            if (part.EndsWith(".R") || part.EndsWith(".L")) part = NearerSide(casualty, part);
            bodyMarker.Show(() => BodyPoint(casualty, part), label);
        }
    }
}
