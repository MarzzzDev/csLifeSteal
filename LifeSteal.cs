using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Events;
using System.Collections.Generic;
using System.Linq;

namespace LifeSteal;

public class LifeSteal : BasePlugin
{
    public override string ModuleName => "LifeSteal";
    public override string ModuleVersion => "0.0.1";

    private readonly HashSet<ulong> TargetedPlayers = new();

    public override void Load(bool hotReload)
    {
        Console.WriteLine("Health Drain + Life Steal engaged!");
        AddTimer(1.0f, hurtPlayer, TimerFlags.REPEAT);
        AddCommand("hd", "Health drain command", healthDrain);
        RegisterEventHandler<EventPlayerHurt>(playerHurt);

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

                        //if (newHealth <= 100) Limits health to 100, disabled.
                        //{
                        playerController.Pawn.Value.Health = newHealth;
                        //}
                        //else
                        //{
                        //    playerController.Pawn.Value.Health = 100;
                        //}

                        Utilities.SetStateChanged(playerController.Pawn.Value, "CBaseEntity", "m_iHealth");
                    });
                }
            }
        }

        return HookResult.Continue;
    }

    [RequiresPermissions("@css/generic")]
    private void healthDrain(CCSPlayerController? sender, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            sender?.PrintToChat("Usage: !hd <@ct/@t/playername>");
            return;
        }

        string arg = command.ArgString.Trim().ToLower();

        var players = Utilities.GetPlayers()
            .Where(p => p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected);
        if (arg == "@ct")
        {
            foreach (var player in players.Where(p => p.TeamNum == 3))
                if (!TargetedPlayers.Contains(player.SteamID))
                {
                    TargetedPlayers.Add(player.SteamID);
                    sender?.PrintToChat($"{player.PlayerName} will now receive health drain.");
                } else
                {
                    TargetedPlayers.Remove(player.SteamID);
                    sender?.PrintToChat($"{player.PlayerName} will no longer receive health drain.");
                }
        }
        else if (arg == "@t")
        {
            foreach (var player in players.Where(p => p.TeamNum == 2))
                if (!TargetedPlayers.Contains(player.SteamID))
                {
                    TargetedPlayers.Add(player.SteamID);
                    sender?.PrintToChat($"{player.PlayerName} will now receive health drain.");
                }
                else
                {
                    TargetedPlayers.Remove(player.SteamID);
                    sender?.PrintToChat($"{player.PlayerName} will no longer receive health drain.");
                }

        }
        else if (arg == "*")
        {
            foreach (var player in players)
                if (!TargetedPlayers.Contains(player.SteamID))
                {
                    TargetedPlayers.Add(player.SteamID);
                    sender?.PrintToChat($"{player.PlayerName} will now receive health drain.");
                }
                else
                {
                    TargetedPlayers.Remove(player.SteamID);
                    sender?.PrintToChat($"{player.PlayerName} will no longer receive health drain.");
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
                    sender?.PrintToChat($"{target.PlayerName} will now receive health drain.");
                } else
                {
                    TargetedPlayers.Remove(target.SteamID);
                    sender?.PrintToChat($"{target.PlayerName} will no longer receive health drain.");
                }
            }
            else
            {
                sender?.PrintToChat("No player found with that name.");
            }
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
                    player.Pawn.Value!.Health -= 1;
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
