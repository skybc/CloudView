namespace CloudView.Controls;

internal interface ISharpRenderBuilder
{
    Type TargetType { get; }
    SharpGeometry Build(BaseSharp shape);
}
