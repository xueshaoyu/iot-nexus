using IoTPlatform.Data;
using IoTPlatform.Rules;

namespace IoTPlatform.Tests;

public class RuleEvaluatorTests
{
    private static RuleContext Context(String trigger, Dictionary<String, Object?> values, Dictionary<String, Object?>? previous = null, String? eventName = null) => new()
    {
        Device = new Device { Id = 1, Code = "d1", ProductId = 1, GroupId = 3 },
        TriggerType = trigger,
        Values = new Dictionary<String, Object?>(values, StringComparer.OrdinalIgnoreCase),
        Previous = new Dictionary<String, Object?>(previous ?? [], StringComparer.OrdinalIgnoreCase),
        EventName = eventName,
    };

    [Theory]
    [InlineData(35.5, ">", "30", true)]
    [InlineData(25, ">", "30", false)]
    [InlineData(30, ">=", "30", true)]
    [InlineData(29.9, "<", "30", true)]
    [InlineData(30, "==", "30.0", true)]
    [InlineData(30, "!=", "31", true)]
    public void Compare_Numeric(Double value, String op, String threshold, Boolean expected) =>
        Assert.Equal(expected, RuleEvaluator.Compare(value, op, threshold));

    [Fact]
    public void Compare_BoolAndText()
    {
        Assert.True(RuleEvaluator.Compare(true, "==", "true"));
        Assert.True(RuleEvaluator.Compare(true, "==", "1"));
        Assert.True(RuleEvaluator.Compare("running", "==", "RUNNING"));
        Assert.True(RuleEvaluator.Compare("error: overheat", "contains", "overheat"));
        Assert.False(RuleEvaluator.Compare(null, ">", "1"));
    }

    [Fact]
    public void Property_RequiresReferencedPropertyInReport()
    {
        var conditions = new List<RuleCondition> { new() { Property = "temperature", Operator = ">", Value = "30" } };

        var unrelated = Context(RuleTriggers.Property, new() { ["humidity"] = 80 });
        unrelated.Shadow["temperature"] = 40;
        Assert.False(RuleEvaluator.IsMatch(RuleTriggers.Property, null, "All", conditions, unrelated));

        Assert.True(RuleEvaluator.IsMatch(RuleTriggers.Property, null, "All", conditions, Context(RuleTriggers.Property, new() { ["temperature"] = 31 })));
    }

    [Fact]
    public void AllAndAny_UseShadowForOtherProperties()
    {
        var conditions = new List<RuleCondition>
        {
            new() { Property = "waterTemp", Operator = ">", Value = "93" },
            new() { Property = "burner", Operator = "==", Value = "true" },
        };
        var ctx = Context(RuleTriggers.Property, new() { ["waterTemp"] = 95 });
        ctx.Shadow["burner"] = "false";

        Assert.False(RuleEvaluator.IsMatch(RuleTriggers.Property, null, "All", conditions, ctx));
        Assert.True(RuleEvaluator.IsMatch(RuleTriggers.Property, null, "Any", conditions, ctx));

        ctx.Shadow["burner"] = "true";
        Assert.True(RuleEvaluator.IsMatch(RuleTriggers.Property, null, "All", conditions, ctx));
    }

    [Fact]
    public void Changed_ComparesWithPrevious()
    {
        var conditions = new List<RuleCondition> { new() { Property = "switch", Operator = RuleOperators.Changed } };

        Assert.True(RuleEvaluator.IsMatch(RuleTriggers.Property, null, "All", conditions, Context(RuleTriggers.Property, new() { ["switch"] = true }, new() { ["switch"] = "false" })));
        Assert.False(RuleEvaluator.IsMatch(RuleTriggers.Property, null, "All", conditions, Context(RuleTriggers.Property, new() { ["switch"] = true }, new() { ["switch"] = "true" })));
    }

    [Fact]
    public void Event_FiltersByName()
    {
        Assert.True(RuleEvaluator.IsMatch(RuleTriggers.Event, "overheat", "All", [], Context(RuleTriggers.Event, [], eventName: "overheat")));
        Assert.False(RuleEvaluator.IsMatch(RuleTriggers.Event, "overheat", "All", [], Context(RuleTriggers.Event, [], eventName: "lowBattery")));
        Assert.True(RuleEvaluator.IsMatch(RuleTriggers.Event, null, "All", [], Context(RuleTriggers.Event, [], eventName: "lowBattery")));
        Assert.False(RuleEvaluator.IsMatch(RuleTriggers.Property, null, "All", [], Context(RuleTriggers.Event, [], eventName: "x")));
    }

    [Fact]
    public void Scope_ProductGroupDevice()
    {
        var device = new Device { Id = 7, ProductId = 2, GroupId = 5 };

        Assert.True(RuleEvaluator.InScope(new Rule(), device));
        Assert.True(RuleEvaluator.InScope(new Rule { ProductId = 2 }, device));
        Assert.False(RuleEvaluator.InScope(new Rule { ProductId = 3 }, device));
        Assert.False(RuleEvaluator.InScope(new Rule { DeviceId = 8 }, device));
        Assert.True(RuleEvaluator.InScope(new Rule { GroupId = 5 }, device));
        Assert.False(RuleEvaluator.InScope(new Rule { GroupId = 1 }, device));
        Assert.True(RuleEvaluator.InScope(new Rule { GroupId = 1 }, device, (child, ancestor) => child == 5 && ancestor == 1));
    }

    [Fact]
    public void Template_RendersPlaceholders()
    {
        var ctx = Context(RuleTriggers.Property, new() { ["temperature"] = 35.2 });
        ctx.Device.Name = "Lobby";
        var rule = new Rule { Id = 1, Name = "Hot" };

        Assert.Equal("Hot: Lobby d1 35.2 {unknown}", RuleTemplate.Render("{rule.name}: {device.name} {device.code} {temperature} {unknown}", rule, ctx));
        Assert.Equal("35.2", RuleTemplate.Render("{value.temperature}", rule, ctx));
    }
}
