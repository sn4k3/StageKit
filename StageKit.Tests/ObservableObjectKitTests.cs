namespace StageKit.Tests;

public sealed class ObservableObjectKitTests
{
    [Fact]
    public void SetProperty_WhenValueChanges_RaisesChangingAndChangedInOrder()
    {
        var model = new TestObservableObject();
        var events = new List<string>();

        model.PropertyChanging += (_, e) => events.Add($"changing:{e.PropertyName}:{model.Value}");
        model.PropertyChanged += (_, e) => events.Add($"changed:{e.PropertyName}:{model.Value}");

        var changed = model.SetValue(42);

        Assert.True(changed);
        Assert.Equal(42, model.Value);
        Assert.Equal(
            [
                "changing:Value:0",
                "changed:Value:42"
            ],
            events);
    }

    [Fact]
    public void SetProperty_WhenValueIsEqual_DoesNotRaiseNotifications()
    {
        var model = new TestObservableObject();
        var count = 0;
        model.SetValue(42);
        model.PropertyChanged += (_, _) => count++;

        var changed = model.SetValue(42);

        Assert.False(changed);
        Assert.Equal(0, count);
    }

    private sealed class TestObservableObject : ObservableObjectKit
    {
        private int _value;

        public int Value => _value;

        public bool SetValue(int value)
        {
            return SetProperty(ref _value, value, nameof(Value));
        }
    }
}
