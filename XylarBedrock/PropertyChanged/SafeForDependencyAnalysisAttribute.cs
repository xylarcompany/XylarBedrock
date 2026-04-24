using System;

namespace PropertyChanged
{
    [AttributeUsage(AttributeTargets.All, Inherited = false)]
    public sealed class SafeForDependencyAnalysisAttribute : Attribute
    {
    }
}
