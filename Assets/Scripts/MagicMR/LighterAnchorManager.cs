using System.Collections;
using UnityEngine;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    /// <summary>
    /// Hybrid tracking for consumer PICO (no markerless 6DoF object tracking):
    /// <list type="bullet">
    /// <item><see cref="TrackingMode.Pinned"/> — static spatial registration on the desk.</item>
    /// <item><see cref="TrackingMode.FollowHandWhileGripping"/> — pseudo-dynamic
    /// tracking by attaching the virtual target to the gripping hand.</item>
    /// </list>
    /// Rule-dimension evade intentionally decouples virtual from physical; grip
    /// attach uses distance to the <b>pinned registration</b>, not the fled pose.
    /// </summary>
    public class LighterAnchorManager : MonoBehaviour
    {
        public enum TrackingMode
        {
            Uncalibrated = 0,
            Pinned = 1,
            FollowHandWhileGripping = 2
        }

        [SerializeField]
        PinchGestureDetector m_PinchDetector;

        [SerializeField]
        Transform m_Target;

        [SerializeField]
        Camera m_UserCamera;

        [SerializeField]
        bool m_PersistAcrossRestarts = true;

        [SerializeField]
        bool m_AttachNativeAnchor = false;

        [SerializeField]
        bool m_EnableHandFollow = true;

        [SerializeField]
        float m_MinCalibrationDistanceFromCamera = 0.28f;

        [SerializeField]
        float m_MaxCalibrationDistanceFromCamera = 1.6f;

        [SerializeField]
        float m_CalibrationCooldownSeconds = 1.5f;

        [SerializeField]
        float m_AnchorJumpThresholdMeters = 0.35f;

        [SerializeField]
        float m_GripAttachDistance = StudySpec.GripAttachDistance;

        [SerializeField]
        float m_GripAttachDwellSeconds = StudySpec.GripAttachDwellSeconds;

        [SerializeField]
        float m_ReleasePinDelaySeconds = StudySpec.GripReleasePinDelaySeconds;

        const string k_AnchorUuidPrefKey = "MagicMR_LighterAnchorUuid";

        Transform m_LoadedAnchorSource;
        RealityEditor m_RealityEditor;
        Rigidbody m_TargetBody;
        Vector3 m_PinnedPosition;
        Quaternion m_PinnedRotation;
        Vector3 m_LastGoodPosition;
        Quaternion m_LastGoodRotation;
        float m_DeskPlaneY;
        float m_NextCalibrationTime;
        float m_WatchAnchorUntil;
        float m_ReleaseTimer;
        float m_AttachDwellTimer;
        bool m_WasGripping;
        /// <summary>
        /// After embodied registration, ignore FollowHand until the calibration
        /// grip is fully released — otherwise the lighter sticks to the hand
        /// and never appears pinned on the desk.
        /// </summary>
        bool m_AwaitingGripReleaseAfterCalibrate;

        public bool IsCalibrated => Mode != TrackingMode.Uncalibrated;
        public TrackingMode Mode { get; private set; } = TrackingMode.Uncalibrated;
        public bool IsFollowingHand => Mode == TrackingMode.FollowHandWhileGripping;
        public Vector3 PinnedPosition => m_PinnedPosition;
        public Quaternion PinnedRotation => m_PinnedRotation;

        /// <summary>Fired when the lighter is pinned (embodied registration ritual).</summary>
        public event System.Action Calibrated;

        public void SetDeskReferenceHeight(float worldY)
        {
            m_DeskPlaneY = worldY;
            Debug.Log($"[MagicMR] Desk reference height set to {worldY:F2}.", this);
        }

        void Awake()
        {
            if (m_Target == null)
            {
                var lighter = GameObject.Find("Lighter");
                if (lighter != null)
                    m_Target = lighter.transform;
            }

            if (m_Target != null)
            {
                m_RealityEditor = m_Target.GetComponent<RealityEditor>();
                m_TargetBody = m_Target.GetComponent<Rigidbody>();
            }

            if (m_PinchDetector == null)
            {
                var root = GameObject.Find("GestureDetectors");
                if (root != null)
                    m_PinchDetector = root.GetComponent<PinchGestureDetector>();
            }

            if (m_UserCamera == null)
                m_UserCamera = Camera.main;
        }

        void OnEnable()
        {
            PicoSpatialAnchor.StartProvider();
            // Calibration + FollowHand are owned by MRGestureController FSM.
            // Keep listener only as a no-op fallback when the controller is absent.
            if (MRGestureController.Instance == null)
                BindPinchHoldListener();

            if (m_PersistAcrossRestarts)
                TryLoadSavedAnchor();
        }

        void OnDisable()
        {
            UnbindPinchHoldListener();
            if (Mode == TrackingMode.FollowHandWhileGripping)
                PinAtCurrentPose("disabled");
        }

        public void BindPinchHoldListener()
        {
            UnbindPinchHoldListener();

            // FSM owns calibration — do not double-fire HeldEvent → CalibrateAt.
            if (MRGestureController.Instance != null)
            {
                Debug.Log("[MagicMR] LighterAnchorManager: FSM owns calibration; HeldEvent unbound.", this);
                return;
            }

            if (m_PinchDetector == null)
            {
                var root = GameObject.Find("GestureDetectors");
                if (root != null)
                    m_PinchDetector = root.GetComponent<PinchGestureDetector>();
            }

            if (m_PinchDetector != null)
            {
                m_PinchDetector.HeldEvent.AddListener(OnPinchHeld);
                Debug.Log("[MagicMR] LighterAnchorManager listening for pinch-and-hold calibration.", this);
            }
            else
            {
                Debug.LogWarning("[MagicMR] LighterAnchorManager found no PinchGestureDetector to calibrate from.", this);
            }
        }

        void UnbindPinchHoldListener()
        {
            if (m_PinchDetector != null)
                m_PinchDetector.HeldEvent.RemoveListener(OnPinchHeld);
        }

        void LateUpdate()
        {
            // When FSM is present it drives FollowHand; only keep anchor safety.
            if (MRGestureController.Instance == null)
                UpdateHybridTracking();
            UpdateLoadedAnchorFollow();
            UpdateNativeAnchorWatch();
        }

        /// <summary>Called by <see cref="MRGestureController"/> when entering FollowingHand.</summary>
        public void BeginFollowHand(Vector3 position, Quaternion rotation)
        {
            EnterFollowHand(position, rotation);
        }

        /// <summary>Called by <see cref="MRGestureController"/> each frame while following.</summary>
        public void UpdateFollowHand(Vector3 position, Quaternion rotation)
        {
            if (Mode != TrackingMode.FollowHandWhileGripping)
                EnterFollowHand(position, rotation);
            else
                ApplyFollowPose(position, rotation);
        }

        /// <summary>Called by <see cref="MRGestureController"/> when leaving FollowingHand.</summary>
        public void EndFollowHand(string reason)
        {
            PinAtCurrentPose(reason);
        }

        void UpdateHybridTracking()
        {
            if (!m_EnableHandFollow || m_Target == null || m_PinchDetector == null)
                return;

            if (m_RealityEditor != null && m_RealityEditor.IsDeconstructed)
            {
                if (Mode == TrackingMode.FollowHandWhileGripping)
                    PinAtCurrentPose("deconstructed");
                return;
            }

            var gripping = m_PinchDetector.IsAnyHandHolding;
            if (gripping)
                m_ReleaseTimer = 0f;

            // Stay nailed to the desk until the user opens the calibration grip.
            if (m_AwaitingGripReleaseAfterCalibrate)
            {
                if (Mode == TrackingMode.FollowHandWhileGripping)
                    SnapBackToPinned("post-calibrate-guard");

                if (!gripping)
                {
                    m_AwaitingGripReleaseAfterCalibrate = false;
                    Debug.Log("[MagicMR] Calibration grip released — FollowHand enabled.", this);
                }

                m_WasGripping = gripping;
                return;
            }

            if (Mode == TrackingMode.FollowHandWhileGripping)
            {
                if (gripping && TryGetGripPose(out var gripPos, out var gripRot))
                {
                    ApplyFollowPose(gripPos, gripRot);
                    m_WasGripping = true;
                    return;
                }

                if (!gripping)
                {
                    m_ReleaseTimer += Time.deltaTime;
                    if (m_ReleaseTimer >= m_ReleasePinDelaySeconds)
                        PinAtCurrentPose("released");
                }

                return;
            }

            // Pinned: require a sustained grip near the registration point
            // (dwell) so Appearance taps do not yank the lighter onto the hand.
            if (Mode == TrackingMode.Pinned && gripping && TryGetGripPose(out var handPos, out var handRot))
            {
                var distToPin = Vector3.Distance(handPos, m_PinnedPosition);
                if (distToPin <= m_GripAttachDistance)
                {
                    m_AttachDwellTimer += Time.deltaTime;
                    if (m_AttachDwellTimer >= m_GripAttachDwellSeconds)
                    {
                        m_AttachDwellTimer = 0f;
                        EnterFollowHand(handPos, handRot);
                        return;
                    }
                }
                else
                {
                    m_AttachDwellTimer = 0f;
                }
            }
            else
            {
                m_AttachDwellTimer = 0f;
            }

            m_WasGripping = gripping;
        }

        void SnapBackToPinned(string reason)
        {
            if (m_Target == null)
                return;

            Mode = TrackingMode.Pinned;
            SetBodyKinematic(false);
            m_Target.SetPositionAndRotation(m_PinnedPosition, m_PinnedRotation);
            m_RealityEditor?.SyncWorldAnchor(m_PinnedPosition);
            m_LastGoodPosition = m_PinnedPosition;
            m_LastGoodRotation = m_PinnedRotation;
            m_ReleaseTimer = 0f;
            Debug.Log($"[MagicMR] SnapBackToPinned ({reason}) at {m_PinnedPosition}.", m_Target);
        }

        void EnterFollowHand(Vector3 position, Quaternion rotation)
        {
            Mode = TrackingMode.FollowHandWhileGripping;
            m_LoadedAnchorSource = null;
            PicoSpatialAnchor.RemoveAnchor(m_Target.gameObject);
            SetBodyKinematic(true);
            ApplyFollowPose(position, rotation);
            m_WasGripping = true;
            m_ReleaseTimer = 0f;
            Debug.Log($"[MagicMR] TrackingMode -> FollowHandWhileGripping at {position}.", m_Target);
        }

        void ApplyFollowPose(Vector3 position, Quaternion rotation)
        {
            m_Target.SetPositionAndRotation(position, rotation);
            m_RealityEditor?.SyncWorldAnchor(position);
            if (m_TargetBody != null)
            {
                m_TargetBody.linearVelocity = Vector3.zero;
                m_TargetBody.angularVelocity = Vector3.zero;
            }
        }

        void PinAtCurrentPose(string reason)
        {
            if (m_Target == null)
                return;

            var position = m_Target.position;
            var rotation = m_Target.rotation;
            SetBodyKinematic(false);
            ApplyCalibrationPose(position, rotation, attachNativeAnchor: false, setModePinned: true);
            m_WasGripping = false;
            m_ReleaseTimer = 0f;
            Debug.Log($"[MagicMR] TrackingMode -> Pinned ({reason}) at {position}.", m_Target);
        }

        bool TryGetGripPose(out Vector3 position, out Quaternion rotation)
        {
            position = m_PinchDetector.LastPinchPosition;
            if (position == Vector3.zero)
                position = m_PinchDetector.LastIndexTipPosition;

            rotation = m_Target != null ? m_Target.rotation : Quaternion.identity;

#if XR_HANDS_1_1_OR_NEWER
            var subsystems = new System.Collections.Generic.List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count > 0)
            {
                var subsystem = subsystems[0];
                if (TryPalm(subsystem.rightHand, out var rightPalm) &&
                    Vector3.Distance(rightPalm.position, position) < 0.2f)
                {
                    position = Vector3.Lerp(rightPalm.position, position, 0.35f);
                    rotation = rightPalm.rotation;
                    return position != Vector3.zero;
                }

                if (TryPalm(subsystem.leftHand, out var leftPalm) &&
                    Vector3.Distance(leftPalm.position, position) < 0.2f)
                {
                    position = Vector3.Lerp(leftPalm.position, position, 0.35f);
                    rotation = leftPalm.rotation;
                    return position != Vector3.zero;
                }
            }
#endif
            return position != Vector3.zero;
        }

#if XR_HANDS_1_1_OR_NEWER
        static bool TryPalm(XRHand hand, out Pose pose)
        {
            pose = default;
            if (!hand.isTracked)
                return false;
            return hand.GetJoint(XRHandJointID.Palm).TryGetPose(out pose);
        }
#endif

        void SetBodyKinematic(bool kinematic)
        {
            if (m_TargetBody == null)
                return;

            m_TargetBody.isKinematic = kinematic;
            if (kinematic)
            {
                m_TargetBody.linearVelocity = Vector3.zero;
                m_TargetBody.angularVelocity = Vector3.zero;
            }
        }

        void UpdateLoadedAnchorFollow()
        {
            if (Mode == TrackingMode.FollowHandWhileGripping)
                return;

            if (m_LoadedAnchorSource == null || m_Target == null)
                return;

            var anchorPos = m_LoadedAnchorSource.position;
            if (!IsPlausibleWorldPosition(anchorPos))
            {
                Debug.LogWarning("[MagicMR] Loaded anchor position invalid; ignoring anchor follow.", this);
                m_LoadedAnchorSource = null;
                PlayerPrefs.DeleteKey(k_AnchorUuidPrefKey);
                return;
            }

            m_Target.SetPositionAndRotation(anchorPos, m_LoadedAnchorSource.rotation);
        }

        void UpdateNativeAnchorWatch()
        {
            if (Mode == TrackingMode.FollowHandWhileGripping)
                return;

            if (m_Target == null || Time.unscaledTime > m_WatchAnchorUntil)
                return;

            var drift = Vector3.Distance(m_Target.position, m_LastGoodPosition);
            if (drift <= m_AnchorJumpThresholdMeters)
                return;

            Debug.LogWarning(
                $"[MagicMR] Lighter jumped {drift:F2}m after calibration ({m_LastGoodPosition} -> {m_Target.position}); " +
                "removing native anchor and restoring pose.",
                m_Target);
            PicoSpatialAnchor.RemoveAnchor(m_Target.gameObject);
            m_Target.SetPositionAndRotation(m_LastGoodPosition, m_LastGoodRotation);
            m_WatchAnchorUntil = 0f;
        }

        void TryLoadSavedAnchor()
        {
            if (!m_AttachNativeAnchor)
                return;

            var savedUuid = PlayerPrefs.GetString(k_AnchorUuidPrefKey, string.Empty);
            if (string.IsNullOrEmpty(savedUuid))
                return;

            Debug.Log($"[MagicMR] Found saved anchor uuid {savedUuid}; attempting to load it.");
            StartCoroutine(PicoSpatialAnchor.TryLoadPersistedAnchor(savedUuid, OnSavedAnchorLoaded));
        }

        void OnSavedAnchorLoaded(GameObject loaded)
        {
            if (loaded == null || m_Target == null)
            {
                Debug.LogWarning("[MagicMR] Could not restore saved Lighter anchor; falling back to default placement.");
                PlayerPrefs.DeleteKey(k_AnchorUuidPrefKey);
                return;
            }

            if (!IsPlausibleWorldPosition(loaded.transform.position))
            {
                Debug.LogWarning("[MagicMR] Saved anchor restored to an implausible position; discarding it.");
                PlayerPrefs.DeleteKey(k_AnchorUuidPrefKey);
                Destroy(loaded);
                return;
            }

            m_LoadedAnchorSource = loaded.transform;
            ApplyCalibrationPose(loaded.transform.position, loaded.transform.rotation, attachNativeAnchor: false, setModePinned: true);
            Debug.Log("[MagicMR] Lighter restored to persisted real-world anchor.", m_Target);
        }

        void OnPinchHeld()
        {
            if (m_PinchDetector == null)
                return;

            // Only step-1 registration uses sustained hold. After pinned, ignore
            // loose holds — otherwise resting fingers ≤14cm re-calibrate forever
            // and GestureManager blocks Appearance/Agency/Rule/Deconstruction.
            if (Mode != TrackingMode.Uncalibrated)
            {
                Debug.Log("[MagicMR] Pinch-hold ignored (already calibrated).");
                return;
            }

            if (Time.unscaledTime < m_NextCalibrationTime)
            {
                Debug.Log("[MagicMR] Calibration ignored (cooldown).");
                return;
            }

            Debug.Log("[MagicMR] Pinch-and-hold received; calibrating Lighter to hand position.");
            var aimPoint = m_PinchDetector.LastIndexTipPosition;
            if (aimPoint == Vector3.zero)
                aimPoint = m_PinchDetector.LastPinchPosition;
            CalibrateAt(aimPoint);
            m_NextCalibrationTime = Time.unscaledTime + m_CalibrationCooldownSeconds;
            m_PinchDetector.ResetForNewTrial();
            m_PinchDetector.SuppressTapFor(0.6f);
        }

        public void CalibrateAt(Vector3 rawAimPoint)
        {
            if (m_Target == null)
            {
                Debug.LogWarning("[MagicMR] LighterAnchorManager has no target to calibrate.", this);
                return;
            }

            var position = RefineCalibrationPosition(rawAimPoint);
            if (!IsPlausibleWorldPosition(position))
            {
                Debug.LogWarning($"[MagicMR] Ignoring calibration at implausible position {position}.", this);
                return;
            }

            m_LoadedAnchorSource = null;
            PicoSpatialAnchor.RemoveAnchor(m_Target.gameObject);
            SetBodyKinematic(false);

            var rotation = m_Target.rotation;
            if (m_UserCamera != null)
            {
                var toUser = m_UserCamera.transform.position - position;
                toUser.y = 0f;
                if (toUser.sqrMagnitude > 0.0001f)
                    rotation = Quaternion.LookRotation(-toUser.normalized, Vector3.up);
            }

            ApplyCalibrationPose(position, rotation, m_AttachNativeAnchor, setModePinned: true, emitCalibrated: true);
            m_AwaitingGripReleaseAfterCalibrate = true;
            StartCoroutine(CalibrationPulse());
            Debug.Log("[MagicMR] Pinned on desk — open your hand before FollowHand / gestures.", this);
        }

        Vector3 RefineCalibrationPosition(Vector3 rawAimPoint)
        {
            var deskY = m_DeskPlaneY;
            if (deskY <= 0f && m_Target != null)
                deskY = m_Target.position.y;
            if (deskY <= 0f)
                deskY = rawAimPoint.y;

            if (m_UserCamera == null)
            {
                rawAimPoint.y = deskY;
                return ClampCalibrationPosition(rawAimPoint);
            }

            var rayOrigin = m_UserCamera.transform.position;
            var rayDir = rawAimPoint - rayOrigin;
            if (rayDir.sqrMagnitude < 0.0001f)
            {
                rawAimPoint.y = deskY;
                return ClampCalibrationPosition(rawAimPoint);
            }

            rayDir.Normalize();
            if (Mathf.Abs(rayDir.y) > 0.001f)
            {
                var t = (deskY - rayOrigin.y) / rayDir.y;
                if (t > 0f)
                {
                    var deskHit = rayOrigin + rayDir * t;
                    Debug.Log($"[MagicMR] Calibration ray -> desk plane Y={deskY:F2} at {deskHit}.");
                    return ClampCalibrationPosition(deskHit);
                }
            }

            rawAimPoint.y = deskY;
            return ClampCalibrationPosition(rawAimPoint);
        }

        Vector3 ClampCalibrationPosition(Vector3 position)
        {
            if (m_UserCamera == null)
                return position;

            var camPos = m_UserCamera.transform.position;
            var offset = position - camPos;
            offset.y = 0f;
            var horizDistance = offset.magnitude;
            if (horizDistance < 0.0001f)
                return position;

            if (horizDistance < m_MinCalibrationDistanceFromCamera)
            {
                var flatDir = offset / horizDistance;
                position = camPos + flatDir * m_MinCalibrationDistanceFromCamera;
            }
            else if (horizDistance > m_MaxCalibrationDistanceFromCamera)
            {
                var flatDir = offset / horizDistance;
                position = camPos + flatDir * m_MaxCalibrationDistanceFromCamera;
            }

            return position;
        }

        void ApplyCalibrationPose(
            Vector3 position,
            Quaternion rotation,
            bool attachNativeAnchor,
            bool setModePinned,
            bool emitCalibrated = false)
        {
            m_RealityEditor?.EnsureVisible();
            m_Target.SetPositionAndRotation(position, rotation);
            m_RealityEditor?.SyncWorldAnchor(position);

            m_LastGoodPosition = position;
            m_LastGoodRotation = rotation;
            m_PinnedPosition = position;
            m_PinnedRotation = rotation;

            if (setModePinned)
                Mode = TrackingMode.Pinned;

            Debug.Log(
                $"[MagicMR] Lighter calibrated/pinned to {position} " +
                $"(mode={Mode}, native anchor: {attachNativeAnchor}).",
                m_Target);

            if (emitCalibrated)
                Calibrated?.Invoke();

            if (!attachNativeAnchor)
                return;

            m_WatchAnchorUntil = Time.unscaledTime + 3f;
            StartCoroutine(AttachNativeAnchorWhenStable(position));
        }

        IEnumerator AttachNativeAnchorWhenStable(Vector3 expectedPosition)
        {
            yield return null;
            yield return null;

            if (m_Target == null || Mode == TrackingMode.FollowHandWhileGripping)
                yield break;

            if (Vector3.Distance(m_Target.position, expectedPosition) > 0.15f)
            {
                Debug.LogWarning("[MagicMR] Lighter moved before anchor attach; skipping native anchor.", m_Target);
                yield break;
            }

            var attached = PicoSpatialAnchor.AttachAnchor(m_Target.gameObject);
            Debug.Log($"[MagicMR] Native spatial anchor attached: {attached}", m_Target);

            if (!attached || !m_PersistAcrossRestarts)
                yield break;

            yield return PicoSpatialAnchor.PersistWhenReady(m_Target.gameObject, uuid =>
            {
                if (string.IsNullOrEmpty(uuid))
                {
                    Debug.LogWarning("[MagicMR] Anchor persistence failed; it will not survive an app restart.");
                    return;
                }

                PlayerPrefs.SetString(k_AnchorUuidPrefKey, uuid);
                PlayerPrefs.Save();
                Debug.Log($"[MagicMR] Saved anchor uuid {uuid} for next launch.");
            });
        }

        bool IsPlausibleWorldPosition(Vector3 position)
        {
            if (m_UserCamera == null)
                return position.sqrMagnitude > 0.01f;

            if (position.sqrMagnitude < 0.01f)
                return false;

            var distance = Vector3.Distance(m_UserCamera.transform.position, position);
            return distance >= m_MinCalibrationDistanceFromCamera * 0.5f &&
                   distance <= m_MaxCalibrationDistanceFromCamera * 1.25f;
        }

        IEnumerator CalibrationPulse()
        {
            if (m_Target == null)
                yield break;

            var original = m_Target.localScale;
            var peak = original * 1.35f;
            const float duration = 0.35f;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                if (Mode == TrackingMode.FollowHandWhileGripping)
                {
                    m_Target.localScale = original;
                    yield break;
                }

                elapsed += Time.deltaTime;
                var t = elapsed / duration;
                m_Target.localScale = t < 0.5f
                    ? Vector3.Lerp(original, peak, t * 2f)
                    : Vector3.Lerp(peak, original, (t - 0.5f) * 2f);
                yield return null;
            }

            m_Target.localScale = original;
        }

        /// <summary>
        /// After study reset: stop hand-follow and snap back to the last pin pose.
        /// </summary>
        public void ReturnToPinned()
        {
            if (m_Target == null || Mode == TrackingMode.Uncalibrated)
                return;

            Mode = TrackingMode.Pinned;
            m_ReleaseTimer = 0f;
            m_AttachDwellTimer = 0f;
            m_AwaitingGripReleaseAfterCalibrate = false;
            SetBodyKinematic(false);
            m_Target.SetPositionAndRotation(m_PinnedPosition, m_PinnedRotation);
            m_LastGoodPosition = m_PinnedPosition;
            m_LastGoodRotation = m_PinnedRotation;
            m_RealityEditor?.SyncWorldAnchor(m_PinnedPosition);
            Debug.Log($"[MagicMR] Returned to pinned pose {m_PinnedPosition}.", m_Target);
        }

        /// <summary>
        /// Clear pin + follow so the embodied registration ritual can run again.
        /// </summary>
        public void BeginRecalibration()
        {
            if (m_Target == null)
                return;

            Mode = TrackingMode.Uncalibrated;
            m_AwaitingGripReleaseAfterCalibrate = false;
            m_AttachDwellTimer = 0f;
            m_ReleaseTimer = 0f;
            m_NextCalibrationTime = 0f;
            SetBodyKinematic(false);
            PicoSpatialAnchor.RemoveAnchor(m_Target.gameObject);
            m_LoadedAnchorSource = null;

            m_RealityEditor?.ResetTarget();
            m_Target.localScale = Vector3.one * 0.08f;

            m_PinchDetector?.ResetForNewTrial();

            Debug.Log("[MagicMR] BeginRecalibration — wireframe ritual, lighter hidden.", this);
        }
    }
}
