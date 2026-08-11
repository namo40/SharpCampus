// netstandard2.1 has no IsExternalInit, which `record` and `init` accessors require.
// The compiler only recognizes the polyfill in this exact namespace, and it must exist per assembly.
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit
{
}
