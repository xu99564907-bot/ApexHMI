using System.Threading;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.Services.RuntimeUi;
using FluentAssertions;
using Xunit;

namespace ApexHMI.Tests.Services.RuntimeUi;

public class WidgetRegistryTests
{
    private readonly WidgetRegistry _registry;
    private readonly IWidgetDataContext _mockContext;

    public WidgetRegistryTests()
    {
        _registry = new WidgetRegistry();
        _mockContext = new StubDataContext();
    }

    [Fact]
    public void CreateUnknownTypeIdReturnsFallbackElement()
    {
        var model = new WidgetInstance { TypeId = "nonexistent-widget", Width = 100, Height = 40 };

        var element = RunOnSta(() => _registry.Create(model, _mockContext));

        element.Should().NotBeNull();
    }

    [Fact]
    public void RegisterCustomFactoryIsInvokedOnCreate()
    {
        var factoryCalled = false;
        _registry.Register("custom", (m, ctx) =>
        {
            factoryCalled = true;
            return new System.Windows.Controls.Border();
        });

        var model = new WidgetInstance { TypeId = "custom" };
        RunOnSta(() => _registry.Create(model, _mockContext));

        factoryCalled.Should().BeTrue();
    }

    [Fact]
    public void RegisterOverridesExistingBuiltInType()
    {
        var customUsed = false;
        _registry.Register("text", (m, ctx) =>
        {
            customUsed = true;
            return new System.Windows.Controls.Border();
        });

        var model = new WidgetInstance { TypeId = "text" };
        RunOnSta(() => _registry.Create(model, _mockContext));

        customUsed.Should().BeTrue();
    }

    /// <summary>
    /// 在 STA 线程上执行操作（创建 WPF UI 元素必须）。
    /// </summary>
    private static T RunOnSta<T>(Func<T> func)
    {
        T result = default!;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = func();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            throw error;
        }

        return result;
    }

    private sealed class StubDataContext : IWidgetDataContext
    {
        public void RegisterValueCallback(string tagId, System.Action<string> callback) { }
        public void ExecuteAction(string actionType, string actionParam) { }
    }
}
