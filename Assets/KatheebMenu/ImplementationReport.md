## VR pointer and selection layout correction

VR selection buttons move to (980, -100) canvas units, leaving clearance beyond Saqeera; desktop uses (620, -180). The VR root canvas is widened for that right-hand column. Character UI targets now lie immediately in front of each actual model, sized from visible skinned-renderer bounds and refreshed during selection animations. This replaces fixed-size targets projected from the headset onto a shared plane, which produced controller-origin parallax errors and missed head/feet regions.

VR character selection uses the same UI press path as menu buttons. Preview-only XRSimpleInteractable components and colliders are disabled in VR so physics selection cannot compete with UI triggering. Menu graphic raycasting ignores 2D/3D scene occlusion; actor and button UI targets remain independently raycastable. Existing UI ray/near-far interactors temporarily allow UI while another interactable is selected; previous blocking settings are restored on countdown and teardown. Existing trigger action bindings and hardware scripts are unchanged.

Validation: C# compilation passed. VRPointerRegression.txt reports PASS for 12 rays from left/right controller positions at three body heights per character, using XRI's actual rectangle-ray intersection; each hit the intended model target and missed the other. Physical Quest controller verification is still required after rebuilding/installing the app.

## Quest doorway and distant follow-camera correction

The menu now consumes Assets/Resources/KatheebUI/MenuTentLayout.json, baked in the Editor from the verified tent entrance. Android no longer performs runtime MeshCollider cooking to choose a doorway; both platforms use identical local anchor and normal data. The menu was visually checked in Play mode using the baked layout.

DesertSelectedCameraClip now uses the close follow frame in VR regardless of the desktop usePlacedAngle flag. The previous authored rig offset was approximately (-24.39, 8.10, -22.41) m and the serialized camera-local offset approximately (-25.86, .8, 20.15) m. After the tracked pose updates, a before-render callback recalculates rig compensation from the current camera-local position so the transition to real HMD tracking cannot move the view far from the selected character. The rig frame stays upright and headset local rotation/position are preserved. Desktop placed camera angles, environment transforms and hardware scripts are unchanged.

Validation: compilation passed; VRPoseRegression.txt reports PASS for the authored large camera-local offset and two simulated tracked poses. Original hardware audit passed (8/8 source/meta, 3/3 serialized components, 115/115 transform records unchanged). Physical Quest validation requires a new Android build/install and is not claimed.

## VR tracking initialization correction

The Quest captures showed the menu staged against an early camera pose: the viewpoint later looked over the roof, with selection actors beneath it. Startup previously waited only two ordinary frames. The seat controller asynchronously recenters in LateUpdate/before-render once tracked input arrives.

The menu now waits for a valid tracked HMD (and presence where supported), plus 0.75 seconds of rendered calibration time before staging. VR placement uses horizontal yaw with a floor 1.35 m below the current eye position, rather than projecting the floor along a pitched head direction. A new menu-only presentation anchor is parented under the existing XR Origin with world placement preserved; tent, preview actors and UI follow origin recentering together, without following individual head rotations or modifying the camera or seat-controller code. Desktop framing is retained.

C# compilation passed. Protected hardware/source and original scene-transform regression audit passed. Physical Quest verification is still required after rebuilding/installing the Android app; the installed APK cannot pick up source changes automatically.

## Doorway alignment follow-up

The menu tent now locates its clear entrance from the visible LOD0 architecture, excluding inactive LOD meshes. Short support-pole occlusions are joined when identifying the full doorway. The opening normal squares the facade toward the player, with an authored porch-center offset for this two-bay tent. Both character previews share the doorway floor and stand immediately in front of it. The menu-only tent height is 3.6 m. Hover tint preserves dark button backgrounds so Arabic labels remain legible.

Verified in Unity Play mode: the full open facade is centered; character selection preview was checked on the entrance carpet. Compilation passed. Protected source/meta hashes 8/8, hardware serialized blocks 3/3, original transform records 115/115 unchanged. No physical headset test.

## October 16 tent-menu correction

The supplied photograph is no longer rendered as the menu. The menu uses the existing 3D tent prefab, with the open front centered on the viewer. Menu-only staging leaves original gameplay object transforms intact. Two selection actors stand symmetrically 2.5 m ahead, with their floor based on the view rather than uneven terrain. Tent cloth submesh bounds exclude long guy ropes when placing the entrance. Arabic buttons and character names use high-resolution transparent text textures, independent of photographs. Menu actors and tent use LOD0, with warm presentation lighting. Car, sled and towing rope renderers are hidden during the menu, including meshes created after Awake, then restored under the start fade.

Validation: C# compilation passed. In-editor visual checks confirmed readable Arabic buttons/names and hidden ride geometry; these checks caught the closed tent face and prompted the final orientation and cloth-bound fixes. Final refinements require a fresh Play run; active user input prevented completing that last UI check. No physical headset verification was performed.

# KATHEEB interactive menu implementation

Implemented in `Assets/Scenes/Demo.unity`. The installer assigns the existing camera, placed man/woman riders, movement drivers, selection prefabs, and original tent prefab. The previous Arabic test menu remains in the project, disabled in Demo.

## Functionality

- World-space VR Canvas with native Unity UI Buttons, tracked-device graphic raycasting, and the existing EventSystem/XRUIInputModule. Desktop-only Editor overlay supports mouse testing.
- Existing supplied KATHEEB logo retained; beige/bronze button artwork uses separate cropped regions of the supplied October 16 reference, with independent button hit targets. Labels: `ابدأ التجربة`, `اختر الشخصيات`.
- Hover scale approximately 1.06, warm tint, and subtle press feedback. Input ownership handles multiple pointers; transitions reject duplicate actions.
- State flow: MainMenu → CharacterSelection → Transitioning → MainMenu → Transitioning → Countdown → Playing.
- Choose Characters shifts/scales the main buttons to the right, keeps the logo visible, and shows independently selectable 3D Mufleh and Saqeera. Supplied faces, clothes, mesh proportions, and materials retained.
- Idle animations loop. Mufleh uses the supplied Salute, Saqeera uses Greeting. Hover triggers a one-shot reaction; confirmation plays it before closing selection and recentering the buttons. Root motion is off.
- Existing `Desert.SelectedCharacter` storage remains compatible with the experience and DesertOverview selection.
- Start without a selection displays `اختر شخصيتك أولاً` and opens selection. No default character is silently selected.
- After selection: comfortable UI fade, selected existing rider enabled, `استعد للانطلاق`, animated 3 → 2 → 1, and exactly one gameplay start.
- Existing XR Origin stays attached to the sled. No tracked-camera transforms, tracking flags, locomotion actions, packages, or OpenXR settings are changed by the new menu.
- Menu-only scenery reuses `Assets/Tent.prefab` and hides before countdown. Its colliders are disabled; supplementary local lights have no shadows and are limited to three. Existing car/sled/tent scene transforms remain unchanged.

## New runtime scripts

- `Assets/KatheebMenu/KatheebMenuManager.cs`: layout, transitions, selection storage, fade/countdown and safe handoff.
- `Assets/KatheebMenu/KatheebVRButton.cs`: pointer ownership, hover and press feedback.
- `Assets/KatheebMenu/KatheebCharacterInteractable.cs`: character XR/UI hover, reaction animations, confirmation.
- `Assets/KatheebMenu/KatheebGameplayStartAdapter.cs`: holds only CarPathFollower and SledFollowCar until countdown ends.

## Editor scripts and scene changes

- `Assets/Editor/KatheebMenuInstaller.cs`: bounded, versioned setup/import utility. Menu: **Tools → Desert VR → Install KATHEEB Interactive Menu**.
- `Assets/Editor/KatheebMenuSmokeTest.cs`: optional Editor flow check. Open only Demo, stop Play, then **Tools → Desert VR → Test KATHEEB Menu Flow**. It enters Play, checks the no-selection gate, 3D characters, UI event dispatch, selection, countdown and handoff, then returns to Edit. Results go to `EditorSmokeTest.txt`.
- Added scene root **KATHEEB Interactive Menu** with manager and adapter, all required references assigned.
- Reused the existing EventSystem; enabled XR UI interaction on existing ray/near-far components without replacing their action references.
- Disabled the legacy ArabicVRSimulationMenu component. Its Awake now exits when disabled; its legacy pause list no longer names HardwareOutput or SledMotionController.
- UI hierarchy and the menu-only model/scenery stage are generated on Play, rather than saved as duplicate XR rigs or gameplay actors.

## Supplied assets

- `Animations/MuflehIdle.fbx` ← Idle mufleh.fbx
- `Animations/SaqeeraIdle.fbx` ← Idle saqeera.fbx
- `Animations/MuflehSalute.fbx` ← Salute Mufleh.fbx
- `Animations/SaqeeraGreeting.fbx` ← Greeting Saqeera.fbx
- `Controllers/MuflehMenu.controller`, `Controllers/SaqeeraMenu.controller`: Idle and React states.
- `Prefabs/MuflehMenu.prefab`, `Prefabs/SaqeeraMenu.prefab`: humanoid source models, original Clothing materials, Animator, collider, XRSimpleInteractable and hover indicator.
- `Assets/Resources/KatheebUI/October16/`: supplied menu reference plus correctly shaped Arabic name/hint textures. Existing logo/countdown artwork is reused.

## Validation and limits

- New C# code compiled using the installed Unity assemblies.
- Unity installer validated humanoid animation import, valid character avatars, material references, required scene assignments, and absence of duplicate EventSystems before saving Demo.
- See `HardwareRegression.txt`: protected source/meta hashes unchanged, original hardware component serialized records unchanged, original scene Transform records unchanged, and original character FBX files match the supplied model folders byte for byte.
- `git diff -- Assets/HardwareOutput.cs Assets/WebsocketSender.cs Assets/SledMotionController.cs` is empty.
- No changes to endpoint, payload, roll/bump math, hardware execution order, networking lifecycle, XR setup, route points, or existing motion calculations. Hardware components are never suspended by the new menu.
- No remaining required Inspector assignments after the installer succeeds.
- Physical Quest 3 controller interaction, headset comfort/performance and the ESP32 connection have **not** been tested here. The Editor smoke test is available; do not count it as passed unless its result file explicitly reports PASS.
- Existing project deprecation warnings are separate from this feature. See Unity Console for live runtime errors.

## Quick test

1. Open Demo and press Play. Use mouse buttons, or **C** to open selection, **1** for Mufleh, **2** for Saqeera, **Enter** to request Start.
2. First request Start without choosing: selection must open and the car must remain still.
3. Hover/select each character. Verify idle → greeting/salute, correct Arabic name and gold feedback, then buttons return to the center.
4. Start: verify fade and 3, 2, 1; car begins only afterwards, selected rider is correct, and the original end transition still works.
5. On Quest, test **both** controller rays/triggers, comfortable UI/model positioning and frame rate.
6. With ESP32 connected, verify the existing endpoint connects once as before, telemetry continues during menus/countdown, and roll/bump/impact/speed/direction behave unchanged during the ride.

## Backup

Pre-change scene/menu and protected hashes are saved outside the project at:
`C:/Users/ramaa/Documents/Codex/2026-10-13/i/work/menu-backup-20261016/`.


Desktop/VR parity follow-up (October 16): both cinematic branches now store the actual desktop lens offset, independent of tracked local camera pose. Menu VR floor height aligned to desktop reference. Android render scale restored from .8 to 1, 4x MSAA, lighting settings aligned with desktop URP. DesktopVRParityRegression.txt passed in Unity; synthetic tracking test only. Physical headset framing and performance require a rebuilt APK.

Hardware protocol change explicitly requested by user: HardwareOutput and WebsocketSender intentionally updated to normalized roll (-10..10) and bump magnitude (0..10), sent as numeric CSV only. Earlier hardware hash audit predates this authorized change; no longer expect those two source hashes to match. SledMotionController, networking endpoint/rate and scene transforms preserved.
