using System;

namespace AppSettingsEditor.Models;

public class ChecklistItem
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CheckType CheckType { get; set; }
    public string? CheckImplementation { get; set; }
    public bool FixAvailable { get; set; }
    public string? FixImplementation { get; set; }
    public string? ManualInstructions { get; set; }
    public bool RequiresAdmin { get; set; }
    public bool RequiresManualConfirmation { get; set; }
}

public enum CheckType
{
    Auto,
    Manual,
    Partial
}

public enum ChecklistStatus
{
    NotChecked,
    Pass,
    Fail,
    Unknown,
    NotApplicable
}

public class ChecklistResult
{
    public string ItemId { get; set; } = string.Empty;
    public ChecklistStatus Status { get; set; } = ChecklistStatus.NotChecked;
    public string Evidence { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.Now;
    public bool CanFix { get; set; }
}

public class ChecklistSession
{
    public string Customer { get; set; } = string.Empty;
    public string System { get; set; } = string.Empty;
    public DateTime InstallationDate { get; set; } = DateTime.Now;
    public string? MacAddress { get; set; }
    public string? AnyDeskId { get; set; }
    public string? RustDeskId { get; set; }
    public string? PerformedBy { get; set; }
    public string? ReviewedBy { get; set; }
    public Dictionary<string, ChecklistResult> Results { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
}

