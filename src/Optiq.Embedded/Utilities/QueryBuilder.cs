using Optiq.Embedded.Models;
using System.Text.Json;

namespace Optiq.Embedded.Utilities
{
    public static class QueryOperators
    {
        public const string EqualsTo = "$eq";
        public const string GreaterThan = "$gt";
        public const string GreaterThanOrEqual = "$gte";
        public const string LessThan = "$lt";
        public const string LessThanOrEqual = "$lte";
    }

    public record FilterDefinition
    {
        public string Field { get; init; } = string.Empty;
        public string Operator { get; init; } = string.Empty;
    }

    public record EqualFilter : FilterDefinition
    {
        public object Value { get; init; } = string.Empty;
        public Type ValueType { get; init; } = typeof(string);
    }

    public record RangeFilter : FilterDefinition
    {
        public bool IsRangeQuery { get; set; }
        public object MinBound { get; set; } = 0.0;
        public object MaxBound { get; set; } = 999999999.99;
    }

    public static class QueryBuilder
    {
        public static List<FilterDefinition> BuildQuery(OptiqQuery query)
        {
            var filters = new List<FilterDefinition>();

            foreach (var field in query.Keys)
            {
                JsonElement value = query[field];

                if (value.ValueKind == JsonValueKind.String)
                {
                    filters.Add(new EqualFilter
                    {
                        Field = field,
                        Operator = QueryOperators.EqualsTo,
                        Value = value.GetString() ?? string.Empty,
                        ValueType = typeof(string)
                    });
                }

                if (value.ValueKind == JsonValueKind.Number)
                {
                    object formattedValue = GetFormattedValue(value);

                    filters.Add(new EqualFilter
                    {
                        Field = field,
                        Operator = QueryOperators.EqualsTo,
                        Value = formattedValue,
                        ValueType = formattedValue.GetType()
                    });
                }

                if (value.ValueKind == JsonValueKind.Array)
                {
                    string[] stringArray = value.Deserialize<string[]>() ?? [];

                    foreach (var item in stringArray)
                    {
                        filters.Add(new EqualFilter
                        {
                            Field = field,
                            Operator = QueryOperators.EqualsTo,
                            Value = item,
                            ValueType = typeof(string)
                        });
                    }
                }

                if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                {
                    filters.Add(new EqualFilter
                    {
                        Field = field,
                        Operator = QueryOperators.EqualsTo,
                        Value = value.GetBoolean(),
                        ValueType = typeof(bool)
                    });
                }

                if (value.ValueKind == JsonValueKind.Object)
                {
                    var obj = value.Deserialize<Dictionary<string, JsonElement>>() ?? new Dictionary<string, JsonElement>();

                    var rangeCondition = new RangeFilter { Field = field, IsRangeQuery = true };

                    foreach (var op in obj.Keys)
                    {
                        if (op == QueryOperators.EqualsTo)
                        {
                            filters.Add(new EqualFilter
                            {
                                Field = field,
                                Operator = op,
                                Value = obj[op].GetString() ?? string.Empty,
                                ValueType = typeof(string)
                            });
                        }
                        else if (op == QueryOperators.LessThan ||
                            op == QueryOperators.GreaterThan ||
                            op == QueryOperators.LessThanOrEqual ||
                            op == QueryOperators.GreaterThanOrEqual)
                        {
                            object formattedValue = GetFormattedValue(obj[op]);
                            rangeCondition.IsRangeQuery = true;

                            switch (op)
                            {
                                case QueryOperators.GreaterThanOrEqual:
                                case QueryOperators.GreaterThan:
                                    rangeCondition.MinBound = formattedValue;
                                    break;

                                case QueryOperators.LessThanOrEqual:
                                case QueryOperators.LessThan:
                                    rangeCondition.MaxBound = formattedValue;
                                    break;
                            }
                        }
                    }

                    if (rangeCondition.IsRangeQuery)
                    {
                        filters.Add(rangeCondition);
                    }
                }
            }

            return filters;
        }

        private static object GetFormattedValue(JsonElement value)
        {
            if (value.TryGetInt64(out long integerVal))
            {
                return integerVal;
            }
            else if (value.TryGetDouble(out double doubleVal))
            {
                return doubleVal;
            }
            else
            {
                return value.GetString() ?? string.Empty;
            }
        }
    }
}
