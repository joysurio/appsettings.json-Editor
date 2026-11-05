using System.ComponentModel;
using Newtonsoft.Json;

namespace AppSettingsEditor.Models;

internal class AppSettingsModel
{
    [Description("Machine number in the DB (machine id)")]
    public int? DispensingMachineId { get; set; }

    [Description("Maximum inactivity time (seconds) in the application. Default: 90")]
    [DefaultValue(90)]
    public int? MaxSecondsInactivity { get; set; } = 90;

    [Description("Default application language. Allowed values: castellano, ingles. Default: castellano")]
    [DefaultValue("castellano")]
    public string? DefaultLanguage { get; set; } = "castellano";

    [Description("Whether the machine has a card reader that emulates a keyboard. Default: False")]
    [DefaultValue(false)]
    public bool? ReaderHID { get; set; } = false;

    [Description("Whether the machine has a fingerprint reader. Default: False")]
    [DefaultValue(false)]
    public bool? FingerprintReader { get; set; } = false;

    [Description("Customer-specific identification/authentication mode (set by customer rules). Default: 0")]
    [DefaultValue(0)]
    public int? AuthenticationType { get; set; } = 0;

    [Description("Directory where logos and images are stored. Default: the directory where the executable is located")]
    public string? DirImageLogos { get; set; }

    [Description("Log is stored from the selected level and above. Allowed: Debug (0), Info(1), Warning(2), Error(3). Default: 1")]
    [DefaultValue(1)]
    public int? LogLevel { get; set; } = 1;

    [Description("Directory where user fingerprints are stored (binary files). Default: C:\\Huellas")]
    [DefaultValue("C:\\Huellas")]
    public string? UserFingerprintDir { get; set; } = "C:\\Huellas";

    [Description("Path where garment model images are stored. Default: C:\\La Fabrica de Software\\Modelos\\")]
    [DefaultValue("C:\\La Fabrica de Software\\Modelos\\")]
    public string? PathGarmentModelImage { get; set; } = "C:\\La Fabrica de Software\\Modelos\\";

    [Description("Hide the settings button on the main screen. Default: True")]
    [DefaultValue(true)]
    public bool? HideButtonsMachine { get; set; } = true;

    [Description("Application test mode (runs without PLC/driver). Default: False")]
    [DefaultValue(false)]
    public bool? AppTestMode { get; set; } = false;

    [Description("Return the UI language to the default language automatically. Default: true")]
    [DefaultValue(true)]
    public bool? EnableReturnToDefaultLanguage { get; set; } = true;

    [Description("During sync, reconfigure remote and local DB. Default: true")]
    [DefaultValue(true)]
    [JsonProperty("DeprovisionDataBase")]
    public bool? DeprovisionDatabase { get; set; } = true;

    [Description("Allows changing size. Default: true")]
    [DefaultValue(true)]
    public bool? ChangeSize { get; set; } = true;

    [Description("Customer-defined \"full uniform\" configuration value (requires pair-buttons option enabled). Default: \" \"")]
    [DefaultValue(" ")]
    public string? SetCustom { get; set; } = " ";

    [Description("Show stock/loading information on the main screen. Default: False")]
    [DefaultValue(false)]
    public bool? ShowDispensingLoad { get; set; } = false;

    [Description("Maximum garments dispensed per head (triggers mail at limit). Default: 5000")]
    [DefaultValue(5000)]
    public int? MaxDispensedGarmentsHead { get; set; } = 5000;

    [Description("Driver type (e.g., single vs double door behavior). Default: 0")]
    [DefaultValue(0)]
    public int? DriverType { get; set; } = 0;

    [Description("Door number where the software runs. Default: 1")]
    [DefaultValue(1)]
    public int? DispensingDoor { get; set; } = 1;

    [Description("Show \"full uniform\" buttons. Default: False")]
    [DefaultValue(false)]
    public bool? ShowPairButtonsClothes { get; set; } = false;

    [Description("Refresh store/warehouse UI schema during load. Default: false")]
    [DefaultValue(false)]
    public bool? ClearStoreSchema { get; set; } = false;

    [Description("Enable Nayax cashless device integration. Default: False")]
    [DefaultValue(false)]
    public bool? NayaxDispenser { get; set; } = false;

    [Description("Enable loading with RFID from the UI. Default: False")]
    [DefaultValue(false)]
    public bool? LoadWithRFID { get; set; } = false;

    [Description("Do not auto-select locker models after user identification. Default: False")]
    [DefaultValue(false)]
    public bool? NoHaveSelectedLockerModels { get; set; } = false;

    [Description("Show \"Open Door\" button in settings. Default: false")]
    [DefaultValue(false)]
    public bool? ViewDoorOpenButton { get; set; } = false;

    [Description("Smart-locker difference mode (subtract before/after RFID read). Default: False")]
    [DefaultValue(false)]
    public bool? DifferenceMode { get; set; } = false;

    [Description("Hide warnings about wrongly placed garments during loading. Default: False")]
    [DefaultValue(false)]
    public bool? NoShowNoticeGarmentWronglyPlacedLocker { get; set; } = false;

    [Description("Lockers controlled by PLC instead of controller boards. Default: False")]
    [DefaultValue(false)]
    public bool? LockersWithPLC { get; set; } = false;

    [Description("Invert \"cheese\" photo-sensor logic. Default: False")]
    [DefaultValue(false)]
    public bool? ReverseCheeseSensor { get; set; } = false;

    [Description("Ultrasonic locker mode. Default: False")]
    [DefaultValue(false)]
    public bool? UltrasonicLocker { get; set; } = false;

    [Description("Invert \"cheese\" lock logic. Default: False")]
    [DefaultValue(false)]
    public bool? ReverseCheeseLocks { get; set; } = false;

    [Description("X displacement for the sensor position. Default: 683115")]
    [DefaultValue(683115)]
    public int? CheeseSensorPositionDisplacement { get; set; } = 683115;

    [Description("Angular speed for cheese. Default: 15000")]
    [DefaultValue(15000)]
    public int? AngularSpeedCheese { get; set; } = 15000;

    [Description("Automatic drawer operation. Default: False")]
    [DefaultValue(false)]
    public bool? AutomaticDrawer { get; set; } = false;
}

