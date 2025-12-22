namespace KeyAsio.Shared.Events;

/// <summary>
/// Represents a method that handles a signal event (no data).
/// </summary>
public delegate void SignalEventHandler();

/// <summary>
/// Represents an asynchronous method that handles a signal event (no data).
/// </summary>
/// <returns>A task that represents the asynchronous operation.</returns>
public delegate Task SignalAsyncEventHandler();

/// <summary>
/// Represents a method that handles a value change event.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
/// <param name="oldValue">The old value.</param>
/// <param name="newValue">The new value.</param>
public delegate void ValueChangedEventHandler<in T>(T oldValue, T newValue);

/// <summary>
/// Represents an asynchronous method that handles a value change event.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
/// <param name="oldValue">The old value.</param>
/// <param name="newValue">The new value.</param>
/// <returns>A task that represents the asynchronous operation.</returns>
public delegate Task ValueChangedAsyncEventHandler<in T>(T oldValue, T newValue);