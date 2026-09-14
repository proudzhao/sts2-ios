using System;
using System.Collections;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Runs;

namespace STS2MobileIos.Patches;

// Dev console: a "Console" button in the in-game pause menu opens a modal overlay
// with two number inputs — gold and HP — applied to every player in the run.
//
// Values are written ONLY through the game's own property setters:
//   Player.Gold                  → fires GoldChanged (top bar refreshes immediately)
//   Player.Creature.CurrentHp    → fires CurrentHpChanged (HP bar refreshes)
// No raw field pokes, so the game's events fire and saves stay consistent.
//
// Hook: postfix on NPauseMenu._Ready, woven AFTER QuickRestartPatch's postfix
// (manifest order), so the Restart Room button already exists. The console
// button is moved to index 2 (Resume, Restart Room, Console, then the native
// buttons).
public static class DevConsolePatch
{
    private const string ButtonName = "DevConsole";
    private const string OverlayName = "DevConsoleOverlay";
    private const int GoldMax = 9999;

    // postfix on MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu.NPauseMenu._Ready
    public static void ReadyPostfix(object __instance)
    {
        try
        {
            var pauseMenu = (Node)__instance;
            if (pauseMenu.GetNodeOrNull(ButtonName) != null)
                return; // duplicate guard: _Ready fired twice

            var container = (Control)PatchHelper.Field(pauseMenu.GetType(), "_buttonContainer")?.GetValue(__instance);
            var saveBtn = (Node)PatchHelper.Field(pauseMenu.GetType(), "_saveAndQuitButton")?.GetValue(__instance);
            if (container == null || saveBtn == null)
            {
                PatchHelper.Log("[Console] _buttonContainer/_saveAndQuitButton not found");
                return;
            }

            // Duplicate the native "Save & Quit" button (flags 14: groups|scripts|
            // instancing, no signals) — the same pattern QuickRestartPatch uses.
            var dup = saveBtn.Duplicate(14);
            dup.Name = ButtonName;
            if (dup.GetNodeOrNull<MegaLabel>("Label") is { } label)
                label.SetTextAutoSize(L("控制台", "Console"));
            container.AddChild(dup);
            // Third position, directly under "Restart Room" (indices 0..1 are
            // Resume + Restart; the snapshot buttons added before us shift to 3..8).
            container.MoveChild(dup, 2);

            var overlay = BuildOverlay(saveBtn);
            pauseMenu.AddChild(overlay);
            ((NClickableControl)dup).Released += _ =>
            {
                if (overlay.Visible) Hide(overlay);
                else Show(overlay);
            };

            PatchHelper.Log("[Console] added console button + overlay to pause menu");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Console] ReadyPostfix failed: {ex}");
        }
    }

    // Modal overlay: full-screen dim behind a centered card. The dim catches taps
    // outside the inputs and releases focus so the iOS keyboard can be dismissed;
    // Apply/Close are duplicated native buttons, so they match the pause menu.
    private static Control BuildOverlay(Node buttonTemplate)
    {
        var overlay = new Control { Name = OverlayName, Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        // Tap on the dim (not on a child that handles input) → close the keyboard
        // without closing the panel, so the typed values are not lost.
        overlay.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true })
                ReleaseFocus(overlay);
        };

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = Control.MouseFilterEnum.Ignore };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(dim);

        var panel = new PanelContainer { Name = "DevConsolePanel", MouseFilter = Control.MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.09f, 0.09f, 0.12f, 0.98f),
            BorderColor = new Color(0.55f, 0.5f, 0.35f, 0.9f),
            BorderWidthTop = 3, BorderWidthBottom = 3, BorderWidthLeft = 3, BorderWidthRight = 3,
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
            ContentMarginTop = 34, ContentMarginBottom = 34, ContentMarginLeft = 40, ContentMarginRight = 40,
        });
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 22);
        panel.AddChild(vbox);

        var title = new Label { Text = L("控制台", "Console"), HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 44);
        vbox.AddChild(title);

        MakeRow(vbox, L("金币", "Gold"), "GoldEdit", "0 - 9999");
        MakeRow(vbox, L("生命", "HP"), "HpEdit", "1 - 最大生命");

        // Apply/Close: duplicated native buttons — same look/feel as the pause menu.
        var btns = new HBoxContainer();
        btns.AddThemeConstantOverride("separation", 18);
        btns.Alignment = BoxContainer.AlignmentMode.Center;
        var apply = MakeMenuButton(buttonTemplate, "ConsoleApply", L("应用", "Apply"), _ => Apply(overlay));
        var close = MakeMenuButton(buttonTemplate, "ConsoleClose", L("关闭", "Close"), _ => Hide(overlay));
        btns.AddChild(apply);
        btns.AddChild(close);
        vbox.AddChild(btns);

        // CJK font for zh locale — bare controls use the default theme font, which
        // has no CJK glyphs (same fix the advisor used).
        var font = GetCjkFont();
        if (font != null)
            foreach (var c in AllControls(panel))
                c.AddThemeFontOverride("font", font);

        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        overlay.AddChild(panel);
        return overlay;
    }

    // One labelled number-input row.
    private static void MakeRow(VBoxContainer parent, string labelText, string editName, string placeholder)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        var label = new Label
        {
            Text = labelText,
            CustomMinimumSize = new Vector2(200, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 34);
        label.AddThemeColorOverride("font_color", new Color(0.9f, 0.88f, 0.84f));

        var edit = new LineEdit
        {
            Name = editName,
            CustomMinimumSize = new Vector2(460, 66),
            PlaceholderText = placeholder,
            VirtualKeyboardType = LineEdit.VirtualKeyboardTypeEnum.Number,
        };
        StyleLineEdit(edit, focused: false);
        edit.FocusEntered += () => StyleLineEdit(edit, focused: true);
        edit.FocusExited += () => StyleLineEdit(edit, focused: false);

        row.AddChild(label);
        row.AddChild(edit);
        parent.AddChild(row);
    }

    // Dark rounded input field with a gold border when focused.
    private static void StyleLineEdit(LineEdit edit, bool focused)
    {
        edit.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.04f, 0.06f, 1f),
            BorderColor = focused ? new Color(1f, 0.84f, 0.3f) : new Color(0.35f, 0.34f, 0.3f),
            BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 14, ContentMarginRight = 14,
        });
        edit.AddThemeStyleboxOverride("focus", new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.04f, 0.06f, 1f),
            BorderColor = new Color(1f, 0.84f, 0.3f),
            BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 14, ContentMarginRight = 14,
        });
        edit.AddThemeColorOverride("font_color", new Color(1, 1, 1));
        edit.AddThemeColorOverride("caret_color", new Color(1, 1, 1));
        edit.AddThemeFontSizeOverride("font_size", 36);
    }

    // Duplicate the native menu button once more for use inside the overlay.
    private static Node MakeMenuButton(Node template, string name, string text,
        NClickableControl.ReleasedEventHandler handler)
    {
        var dup = template.Duplicate(14);
        dup.Name = name;
        if (dup.GetNodeOrNull<MegaLabel>("Label") is { } label)
            label.SetTextAutoSize(text);
        ((NClickableControl)dup).Released += handler;
        return dup;
    }

    private static void Show(Control overlay)
    {
        RefreshPlaceholders(overlay);
        overlay.Visible = true;
    }

    private static void Hide(Control overlay)
    {
        overlay.Visible = false;
        ReleaseFocus(overlay); // dismiss the iOS keyboard with the panel
    }

    // Release focus from every LineEdit in the overlay so the virtual keyboard
    // closes (LineEdit keeps the keyboard open while focused), plus an explicit
    // VirtualKeyboardHide as a belt-and-braces path for iOS quirks.
    private static void ReleaseFocus(Control overlay)
    {
        foreach (var n in overlay.FindChildren("*", "LineEdit", true, false))
            if (n is LineEdit edit)
                edit.ReleaseFocus();
        try { DisplayServer.VirtualKeyboardHide(); } catch { /* keyboard calls must never hurt the game */ }
    }

    // Apply both inputs to every player in the run. Empty/invalid inputs are skipped
    // (both empty = nothing to do). Gold clamps to [0, 9999]; HP clamps to
    // [1, that player's MaxHp] — never below 1, never above their own maximum.
    private static void Apply(Control overlay)
    {
        try
        {
            var goldEdit = overlay.FindChild("GoldEdit", true, false) as LineEdit;
            var hpEdit = overlay.FindChild("HpEdit", true, false) as LineEdit;
            if (goldEdit == null || hpEdit == null)
                return;

            int gold = ParseOrMinus(goldEdit.Text);
            int hp = ParseOrMinus(hpEdit.Text);
            if (gold < 0 && hp < 0)
            {
                Hide(overlay); // both empty/invalid — just close
                return;
            }

            var state = GetRunState();
            if (state == null)
            {
                PatchHelper.Log("[Console] no run state — not in a run?");
                Hide(overlay);
                return;
            }
            if (PatchHelper.Field(state.GetType(), "_players")?.GetValue(state) is not IEnumerable players)
                return;

            int touched = 0;
            foreach (var p in players)
            {
                if (p is not Player player)
                    continue;
                if (gold >= 0)
                    player.Gold = Math.Clamp(gold, 0, GoldMax); // fires GoldChanged
                if (hp >= 0 && player.Creature != null)
                    SetHp(player.Creature, hp);
                touched++;
            }
            PatchHelper.Log($"[Console] applied gold={gold} hp={hp} to {touched} player(s)");

            Hide(overlay);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Console] apply failed: {ex}");
        }
    }

    // Set arbitrary HP through the game's own mutation paths (verified against the
    // game assembly IL):
    //   heal direction → HealInternal (public): clamps to MaxHp and — unlike the
    //     raw setter — REVIVES a dead creature (fires Revived + ActivateHooks)
    //   damage direction → private set_CurrentHp via reflection: fires
    //     CurrentHpChanged so the HP bar refreshes
    // Target is clamped to [1, MaxHp], so the setter's "must be positive" guard
    // (throws ArgumentException below 0) can never fire.
    private static void SetHp(Creature creature, int requested)
    {
        int max = Math.Max(1, creature.MaxHp);
        int target = Math.Clamp(requested, 1, max);
        int cur = creature.CurrentHp;
        if (target >= cur)
            creature.HealInternal(target - cur);
        else
            PatchHelper.Method(creature.GetType(), "set_CurrentHp")
                ?.Invoke(creature, new object[] { target });
    }

    // Placeholders show the LIVE values ("当前 123", "当前 40/80") so the player
    // knows what they're editing from. Refreshed on every open and after Apply.
    private static void RefreshPlaceholders(Control overlay)
    {
        try
        {
            var goldEdit = overlay.FindChild("GoldEdit", true, false) as LineEdit;
            var hpEdit = overlay.FindChild("HpEdit", true, false) as LineEdit;
            var state = GetRunState();
            var players = state == null
                ? null
                : PatchHelper.Field(state.GetType(), "_players")?.GetValue(state) as IEnumerable;
            Player first = null;
            if (players != null)
                foreach (var p in players)
                    if (p is Player pl) { first = pl; break; }

            if (goldEdit != null)
                goldEdit.PlaceholderText = first == null ? "—" : string.Format(L("当前 {0}", "Now {0}"), first.Gold);
            if (hpEdit != null)
                hpEdit.PlaceholderText = first?.Creature == null
                    ? "—"
                    : string.Format(L("当前 {0}/{1}", "Now {0}/{1}"), first.Creature.CurrentHp, first.Creature.MaxHp);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Console] placeholder refresh failed: {ex.Message}");
        }
    }

    // RunManager.Instance is public, but State is not — getter first, backing
    // field fallback.
    private static RunState GetRunState()
    {
        var rm = RunManager.Instance;
        if (rm == null)
            return null;
        var st = PatchHelper.Method(rm.GetType(), "get_State")?.Invoke(rm, null)
                 ?? PatchHelper.Field(rm.GetType(), "<State>k__BackingField")?.GetValue(rm);
        return st as RunState;
    }

    private static int ParseOrMinus(string text) =>
        int.TryParse(text?.Trim(), out var v) ? v : -1;

    private static string L(string zh, string en)
    {
        try
        {
            var locale = TranslationServer.GetLocale();
            if (!string.IsNullOrEmpty(locale) && locale.StartsWith("zh"))
                return zh;
        }
        catch
        {
            // fall through to English
        }
        return en;
    }

    private static IEnumerable<Control> AllControls(Node node)
    {
        if (node is Control c)
            yield return c;
        foreach (var ch in node.GetChildren())
            foreach (var x in AllControls(ch))
                yield return x;
    }

    // The game's Simplified-Chinese UI font (loaded once, cached) — same asset the
    // game itself uses, keeps the panel's CJK text rendering instead of tofu boxes.
    private static Font _cjkFont;
    private static bool _fontTried;
    private static Font GetCjkFont()
    {
        if (_fontTried)
            return _cjkFont;
        _fontTried = true;
        try
        {
            _cjkFont = ResourceLoader.Load("res://fonts/zhs/SourceHanSerifSC-Bold.otf") as Font;
        }
        catch (Exception e)
        {
            PatchHelper.Log($"[Console] font load failed: {e.Message}");
        }
        if (_cjkFont == null)
            PatchHelper.Log("[Console] CJK font missing, panel text may be tofu");
        return _cjkFont;
    }
}
