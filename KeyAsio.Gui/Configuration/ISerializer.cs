namespace KeyAsio.Gui.Configuration;

public interface ISerializer<T>
{
    T DeserializeSettings(string content);
    string SerializeSettings(T obj);
}