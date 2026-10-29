# Cursor warp test

A small RimWorld 1.7 mod to test the "glfwSetCursorPos moves the drawn cursor" change in RimDroid.
No Harmony needed, no PC needed.

## Use

1. Install the zip in a RimWorld 1.7 instance (Install mod / DLC (ZIP)), enable "Cursor warp test",
   restart the game.
2. Options -> Mod settings -> Cursor warp test.
3. Press a button with the stick (for example "Move the pointer 200 px to the right"), then let go of
   the stick. Two seconds later the mod moves the pointer.
4. Nudge the stick.
   - With the fix: the drawn cursor has jumped (200 px right, 200 px down, or to the centre) and the
     stick carries on from there.
   - Without it: the cursor is where you left it.

Use the stick, not a finger tap, before testing: by design a tap moves the game's pointer to the
finger but leaves the drawn cursor where it was, and the next stick move brings them together. A
move right after a tap therefore starts from the finger's spot.

Each move is also logged, so it can be checked from a PC:

    adb logcat | grep -E "CursorWarpTest|RimDroid/glfw"

The line reads like `[CursorWarpTest] Set CursorPos from 625,389 to 825,389 (game 1382x778)`, in game
pixels (y down). The window's `CursorPos` getter returns the last value from a cursor event, so it can
lag behind a tap (see above).

## How it works

`Source/CursorWarpTest.cs` (about 100 lines): reads `Waylith.EngineApplication.Instance.Window.CursorPos`
(a `Brutal.Numerics.double2`) by reflection, adds the offset and sets it again, which calls
`glfwSetCursorPos`. Plain .NET reflection, nothing patched.
