using ff16.ability.wings_fly.Configuration;
using ff16.ability.wings_fly.Template;

using Reloaded.Mod.Interfaces;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;


using FF16Tools.Files.Nex;
using FF16Tools.Files.Nex.Entities;
using FF16Framework.Interfaces.Nex;

namespace ff16.ability.wings_fly;

/// <summary>
/// Your mod logic goes here.
/// </summary>
public class Mod : ModBase // <= Do not Remove.
{
    /// <summary>
    /// Provides access to the mod loader API.
    /// </summary>
    private readonly IModLoader _modLoader;

    /// <summary>
    /// Provides access to the Reloaded.Hooks API.
    /// </summary>
    /// <remarks>This is null if you remove dependency on Reloaded.SharedLib.Hooks in your mod.</remarks>
    private readonly IReloadedHooks? _hooks;

    /// <summary>
    /// Provides access to the Reloaded logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Entry point into the mod, instance that created this class.
    /// </summary>
    private readonly IMod _owner;

    /// <summary>
    /// Provides access to this mod's configuration.
    /// </summary>
    private Config _configuration;

    /// <summary>
    /// The configuration of the currently executing mod.
    /// </summary>
    private readonly IModConfig _modConfig;

    private enum SystemMoveKey : uint
    {
        Fly = 9011,
        Fall = 9012,
        Dodge = 1001,
        PreciseDodge = 9013
    }

    private enum CharaTimelineKey : uint
    {
        Falling = 3405,
        WingsCancel = 2571
    }

    private enum ActionKey : uint
    {
        Wings = 773,
        WingsAirborne = 774,
        WingsCancel = 810,
        WingsDodge = 811,
        WingsPreciseDodge = 812
    }


    public WeakReference<INextExcelDBApiManaged> _managedNexApi;

    public Mod(ModContext context)
    {
        _modLoader = context.ModLoader;
        _hooks = context.Hooks;
        _logger = context.Logger;
        _owner = context.Owner;
        _configuration = context.Configuration;
        _modConfig = context.ModConfig;

#if DEBUG
        Debugger.Launch();
#endif

        _logger.WriteLine($"[{_modConfig.ModId}] Initializing...", _logger.ColorGreen);

        _managedNexApi = _modLoader.GetController<INextExcelDBApiManaged>();
        if (!_managedNexApi.TryGetTarget(out INextExcelDBApiManaged managedNextExcelDBApi))
        {
            _logger.WriteLine($"[{_modConfig.ModId}] Could not get INextExcelDBApi. Is the FFXVI Mod Framework installed/loaded?");
            return;
        }

        managedNextExcelDBApi.OnNexLoaded += NextExcelDBApi_OnNexLoaded;
    }



    /// <summary>
    /// Fired when the game has loaded all nex tables.
    /// </summary>
    private unsafe void NextExcelDBApi_OnNexLoaded()
    {
        ApplyFlyingParam();
    }

    private unsafe void ApplyFlyingParam() {
        _logger.WriteLine($"[{_modConfig.ModId}] Applying Wings of Light flying parameters...", _logger.ColorGreen);
        
        _managedNexApi.TryGetTarget(out var nextExcelDBApi);

        INexTable actionTable = nextExcelDBApi.GetTable(Enum.Parse<NexTableIds>("action"));
        NexTableLayout actionLayout = TableMappingReader.ReadTableLayout("action", new Version(1, 0, 3));

        INexTable systemMoveTable = nextExcelDBApi.GetTable(Enum.Parse<NexTableIds>("systemmove"));
        NexTableLayout systemMoveLayout = TableMappingReader.ReadTableLayout("systemmove", new Version(1, 0, 3));

        INexRow WingsRow = actionTable.GetRow(((uint)ActionKey.Wings));
        INexRow WingsAirborneRow = actionTable.GetRow(((uint)ActionKey.WingsAirborne));
        INexRow WingsCancelRow = actionTable.GetRow(((uint)ActionKey.WingsCancel));
        INexRow WingsDodgeRow = actionTable.GetRow(((uint)ActionKey.WingsDodge));
        INexRow WingsPreciseDodgeRow = actionTable.GetRow(((uint)ActionKey.WingsPreciseDodge));

        INexRow FlyRow = systemMoveTable.GetRow((uint)SystemMoveKey.Fly);
        INexRow FallRow = systemMoveTable.GetRow((uint)SystemMoveKey.Fall);
        INexRow DodgeRow = systemMoveTable.GetRow((uint)SystemMoveKey.Dodge);
        INexRow PreciseDodgeRow = systemMoveTable.GetRow((uint)SystemMoveKey.PreciseDodge);


        // Update Cancel animation
        WingsCancelRow.SetInt32((uint)actionLayout.Columns["CharaTimelineId"].Offset, (int)(_configuration.ReplaceCancelAnimation ? CharaTimelineKey.Falling : CharaTimelineKey.WingsCancel));

        // Link actions to appropriate systemMoves
        WingsRow.SetInt32((uint)actionLayout.Columns["SystemMoveId"].Offset, (int)SystemMoveKey.Fly);
        WingsAirborneRow.SetInt32((uint)actionLayout.Columns["SystemMoveId"].Offset, (int)SystemMoveKey.Fly);
        WingsCancelRow.SetInt32((uint)actionLayout.Columns["SystemMoveId"].Offset, (int)SystemMoveKey.Fall);
        // Dodge is already set by default
        // WingsDodgeRow.SetInt32((uint)actionLayout.Columns["SystemMoveId"].Offset, (int)SystemMoveKey.Dodge);
        WingsPreciseDodgeRow.SetInt32((uint)actionLayout.Columns["SystemMoveId"].Offset, (int)SystemMoveKey.PreciseDodge);

        // Configure system moves
        // Flight
        FlyRow.SetSingle((uint)systemMoveLayout.Columns["VerticalPush"].Offset, _configuration.IntialFlightHeight);
        FlyRow.SetSingle((uint)systemMoveLayout.Columns["VerticalInterpolation"].Offset, 1f);
        FlyRow.SetSingle((uint)systemMoveLayout.Columns["FallGravity"].Offset, 0.01f);
        FlyRow.SetSingle((uint)systemMoveLayout.Columns["ForwardPush"].Offset, _configuration.FlightForwardSpeed);

        // Fall
        FallRow.SetSingle((uint)systemMoveLayout.Columns["FallGravity"].Offset, -12f);

        // Dodge
        DodgeRow.SetSingle((uint)systemMoveLayout.Columns["Unk2"].Offset, _configuration.DodgeDistance);
        DodgeRow.SetSingle((uint)systemMoveLayout.Columns["Unk3"].Offset, _configuration.DodgeDuration);
        DodgeRow.SetSingle((uint)systemMoveLayout.Columns["VerticalPush"].Offset, 0.01f);
        DodgeRow.SetSingle((uint)systemMoveLayout.Columns["VerticalInterpolation"].Offset, 1f);
        DodgeRow.SetSingle((uint)systemMoveLayout.Columns["FallGravity"].Offset, 0.01f);

        // Precise Dodge
        PreciseDodgeRow.SetSingle((uint)systemMoveLayout.Columns["VerticalPush"].Offset, _configuration.PreciseDodgeExtraHeight);
        PreciseDodgeRow.SetSingle((uint)systemMoveLayout.Columns["VerticalInterpolation"].Offset, 1f);
        PreciseDodgeRow.SetSingle((uint)systemMoveLayout.Columns["FallGravity"].Offset, 0.01f);

        _logger.WriteLine($"[{_modConfig.ModId}] Applied flying params!", _logger.ColorGreen);
    }

    #region Standard Overrides
    public override void ConfigurationUpdated(Config configuration)
    {
        _configuration = configuration;
        _logger.WriteLine($"[{_modConfig.ModId}] Config Updated");

        ApplyFlyingParam();
    }

    #endregion

    #region For Exports, Serialization etc.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public Mod() { }
#pragma warning restore CS8618
    #endregion
}