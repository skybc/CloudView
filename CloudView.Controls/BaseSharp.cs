namespace CloudView.Controls;

/// <summary>
/// 几何对象基类，所有可渲染的几何类型均继承此类。
/// </summary>
public abstract class BaseSharp
{
    /// <summary>
    /// 对象标识。
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 对象名称。
    /// </summary>
    public string Name { get; init; } = string.Empty;
}
