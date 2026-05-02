using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StageKit;

/// <summary>
/// Provides property change notification helpers without requiring CommunityToolkit.Mvvm.
/// </summary>
/// <remarks>
/// The protected method shape intentionally matches common CommunityToolkit.Mvvm generated-code expectations.
/// </remarks>
public class ObservableObjectKit : INotifyPropertyChanged, INotifyPropertyChanging
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public event PropertyChangingEventHandler? PropertyChanging;

    /// <summary>
    /// Raises <see cref="PropertyChanged"/>.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, e);
    }

    /// <summary>
    /// Raises <see cref="PropertyChanged"/> for the specified property.
    /// </summary>
    /// <param name="propertyName">The changed property name.</param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Raises <see cref="PropertyChanging"/>.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnPropertyChanging(PropertyChangingEventArgs e)
    {
        PropertyChanging?.Invoke(this, e);
    }

    /// <summary>
    /// Raises <see cref="PropertyChanging"/> for the specified property.
    /// </summary>
    /// <param name="propertyName">The changing property name.</param>
    protected void OnPropertyChanging([CallerMemberName] string? propertyName = null)
    {
        OnPropertyChanging(new PropertyChangingEventArgs(propertyName));
    }

    /// <summary>
    /// Sets a backing field and raises change notifications when the value changes.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="field">The backing field to update.</param>
    /// <param name="value">The new value.</param>
    /// <param name="propertyName">The property name associated with the field.</param>
    /// <returns><see langword="true"/> when the value changed; otherwise, <see langword="false"/>.</returns>
    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        return SetProperty(ref field, value, EqualityComparer<T>.Default, propertyName);
    }

    /// <summary>
    /// Sets a backing field using a comparer and raises change notifications when the value changes.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="field">The backing field to update.</param>
    /// <param name="value">The new value.</param>
    /// <param name="comparer">The equality comparer used to detect changes.</param>
    /// <param name="propertyName">The property name associated with the field.</param>
    /// <returns><see langword="true"/> when the value changed; otherwise, <see langword="false"/>.</returns>
    protected bool SetProperty<T>(
        ref T field,
        T value,
        IEqualityComparer<T> comparer,
        [CallerMemberName] string? propertyName = null)
    {
        ArgumentNullException.ThrowIfNull(comparer);

        if (comparer.Equals(field, value))
        {
            return false;
        }

        OnPropertyChanging(propertyName);
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Invokes a callback and raises change notifications when the value changes.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="oldValue">The previous value.</param>
    /// <param name="newValue">The new value.</param>
    /// <param name="callback">The callback used to update the value.</param>
    /// <param name="propertyName">The property name associated with the change.</param>
    /// <returns><see langword="true"/> when the value changed; otherwise, <see langword="false"/>.</returns>
    protected bool SetProperty<T>(
        T oldValue,
        T newValue,
        Action<T> callback,
        [CallerMemberName] string? propertyName = null)
    {
        return SetProperty(oldValue, newValue, EqualityComparer<T>.Default, callback, propertyName);
    }

    /// <summary>
    /// Invokes a callback using a comparer and raises change notifications when the value changes.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="oldValue">The previous value.</param>
    /// <param name="newValue">The new value.</param>
    /// <param name="comparer">The equality comparer used to detect changes.</param>
    /// <param name="callback">The callback used to update the value.</param>
    /// <param name="propertyName">The property name associated with the change.</param>
    /// <returns><see langword="true"/> when the value changed; otherwise, <see langword="false"/>.</returns>
    protected bool SetProperty<T>(
        T oldValue,
        T newValue,
        IEqualityComparer<T> comparer,
        Action<T> callback,
        [CallerMemberName] string? propertyName = null)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        ArgumentNullException.ThrowIfNull(callback);

        if (comparer.Equals(oldValue, newValue))
        {
            return false;
        }

        OnPropertyChanging(propertyName);
        callback(newValue);
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Invokes a model callback and raises change notifications when the value changes.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="oldValue">The previous value.</param>
    /// <param name="newValue">The new value.</param>
    /// <param name="model">The model passed to the callback.</param>
    /// <param name="callback">The callback used to update the model.</param>
    /// <param name="propertyName">The property name associated with the change.</param>
    /// <returns><see langword="true"/> when the value changed; otherwise, <see langword="false"/>.</returns>
    protected bool SetProperty<TModel, T>(
        T oldValue,
        T newValue,
        TModel model,
        Action<TModel, T> callback,
        [CallerMemberName] string? propertyName = null)
        where TModel : class
    {
        return SetProperty(oldValue, newValue, model, EqualityComparer<T>.Default, callback, propertyName);
    }

    /// <summary>
    /// Invokes a model callback using a comparer and raises change notifications when the value changes.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="oldValue">The previous value.</param>
    /// <param name="newValue">The new value.</param>
    /// <param name="model">The model passed to the callback.</param>
    /// <param name="comparer">The equality comparer used to detect changes.</param>
    /// <param name="callback">The callback used to update the model.</param>
    /// <param name="propertyName">The property name associated with the change.</param>
    /// <returns><see langword="true"/> when the value changed; otherwise, <see langword="false"/>.</returns>
    protected bool SetProperty<TModel, T>(
        T oldValue,
        T newValue,
        TModel model,
        IEqualityComparer<T> comparer,
        Action<TModel, T> callback,
        [CallerMemberName] string? propertyName = null)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(comparer);
        ArgumentNullException.ThrowIfNull(callback);

        if (comparer.Equals(oldValue, newValue))
        {
            return false;
        }

        OnPropertyChanging(propertyName);
        callback(model, newValue);
        OnPropertyChanged(propertyName);
        return true;
    }
}
