using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class WorkspaceLifecycleSourceContractTests
{
    [Fact]
    public void ScanDebug_UsesNamedManagedWorkspaceSubscriptionLifecycle()
    {
        var source = ReadHostSource("PRISM Utility", "ViewModels", "ScanDebugViewModel.cs");

        Assert.DoesNotContain("SnapshotChanged += _ =>", source, StringComparison.Ordinal);
        Assert.Contains("SubscribeFilmProfileWorkspace();", ExtractMethod(source, "public void AttachRuntimeBindings"), StringComparison.Ordinal);
        Assert.Contains("UnsubscribeFilmProfileWorkspace();", ExtractMethod(source, "public async Task DeactivateAsync"), StringComparison.Ordinal);

        var subscribe = ExtractMethod(source, "private void SubscribeFilmProfileWorkspace");
        Assert.Contains("if (_isFilmProfileWorkspaceSubscribed)", subscribe, StringComparison.Ordinal);
        Assert.Contains("SnapshotChanged += OnFilmProfileWorkspaceSnapshotChanged", subscribe, StringComparison.Ordinal);

        var unsubscribe = ExtractMethod(source, "private void UnsubscribeFilmProfileWorkspace");
        Assert.Contains("if (!_isFilmProfileWorkspaceSubscribed)", unsubscribe, StringComparison.Ordinal);
        Assert.Contains("SnapshotChanged -= OnFilmProfileWorkspaceSnapshotChanged", unsubscribe, StringComparison.Ordinal);
    }

    private static string ExtractMethod(string source, string methodDeclaration)
    {
        var methodStart = source.IndexOf($"{methodDeclaration}(", StringComparison.Ordinal);
        Assert.True(methodStart >= 0, $"Could not find {methodDeclaration} in source.");

        var bodyStart = source.IndexOf('{', methodStart);
        Assert.True(bodyStart >= 0, $"Could not find {methodDeclaration} body in source.");

        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[methodStart..(index + 1)];
        }

        throw new InvalidOperationException($"Could not find {methodDeclaration} closing brace in source.");
    }

    private static string ReadHostSource(params string[] path)
        => File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), Path.Combine(path)));

    private static string FindHostSoftwareRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PRISM Utility")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate Host Software source root.");
    }
}
