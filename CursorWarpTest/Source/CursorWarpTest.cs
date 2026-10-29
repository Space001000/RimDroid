using System;
using System.Reflection;
using UnityEngine;
using Verse;

namespace CursorWarpTest
{
    /// <summary>
    /// Test for RimDroid: moves the pointer the way a mod can on desktop RimWorld 1.7
    /// (Waylith.EngineApplication.Instance.Window.CursorPos, which calls glfwSetCursorPos) and logs it.
    /// Options -> Mod settings -> "Cursor warp test": each button moves the pointer 2 seconds after the
    /// press, so you can let go of the stick and watch. If the drawn cursor jumps and the stick carries on
    /// from the new spot, RimDroid honours glfwSetCursorPos. Plain reflection, no Harmony.
    /// </summary>
    public class CursorWarpTestMod : Mod
    {
        const int DelayMs = 2000;
        static int dueAt;          // Environment.TickCount when the pending move runs (0: none)
        static double dueDx, dueDy;
        static bool dueCentre;
        static string last = "No move yet.";

        public CursorWarpTestMod(ModContentPack content) : base(content)
        {
            Log.Message("[CursorWarpTest] loaded");
        }

        public override string SettingsCategory() => "Cursor warp test";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // The settings window draws every frame while it's open: run a due move here.
            if (dueAt != 0 && Environment.TickCount - dueAt >= 0)
            {
                dueAt = 0;
                last = Move(dueDx, dueDy, dueCentre);
                Log.Message("[CursorWarpTest] " + last);
            }

            var l = new Listing_Standard();
            l.Begin(inRect);
            l.Label("Each button moves the pointer 2 seconds after you press it. Let go of the stick, wait, "
                + "then nudge the stick: with the fix the cursor jumps and the stick carries on from the new spot; "
                + "without it the cursor stays where it was.");
            l.Gap();
            if (l.ButtonText("Move the pointer 200 px to the right")) Schedule(200, 0, false);
            if (l.ButtonText("Move the pointer 200 px down")) Schedule(0, 200, false);
            if (l.ButtonText("Move the pointer to the centre of the screen")) Schedule(0, 0, true);
            l.Gap();
            l.Label(dueAt != 0 ? "Moving in a moment..." : last);
            l.End();
        }

        static void Schedule(double dx, double dy, bool centre)
        {
            dueDx = dx; dueDy = dy; dueCentre = centre;
            dueAt = Environment.TickCount + DelayMs;
        }

        /// <summary>Sets Window.CursorPos (a Brutal.Numerics.double2, game pixels, y down) and reports.</summary>
        static string Move(double dx, double dy, bool centre)
        {
            try
            {
                const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                var app = Type.GetType("Waylith.EngineApplication, Waylith");
                var inst = app?.GetProperty("Instance", all)?.GetValue(null);
                var win = inst?.GetType().GetProperty("Window", all)?.GetValue(inst);
                var prop = win?.GetType().GetProperty("CursorPos", all);
                if (prop == null) return "Not found: Waylith.EngineApplication.Instance.Window.CursorPos (not RimWorld 1.7?)";

                object pos = prop.GetValue(win);
                var fx = pos.GetType().GetField("X", all);
                var fy = pos.GetType().GetField("Y", all);
                double x = Convert.ToDouble(fx.GetValue(pos)), y = Convert.ToDouble(fy.GetValue(pos));
                double nx = centre ? Screen.width / 2.0 : x + dx;
                double ny = centre ? Screen.height / 2.0 : y + dy;
                fx.SetValue(pos, Convert.ChangeType(nx, fx.FieldType));
                fy.SetValue(pos, Convert.ChangeType(ny, fy.FieldType));
                prop.SetValue(win, pos);
                return string.Format("Set CursorPos from {0:0},{1:0} to {2:0},{3:0} (game {4}x{5})", x, y, nx, ny, Screen.width, Screen.height);
            }
            catch (Exception e)
            {
                return "Failed: " + e.GetType().Name + ": " + (e.InnerException ?? e).Message;
            }
        }
    }
}
