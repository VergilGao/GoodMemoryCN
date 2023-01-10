using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.IoC;

namespace GoodMemory;

internal class Service
{
    [PluginService] internal static ClientState ClientState { get; set; } = null!;

    [PluginService] internal static DataManager DataManager { get; set; } = null!;

    [PluginService] internal static SigScanner SigScanner { get; set; } = null!;

    [PluginService] internal static GameGui GameUi { get; set; } = null!;
}