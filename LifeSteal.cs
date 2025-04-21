using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Events;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using CounterStrikeSharp.API.Modules.Cvars;

namespace LifeSteal;

public class LifeSteal : BasePlugin
{
    public override string ModuleName => "LifeSteal";
    public override string ModuleVersion => "0.0.1";

    private readonly HashSet<ulong> TargetedPlayers = new();
    private int mHealth = int.MaxValue;
    private int hDrain = 1;
    private int keepHealth = 0;
    private string _configPath => Path.Combine(ModuleDirectory, "lifesteal_config.cfg");
    private string _configPathDrain => Path.Combine(ModuleDirectory, "drain_config.cfg");
    private string _cfgKeepHealth => Path.Combine(ModuleDirectory, "keep_health.cfg");
    private readonly Dictionary<CCSPlayerController, float> msgTimer = new();
    private readonly Dictionary<CCSPlayerController, string> dmgMsg = new();
    private readonly Dictionary<CCSPlayerController, int> finishHealth = new();
    public static Dictionary<string, string> Messages = new();
    private static string _filePath = string.Empty;
    private static Dictionary<string, string> GetDefaultMessages()
    {
        return new Dictionary<string, string>
        {
            {
                "LifeStealGained",
                "<font size='32' color='#fc4040'>[LifeSteal]</font><br><font size='28'><font color='#b82000'>+{0}</font> <font color='#dadada'>HP Gained</font></font>"
            },
            {
                "LifeStealMax",
                "<font size='32' color='#fc4040'>[LifeSteal]</font><br><font size='28'><font color='#b82000'>MAXIMUM</font> <font color='#dadada'>HP Reached</font></font>"
            }
        };
    }
    public override void Load(bool hotReload)
    {
        Console.WriteLine("Life Steal engaged!");
        LoadMessages(Path.Combine(ModuleDirectory, "lifesteal_messages.json"));
        SaveMessages();
        LoadConfig();
        LoadDrainConfig();
        loadKeepHealth();

        AddTimer(1.0f, () => hurtPlayer(), TimerFlags.REPEAT);
        AddCommand("ls", "LifeSteal command", healthDrain);
        AddCommand("mh", "Changes max health", maxH);
        AddCommand("hp", "Changes health drain", hP);
        AddCommand("lshelp", "Tells all commands about the plugin.", help);
        AddCommand("kh", "Enables/Disables keeping health on round end.", kH);
        RegisterEventHandler<EventPlayerHurt>(playerHurt);
        RegisterEventHandler<EventRoundStart>(roundStart);
        RegisterEventHandler<EventRoundEnd>(roundEnd);
        RegisterEventHandler<EventServerPreShutdown>(preShutDown);
        RegisterEventHandler<EventServerShutdown>(ShutDown);
        RegisterListener<Listeners.OnTick>(OnTick);

    }

    public override void Unload(bool hotReload)
    {
        SaveConfig();
        SaveDrainConfig();
        saveKeepHealth();
        SaveMessages();
    }

    public HookResult preShutDown(EventServerPreShutdown @event, GameEventInfo info)
    {
        SaveConfig();
        SaveDrainConfig();
        saveKeepHealth();
        SaveMessages();

        return HookResult.Continue;
    }

    public HookResult ShutDown(EventServerShutdown @event, GameEventInfo info)
    {
        SaveConfig();
        SaveDrainConfig();
        saveKeepHealth();
        SaveMessages();

        return HookResult.Continue;
    }


    public static void LoadMessages(string filePath)
    {
        _filePath = filePath;

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File not found, creating default: {filePath}");
            Messages = GetDefaultMessages();
            SaveMessages();
            return;
        }

        var json = File.ReadAllText(filePath);
        Messages = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? GetDefaultMessages();

        Console.WriteLine($"Loaded {Messages.Count} messages from {filePath}");
    }

    public static void SaveMessages()
    {
        if (string.IsNullOrEmpty(_filePath))
            return;

        var json = JsonSerializer.Serialize(Messages, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_filePath, json);
        Console.WriteLine($"Saved messages to {_filePath}");
    }

    private void LoadConfig()
    {
        if (!File.Exists(_configPath))
        {
            File.WriteAllText(_configPath, mHealth.ToString());
            return;
        }

        try
        {
            string content = File.ReadAllText(_configPath);
            if (int.TryParse(content, out int loadedValue))
            {
                mHealth = loadedValue;
            }
        }
        catch { }
    }

    private void LoadDrainConfig()
    {
        if (!File.Exists(_configPathDrain))
        {
            File.WriteAllText(_configPathDrain, hDrain.ToString());
            return;
        }

        try
        {
            string content = File.ReadAllText(_configPathDrain);
            if (int.TryParse(content, out int loadedValue))
            {
                hDrain = loadedValue;
            }
        }
        catch { }
    }

    private void SaveConfig()
    {
        try
        {
            File.WriteAllText(_configPath, mHealth.ToString());
        }
        catch { }
    }

    private void SaveDrainConfig()
    {
        try
        {
            File.WriteAllText(_configPathDrain, hDrain.ToString());
        }
        catch { }
    }

    private void loadKeepHealth()
    {
        if (!File.Exists(_cfgKeepHealth))
        {
            File.WriteAllText(_cfgKeepHealth, keepHealth.ToString());
            return;
        }

        try
        {
            string content = File.ReadAllText(_cfgKeepHealth);
            if (int.TryParse(content, out int loadedValue))
            {
                keepHealth = loadedValue;
            }
        }
        catch { }
    }

    private void saveKeepHealth()
    {
        try
        {
            File.WriteAllText(_cfgKeepHealth, keepHealth.ToString());
        }
        catch { }
    }


    private void OnTick()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (player == null || !player.IsValid || !player.PawnIsAlive)
                continue;

            if (msgTimer.TryGetValue(player, out float timer) && timer > 0f)
            {
                timer -= Server.TickInterval;
                if (timer < 0f)
                    timer = 0f;

                msgTimer[player] = timer;

                if (dmgMsg.TryGetValue(player, out var msg))
                {
                    player.PrintToCenterHtml($"{msg}");

                }
            }
        }
    }


    public void StartPlayerMsgTimer(CCSPlayerController player, float duration, string message)
    {
        if (player == null || !player.IsValid)
            return;

        msgTimer[player] = duration;
        dmgMsg[player] = message;
    }

    public HookResult playerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (@event.Attacker is CCSPlayerController playerController)
        {
            if (@event.Userid!.TeamNum != @event.Attacker.TeamNum)
            {
                if (TargetedPlayers.Contains(playerController.SteamID))
                {
                    var currentHealth = playerController.Pawn.Value!.Health;
                    var damageAmount = @event.DmgHealth;

                    Server.NextFrame(() =>
                    {
                        if (!playerController.IsValid || playerController.Pawn.Value == null)
                            return;

                        int newHealth = currentHealth + damageAmount;
                        if (newHealth <= mHealth)
                        {
                            playerController.Pawn.Value.Health = newHealth;

                            string msg = string.Format(
                                Messages["LifeStealGained"],
                                damageAmount
                            );

                            StartPlayerMsgTimer(playerController, 1f, msg);
                        }
                        else
                        {
                            playerController.Pawn.Value.Health = mHealth;

                            string msg = Messages["LifeStealMax"];
                            StartPlayerMsgTimer(playerController, 1f, msg);
                        }

                        Utilities.SetStateChanged(playerController.Pawn.Value, "CBaseEntity", "m_iHealth");

                    });
                }
            }
        }

        return HookResult.Continue;
    }

    public HookResult roundStart(EventRoundStart @event, GameEventInfo info)
    {
        var players = Utilities.GetPlayers()
            .Where(p => p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected);
        foreach (var player in players)
            if (keepHealth == 1) {
                if (finishHealth.TryGetValue(player, out int savedHealth))
                {
                    int currentHealth = player.Pawn?.Value?.Health ?? 0;

                    finishHealth[player] = currentHealth;

                    if (savedHealth > 100)
                    {
                        player.Pawn.Value!.Health = savedHealth;
                    }
                }

            }

        return HookResult.Continue;
    }

    private float roundRestartDelay =>
    float.TryParse(ConVar.Find("mp_round_restart_delay")?.StringValue, out var value) ? value : 7.0f;


    public HookResult roundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        AddTimer(roundRestartDelay - 0.1f, () =>
        {
            var players = Utilities.GetPlayers()
                .Where(p => p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected);

            foreach (var player in players)
            {
                finishHealth[player] = player.Pawn?.Value?.Health ?? 0;
            }
        });

        return HookResult.Continue;
    }

    [RequiresPermissions("@css/generic")]
    private void healthDrain(CCSPlayerController? sender, CommandInfo command)
    {

        string arg = command.ArgString.Trim().ToLower();

        var players = Utilities.GetPlayers()
            .Where(p => p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected);
        if (command.ArgCount < 2)
        {
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} Valid usage:");
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.Grey}       list: List all players that have LifeSteal enabled.");
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.Grey}       *: Enables/Disables LifeSteal for Everyone");
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.Grey}       @ct: Enables/Disables LifeSteal for Counter-Terrorists");
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.Grey}       @t: Enables/Disables LifeSteal for Terrorists");
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.Grey}       <player-name>: Enables/Disables LifeSteal for provided player.");
        }
        else if (arg == "@ct")
        {
            foreach (var player in players.Where(p => p.TeamNum == 3))
                if (!TargetedPlayers.Contains(player.SteamID))
                {
                    TargetedPlayers.Add(player.SteamID);
                    sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} {player.PlayerName} now has LifeSteal {ChatColors.Green}enabled");
                }
                else
                {
                    TargetedPlayers.Remove(player.SteamID);
                    sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} {player.PlayerName} now has LifeSteal {ChatColors.Red}disabled.");
                }
        }
        else if (arg == "@t")
        {
            foreach (var player in players.Where(p => p.TeamNum == 2))
                if (!TargetedPlayers.Contains(player.SteamID))
                {
                    TargetedPlayers.Add(player.SteamID);
                    sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} {player.PlayerName} now has LifeSteal {ChatColors.Green}enabled");
                }
                else
                {
                    TargetedPlayers.Remove(player.SteamID);
                    sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} {player.PlayerName} now has LifeSteal {ChatColors.Red}disabled.");
                }

        }
        else if (arg == "*")
        {
            foreach (var player in players)
                if (!TargetedPlayers.Contains(player.SteamID))
                {
                    TargetedPlayers.Add(player.SteamID);
                    sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} {player.PlayerName} now has LifeSteal {ChatColors.Green}enabled");
                }
                else
                {
                    TargetedPlayers.Remove(player.SteamID);
                    sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} {player.PlayerName} now has LifeSteal {ChatColors.Red}disabled.");
                }
        }
        else if (arg == "list")
        {
            foreach (var player in players)
                if (TargetedPlayers.Contains(player.SteamID))
                {
                    sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White}       {player.PlayerName}");
                }
        }

        else
        {
                    var target = players.FirstOrDefault(p => p.PlayerName.ToLower().Contains(arg));
                    if (target != null)
                    {
                        if (!TargetedPlayers.Contains(target.SteamID))
                        {
                            TargetedPlayers.Add(target.SteamID);
                            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} {target.PlayerName} now has LifeSteal {ChatColors.Green}enabled");
                        }
                        else
                        {
                            TargetedPlayers.Remove(target.SteamID);
                            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} {target.PlayerName} now has LifeSteal {ChatColors.Red}disabled.");
                        }
                    }

                    else
                    {
                        sender?.PrintToChat($" {ChatColors.Red}[LifeSteal] No player found with that name.");
                    }
                
            
        }
    }

    [RequiresPermissions("@css/generic")]
    private void maxH(CCSPlayerController? sender, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} Valid usage:");
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.Grey}       value: Lists the current max health.");
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.Grey}       infinite: Sets max health to infinity.");
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.Grey}       <number>: Sets max health to given number.");
        }

        string arg = command.ArgString.Trim().ToLower();

        var players = Utilities.GetPlayers()
            .Where(p => p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected);

        if (arg == "value")
        {
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White}      {mHealth}");
        }

        if (arg == "infinite")
        {
            mHealth = int.MaxValue;
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} The max health is now {ChatColors.Green}infinite.");
        }
        else
        {
            if (!int.TryParse(arg, out int parsedHealth) || parsedHealth <= 0)
            {
                return;
            }

            mHealth = parsedHealth;
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} The max health is now {ChatColors.Green}{mHealth}.");

            foreach (var player in players)
            {
                if (player.Pawn.Value!.Health <= mHealth)
                {

                }
                else
                {
                    player.Pawn.Value!.Health = mHealth;
                }
            }
        }
    }

    [RequiresPermissions("@css/generic")]
    private void hP(CCSPlayerController? sender, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} Valid usage:");
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.Grey}       value: Lists the current health drain per second.");
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.Grey}       disable: Disables health drain");
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.Grey}       default: Sets health drain per second to default (1).");
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.Grey}       <number>: Sets health drain per second to given number.");
        }

        string arg = command.ArgString.Trim().ToLower();

        var players = Utilities.GetPlayers()
            .Where(p => p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected);
        if (arg == "disable")
        {
            hDrain = 0;
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} Health drain is now {ChatColors.Red}disabled.");
        }
        if (arg == "value")
        {
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White}       {hDrain}");
        }

        if (arg == "default")
        {
            hDrain = 1;
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} The health drain per second is now {ChatColors.Green}default.");
        }
        else
        {
            if (!int.TryParse(arg, out int parsedHealth) || parsedHealth <= 0)
            {
                return;
            }

            hDrain = parsedHealth;
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} The health drain per second is now {ChatColors.Green}{hDrain}.");
        }
    }


    [RequiresPermissions("@css/generic")]
    private void kH(CCSPlayerController? sender, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} Valid usage:");
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.Grey}       on: Enables keeping previous health after start of round.");
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.Grey}       off: Disables keeping previous health after start of round.");
        }

        string arg = command.ArgString.Trim().ToLower();

        var players = Utilities.GetPlayers()
            .Where(p => p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected);

        if (arg == "on")
        {
            keepHealth = 1;
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} Keeping health is now {ChatColors.Green}enabled.");
        }

        if (arg == "off")
        {
            keepHealth = 0;
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} Keeping health is now {ChatColors.Red}disabled.");
        }
    }

    [RequiresPermissions("@css/generic")]
    private void help(CCSPlayerController? sender, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            sender?.PrintToChat($" {ChatColors.Red}[LifeSteal]{ChatColors.White} Available Commands:");

            sender?.PrintToChat($" {ChatColors.Red}[!ls]{ChatColors.White} list / * / @ct / @t / <name>");
            sender?.PrintToChat($" {ChatColors.Grey}    Toggle LifeSteal for players or teams.");

            sender?.PrintToChat($" {ChatColors.Red}[!mh]{ChatColors.White} value / infinite / <number>");
            sender?.PrintToChat($" {ChatColors.Grey}    Set or view max health.");

            sender?.PrintToChat($" {ChatColors.Red}[!hp]{ChatColors.White} value / disable / default / <number>");
            sender?.PrintToChat($" {ChatColors.Grey}    Set or view health drain per second.");

            sender?.PrintToChat($" {ChatColors.Red}[!kh]{ChatColors.White} on / off");
            sender?.PrintToChat($" {ChatColors.Grey}    Toggle keeping health between rounds.");
        }
    }

    private void hurtPlayer()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && player.PawnIsAlive)
            {
                var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
                if (!gameRules!.FreezePeriod && !gameRules!.GamePaused && !gameRules!.WarmupPeriod)
                {
                    if (!TargetedPlayers.Contains(player.SteamID))
                        continue;
                    if (hDrain > 0)
                    {
                        player.Pawn.Value!.Health -= hDrain;
                    }
                    if (player.Pawn.Value!.Health <= 0)
                    {
                        player.CommitSuicide(true, true);
                    }
                    Server.NextFrame(() => Utilities.SetStateChanged(player.PlayerPawn.Value!, "CBaseEntity", "m_iHealth"));
                }
            }
        }
    }
}