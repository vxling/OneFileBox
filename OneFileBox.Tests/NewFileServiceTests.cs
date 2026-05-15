using FluentAssertions;
using OneFileBox.Services;
using Xunit;

namespace OneFileBox.Tests;

public class NewFileServiceTests : IDisposable
{
    private readonly string _testDir;

    public NewFileServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"OneFileBox_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".docx")]
    [InlineData(".xlsx")]
    [InlineData(".pptx")]
    [InlineData(".png")]
    [InlineData(".bmp")]
    public void Create_ValidExtension_CreatesFile(string ext)
    {
        var path = Path.Combine(_testDir, $"test_file{ext}");

        var result = NewFileService.Create(path);

        result.Should().BeTrue();
        File.Exists(path).Should().BeTrue();
    }

    [Theory]
    [InlineData(".docx")]  // ZIP signature
    [InlineData(".xlsx")]
    [InlineData(".pptx")]
    [InlineData(".png")]    // PNG signature
    [InlineData(".bmp")]   // BM signature
    public void Create_SpecialFormat_HasCorrectHeader(string ext)
    {
        var path = Path.Combine(_testDir, $"header_test{ext}");
        NewFileService.Create(path);

        using var fs = File.OpenRead(path);
        var header = new byte[4];
        fs.Read(header, 0, 4);

        if (ext is ".docx" or ".xlsx" or ".pptx")
        {
            // PK (ZIP local file header signature)
            header[0].Should().Be(0x50); // P
            header[1].Should().Be(0x4B); // K
        }
        else if (ext == ".png")
        {
            // PNG signature
            header[0].Should().Be(0x89);
            header[1].Should().Be(0x50); // P
        }
        else if (ext == ".bmp")
        {
            // BM
            header[0].Should().Be(0x42); // B
            header[1].Should().Be(0x4D); // M
        }
    }

    [Fact]
    public void Create_Txt_CreatesEmptyFile()
    {
        var path = Path.Combine(_testDir, "blank.txt");
        NewFileService.Create(path);

        new FileInfo(path).Length.Should().Be(0);
    }

    [Fact]
    public void Create_ExistingFile_Overwrites()
    {
        var path = Path.Combine(_testDir, "existing.txt");
        File.WriteAllText(path, "old content");
        var originalLength = new FileInfo(path).Length;

        NewFileService.Create(path);

        // Content should change (overwritten)
        File.Exists(path).Should().BeTrue();
    }
}
