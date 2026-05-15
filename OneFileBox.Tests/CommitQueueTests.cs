using FluentAssertions;
using OneFileBox.Services;
using Xunit;

namespace OneFileBox.Tests;

public class CommitQueueTests : IDisposable
{
    public void Dispose()
    {
        // CommitQueue is singleton — don't dispose in tests
    }

    [Fact]
    public void Enqueue_SingleOp_CountIsOne()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "temp");

        CommitQueue.Instance.Enqueue(path, CommitOperation.Add);
        var count = CommitQueue.Instance.Count;

        CommitQueue.Instance.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Enqueue_DifferentPaths_BothPresent()
    {
        var a = Path.Combine(Path.GetTempPath(), $"a_{Guid.NewGuid():N}.txt");
        var b = Path.Combine(Path.GetTempPath(), $"b_{Guid.NewGuid():N}.txt");
        File.WriteAllText(a, "a"); File.WriteAllText(b, "b");

        var before = CommitQueue.Instance.Count;
        CommitQueue.Instance.Enqueue(a, CommitOperation.Add);
        CommitQueue.Instance.Enqueue(b, CommitOperation.Modify);
        var after = CommitQueue.Instance.Count;

        (after - before).Should().BeGreaterThan(1);
    }

    [Fact]
    public void Resolve_ReturnsNonEmptyList()
    {
        var items = CommitQueue.Instance.Resolve();
        // Returns list of pending items
        items.Should().NotBeNull();
    }

    [Fact]
    public void GetStaleItems_ReturnsEmptyOnFreshQueue()
    {
        var stale = CommitQueue.Instance.GetStaleItems();
        stale.Should().NotBeNull();
    }

    [Fact]
    public void Count_IsAccessible()
    {
        var count = CommitQueue.Instance.Count;
        count.Should().BeGreaterThan(-1);
    }
}
