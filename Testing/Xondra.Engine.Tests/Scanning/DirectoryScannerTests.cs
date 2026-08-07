using FluentAssertions;
using Xondra.Engine.Scanning;
using Xondra.Engine.Tests.TestSupport;
using Xunit;

namespace Xondra.Engine.Tests.Scanning;

public class DirectoryScannerTests
{
    [Fact]
    public void Scan_walks_a_real_directory_tree_reporting_files_and_empty_directories()
    {
        using var temp = new TempDirectory();
        var root = temp.FullPath;
        var subdir1 = Directory.CreateDirectory(Path.Combine(root, "subdir1")).FullName;
        var emptyDir = Directory.CreateDirectory(Path.Combine(root, "emptydir")).FullName;

        var fileA = Path.Combine(root, "fileA.txt");
        File.WriteAllText(fileA, "a");
        File.SetAttributes(fileA, File.GetAttributes(fileA) & ~FileAttributes.Archive);

        var fileB = Path.Combine(root, "fileB.txt");
        File.WriteAllText(fileB, "b");

        var fileC = Path.Combine(subdir1, "fileC.txt");
        File.WriteAllText(fileC, "c");

        var scanner = new DirectoryScanner(new WindowsFileSystem());
        var result = scanner.Scan(root);

        result.Errors.Should().BeEmpty();
        result.Files.Should().HaveCount(3);

        var scannedFileA = result.Files.Should().ContainSingle(f => f.FullPath == fileA).Subject;
        scannedFileA.ArchiveBitSet.Should().BeFalse();
        scannedFileA.Filename.Should().Be("fileA.txt");
        scannedFileA.Drive.Should().Be(Path.GetPathRoot(root));
        scannedFileA.Directory.Should().Be(root[Path.GetPathRoot(root)!.Length..]);

        var scannedFileB = result.Files.Should().ContainSingle(f => f.FullPath == fileB).Subject;
        scannedFileB.ArchiveBitSet.Should().BeTrue();

        var scannedFileC = result.Files.Should().ContainSingle(f => f.FullPath == fileC).Subject;
        scannedFileC.Directory.Should().Be(subdir1[Path.GetPathRoot(subdir1)!.Length..]);

        var expectedEmptyDirectory = emptyDir[Path.GetPathRoot(emptyDir)!.Length..];
        result.EmptyDirectories.Should().ContainSingle(d => d.Directory == expectedEmptyDirectory);
    }

    [Fact]
    public void Scan_does_not_report_a_directory_containing_only_subdirectories_as_empty()
    {
        using var temp = new TempDirectory();
        var root = temp.FullPath;
        Directory.CreateDirectory(Path.Combine(root, "parent", "child"));

        var scanner = new DirectoryScanner(new WindowsFileSystem());
        var result = scanner.Scan(root);

        result.EmptyDirectories.Should().ContainSingle(d => d.Directory.EndsWith("child"));
        result.EmptyDirectories.Should().NotContain(d => d.Directory.EndsWith("parent"));
    }

    [Fact]
    public void Scan_skips_a_directory_that_denies_access_and_records_the_error()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddDirectory(@"C:\root", subdirectories: [@"C:\root\locked", @"C:\root\open"]);
        fileSystem.DenyAccess(@"C:\root\locked");
        fileSystem.AddDirectory(@"C:\root\open", files: [@"C:\root\open\file.txt"]);
        fileSystem.AddFile(@"C:\root\open\file.txt",
            new FileEntryInfo(1, DateTime.UtcNow, DateTime.UtcNow, FileAttributes.Archive));

        var scanner = new DirectoryScanner(fileSystem);
        var result = scanner.Scan(@"C:\root");

        result.Files.Should().ContainSingle(f => f.FullPath == @"C:\root\open\file.txt");
        result.Errors.Should().ContainSingle(e => e.Path == @"C:\root\locked" && e.Message.Contains("Access denied"));
    }

    [Fact]
    public void Scan_skips_a_file_that_disappears_before_it_can_be_stat_and_records_the_error()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddDirectory(@"C:\root", files: [@"C:\root\gone.txt", @"C:\root\present.txt"]);
        fileSystem.MakeFileDisappear(@"C:\root\gone.txt");
        fileSystem.AddFile(@"C:\root\present.txt",
            new FileEntryInfo(1, DateTime.UtcNow, DateTime.UtcNow, FileAttributes.Archive));

        var scanner = new DirectoryScanner(fileSystem);
        var result = scanner.Scan(@"C:\root");

        result.Files.Should().ContainSingle(f => f.FullPath == @"C:\root\present.txt");
        result.Errors.Should().ContainSingle(e => e.Path == @"C:\root\gone.txt");
    }
}
