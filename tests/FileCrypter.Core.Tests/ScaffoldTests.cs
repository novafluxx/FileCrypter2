using System.Reflection;

namespace FileCrypter.Core.Tests;

public sealed class ScaffoldTests
{
    [Fact]
    public void CoreAssemblyCanBeLoaded()
    {
        Assembly assembly = Assembly.Load("FileCrypter.Core");

        Assert.Equal("FileCrypter.Core", assembly.GetName().Name);
    }
}
