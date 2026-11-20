using System.Text.Json;
using System.Text.Json.Nodes;

namespace RimChronicles.Llm.Validation;

/// <summary>
/// Validates JSON against the subset of JSON Schema our prompts use: type, properties, required,
/// additionalProperties (false), enum, items, minLength/maxLength, minimum/maximum, minItems/maxItems.
/// Kept dependency-free because this library ships inside the mod.
/// </summary>
public static class JsonSchemaValidator
{
    public static IReadOnlyList<string> Validate(JsonNode? value, JsonObject schema)
    {
        var errors = new List<string>();
        Validate(value, schema, "$", errors);
        return errors;
    }

    private static void Validate(JsonNode? value, JsonObject schema, string path, List<string> errors)
    {
        if (schema["type"] is { } typeNode && !MatchesType(value, typeNode))
        {
            errors.Add($"{path} should be {typeNode.ToJsonString()} but is {KindOf(value)}");
            return;
        }

        if (schema["enum"] is JsonArray options && !options.Any(o => JsonNode.DeepEquals(o, value)))
        {
            errors.Add($"{path} must be one of {options.ToJsonString()}");
        }

        switch (value)
        {
            case JsonObject obj:
                ValidateObject(obj, schema, path, errors);
                break;
            case JsonArray arr:
                ValidateArray(arr, schema, path, errors);
                break;
            case JsonValue v when v.GetValueKind() == JsonValueKind.String:
                var length = v.GetValue<string>().Length;
                if (schema["minLength"] is { } minLen && length < minLen.GetValue<int>())
                {
                    errors.Add($"{path} is shorter than {minLen} characters");
                }

                if (schema["maxLength"] is { } maxLen && length > maxLen.GetValue<int>())
                {
                    errors.Add($"{path} is longer than {maxLen} characters");
                }

                break;
            case JsonValue v when v.GetValueKind() == JsonValueKind.Number:
                var number = v.GetValue<double>();
                if (schema["minimum"] is { } min && number < min.GetValue<double>())
                {
                    errors.Add($"{path} is below the minimum {min}");
                }

                if (schema["maximum"] is { } max && number > max.GetValue<double>())
                {
                    errors.Add($"{path} is above the maximum {max}");
                }

                break;
        }
    }

    private static void ValidateObject(JsonObject obj, JsonObject schema, string path, List<string> errors)
    {
        var properties = schema["properties"] as JsonObject;

        if (schema["required"] is JsonArray required)
        {
            foreach (var name in required.Select(r => r!.GetValue<string>()))
            {
                if (!obj.ContainsKey(name))
                {
                    errors.Add($"{path}.{name} is required");
                }
            }
        }

        foreach (var (name, child) in obj)
        {
            if (properties?[name] is JsonObject childSchema)
            {
                Validate(child, childSchema, $"{path}.{name}", errors);
            }
            else if (schema["additionalProperties"] is JsonValue extra && extra.GetValueKind() == JsonValueKind.False)
            {
                errors.Add($"{path}.{name} is not an allowed property");
            }
        }
    }

    private static void ValidateArray(JsonArray arr, JsonObject schema, string path, List<string> errors)
    {
        if (schema["minItems"] is { } minItems && arr.Count < minItems.GetValue<int>())
        {
            errors.Add($"{path} has fewer than {minItems} items");
        }

        if (schema["maxItems"] is { } maxItems && arr.Count > maxItems.GetValue<int>())
        {
            errors.Add($"{path} has more than {maxItems} items");
        }

        if (schema["items"] is JsonObject itemSchema)
        {
            for (var i = 0; i < arr.Count; i++)
            {
                Validate(arr[i], itemSchema, $"{path}[{i}]", errors);
            }
        }
    }

    private static bool MatchesType(JsonNode? value, JsonNode typeNode) => typeNode switch
    {
        JsonArray types => types.Any(t => MatchesType(value, t!)),
        _ => typeNode.GetValue<string>() switch
        {
            "object" => value is JsonObject,
            "array" => value is JsonArray,
            "string" => Kind(value) == JsonValueKind.String,
            "boolean" => Kind(value) is JsonValueKind.True or JsonValueKind.False,
            "null" => value is null || Kind(value) == JsonValueKind.Null,
            "number" => Kind(value) == JsonValueKind.Number,
            "integer" => Kind(value) == JsonValueKind.Number && IsWhole(value!.GetValue<double>()),
            _ => true,
        },
    };

    private static bool IsWhole(double d) => Math.Abs(d % 1) < double.Epsilon;

    private static JsonValueKind Kind(JsonNode? value) => value?.GetValueKind() ?? JsonValueKind.Null;

    private static string KindOf(JsonNode? value) => Kind(value).ToString().ToLowerInvariant();
}
