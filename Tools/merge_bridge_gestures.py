"""Merge VstTest XR Origin + gesture stack into BridgeTest.unity (one-shot)."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(r"d:\Unity\Project\magic-wand-amusement-park")
VST = ROOT / "Assets/Scenes/VstTest.unity"
BRIDGE = ROOT / "Assets/Scenes/BridgeTest.unity"

# VstTest IDs (exclude VstTestRunner 44745101)
NEEDED = {
    44745095,
    44745096,
    44745097,
    44745098,
    44745099,
    44745100,
    1095442946,
    1095442947,
    1439005889,
    1439005890,
    1439005891,
    1439005892,
    1439005893,
    1439005894,
    189437447,
    189437448,
    189437449,
}

GUID_PINCH = "a85d3eccc03e2495ba0f760917496a52"
GUID_CIRCLE = "9ad2dab3070424df1a30cb8b315ac9f8"
GUID_SWIPE = "c61d22baee5dd4719a7cf80da628b839"
GUID_SNAP = "02447a778186d4577bafd1b852a9f142"
GUID_FIST = "8397f7661b1bd4ca08ea906ba7fa24f6"
GUID_GESTURE_MGR = "1cb706bbd04d41b4ae5cd7d28a329437"
GUID_GATE = "b5c6d7e8f90123456789abcdef012345"
GUID_BOOT = "c6d7e8f90123456789abcdef01234567"


def parse_docs(text: str):
    parts = re.split(r"(?=^--- !u!)", text, flags=re.M)
    docs = []
    for p in parts:
        if not p.startswith("--- !u!"):
            continue
        m = re.match(r"--- !u!(\d+) &(\d+)", p)
        if not m:
            continue
        docs.append((int(m.group(1)), int(m.group(2)), p))
    return docs


def remap_text(text: str, mapping: dict[int, int]) -> str:
    for old in sorted(mapping.keys(), reverse=True):
        new = mapping[old]
        text = re.sub(rf"&{old}\b", f"&{new}", text)
        text = re.sub(rf"\{{fileID: {old}\}}", f"{{fileID: {new}}}", text)
    return text


def main() -> None:
    vst = VST.read_text(encoding="utf-8")
    bridge = BRIDGE.read_text(encoding="utf-8")

    if "GestureDetectors" in bridge and "BridgeGestureBootstrap" in bridge:
        print("BridgeTest already contains gesture stack; aborting.")
        return

    if "m_Name: XR Origin (VR)" in bridge or "PICO Video Seethrough XR Origin" in bridge:
        print("BridgeTest already has XR Origin; aborting XR merge.")
        return

    selected = [(t, i, p) for t, i, p in parse_docs(vst) if i in NEEDED]
    old_ids = sorted({i for _, i, _ in selected})
    start = 820000100
    mapping = {old: start + idx for idx, old in enumerate(old_ids)}

    remapped_chunks = []
    for _, i, p in selected:
        rp = remap_text(p, mapping)
        if i == 44745095:
            rp = rp.replace("  - component: {fileID: 44745101}\n", "")
            # after remap, VstTestRunner id would not be in mapping; still strip if present
            for mid in mapping.values():
                pass
            rp = rp.replace(
                "m_Name: '[Building Block] PICO Video Seethrough XR Origin (XR Rig)'",
                "m_Name: XR Origin (VR)",
            )
        remapped_chunks.append(rp)

    xr_chunk = "".join(remapped_chunks)

    # New BridgeTest IDs for gesture objects
    gd_go = 830000001
    gd_tr = 830000002
    pinch = 830000003
    circle = 830000004
    swipe = 830000005
    snap = 830000006
    fist = 830000007
    gm = 830000008
    gate = 830000009
    boot = 830000010

    gesture_yaml = f"""--- !u!1 &{gd_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {gd_tr}}}
  - component: {{fileID: {pinch}}}
  - component: {{fileID: {circle}}}
  - component: {{fileID: {swipe}}}
  - component: {{fileID: {snap}}}
  - component: {{fileID: {fist}}}
  - component: {{fileID: {gm}}}
  - component: {{fileID: {gate}}}
  - component: {{fileID: {boot}}}
  m_Layer: 0
  m_Name: GestureDetectors
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{gd_tr}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gd_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &{pinch}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gd_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_PINCH}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Handedness: 2
  m_PinchDistanceThreshold: 0.055
  m_HoldPinchDistanceThreshold: 0.12
  m_HoldDurationSeconds: 1
  m_TapMaxSeconds: 0.5
  m_HoldDropoutGraceSeconds: 0.4
  m_StartupGuardSeconds: 2.5
  m_FollowGripDistance: 0.07
  m_PinchStarted:
    m_PersistentCalls:
      m_Calls: []
  m_PinchEnded:
    m_PersistentCalls:
      m_Calls: []
  m_PinchHeld:
    m_PersistentCalls:
      m_Calls: []
--- !u!114 &{circle}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gd_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_CIRCLE}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Handedness: 2
  m_WindowSeconds: 2.5
  m_MinPathLength: 0.09
  m_MaxAspectRatio: 2.6
  m_MinAxisSpan: 0.025
  m_CircleDetected:
    m_PersistentCalls:
      m_Calls: []
--- !u!114 &{swipe}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gd_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_SWIPE}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Handedness: 2
  m_SwipeSpeedThreshold: 0.85
  m_CooldownSeconds: 0.5
  m_MinHorizontalRatio: 0.65
  m_SwipeDetected:
    m_PersistentCalls:
      m_Calls: []
--- !u!114 &{snap}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gd_go}}}
  m_Enabled: 0
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_SNAP}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Handedness: 2
  m_CloseDistanceThreshold: 0.055
  m_OpenDistanceThreshold: 0.08
  m_MinCloseSpeed: 0.55
  m_SnapDetected:
    m_PersistentCalls:
      m_Calls: []
--- !u!114 &{fist}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gd_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_FIST}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Handedness: 2
  m_FistMaxTipDistance: 0.095
  m_OpenMinTipDistance: 0.10
  m_MinFistHoldSeconds: 0.04
  m_MaxBurstSeconds: 0.55
  m_FistBurstDetected:
    m_PersistentCalls:
      m_Calls: []
--- !u!114 &{gm}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gd_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_GESTURE_MGR}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_SubjectId: S01
  m_Condition: Bridge
  m_TrialId: 1
  m_AutoStartSession: 0
  m_EnabledDimensions: -1
  m_GlobalCooldownSeconds: 0.5
  m_TrackedHand: 2
  m_PinchDetector: {{fileID: {pinch}}}
  m_CircleDetector: {{fileID: {circle}}}
  m_SwipeDetector: {{fileID: {swipe}}}
  m_SnapDetector: {{fileID: {snap}}}
  m_FistBurstDetector: {{fileID: {fist}}}
  m_RealityEditor: {{fileID: 0}}
--- !u!114 &{gate}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gd_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_GATE}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Lighter: {{fileID: 710000006}}
  m_GestureTargetDistance: 0.15
  m_RejectWhenNoHandPosition: 1
--- !u!114 &{boot}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {gd_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GUID_BOOT}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_ConfigureVst: 1
  m_DisableOrphanCameras: 1
"""

    # Deactivate orphan desktop Main Camera
    bridge = bridge.replace(
        "--- !u!1 &1466788118\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n  - component: {fileID: 1466788122}\n  - component: {fileID: 1466788121}\n  - component: {fileID: 1466788120}\n  - component: {fileID: 1466788119}\n  m_Layer: 0\n  m_Name: Main Camera\n  m_TagString: MainCamera\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n",
        "--- !u!1 &1466788118\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n  - component: {fileID: 1466788122}\n  - component: {fileID: 1466788121}\n  - component: {fileID: 1466788120}\n  - component: {fileID: 1466788119}\n  m_Layer: 0\n  m_Name: Main Camera\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 0\n",
    )

    # Prefer VST-style skybox off for passthrough clarity
    bridge = bridge.replace(
        "  m_SkyboxMaterial: {fileID: 10304, guid: 0000000000000000f000000000000000, type: 0}",
        "  m_SkyboxMaterial: {fileID: 0}",
    )

    xr_origin_tr = mapping[44745100]
    xr_im_tr = mapping[189437449]

    # Insert before SceneRoots
    insert = xr_chunk + gesture_yaml
    marker = "--- !u!1660057539 &9223372036854775807\nSceneRoots:"
    if marker not in bridge:
        raise SystemExit("SceneRoots marker not found")

    bridge = bridge.replace(marker, insert + marker)

    # Update SceneRoots
    old_roots = """SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
  - {fileID: 1466788122}
  - {fileID: 42275452}
  - {fileID: 881570802}
  - {fileID: 710000006}
"""
    new_roots = f"""SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
  - {{fileID: 42275452}}
  - {{fileID: 881570802}}
  - {{fileID: 710000006}}
  - {{fileID: {xr_origin_tr}}}
  - {{fileID: {xr_im_tr}}}
  - {{fileID: {gd_tr}}}
  - {{fileID: 1466788122}}
"""
    if old_roots not in bridge:
        raise SystemExit("SceneRoots block mismatch; update script")
    bridge = bridge.replace(old_roots, new_roots)

    out_frag = ROOT / "Tools/bridge_gesture_fragment.txt"
    out_full = ROOT / "Tools/BridgeTest.merged.unity"
    out_frag.write_text(insert, encoding="utf-8")
    out_full.write_text(bridge, encoding="utf-8")
    print("Wrote fragment", out_frag)
    print("Wrote merged preview", out_full)
    print("XR Origin transform id", xr_origin_tr)
    print("GestureDetectors transform id", gd_tr)
    print("Mapped XR ids:", mapping)
    print("NOTE: BridgeTest.unity not overwritten; copy Tools/BridgeTest.merged.unity over it.")


if __name__ == "__main__":
    main()
