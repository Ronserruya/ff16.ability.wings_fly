using ff16.ability.wings_fly.Configuration;
using ff16.ability.wings_fly.Template;

using Reloaded.Mod.Interfaces;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;


using FF16Tools.Files.Nex;
using FF16Tools.Files.Nex.Entities;
using FF16Framework.Interfaces.Nex;
using SharpDX.XInput;
using DualSenseAPI;
using GlobalKeyInterceptor;
using Reloaded.Memory.Interfaces;
using SharpDX.Mathematics.Interop;
using System.Data.Common;
using System.Text;
using FF16Framework.Interfaces.Nex.Structures;

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

    private INexRow _wingsPlayerCommandBuilder;
    private INexRow _commandAttackRow;
    private NexTableLayout _playerCommandLayout = TableMappingReader.ReadTableLayout("playercommandbuilder", new Version(1, 0, 3));
    private NexTableLayout _summonLayout = TableMappingReader.ReadTableLayout("summonmode", new Version(1, 0, 3));
    private NexTableLayout _cmdLayout = TableMappingReader.ReadTableLayout("command", new Version(1, 0, 3));
    private nint _bahaMagicStrPtr;
    private nint _atkStrPtr;


    private Controller _controller = new Controller(UserIndex.One);

    private enum SystemMoveKey : uint
    {
        Fly = 9011,
        Fall = 9012,
        Dodge = 1001,
        PreciseDodge = 9013
    }

    private enum CommandKey : uint
    {
        Ascend = 9001,
        Descend = 9002
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

    private byte[] AscendBytes = Encoding.UTF8.GetBytes("Ascend\0");
    private byte[] originalBytes;
    private int _atkStrOffset;
    private int _newDescStrOffset;
    private bool _renameAirActions = false;


    public WeakReference<INextExcelDBApiManaged> _managedNexApi;
    public WeakReference<INextExcelDBApi> _rawNexApi;

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
        _rawNexApi = _modLoader.GetController<INextExcelDBApi>();
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
        SetRowsAndPtrs();
        CreateInputTasks();
    }

    private unsafe void EnableFlightMode()
    {
        if (_renameAirActions)
        {
            Reloaded.Memory.Memory.Instance.WriteRaw((nuint)_bahaMagicStrPtr, AscendBytes);
            _commandAttackRow.SetInt32((uint)_cmdLayout.Columns["Name"].Offset, _newDescStrOffset);
        }

        _wingsPlayerCommandBuilder.SetInt32((uint)_playerCommandLayout.Columns["Unk4"].Offset, (int)CommandKey.Descend);
        _wingsPlayerCommandBuilder.SetInt32((uint)_playerCommandLayout.Columns["TriangleCommandId"].Offset, (int)CommandKey.Ascend);
    }
    private unsafe void DisableFlightMode()
    {
        if (_renameAirActions)
        {
            Reloaded.Memory.Memory.Instance.WriteRaw((nuint)_bahaMagicStrPtr, originalBytes);
            _commandAttackRow.SetInt32((uint)_cmdLayout.Columns["Name"].Offset, _atkStrOffset);
        }

        _wingsPlayerCommandBuilder.SetInt32((uint)_playerCommandLayout.Columns["Unk4"].Offset, 0);
        _wingsPlayerCommandBuilder.SetInt32((uint)_playerCommandLayout.Columns["TriangleCommandId"].Offset, 0);
    }

    private unsafe void SetRowsAndPtrs()
    {
        _managedNexApi.TryGetTarget(out var nextExcelDBApi);
        INexTable? table = nextExcelDBApi!.GetTable(NexTableIds.playercommandbuilder);

        _wingsPlayerCommandBuilder = table.GetRow(23);

        _commandAttackRow = nextExcelDBApi!.GetTable(NexTableIds.command).GetRow(1);

        INexTable? summonTable = nextExcelDBApi!.GetTable(NexTableIds.summonmode);


        // Get original magic + chanrged Magic bytes to re-write after disabling flight mode
        var _bahamutSummonRow = summonTable.GetRow(8);
        var orgMagic = _bahamutSummonRow.GetString((uint)_summonLayout.Columns["MagicName"].Offset, relative: true, relativeOffset: 0);
        var orgChargedMagic = _bahamutSummonRow.GetString((uint)_summonLayout.Columns["ChargedMagicName"].Offset, relative: true, relativeOffset: -4);
        originalBytes = Encoding.UTF8.GetBytes(orgMagic + "\0").Concat(Encoding.UTF8.GetBytes(orgChargedMagic + "\0")).ToArray();

        // Should always be true, but just making sure
        if (originalBytes.Length > AscendBytes.Length)
        {
            _renameAirActions = true;
        }

        _rawNexApi.TryGetTarget(out var rawDBApi);

        NexTableInstance* rawTable = rawDBApi!.GetTable(NexTableIds.summonmode);

        var bahaRow = rawDBApi.SearchRow(rawTable, 8);
        var _bahaRowDataPtr = rawDBApi.GetRowData(bahaRow);

        int strOffset = *(int*)(_bahaRowDataPtr + _summonLayout.Columns["MagicName"].Offset);
        _bahaMagicStrPtr = (nint)(_bahaRowDataPtr + strOffset + _summonLayout.Columns["MagicName"].Offset);

        // Calculate a new str offset to write into the atk Row to direct it to the "Descned" row
        NexTableInstance* cmdTable = rawDBApi!.GetTable(NexTableIds.command);

        var atkRow = rawDBApi.SearchRow(cmdTable, 1);
        var atkRowDataPtr = rawDBApi.GetRowData(atkRow);
        _atkStrOffset = *(int*)(atkRowDataPtr + _cmdLayout.Columns["Name"].Offset);
        nint atkStrPtr = (nint)(atkRowDataPtr + _atkStrOffset + _cmdLayout.Columns["Name"].Offset);

        var descRow = rawDBApi.SearchRow(cmdTable, 21);
        var descRowDataPtr = rawDBApi.GetRowData(descRow);
        int descStrOffset = *(int*)(descRowDataPtr + _cmdLayout.Columns["Name"].Offset);
        nint descStrPtr = (nint)(descRowDataPtr + descStrOffset + _cmdLayout.Columns["Name"].Offset);

        var diff = (int)(descStrPtr - atkStrPtr);
        _newDescStrOffset = _atkStrOffset + diff;

        // Should always be false, but just making sure
        if (descStrPtr < atkStrPtr)
            _renameAirActions = false;

    }

    private unsafe void CreateInputTasks()
    {
        // Create the tasks that handle the "flight mode" input

        // Always monitor keyboard
        Task.Run(KeyboardInputThread);

        // Monitor either xInput or Dualsense, or both of them if its not clear
        if (_controller.IsConnected)
        {
            Task.Run(ControllerInputThread);
        }
        else if (DualSense.EnumerateControllers().Any())
        {
            Task.Run(DualSenseInputThread);
        }
        else
        {
            Task.Run(ControllerInputThread);
            Task.Run(DualSenseInputThread);
        }
    }

    private void KeyboardInputThread()
    {
        Shortcut[] shortcuts = {
            new(Key.LeftAlt, state: KeyState.Down, name: "AltPressed"),
            new(Key.LeftAlt, state: KeyState.Up, name: "AltUp")
        };

        var interceptor = new KeyInterceptor(shortcuts);
        bool altDown = false;

        interceptor.ShortcutPressed += (_, e) =>
        {
            switch (e.Shortcut.Name)
            {
                case "AltPressed":
                    if (!altDown)
                    {
                        altDown = true;
                        EnableFlightMode();
                    }
                    break;
                case "AltUp":
                    if (altDown)
                    {
                        altDown = false;
                        DisableFlightMode();
                    }
                    break;
            }
        };

        interceptor.RunMessageLoop();
    }

    private void DualSenseInputThread()
    {
        DualSense? dualSense = null;
        bool prevL3 = false;
        bool currentL3 = false;

        while (true)
        {
            if (DualSense.EnumerateControllers().Any())
            {
                _logger.WriteLine($"[{_modConfig.ModId}] DualSense detected.");

                dualSense = DualSense.EnumerateControllers().First();

                dualSense.Acquire();

                try
                {
                    while (true)
                    {
                        currentL3 = dualSense.ReadWriteOnce().L3Button;
                        if (currentL3 && !prevL3)
                        {
                            prevL3 = currentL3;
                            EnableFlightMode();
                        }
                        else if (!currentL3 && prevL3)
                        {
                            prevL3 = currentL3;
                            DisableFlightMode();
                        }

                        Thread.Sleep(20);
                    }
                }
                catch (Exception e)
                {
                    DisableFlightMode();
                    dualSense.Release();
                }
            }
            else
            {
                Thread.Sleep(1000);
            }
        }
    }

    private void ControllerInputThread()
    {
        GamepadButtonFlags l3 = GamepadButtonFlags.LeftThumb;
        GamepadButtonFlags previousButtons = 0;

        while (true)
        {
            if (_controller.IsConnected)
            {
                var state = _controller.GetState();
                GamepadButtonFlags currentButtons = state.Gamepad.Buttons;

                if ((currentButtons & l3) != 0 && (previousButtons & l3) == 0)
                {
                    EnableFlightMode();
                }
                else if ((currentButtons & l3) == 0 && (previousButtons & l3) != 0)
                {
                    DisableFlightMode();
                }

                previousButtons = currentButtons;
                Thread.Sleep(20);
            }
            else
            {
                Thread.Sleep(1000);
            }
        }
    }

    private unsafe void ApplyFlyingParam()
    {
#pragma warning disable CS8600, CS8602
        _logger.WriteLine($"[{_modConfig.ModId}] Applying Wings of Light flying parameters...", _logger.ColorGreen);

        _managedNexApi.TryGetTarget(out var nextExcelDBApi);

        INexTable actionTable = nextExcelDBApi.GetTable(Enum.Parse<NexTableIds>("action"));
        NexTableLayout actionLayout = TableMappingReader.ReadTableLayout("action", new Version(1, 0, 3));

        INexTable systemMoveTable = nextExcelDBApi.GetTable(Enum.Parse<NexTableIds>("systemmove"));
        NexTableLayout systemMoveLayout = TableMappingReader.ReadTableLayout("systemmove", new Version(1, 0, 3));

        // Wait for the nex table to be loaded since the framework can fire the event too early.
        var counter = 0;
        while (actionTable.GetRow((uint)ActionKey.Wings) == null || systemMoveTable.GetRow((uint)SystemMoveKey.Fly) == null)
        {
            Thread.Sleep(100);
            counter++;
            if (counter > 20)
            {
                throw new Exception($"[{_modConfig.ModId}] Failed initialization due to nex loading timeout!");
            }
        }

        INexRow WingsRow = actionTable.GetRow((uint)ActionKey.Wings);
        INexRow WingsAirborneRow = actionTable.GetRow((uint)ActionKey.WingsAirborne);
        INexRow WingsCancelRow = actionTable.GetRow((uint)ActionKey.WingsCancel);
        INexRow WingsDodgeRow = actionTable.GetRow((uint)ActionKey.WingsDodge);
        INexRow WingsPreciseDodgeRow = actionTable.GetRow((uint)ActionKey.WingsPreciseDodge);

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