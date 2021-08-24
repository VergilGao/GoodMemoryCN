using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using System.Diagnostics;
using System.Linq;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using XivCommon;
using XivCommon.Functions.Tooltips;

namespace GoodMemory {
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Plugin : IDalamudPlugin {
        public string Name => "Good Memory";

        [PluginService]
        private ClientState ClientState { get; init; } = null!;

        [PluginService]
        internal DataManager DataManager { get; init; } = null!;

        [PluginService]
        internal SigScanner SigScanner { get; init; } = null!;

        private GameFunctions Functions { get; }
        private XivCommonBase Common { get; }

        public Plugin() {
            this.Common = new XivCommonBase(Hooks.Tooltips);
            this.Common.Functions.Tooltips.OnItemTooltip += this.OnItemTooltip;

            this.Functions = new GameFunctions(this);
        }

        public void Dispose() {
            this.Common.Functions.Tooltips.OnItemTooltip -= this.OnItemTooltip;
            this.Common.Dispose();
        }

        private void OnItemTooltip(ItemTooltip tooltip, ulong itemId) {
            if (!tooltip.Fields.HasFlag(ItemTooltipFields.Description)) {
                return;
            }

            if (itemId > 2_000_000) {
                return;
            }

            if (itemId > 1_000_000) {
                itemId -= 1_000_000;
            }

            var item = this.DataManager.GetExcelSheet<Item>()!.GetRow((uint) itemId);
            if (item == null) {
                return;
            }

            var description = tooltip[ItemTooltipString.Description];

            // Faded Copies
            if (item.FilterGroup == 12 && item.ItemUICategory.Value?.RowId == 94 && item.LevelItem.Value?.RowId == 1) {
                var recipeResults = this.DataManager.GetExcelSheet<Recipe>()!
                    .Where(recipe => recipe.UnkStruct5.Any(ritem => ritem.ItemIngredient == item.RowId))
                    .Select(recipe => recipe.ItemResult.Value)
                    .Where(result => result != null)
                    .ToArray();

                foreach (var result in recipeResults) {
                    var resultAction = result?.ItemAction?.Value;
                    if (!ActionTypeExt.IsValidAction(resultAction)) {
                        continue;
                    }

                    Debug.Assert(resultAction != null, nameof(resultAction) + " != null");

                    uint orchId = resultAction.Data[0];
                    var orch = this.DataManager.GetExcelSheet<Orchestrion>()!.GetRow(orchId);
                    if (orch == null) {
                        continue;
                    }

                    this.AppendIfAcquired(description, result!, orch.Name);
                }
            } else {
                var action = item.ItemAction?.Value;

                if (!ActionTypeExt.IsValidAction(action)) {
                    return;
                }

                // generate our replacement text
                this.AppendIfAcquired(description, item);
            }

            tooltip[ItemTooltipString.Description] = description;
        }

        private void AppendIfAcquired(SeString txt, Item item, string? name = null) {
            string yes;
            string no;
            string acquired;
            string colon;
            string parenL;
            string parenR;
            switch (this.ClientState.ClientLanguage) {
                default:
                    acquired = "Acquired";
                    colon = ": ";
                    yes = "Yes";
                    no = "No";
                    parenL = " (";
                    parenR = ")";
                    break;
                case Dalamud.ClientLanguage.French:
                    acquired = "Acquis";
                    colon = " : ";
                    yes = "Oui";
                    no = "Non";
                    parenL = " (";
                    parenR = ")";
                    break;
                case Dalamud.ClientLanguage.German:
                    acquired = "Erhalten";
                    colon = ": ";
                    yes = "Ja";
                    no = "Nein";
                    parenL = " (";
                    parenR = ")";
                    break;
                case Dalamud.ClientLanguage.Japanese:
                    acquired = "取得";
                    colon = "：";
                    yes = "あり";
                    no = "なし";
                    parenL = "（";
                    parenR = "）";
                    break;
            }

            var has = this.Functions.HasAcquired(item) ? yes : no;
            var text = name == null
                ? $"\n{acquired}{colon}{has}"
                : $"\n{acquired}{parenL}{name}{parenR}{colon}{has}";
            txt.Payloads.Add(new TextPayload(text));
        }
    }
}
