namespace CsSqlite;

#if NETSTANDARD2_1
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
internal sealed class InterpolatedStringHandlerArgumentAttribute : Attribute
{
    public InterpolatedStringHandlerArgumentAttribute(string argument) => Arguments = [argument];
    public InterpolatedStringHandlerArgumentAttribute(params string[] arguments) => Arguments = arguments;
    public string[] Arguments { get; }
}
#endif
