namespace nem.Mimir.PerformanceTests;

using System.Collections;
using System.Reflection;
using NBomber.Contracts;
using NBomber.CSharp;
using Shouldly;

public sealed class PerformanceTestRunner
{
    [Fact]
    public void ChatThroughput_ShouldMeetP95Gate()
    {
        var result = RunScenario(PerformanceScenarios.ChatThroughput());
        AssertThresholds(result);
        AssertP95Latency(result, PerformanceScenarios.ChatThroughputScenarioName, PerformanceScenarios.ChatThroughputStepName, 2000);
    }

    [Fact]
    public void SandboxPool_ShouldMeetP95Gate()
    {
        var result = RunScenario(PerformanceScenarios.SandboxPool());
        AssertThresholds(result);
        AssertP95Latency(result, PerformanceScenarios.SandboxPoolScenarioName, PerformanceScenarios.SandboxPoolStepName, 500);
    }

    [Fact]
    public void McpToolDiscovery_ShouldMeetP95Gate()
    {
        var result = RunScenario(PerformanceScenarios.McpToolDiscovery());
        AssertThresholds(result);
        AssertP95Latency(result, PerformanceScenarios.McpToolDiscoveryScenarioName, PerformanceScenarios.McpToolDiscoveryStepName, 200);
    }

    [Fact]
    public void SemanticCache_ShouldMeetP95Gate()
    {
        var result = RunScenario(PerformanceScenarios.SemanticCache());
        AssertThresholds(result);
        AssertP95Latency(result, PerformanceScenarios.SemanticCacheScenarioName, PerformanceScenarios.SemanticCacheStepName, 50);
    }

    [Fact]
    public void MemoryConsolidation_ShouldMeetP95Gate()
    {
        var result = RunScenario(PerformanceScenarios.MemoryConsolidation());
        AssertThresholds(result);
        AssertP95Latency(result, PerformanceScenarios.MemoryConsolidationScenarioName, PerformanceScenarios.MemoryConsolidationStepName, 1000);
    }

    [Fact]
    public void AgentOrchestration_ShouldCompleteWithoutFailures()
    {
        var result = RunScenario(PerformanceScenarios.AgentOrchestration());
        AssertThresholds(result);
        AssertP95Latency(result, PerformanceScenarios.AgentOrchestrationScenarioName, PerformanceScenarios.AgentOrchestrationStepName, 1200);
    }

    private static object RunScenario(ScenarioProps scenario)
    {
        return NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    private static void AssertThresholds(object result)
    {
        var thresholds = GetPropertyValue<IEnumerable>(result, "Thresholds");

        foreach (var threshold in thresholds)
        {
            var isFailed = GetPropertyValue<bool>(threshold!, "IsFailed");
            isFailed.ShouldBeFalse();
        }
    }

    private static void AssertP95Latency(object result, string scenarioName, string stepName, int thresholdMs)
    {
        var scenarioStatsCollection = GetPropertyValue<object>(result, "ScenarioStats");
        var scenarioStats = InvokeGetByName(scenarioStatsCollection, scenarioName);

        var stepStatsCollection = GetPropertyValue<object>(scenarioStats, "StepStats");
        var stepStats = InvokeGetByName(stepStatsCollection, stepName);

        var okStats = GetPropertyValue<object>(stepStats, "Ok");
        var latency = GetPropertyValue<object>(okStats, "Latency");
        var p95 = Convert.ToDouble(GetPropertyValue<object>(latency, "Percent95"));

        var failStats = GetPropertyValue<object>(stepStats, "Fail");
        var request = GetPropertyValue<object>(failStats, "Request");
        var failCount = Convert.ToInt32(GetPropertyValue<object>(request, "Count"));

        p95.ShouldBeLessThan(thresholdMs);
        failCount.ShouldBe(0);
    }

    private static object InvokeGetByName(object target, string name)
    {
        var targetType = target.GetType();

        var getMethod = targetType.GetMethod("Get", [typeof(string)])
            ?? targetType.GetMethod("GetByKey", [typeof(string)])
            ?? targetType.GetMethod("TryFind", [typeof(string)]);

        if (getMethod is not null)
        {
            var directValue = getMethod.Invoke(target, [name]);
            if (directValue is not null)
            {
                return directValue;
            }
        }

        if (TryGetDictionaryValue(target, name, out var dictionaryValue))
        {
            return dictionaryValue!;
        }

        if (target is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is null)
                {
                    continue;
                }

                if (IsNamedEntry(item, name))
                {
                    return item;
                }
            }
        }

        throw new ShouldAssertException($"Could not resolve entry '{name}' on type '{targetType.FullName}'.");
    }

    private static bool TryGetDictionaryValue(object target, string key, out object? value)
    {
        value = null;

        if (target is IDictionary dictionary)
        {
            if (dictionary.Contains(key))
            {
                value = dictionary[key];
                return value is not null;
            }

            return false;
        }

        var targetType = target.GetType();
        var dictionaryInterface = targetType
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

        if (dictionaryInterface is null)
        {
            return false;
        }

        var keyType = dictionaryInterface.GetGenericArguments()[0];
        if (keyType != typeof(string))
        {
            return false;
        }

        var tryGetValueMethod = dictionaryInterface.GetMethod(
            "TryGetValue",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            [typeof(string), dictionaryInterface.GetGenericArguments()[1].MakeByRefType()],
            modifiers: null);

        if (tryGetValueMethod is null)
        {
            return false;
        }

        var args = new object?[] { key, null };
        var found = (bool)tryGetValueMethod.Invoke(target, args)!;
        if (!found)
        {
            return false;
        }

        value = args[1];
        return value is not null;
    }

    private static bool IsNamedEntry(object item, string expectedName)
    {
        foreach (var propertyName in new[] { "Name", "ScenarioName", "StepName", "Key" })
        {
            var property = item.GetType().GetProperty(propertyName);
            if (property?.GetValue(item) is string actualName
                && string.Equals(actualName, expectedName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static T GetPropertyValue<T>(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName);
        property.ShouldNotBeNull();

        var value = property!.GetValue(target);
        value.ShouldNotBeNull();

        return (T)value!;
    }
}
