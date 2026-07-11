namespace KeyAsio.Application.Abstractions;

public interface IApplicationDispatcher
{
    Task InvokeAsync(Action action);
}
