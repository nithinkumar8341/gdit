using System;
using System.IO;
using TMPro;
using UnityEngine;

namespace GoldenHour
{
    /// <summary>
    /// F10 photo mode: freezes the simulation (time scale 0), hides the HUD and hands, and gives a
    /// free camera (move keys + look, Q/E down/up) limited to 12 m from where it started.
    /// Space saves a PNG to persistentDataPath/Photos. F10 or Esc returns to play.
    /// </summary>
    public sealed class PhotoMode : MonoBehaviour
    {
        public const float MaxDistance = 12f;

        public static PhotoMode Instance { get; private set; }
        public bool Active { get; private set; }
        public string LastPhotoPath { get; private set; }
        public int ExitFrame { get; private set; } = -1;

        private IncidentDirector director;
        private Transform view;
        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private Vector3 anchor;
        private float yaw;
        private float pitch;
        private float savedTimeScale = 1f;
        private GameObject overlay;
        private bool captureNextFrame;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Active) Time.timeScale = savedTimeScale;
        }

        private void Start()
        {
            director = GetComponent<IncidentDirector>();
        }

        public bool CanEnter => director != null && !Active && !director.Paused && !director.Modal.IsOpen && !director.ActionRunning &&
                                (director.Phase == IncidentPhase.Safety || director.Phase == IncidentPhase.Response);

        private void Update()
        {
            if (director == null) return;
            if (!Active)
            {
                if (GameInput.Pressed(GameInput.PhotoMode) && CanEnter) Enter();
                return;
            }

            if (GameInput.Pressed(GameInput.PhotoMode) || GameInput.Pressed(GameInput.Pause))
            {
                Exit();
                return;
            }

            float dt = Time.unscaledDeltaTime;
            Vector2 look = GameInput.LookMouse.ReadValue<Vector2>() * 0.1f * GameSettings.MouseSensitivity +
                           GameInput.LookStick.ReadValue<Vector2>() * 120f * dt;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
            Vector2 move = GameInput.Move.ReadValue<Vector2>();
            float rise = GameInput.Rise.ReadValue<float>();
            float speed = GameInput.Held(GameInput.Sprint) ? 6f : 2.5f;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 position = view.position + (rotation * new Vector3(move.x, 0f, move.y) + Vector3.up * rise) * speed * dt;
            Vector3 offset = position - anchor;
            if (offset.magnitude > MaxDistance) position = anchor + offset.normalized * MaxDistance;
            position.y = Mathf.Max(0.2f, position.y);
            view.SetPositionAndRotation(position, rotation);
            if (GameInput.Pressed(GameInput.Primary)) Capture();
        }

        public void Enter()
        {
            if (!CanEnter) return;
            view = director.playerCamera;
            if (view == null) return;
            Active = true;
            savedPosition = view.localPosition;
            savedRotation = view.localRotation;
            anchor = view.position;
            Vector3 euler = view.rotation.eulerAngles;
            yaw = euler.y;
            pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (director.player != null) director.player.SetControlsEnabled(false);
            director.Hud.SetVisible(false);
            if (director.Hud.OverlayRoot != null) director.Hud.OverlayRoot.SetActive(false);
            if (Viewmodel.Instance != null) Viewmodel.Instance.SetVisible(false);
            BuildOverlay();
            director.Log("photo", string.Empty, "Photo mode opened");
        }

        public void Exit()
        {
            if (!Active) return;
            Active = false;
            ExitFrame = Time.frameCount;
            view.localPosition = savedPosition;
            view.localRotation = savedRotation;
            Time.timeScale = savedTimeScale;
            if (overlay != null) Destroy(overlay);
            director.Hud.SetVisible(true);
            if (director.Hud.OverlayRoot != null) director.Hud.OverlayRoot.SetActive(true);
            if (Viewmodel.Instance != null) Viewmodel.Instance.SetVisible(true);
            if (director.player != null) director.player.SetControlsEnabled(true);
        }

        /// <summary>Saves the next rendered frame (without the photo-mode hint) as a PNG.</summary>
        public void Capture()
        {
            string folder = Path.Combine(Application.persistentDataPath, "Photos");
            Directory.CreateDirectory(folder);
            LastPhotoPath = Path.Combine(folder, "GoldenHour_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png");
            if (overlay != null) overlay.SetActive(false);
            ScreenCapture.CaptureScreenshot(LastPhotoPath);
            captureNextFrame = true;
        }

        private void LateUpdate()
        {
            if (!captureNextFrame) return;
            captureNextFrame = false;
            if (overlay != null && Active) overlay.SetActive(true);
        }

        private void BuildOverlay()
        {
            Canvas canvas = UiFactory.CreateCanvas("Photo Mode Overlay");
            canvas.sortingOrder = 65;
            overlay = canvas.gameObject;
            TMP_Text hint = UiFactory.Text(canvas.transform,
                "<b>PHOTO MODE</b>  (clock paused)   Move " + GameInput.KeyName(GameInput.Move) + "  |  Up/Down " + GameInput.KeyName(GameInput.Rise) +
                "  |  Save " + GameInput.KeyName(GameInput.Primary) + "  |  Exit " + GameInput.KeyName(GameInput.PhotoMode),
                18, new Vector2(1400f, 40f), new Vector2(0f, -500f), TextAlignmentOptions.Center);
            hint.color = Palette.White;
        }
    }
}
