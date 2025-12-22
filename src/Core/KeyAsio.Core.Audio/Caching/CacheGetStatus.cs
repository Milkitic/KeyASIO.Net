namespace KeyAsio.Core.Audio.Caching;

public enum CacheGetStatus : byte
{
    /// <summary>
    /// 成功从缓存中获取。
    /// </summary>
    Hit,

    /// <summary>
    /// 缓存未命中，已成功创建并添加了新缓存。
    /// </summary>
    Created,

    /// <summary>
    /// 获取或创建失败（如文件不存在、解码错误）。
    /// </summary>
    Failed
}