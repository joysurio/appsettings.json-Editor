using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AppSettingsEditor.Services;

internal class ValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}

internal class ValidationService
{
    public ValidationResult ValidateProperty(object? value, PropertyInfo property, Type propertyType)
    {
        var result = new ValidationResult { IsValid = true };

        // Check if nullable and value is null
        if (value == null && IsNullable(propertyType))
        {
            return result; // Nullable values are allowed to be null
        }

        // Get description attribute for allowed values info
        var descriptionAttr = property.GetCustomAttribute<DescriptionAttribute>();
        var description = descriptionAttr?.Description ?? "";

        // Validate based on type
        if (propertyType == typeof(bool?) || propertyType == typeof(bool))
        {
            if (value is not bool)
            {
                result.IsValid = false;
                result.ErrorMessage = "Must be True or False";
            }
        }
        else if (propertyType == typeof(int?) || propertyType == typeof(int))
        {
            if (value is not int intVal)
            {
                result.IsValid = false;
                result.ErrorMessage = "Must be an integer";
                return result;
            }

            // Check for range constraints (e.g., "Allowed: 0..3")
            if (description.Contains("Allowed:") && description.Contains(".."))
            {
                var rangeMatch = Regex.Match(description, @"(\d+)\.\.(\d+)");
                if (rangeMatch.Success)
                {
                    var min = int.Parse(rangeMatch.Groups[1].Value);
                    var max = int.Parse(rangeMatch.Groups[2].Value);
                    if (intVal < min || intVal > max)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"Must be between {min} and {max}";
                    }
                }
            }
        }
        else if (propertyType == typeof(string))
        {
            var stringValue = value as string;
            
            // Note: "Allowed:" values in description are treated as suggestions/examples only,
            // not strict validation rules. This allows users to add custom values through the UI.
            // The "Add custom value..." option in the UI indicates that custom values are permitted.

            // Check for range constraints (e.g., "0..3") - only for numeric strings
            if (description.Contains("Allowed:") && description.Contains(".."))
            {
                var rangeMatch = Regex.Match(description, @"(\d+)\.\.(\d+)");
                if (rangeMatch.Success && int.TryParse(stringValue, out var intVal))
                {
                    var min = int.Parse(rangeMatch.Groups[1].Value);
                    var max = int.Parse(rangeMatch.Groups[2].Value);
                    if (intVal < min || intVal > max)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"Must be between {min} and {max}";
                    }
                }
            }
        }

        return result;
    }

    private bool IsNullable(Type type)
    {
        return !type.IsValueType || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
    }
}

