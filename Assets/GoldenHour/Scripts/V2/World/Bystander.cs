using UnityEngine;

namespace GoldenHour
{
    /// <summary>A panicking bystander who wanders and shouts until the player calms them and sends them to the green zone.</summary>
    public sealed class Bystander : MonoBehaviour, IInteractable
    {
        private static readonly string[] PanicLines =
        {
            "Oh my god, oh my god!", "Somebody do something!", "Is she breathing?!", "I called my mum, I don't know what to do!",
            "There's petrol everywhere!", "Help! Over here!"
        };

        private HumanoidBody body;
        private Walker walker;
        private SpeechBark bark;
        private Vector3 anchor;
        private Vector3 greenZone;
        private float nextMove;
        private float nextBark;
        private System.Random random;

        public bool Calmed { get; private set; }
        public string Prompt => "Calm bystander - send to the green zone";
        public bool CanInteract => !Calmed;
        public Vector3 FocusPoint => transform.position + Vector3.up * 1.4f;

        public void Initialize(Vector3 start, Vector3 wanderAnchor, Vector3 green, int seed, GameObject rigPrefab)
        {
            random = new System.Random(seed);
            anchor = wanderAnchor;
            greenZone = green;
            transform.position = start;
            Color[] colors =
            {
                new Color32(160, 70, 60, 255), new Color32(70, 90, 140, 255), new Color32(90, 120, 80, 255), new Color32(130, 110, 90, 255)
            };
            body = new GameObject("Body").AddComponent<HumanoidBody>();
            body.transform.SetParent(transform, false);
            body.Build(BodyStyle.Civilian, colors[random.Next(colors.Length)], 1f, rigPrefab, seed);
            body.Restless = true;
            walker = gameObject.AddComponent<Walker>();
            walker.Initialize(body, 2.4f);
            bark = gameObject.AddComponent<SpeechBark>();
            bark.Initialize(2.2f);
            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.3f;
            Walker.SetCharacterLayer(gameObject);
            nextMove = Time.time + 1f;
            nextBark = Time.time + 2f + (float)random.NextDouble() * 4f;
        }

        public void Interact()
        {
            if (IncidentDirector.Instance != null) IncidentDirector.Instance.CalmBystander(this);
        }

        public void Calm()
        {
            Calmed = true;
            body.Restless = false;
            bark.Say("Okay... okay. Green zone. Got it.");
            Vector3 offset = new Vector3((float)random.NextDouble() * 3f - 1.5f, 0f, (float)random.NextDouble() * 3f - 1.5f);
            walker.GoTo(greenZone + offset, null);
        }

        private void Update()
        {
            if (Calmed) return;
            if (!walker.Moving && Time.time > nextMove)
            {
                nextMove = Time.time + 2f + (float)random.NextDouble() * 4f;
                Vector3 target = anchor + new Vector3((float)random.NextDouble() * 12f - 6f, 0f, (float)random.NextDouble() * 10f - 5f);
                walker.GoTo(target, null);
            }

            if (Time.time > nextBark)
            {
                nextBark = Time.time + 6f + (float)random.NextDouble() * 6f;
                bark.Say(PanicLines[random.Next(PanicLines.Length)]);
            }
        }
    }
}
