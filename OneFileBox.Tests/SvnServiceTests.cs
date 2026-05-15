using FluentAssertions;
using OneFileBox.Services;
using Xunit;

namespace OneFileBox.Tests;

public class SvnServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly SvnService _svn;

    public SvnServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"SvnService_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _svn = new SvnService();
    }

    public void Dispose()
    {
        _svn.Dispose();
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    [Fact]
    public void Ctor_CreatesInstance()
    {
        using var svc = new SvnService();
        svc.Should().NotBeNull();
    }

    [Fact]
    public void IsValidWorkingCopy_FakePath_ReturnsFalse()
    {
        var result = _svn.IsValidWorkingCopy(Path.Combine(_testDir, "nonexistent"));
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatusAsync_InvalidPath_ReturnsEmpty()
    {
        var result = await _svn.GetStatusAsync("/nonexistent/path");
        result.Should().BeEmpty();
    }
}
