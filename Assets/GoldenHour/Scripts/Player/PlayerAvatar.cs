using UnityEngine;
using UnityEngine.Rendering;

namespace GoldenHour
{
    /// <summary>
    /// The visible main character: the skinned Blender model (PlayerCharacter.fbx, red responder jacket with reflective
    /// bands) placed at the player's feet and animated with its baked legacy clips (Idle, Walk, Run, Kneel) from the
    /// controller's speed and kneel state. In third person the whole body is drawn; in first person it only casts a
    /// shadow (the camera sits inside the head).
    /// </summary>
    [RequireComponent(typeof(FirstPersonPlayer))]
    public sealed class PlayerAvatar : MonoBehaviour
    {
        /// <summary>Blender PlayerCharacter.fbx, wired by Build Project.</summary>
        public GameObject model;

        public const float RunThreshold = 4.6f;
        public const float WalkThreshold = 0.3f;

        private FirstPersonPlayer player;
        private GameObject instance;
        private Animation clips;
        private Renderer[] renderers = new Renderer[0];
        private string idleClip;
        private string walkClip;
        private string runClip;
        private string kneelClip;
        private bool shadowsOnly = true;

        public bool Built => instance != null && clips != null;
        public bool BodyVisible => Built && !shadowsOnly;
        public string CurrentClip { get; private set; } = string.Empty;
        public int ClipCount { get; private set; }
        public Transform Root => instance != null ? instance.transform : null;
        public SkinnedMeshRenderer Skin { get; private set; }

        private void Awake()
        {
            player = GetComponent<FirstPersonPlayer>();
            if (model == null) return;
            instance = Instantiate(model, transform, false);
            instance.name = "Player Character";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>()) Destroy(collider);

            clips = instance.GetComponentInChildren<Animation>();
            if (clips != null)
            {
                clips.cullingType = AnimationCullingType.AlwaysAnimate;
                foreach (AnimationState state in clips)
                {
                    ClipCount++;
                    string clipName = state.name;
                    if (clipName.Contains("Idle")) idleClip = clipName;
                    else if (clipName.Contains("Walk")) walkClip = clipName;
                    else if (clipName.Contains("Run")) runClip = clipName;
                    else if (clipName.Contains("Kneel")) kneelClip = clipName;
                    state.wrapMode = clipName.Contains("Kneel") ? WrapMode.ClampForever : WrapMode.Loop;
                }
            }

            renderers = instance.GetComponentsInChildren<Renderer>();
            Skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            if (Skin != null) Skin.updateWhenOffscreen = true;
            SetBodyDrawn(false);
            Play(idleClip, 0f);
        }

        /// <summary>True when the clip for <paramref name="kind"/> (Idle, Walk, Run, Kneel) is the one playing.</summary>
        public bool Playing(string kind) => Built && CurrentClip.Contains(kind) && clips.IsPlaying(CurrentClip);

        private void SetBodyDrawn(bool drawn)
        {
            shadowsOnly = !drawn;
            foreach (Renderer part in renderers)
            {
                if (part == null) continue;
                part.shadowCastingMode = drawn ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
            }
        }

        private void Play(string clipName, float fade)
        {
            if (clips == null || string.IsNullOrEmpty(clipName) || clipName == CurrentClip) return;
            CurrentClip = clipName;
            if (fade <= 0f) clips.Play(clipName);
            else clips.CrossFade(clipName, fade);
        }

        private void LateUpdate()
        {
            if (!Built || player == null) return;
            bool drawn = player.ThirdPersonActive;
            if (drawn == shadowsOnly) SetBodyDrawn(drawn);

            float speed = player.Speed;
            string next = player.IsKneeling ? kneelClip : speed > RunThreshold ? runClip : speed > WalkThreshold ? walkClip : idleClip;
            Play(next ?? idleClip, 0.18f);
            AnimationState state = string.IsNullOrEmpty(CurrentClip) ? null : clips[CurrentClip];
            if (state == null) return;
            if (CurrentClip == walkClip) state.speed = Mathf.Clamp(speed / 3.2f, 0.6f, 1.6f);
            else if (CurrentClip == runClip) state.speed = Mathf.Clamp(speed / 5.6f, 0.8f, 1.5f);
            else state.speed = 1f;
        }
    }
}
