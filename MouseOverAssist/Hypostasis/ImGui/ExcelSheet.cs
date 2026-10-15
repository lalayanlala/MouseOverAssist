using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Utility;
using Lumina.Excel;

namespace Dalamud.Bindings.ImGui;

public static partial class ImGuiEx
{
    public record ExcelSheetOptions<T> where T : struct, IExcelRow<T>
    {
        public Func<T, string> FormatRow { get; init; } = row => row.ToString();
        public Func<T, string, bool> SearchPredicate { get; init; } = null;
        public Func<T, bool, bool> DrawSelectable { get; init; } = null;
        public IEnumerable<T> FilteredSheet { get; init; } = null;
        public Vector2? Size { get; init; } = null;
    }

    public record ExcelSheetComboOptions<T> : ExcelSheetOptions<T> where T : struct, IExcelRow<T>
    {
        public Func<T, string> GetPreview { get; init; } = null;
        public ImGuiComboFlags ComboFlags { get; init; } = ImGuiComboFlags.None;
    }

    public record ExcelSheetPopupOptions<T> : ExcelSheetOptions<T> where T : struct, IExcelRow<T>
    {
        public ImGuiPopupFlags PopupFlags { get; init; } = ImGuiPopupFlags.None;
        public bool CloseOnSelection { get; init; } = false;
        public Func<T, bool> IsRowSelected { get; init; } = _ => false;
    }

    private static string sheetSearchText;
    private static uint[] filteredSearchIDs;
    private static string prevSearchID;
    private static Type prevSearchType;

    private static void ExcelSheetSearchInput<T>(string id, IEnumerable<T> filteredSheet, Func<T, string, bool> searchPredicate) where T : struct, IExcelRow<T>
    {
        if (ImGui.IsWindowAppearing() && ImGui.IsWindowFocused() && !ImGui.IsAnyItemActive())
        {
            if (id != prevSearchID)
            {
                if (typeof(T) != prevSearchType)
                {
                    sheetSearchText = string.Empty;
                    prevSearchType = typeof(T);
                }

                filteredSearchIDs = null;
                prevSearchID = id;
            }

            ImGui.SetKeyboardFocusHere(0);
        }

        if (ImGui.InputTextWithHint("##ExcelSheetSearch", "Search", ref sheetSearchText, 128, ImGuiInputTextFlags.AutoSelectAll))
            filteredSearchIDs = null;

        filteredSearchIDs ??= filteredSheet.Where(s => searchPredicate(s, sheetSearchText)).Select(r => r.RowId).ToArray();
    }

    public static bool ExcelSheetCombo<T>(string id, ref uint selectedRow, ExcelSheetComboOptions<T> options = null) where T : struct, IExcelRow<T>
    {
        options ??= new ExcelSheetComboOptions<T>();
        var sheet = DalamudApi.DataManager.GetExcelSheet<T>();

        var getPreview = options.GetPreview ?? options.FormatRow;
        if (!ImGui.BeginCombo(id, sheet.GetRowOrDefault(selectedRow) is { } r ? getPreview(r) : selectedRow.ToString(), options.ComboFlags | ImGuiComboFlags.HeightLargest)) return false;

        ExcelSheetSearchInput(id, options.FilteredSheet ?? sheet, options.SearchPredicate ?? ((row, s) => options.FormatRow(row).Contains(s, StringComparison.CurrentCultureIgnoreCase)));

        ImGui.BeginChild("ExcelSheetSearchList", options.Size ?? new Vector2(0, 200 * ImGuiHelpers.GlobalScale), true);

        var ret = false;
        var drawSelectable = options.DrawSelectable ?? ((row, selected) => ImGui.Selectable(options.FormatRow(row), selected));
        using (var clipper = new ListClipper(filteredSearchIDs.Length))
        {
            foreach (var i in clipper.Rows)
            {
                if (sheet.GetRowOrDefault(filteredSearchIDs[i]) is not { } row) continue;

                using var _ = IDBlock.Begin(i);
                if (!drawSelectable(row, selectedRow == row.RowId)) continue;
                selectedRow = row.RowId;
                ret = true;
                break;
            }
        }

        // ImGui issue #273849, children keep popups from closing automatically
        if (ret)
            ImGui.CloseCurrentPopup();

        ImGui.EndChild();
        ImGui.EndCombo();
        return ret;
    }

    public static bool ExcelSheetPopup<T>(string id, out uint selectedRow, ExcelSheetPopupOptions<T> options = null) where T : struct, IExcelRow<T>
    {
        options ??= new ExcelSheetPopupOptions<T>();
        var sheet = DalamudApi.DataManager.GetExcelSheet<T>();
        selectedRow = 0;

        ImGui.SetNextWindowSize(options.Size ?? new Vector2(0, 250 * ImGuiHelpers.GlobalScale));
        if (!ImGui.BeginPopupContextItem(id, options.PopupFlags)) return false;

        ExcelSheetSearchInput(id, options.FilteredSheet ?? sheet, options.SearchPredicate ?? ((row, s) => options.FormatRow(row).Contains(s, StringComparison.CurrentCultureIgnoreCase)));

        ImGui.BeginChild("ExcelSheetSearchList", Vector2.Zero, true);

        var ret = false;
        var drawSelectable = options.DrawSelectable ?? ((row, selected) => ImGui.Selectable(options.FormatRow(row), selected));
        using (var clipper = new ListClipper(filteredSearchIDs.Length))
        {
            foreach (var i in clipper.Rows)
            {
                if (sheet.GetRowOrDefault(filteredSearchIDs[i]) is not { } row) continue;

                using var _ = IDBlock.Begin(i);
                if (!drawSelectable(row, options.IsRowSelected(row))) continue;
                selectedRow = row.RowId;
                ret = true;
            }
        }

        // ImGui issue #273849, children keep popups from closing automatically
        if (ret && options.CloseOnSelection)
            ImGui.CloseCurrentPopup();

        ImGui.EndChild();
        ImGui.EndPopup();
        return ret;
    }

    public static bool ExcelSheetMultiselectPopup<T>(string id, ICollection<uint> selectedRows, ExcelSheetPopupOptions<T> options = null) where T : struct, IExcelRow<T>
    {
        options ??= new ExcelSheetPopupOptions<T>();
        options = options with { IsRowSelected = row => selectedRows.Contains(row.RowId) };
        if (!ExcelSheetPopup(id, out var selectedRow, options)) return false;
        if (!selectedRows.Remove(selectedRow))
            selectedRows.Add(selectedRow);
        return true;
    }

    // This legacy debug table was permanently disabled because it crashes with the current ImGui/Dalamud stack.
    // Keep the public helper as a no-op for source compatibility, without compiling unreachable code.
    public static void ExcelSheetTable<T>(string id) where T : struct, IExcelRow<T>
    {
    }

}