using System.ComponentModel;

namespace AppSettingsEditor.Models;

internal class DispenseConfigurationModel
{
    [Description("Distance for YUp (multi-height)")]
    public int? DistanceBaldaYUP { get; set; }

    [Description("Distance for YDown (multi-height)")]
    public int? DistanceBaldaYDOWN { get; set; }

    [Description("Distance for YDown2 (multi-height)")]
    public int? DistanceBaldaYDOWN2 { get; set; }

    [Description("Distance for YDown3 (multi-height)")]
    public int? DistanceBaldaYDOWN3 { get; set; }

    [Description("Distance for YDown4 (multi-height)")]
    public int? DistanceBaldaYDOWN4 { get; set; }

    [Description("Maximum allowed lowering from shelf start to shelf end")]
    public int? MajorLoweredAllowed { get; set; }

    [Description("Maximum allowed lowering for YDown")]
    public int? MajorLoweredAllowedYDown { get; set; }

    [Description("Maximum allowed lowering for YDown2")]
    public int? MajorLoweredAllowedYDown2 { get; set; }

    [Description("Maximum allowed lowering for YDown3")]
    public int? MajorLoweredAllowedYDown3 { get; set; }

    [Description("Maximum allowed lowering for YDown4")]
    public int? MajorLoweredAllowedYDown4 { get; set; }

    [Description("X dispensing position")]
    public int? PosDisX { get; set; }

    [Description("X rest position (avoid drawer collisions)")]
    public int? PosXRest { get; set; }

    [Description("Y rest position")]
    public int? PosYRest { get; set; }

    [Description("Y \"upload\" position used for stretching behavior (defaults to YUp). Default: YUp")]
    public int? PostYUpload { get; set; }

    [Description("X homing speed")]
    public int? SpeedXHoming { get; set; }

    [Description("Y homing speed")]
    public int? SpeedYHoming { get; set; }

    [Description("X work speed")]
    public int? SpeedXWork { get; set; }

    [Description("Y work speed")]
    public int? SpeedYWork { get; set; }

    [Description("X acceleration")]
    public int? AcelX { get; set; }

    [Description("Y acceleration")]
    public int? AcelY { get; set; }

    [Description("X deceleration")]
    public int? DecelerationX { get; set; }

    [Description("Y deceleration")]
    public int? DecelerationY { get; set; }

    [Description("Use \"garment in drawer\" sensor. Default: true")]
    [DefaultValue(true)]
    public bool? SensorGarmentInDrawer { get; set; } = true;

    [Description("Use \"garment in clamp\" sensor. Default: True")]
    [DefaultValue(true)]
    public bool? SensorGarmentInClamp { get; set; } = true;

    [Description("Use \"drawer door closed\" sensor. Default: true")]
    [DefaultValue(true)]
    public bool? SensorDoorClosed { get; set; } = true;

    [Description("Stretching sequence (go up to YUp and return to dispense position). Default: False")]
    [DefaultValue(false)]
    public bool? RevoltDropGarment { get; set; } = false;

    [Description("Wait time at YUp before delivery to reduce swinging. Default: 0")]
    [DefaultValue(0)]
    public int? SecondsWaitDeliveryGarment { get; set; } = 0;

    [Description("Max upward distance while pinching a garment. Default: 500")]
    [DefaultValue(500)]
    public int? PulsesRiseClip { get; set; } = 500;

    [Description("Pulses to move gripper when it does not pick up garment. Required: -350")]
    public int? PulsesMoveGripperIfItDoesNotPickUpGarment { get; set; }

    [Description("Motor force for the \"push\" operation")]
    public int? CurrPush { get; set; }

    [Description("Downward speed for the \"push\" operation")]
    public int? SpeedPush { get; set; }

    [Description("Number of attempts to grab a garment. Default: 4")]
    [DefaultValue(4)]
    public int? NumberRetriesDispenser { get; set; } = 4;

    [Description("Pinch revolution speed on attempt #1 (clamp v2). Default: 350")]
    [DefaultValue(350)]
    public int? SpeedRevolutionPinch1 { get; set; } = 350;

    [Description("Pinch revolution speed on attempt #2 (clamp v2). Default: 450")]
    [DefaultValue(450)]
    public int? SpeedRevolutionPinch2 { get; set; } = 450;

    [Description("Pinch revolution speed on attempt #3 (clamp v2). Default: 550")]
    [DefaultValue(550)]
    public int? SpeedRevolutionPinch3 { get; set; } = 550;

    [Description("Pinch revolution speed on attempt #4 (clamp v2). Default: 650")]
    [DefaultValue(650)]
    public int? SpeedRevolutionPinch4 { get; set; } = 650;

    [Description("Set store to 0 after N consecutive failures (resets after success). Default: 5")]
    [DefaultValue(5)]
    public int? NonDispensedGarmentCount { get; set; } = 5;

    [Description("Show \"Do you want another garment?\" message after dispensing. Default: True")]
    [DefaultValue(true)]
    public bool? ShowMessageOtherGarmentDispenser { get; set; } = true;

    [Description("Show message indicating another machine has available stock. Default: False")]
    [DefaultValue(false)]
    public bool? ShowAvailableGarmentsAnotherMachine { get; set; } = false;

    [Description("Force homing preset always to the left. Default: False")]
    [DefaultValue(false)]
    public bool? PresetAlwaysLeft { get; set; } = false;

    [Description("Force homing preset always at YUp. Default: False")]
    [DefaultValue(false)]
    public bool? PresetAlwaysOnYUp { get; set; } = false;

    [Description("Asymmetrical stores layout (different homing preset). Default: False")]
    [DefaultValue(false)]
    public bool? AsymmetricalMachine { get; set; } = false;

    [Description("Oriental motor mode. Default: true")]
    [DefaultValue(true)]
    public bool? OrientalMotor { get; set; } = true;

    [Description("LED indicator for drawer open state")]
    public string? LedOpenDrawer { get; set; }

    [Description("LED indicator for drawer close state")]
    public string? LedCloseDrawer { get; set; }
}

