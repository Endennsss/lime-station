using System.Globalization;
using Robust.Shared.Console;

namespace Content.Client._Lime.Dissolve;

/// <summary>Client dissolve preview for a selected local entity.</summary>
public sealed class DissolveCommand : LocalizedEntityCommands
{
    [Dependency] private readonly DissolveSystem _dissolve = default!;

    public override string Command => "lime_dissolve";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2 || !EntityUid.TryParse(args[0], out var uid) || !EntityManager.EntityExists(uid))
        {
            shell.WriteError(Help);
            return;
        }

        if (args[1] == "reset")
        {
            _dissolve.Reset(uid);
            return;
        }

        if (args.Length < 3 || args.Length > 8 || !Parse(args[2], out var value))
        {
            shell.WriteError(Help);
            return;
        }

        var settings = (args.Length > 3 ? args[3].ToLowerInvariant() : "burn") switch
        {
            "burn" => DissolveSettings.Burn,
            "teleport" => DissolveSettings.Teleport,
            "anomaly" => DissolveSettings.Anomaly,
            "energy" => DissolveSettings.Energy,
            _ => null,
        };
        if (settings == null)
        {
            shell.WriteError(Help);
            return;
        }

        if (args.Length > 4)
        {
            if (!Parse(args[4], out var width))
            {
                shell.WriteError(Help);
                return;
            }
            settings = settings with { EdgeWidth = width };
        }
        if (args.Length > 5)
        {
            if (!Color.TryFromHex(args[5], out var color))
            {
                shell.WriteError(Help);
                return;
            }
            settings = settings with { EdgeColor = color };
        }
        if (args.Length > 6)
        {
            if (!Enum.TryParse<DissolveDirection>(args[6], true, out var direction))
            {
                shell.WriteError(Help);
                return;
            }
            settings = settings with { Direction = direction };
        }
        if (args.Length > 7)
        {
            if (!Parse(args[7], out var scale))
            {
                shell.WriteError(Help);
                return;
            }
            settings = settings with { NoiseScale = scale };
        }

        var success = args[1] switch
        {
            "out" => _dissolve.StartDissolve(uid, value, settings),
            "in" => _dissolve.StartMaterialize(uid, value, settings),
            "set" => _dissolve.SetAmount(uid, value, settings),
            _ => false,
        };
        if (!success)
            shell.WriteError(Loc.GetString("lime-dissolve-failed"));
    }

    private static bool Parse(string input, out float value)
    {
        return float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value);
    }
}
