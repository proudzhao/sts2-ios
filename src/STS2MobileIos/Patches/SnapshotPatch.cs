using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Runs;

namespace STS2MobileIos.Patches;

// Save-state snapshots / time-travel rollback for the mobile port ("free SL"):
// adds six buttons to the in-game pause menu, wired in from QuickRestartPatch.ReadyPostfix
// (one _Ready hook adds Restart Room + these six). Three fixed save slots, each with an
// independent Save and Load button:
//   存档位1/2/3 (Save Slot 1/2/3) and 读档位1/2/3 (Load Slot 1/2/3).
//
// Design — purely file-level, does NOT touch the game's save format or any game value.
// Each slot is a folder user://snapshots/slot_<N>/ holding a full recursive copy of the
// game's on-disk save tree (user://default, which holds every profile, the active run,
// progression and run history).
//   Save Slot N: copy user://default into slot_<N>/ (temp dir + swap, overwriting any
//                previous contents of that slot). Other slots are untouched.
//   Load Slot N: copy slot_<N>/ back over user://default (staged copy + crash-safe
//                swap: the old tree moves aside first, so a kill mid-swap never leaves
//                the live save missing — the next entry self-heals), then reload through
//                QuickRestartPatch.ReloadCurrentRunAsync (the game's own
//                quit-to-menu -> Continue path, already proven on device).
//
// Because slots are fixed and independent, you can Save Slot 1, play on, Save Slot 2,
// then Load Slot 1 to jump back exactly to slot 1's point — the multi-slot selection the
// previous incremental "save many / load latest only" version could not do.
//
// Because we snapshot what is on disk, a slot captures the game's most recent autosave
// point (the game autosaves on room entry etc.) — the same granularity the "Restart Room"
// button relies on.
public static class SnapshotPatch
{
    private const int SlotCount = 3;
    private const string SaveRootFolder = "default";     // game save tree under user://
    private const string SnapshotsFolder = "snapshots";  // our slots live under user://
    private const string SlotPrefix = "slot_";           // fixed dirs slot_1 / slot_2 / slot_3
    // 串行化 Save/Load 的暂存+交换:若 TaskHelper 把 Load 派发到线程池,两个操作会互踩
    // 共享的 .writing/.restoring/.old 路径,锁住整段磁盘序列即可
    private static readonly object SwapLock = new();

    // Called from QuickRestartPatch.ReadyPostfix. `template` is the native
    // "Save and Quit" button, duplicated so layout/style/script match automatically.
    public static void AddButtons(Control container, Node template)
    {
        try
        {
            if (container == null || template == null)
            {
                PatchHelper.Log("[Snapshot] container/template button not available");
                return;
            }

            // Guard against a second _Ready adding duplicates.
            if (container.GetNodeOrNull(SaveButtonName(1)) != null)
                return;

            // "Restart Room" was inserted at index 1; place our six right below it.
            // Grouped: Save 1/2/3 first, then Load 1/2/3 (indices 2..7).
            int index = 2;
            for (int n = 1; n <= SlotCount; n++)
            {
                int slot = n; // capture per-iteration to avoid the shared-closure bug
                bool occ = SlotOccupied(slot);
                int? floor = GetSlotFloor(slot);
                AddButton(container, template, SaveButtonName(slot), SaveLabelText(slot, occ, floor),
                    index++, b => OnSaveSlot(slot, b));
            }
            for (int n = 1; n <= SlotCount; n++)
            {
                int slot = n;
                bool occ = SlotOccupied(slot);
                int? floor = GetSlotFloor(slot);
                AddButton(container, template, LoadButtonName(slot), LoadLabelText(slot, occ, floor),
                    index++, b => OnLoadSlot(slot, b));
            }

            PatchHelper.Log("[Snapshot] Added 3-slot Save/Load buttons to pause menu");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Snapshot] AddButtons failed: {ex}");
        }
    }

    private static Node AddButton(Control container, Node template, string name, string text,
        int index, NClickableControl.ReleasedEventHandler handler)
    {
        // Duplicate flags 14 (groups|scripts|instancing, no signals) — same as
        // QuickRestartPatch, so the template's own Released connection is not copied.
        var dup = template.Duplicate(14);
        dup.Name = name;

        var label = dup.GetNodeOrNull<MegaLabel>("Label");
        if (label != null)
            label.SetTextAutoSize(text);

        container.AddChild(dup);
        container.MoveChild(dup, index);

        ((NClickableControl)dup).Released += handler;
        return dup;
    }

    // ---- Save ------------------------------------------------------------------

    private static void OnSaveSlot(int slot, NClickableControl button)
    {
        try
        {
            lock (SwapLock)
            {
                var source = SaveRootPath();
                // 上次读档交换若被系统杀,活存档可能停在"旧树已挪走"的中间态——先修复再读
                HealSwapState(source, source + ".restoring");
                if (!Directory.Exists(source))
                {
                    PatchHelper.Log($"[Snapshot] save tree not found at {source}");
                    return;
                }

                Directory.CreateDirectory(SnapshotsRootPath());
                var dest = SlotPath(slot);

                // Staged copy + crash-safe swap, so a failure mid-copy never leaves a
                // half-written slot that Load could pick up, and a kill mid-swap never
                // loses the slot (old tree moves aside first; next entry self-heals).
                // Only this slot's folder is replaced; the other two slots are untouched.
                var tmp = dest + ".writing";
                HealSwapState(dest, tmp);
                StageCopy(source, tmp);
                SwapInPlace(tmp, dest);
            }

            // Record the run's current floor at save time so the button can show it
            // (e.g. "存档位1 [第12层]"). Kept in a sibling meta file — never inside the
            // slot tree — so Load does not copy it back into the live save.
            SetSlotFloor(slot, GetCurrentFloor());

            PatchHelper.Log($"[Snapshot] saved slot {slot} -> {dest}");

            // Reflect the new occupancy on this slot's Save + Load buttons.
            RefreshLabels((Node)button);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Snapshot] OnSaveSlot({slot}) failed: {ex}");
        }
    }

    // ---- Load ------------------------------------------------------------------

    private static void OnLoadSlot(int slot, NClickableControl _)
    {
        TaskHelper.RunSafely(LoadSlotAsync(slot));
    }

    private static async Task LoadSlotAsync(int slot)
    {
        try
        {
            var src = SlotPath(slot);
            lock (SwapLock)
            {
                HealSwapState(src, src + ".writing");   // 上次存档交换被杀的残留:槽位先自愈
                if (!Directory.Exists(src) || !Directory.EnumerateFileSystemEntries(src).Any())
                {
                    // Empty slot: do nothing (the button label already shows [空]/[Empty]).
                    PatchHelper.Log($"[Snapshot] slot {slot} is empty, nothing to load");
                    return;
                }

                var target = SaveRootPath();
                HealSwapState(target, target + ".restoring");   // 活存档先自愈再动手

                // Rebuild the live save tree from the slot via a staged copy + crash-safe
                // swap: the old tree moves aside first, so a kill mid-swap never leaves the
                // live save missing — next entry heals from the leftovers.
                var tmp = target + ".restoring";
                StageCopy(src, tmp);
                SwapInPlace(tmp, target);
            }

            PatchHelper.Log($"[Snapshot] restored save tree from slot {slot}, reloading run");

            // Reload the run through the game's own, on-device-proven path.
            await QuickRestartPatch.ReloadCurrentRunAsync();
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Snapshot] LoadSlotAsync({slot}) failed: {ex}");
        }
    }

    // ---- New run: clear all slots ---------------------------------------------

    // postfix on MegaCrit.Sts2.Core.Runs.RunManager.SetUpNewSingleplayer (single overload,
    // so the weaver resolves it unambiguously). Fires only when a brand-new singleplayer
    // run starts — continuing a saved run goes through SetUpSavedSingleplayer, which we do
    // NOT hook, so loading a save never wipes the slots. Deletes the three slot folders and
    // their floor meta files so the pause-menu buttons read [空] again for the new run.
    // (If the pause menu is already open its labels won't update until reopened — acceptable;
    // ReadyPostfix re-labels from disk every time the menu is opened.)
    public static void OnNewRun()
    {
        try
        {
            for (int n = 1; n <= SlotCount; n++)
            {
                var dir = SlotPath(n);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);

                foreach (var stale in new[] { dir + ".writing", dir + ".old" })
                    if (Directory.Exists(stale)) Directory.Delete(stale, true);
                var ready = dir + ".writing.ready";
                if (File.Exists(ready)) File.Delete(ready);
                if (Directory.Exists(SnapshotsRootPath()))
                    foreach (var arch in Directory.GetDirectories(SnapshotsRootPath(), Path.GetFileName(dir) + ".old-*"))
                        Directory.Delete(arch, true);

                var meta = SlotMetaPath(n);
                if (File.Exists(meta)) File.Delete(meta);
            }
            PatchHelper.Log("[Snapshot] new run started, cleared all 3 slots");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Snapshot] OnNewRun clear failed: {ex}");
        }
    }

    // ---- Slot state ------------------------------------------------------------

    private static bool SlotOccupied(int slot)
    {
        try
        {
            var path = SlotPath(slot);
            return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
        }
        catch { return false; }
    }

    // Re-label all six buttons from current on-disk slot occupancy. Given any one of our
    // buttons, walk up to the shared container and update siblings by name.
    private static void RefreshLabels(Node anyButton)
    {
        try
        {
            var container = anyButton?.GetParent();
            if (container == null) return;
            for (int n = 1; n <= SlotCount; n++)
            {
                bool occ = SlotOccupied(n);
                int? floor = GetSlotFloor(n);
                SetLabel(container.GetNodeOrNull(SaveButtonName(n)), SaveLabelText(n, occ, floor));
                SetLabel(container.GetNodeOrNull(LoadButtonName(n)), LoadLabelText(n, occ, floor));
            }
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Snapshot] RefreshLabels failed: {ex.Message}");
        }
    }

    private static void SetLabel(Node button, string text)
    {
        var label = button?.GetNodeOrNull<MegaLabel>("Label");
        if (label != null)
            label.SetTextAutoSize(text);
    }

    // ---- Paths -----------------------------------------------------------------

    private static string SaveRootPath() => Path.Combine(OS.GetUserDataDir(), SaveRootFolder);
    private static string SnapshotsRootPath() => Path.Combine(OS.GetUserDataDir(), SnapshotsFolder);
    private static string SlotPath(int slot) => Path.Combine(SnapshotsRootPath(), SlotPrefix + slot);
    // Floor meta lives as a sibling file (slot_<N>.floor), never inside the slot tree, so
    // restoring a slot into user://default never carries it into the live save.
    private static string SlotMetaPath(int slot) => Path.Combine(SnapshotsRootPath(), SlotPrefix + slot + ".floor");

    // ---- Floor number ----------------------------------------------------------

    // The run's current floor, read live from the game. Null when not in a run
    // (State is null on the main menu) or if anything is unavailable — never throws.
    // RunManager.Instance is public, but State/ActFloor are not, so they are read via
    // reflection (getter first, backing field as fallback) — the same reflection path
    // QuickRestartPatch already relies on and that survives this AOT build (sts2 is the
    // rooted main assembly, so its metadata is preserved).
    private static int? GetCurrentFloor()
    {
        try
        {
            var rm = RunManager.Instance;
            if (rm == null) return null;

            var state = PatchHelper.Method(rm.GetType(), "get_State")?.Invoke(rm, null)
                        ?? PatchHelper.Field(rm.GetType(), "<State>k__BackingField")?.GetValue(rm);
            if (state == null) return null;

            var floor = PatchHelper.Method(state.GetType(), "get_ActFloor")?.Invoke(state, null)
                        ?? PatchHelper.Field(state.GetType(), "<ActFloor>k__BackingField")?.GetValue(state);
            return floor is int i ? i : (int?)null;
        }
        catch { return null; }
    }

    private static void SetSlotFloor(int slot, int? floor)
    {
        try
        {
            var meta = SlotMetaPath(slot);
            if (floor.HasValue)
                File.WriteAllText(meta, floor.Value.ToString());
            else if (File.Exists(meta))
                File.Delete(meta); // unknown floor -> label degrades to [已存]/[Saved]
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Snapshot] SetSlotFloor({slot}) failed: {ex.Message}");
        }
    }

    private static int? GetSlotFloor(int slot)
    {
        try
        {
            var meta = SlotMetaPath(slot);
            if (!File.Exists(meta)) return null;
            return int.TryParse(File.ReadAllText(meta).Trim(), out var f) ? f : (int?)null;
        }
        catch { return null; }
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    // ---- Crash-safe swap helpers ---------------------------------------------

    // Copy `source` into a staged sibling directory and mark it complete with a
    // sibling flag file. A kill mid-copy leaves the flag absent, so the heal step
    // never promotes a half-written tree into place.
    private static void StageCopy(string source, string staged)
    {
        if (Directory.Exists(staged)) Directory.Delete(staged, true);
        var flag = staged + ".ready";
        if (File.Exists(flag)) File.Delete(flag);
        CopyDirectory(source, staged);
        File.WriteAllText(flag, "");
    }

    // Replace `target` with the staged tree without any moment where `target` is
    // missing-and-unrecoverable: the old tree moves aside to `<target>.old` first,
    // then the staged copy moves in, then the old tree is deleted. The only crash
    // window (between the two moves) leaves BOTH trees on disk, and HealSwapState
    // repairs it on the next entry.
    private static void SwapInPlace(string staged, string target)
    {
        var old = target + ".old";
        if (Directory.Exists(old)) Directory.Delete(old, true);
        if (Directory.Exists(target)) Directory.Move(target, old);
        Directory.Move(staged, target);
        var flag = staged + ".ready";
        if (File.Exists(flag)) File.Delete(flag);
        if (Directory.Exists(old)) Directory.Delete(old, true);
    }

    // Repair whatever state an interrupted swap left behind for `target`:
    //   target present -> swap finished (or never started): drop stale staged leftovers;
    //                     the old tree is archived, never deleted — on disk it is
    //                     indistinguishable from real progress (a killed swap followed by
    //                     the game rebuilding `default` looks identical), so keep it.
    //   target missing -> promote staged (only if complete, flag present), else
    //                     restore the old tree from `<target>.old`
    private static void HealSwapState(string target, string staged)
    {
        var flag = staged + ".ready";
        var old = target + ".old";
        if (Directory.Exists(target))
        {
            if (Directory.Exists(staged)) Directory.Delete(staged, true);
            if (File.Exists(flag)) File.Delete(flag);
            if (Directory.Exists(old)) ArchiveOld(old);
        }
        else if (Directory.Exists(staged) && File.Exists(flag))
        {
            Directory.Move(staged, target);
            File.Delete(flag);
            if (Directory.Exists(old)) Directory.Delete(old, true);
        }
        else
        {
            if (Directory.Exists(staged)) Directory.Delete(staged, true);
            if (File.Exists(flag)) File.Delete(flag);
            if (Directory.Exists(old)) Directory.Move(old, target);
        }
    }

    // 归档旧树(保留 3 份):理由见 HealSwapState——不能删,只能改名留底
    private static void ArchiveOld(string old)
    {
        if (!Directory.Exists(old)) return;
        var arch = old + "-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Directory.Move(old, arch);
        var parent = Path.GetDirectoryName(old);
        var prefix = Path.GetFileName(old) + "-";
        var kept = Directory.GetDirectories(parent, prefix + "*").OrderByDescending(d => d).ToList();
        foreach (var d in kept.Skip(3))
            Directory.Delete(d, true);
    }

    // ---- Node names & localized labels -----------------------------------------

    private static string SaveButtonName(int slot) => "SaveSlot" + slot;
    private static string LoadButtonName(int slot) => "LoadSlot" + slot;

    private static bool IsChinese()
    {
        try
        {
            var locale = TranslationServer.GetLocale();
            return !string.IsNullOrEmpty(locale) && locale.StartsWith("zh");
        }
        catch { return false; }
    }

    private static string SaveLabelText(int slot, bool occupied, int? floor)
    {
        if (!occupied)
            return IsChinese() ? $"存档位{slot}" : $"Save Slot {slot}";
        return IsChinese() ? $"存档位{slot} {FloorTag(floor)}" : $"Save Slot {slot} {FloorTag(floor)}";
    }

    private static string LoadLabelText(int slot, bool occupied, int? floor)
    {
        if (!occupied)
            return IsChinese() ? $"读档位{slot} [空]" : $"Load Slot {slot} [Empty]";
        return IsChinese() ? $"读档位{slot} {FloorTag(floor)}" : $"Load Slot {slot} {FloorTag(floor)}";
    }

    // Floor suffix for an occupied slot: "[第12层]"/"[Floor 12]" when the floor was
    // captured, degrading to "[已存]"/"[Saved]" when it could not be read at save time.
    private static string FloorTag(int? floor)
    {
        if (floor.HasValue && floor.Value >= 0)
            return IsChinese() ? $"[第{floor.Value}层]" : $"[Floor {floor.Value}]";
        return IsChinese() ? "[已存]" : "[Saved]";
    }
}
