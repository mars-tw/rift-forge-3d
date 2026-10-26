# Touch movement input foundation

`TouchPlayerInputSource` implements the existing `IPlayerInputSource` contract. It holds movement intent only. `PlayerController` continues to calculate ground-plane movement and call `CharacterController.Move`; neither the input source nor the uGUI adapter changes a Transform or physics state.

## Integration

1. Use a Canvas with a GraphicRaycaster and an EventSystem with the installed InputSystemUIInputModule. Set `pointerBehavior` to `UIPointerBehavior.AllPointersAsIs`. Bind the UI point/click actions as PassThrough to `<Touchscreen>/touch*/position` and `<Touchscreen>/touch*/press` so each finger retains its own pointer events. Keep the normal mouse bindings for desktop interaction.
2. Put `TouchMoveArea` on a dedicated RectTransform with a raycastable UI Graphic. Keep action buttons outside this movement area's raycast region and avoid a full-screen transparent Graphic intercepting them. Decorative children should have `raycastTarget = false`.
3. Assign the existing player controller in the adapter Inspector, or call `BindTo(playerController)` during composition. The five existing runtime foundation components are unchanged. No scene, prefab, visual treatment, action buttons, or gameplay genre is included in this input-only change.
4. Choose the travel radius in RectTransform local units. A press establishes a floating origin; dragging one radius reaches full input. The default radial dead zone is 0.15. Movement inside that radius is zero, and the remaining range is remapped continuously to full scale.

The first left-button pointer pressing the movement area owns it until release or cancellation. Other pointer down, drag and up events cannot replace its origin or movement. A second finger remains available to independent uGUI buttons. The adapter intentionally does not cancel on pointer exit, so a captured finger may drag beyond the area and release outside it.

The Input System UI module dispatches pointer-up for click release, including a touch whose press was cancelled. Both pointer-up and end-drag clear the owning pointer. A custom event bridge can call `Source.CancelPointer(pointerId)` or `Source.ResetTouch()` explicitly. A discarded pointer cannot resume movement through a late drag; it must press again.

When no pointer is captured, the existing InputSystemPlayerInputSource supplies keyboard/gamepad input. A captured pointer at rest suppresses this fallback. Focus loss, application pause and component disable clear the captured state and return zero for all movement while unavailable. Resuming restores fallback but never restores a previous touch. If a separate game pause disables only the EventSystem, its composition code must also suspend the input source or disable this adapter.

## Verification status

EditMode tests cover analog preservation, radial dead-zone boundaries and continuity, diagonal/full-travel normalization, invalid input, pointer ownership/cancellation, fallback priority, independent focus/pause/enable state, local-coordinate UI drag conversion, other-finger interference, release, mouse-button filtering and lifecycle callback wiring. The Unity 6000.5.4f1 EditMode suite passed 81/81; the PlayMode suite, including touch lifecycle, passed 4/4 after the pause-screen repair. Results are in the local ignored `Logs/rift-editmode-green-20261013.xml` and `Logs/rift-playmode-after-pause-contrast.xml`. Real phone/tablet multi-touch, safe-area scaling, OS interruptions and native player wiring still need device checks.

API checks used the existing read-only PackageCache source for Input System 1.19.0 and uGUI 2.5.0, including EventInterfaces, PointerEventData, ExtendedPointerEventData and InputSystemUIInputModule. Both runtime and EditMode asmdefs now explicitly reference the package's `UnityEngine.UI` assembly.
