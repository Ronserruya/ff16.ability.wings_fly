using ff16.ability.wings_fly.Template.Configuration;

using Reloaded.Mod.Interfaces.Structs;

using System.ComponentModel;

namespace ff16.ability.wings_fly.Configuration;

public class Config : Configurable<Config>
{
    [DisplayName("Initial Flight Height")]
    [Description("How high to 'jump' before starting to fly")]
    [DefaultValue(1.7f)]
    public float IntialFlightHeight { get; set; } = 1.7f;

    [DisplayName("Flying Forwrd Speed (not recommended)")]
    [Description("Constant forward movment while flying")]
    [DefaultValue(0f)]
    public float FlightForwardSpeed { get; set; } = 0f;

    [DisplayName("Flying Dodge Distance")]
    [Description("How far do dodges move while flying")]
    [DefaultValue(7f)]
    public float DodgeDistance { get; set; } = 7f;

    [DisplayName("Flying Dodge duration (speed)")]
    [Description("How long does the dodge take to complete, lower means faster movment")]
    [DefaultValue(1f)]
    public float DodgeDuration { get; set; } = 1f;

    [DisplayName("Megaflare Dodge Extra Height")]
    [Description("Extra height gained after a succesfull Megafalre precise dodge")]
    [DefaultValue(0.1f)]
    public float PreciseDodgeExtraHeight { get; set; } = 0.1f;

    [DisplayName("Replace Flaying Cancel Animation (recommended)")]
    [Description("Replace the animation that plays when canceling the wings with a falling animation")]
    [DefaultValue(true)]
    public bool ReplaceCancelAnimation { get; set; } = true;
}

/// <summary>
/// Allows you to override certain aspects of the configuration creation process (e.g. create multiple configurations).
/// Override elements in <see cref="ConfiguratorMixinBase"/> for finer control.
/// </summary>
public class ConfiguratorMixin : ConfiguratorMixinBase
{
    // 
}
