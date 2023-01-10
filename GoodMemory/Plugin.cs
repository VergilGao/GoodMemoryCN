using System;
using System.Linq;
using Dalamud;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;

namespace GoodMemory;

// ReSharper disable once ClassNeverInstantiated.Global
public class Plugin : IDalamudPlugin
{
    private readonly Hook<TooltipDelegate> tooltipHook;
    private ItemTooltip? _tooltip;
    private readonly GameFunctions Functions;

    public Plugin(
        [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();
        Functions = new GameFunctions();

        unsafe
        {
            var tooltipAddress = Service.SigScanner.ScanText("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 50 48 8B 42 ??");
            tooltipHook = Hook<TooltipDelegate>.FromAddress(tooltipAddress, TooltipDetour);
            tooltipHook.Enable();
        }
    }

    public string Name => "Good Memory";

    public void Dispose()
    {
        tooltipHook.Dispose();
        _tooltip?.Dispose();
    }

    private unsafe IntPtr TooltipDetour(IntPtr a1, uint** a2, byte*** a3)
    {
        try
        {
            _tooltip ??= new ItemTooltip();
            _tooltip.SetPointer(a3);
            OnItemTooltip(_tooltip, Service.GameUi.HoveredItem);
        }
        catch (Exception ex)
        {
            PluginLog.LogError(ex, "Failed to handle tooltip detour");
        }

        return tooltipHook.Original(a1, a2, a3);
    }

    private void OnItemTooltip(ItemTooltip tooltip, ulong itemId)
    {
        switch (itemId)
        {
            case > 2_000_000:
                return;
            case > 1_000_000:
                itemId -= 1_000_000;
                break;
        }

        var item = Service.DataManager.GetExcelSheet<Item>()!.GetRow((uint)itemId);
        if (item == null) return;

        var description = tooltip[ItemTooltip.TooltipField.ItemDescription];

        foreach (var payload in description.Payloads)
        {
            if (payload is not TextPayload p) continue;
            if (p.Text == null) continue;

            p.Text = p.Text.Replace("\n未学习或收录", "").Replace("\n已学习或收录", "");
        }

        // Faded Copies
        if (item.FilterGroup == 12 && item.ItemUICategory.Value?.RowId == 94 && item.LevelItem.Value?.RowId == 1)
        {
            var recipeResults = Service.DataManager.GetExcelSheet<Recipe>()!
                .Where(recipe => recipe.UnkData5.Any(ritem => ritem.ItemIngredient == item.RowId))
                .Select(recipe => recipe.ItemResult.Value)
                .Where(result => result != null)
                .ToArray();

            foreach (var result in recipeResults)
            {
                var resultAction = result?.ItemAction?.Value;
                if (result == null || (ActionType?)resultAction?.Type != ActionType.OrchestrionRolls) continue;

                var orchId = result.AdditionalData;
                var orch = Service.DataManager.GetExcelSheet<Orchestrion>()!.GetRow(orchId);
                if (orch == null) continue;

                AppendIfAcquired(description, result!, orch.Name);
            }
        }
        else
        {
            var action = item.ItemAction?.Value;

            if (!ActionTypeExt.IsValidAction(action)) return;

            // generate our replacement text
            AppendIfAcquired(description, item);
        }

        tooltip[ItemTooltip.TooltipField.ItemDescription] = description;
    }

    private void AppendIfAcquired(SeString txt, Item item, string? name = null)
    {
        string yes;
        string no;
        string acquired;
        string colon;
        string parenL;
        string parenR;
        switch (Service.ClientState.ClientLanguage)
        {
            default:
                acquired = "Acquired";
                colon = ": ";
                yes = "Yes";
                no = "No";
                parenL = " (";
                parenR = ")";
                break;
            case ClientLanguage.ChineseSimplified:
                acquired = "已获得";
                colon = "：";
                yes = "是";
                no = "否";
                parenL = " (";
                parenR = ")";
                break;
            case ClientLanguage.French:
                acquired = "Acquis";
                colon = " : ";
                yes = "Oui";
                no = "Non";
                parenL = " (";
                parenR = ")";
                break;
            case ClientLanguage.German:
                acquired = "Erhalten";
                colon = ": ";
                yes = "Ja";
                no = "Nein";
                parenL = " (";
                parenR = ")";
                break;
            case ClientLanguage.Japanese:
                acquired = "取得";
                colon = "：";
                yes = "あり";
                no = "なし";
                parenL = "（";
                parenR = "）";
                break;
        }

        var has = Functions.HasAcquired(item) ? yes : no;
        var text = name == null
            ? $"\n{acquired}{colon}{has}"
            : $"\n{acquired}{parenL}{name}{parenR}{colon}{has}";
        txt.Payloads.Add(new TextPayload(text));
    }

    private unsafe delegate IntPtr TooltipDelegate(IntPtr a1, uint** a2, byte*** a3);
}