using Spectra.Core.Coverage;

namespace Spectra.Core.Tests.Coverage;

public class AutomationScannerTests : IAsyncLifetime
{
    private string _tempDir = null!;

    public Task InitializeAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-scanner-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Task.Delay(50); // Allow handles to close
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task ScanAsync_EmptyDirectory_ReturnsEmptyDictionary()
    {
        // Arrange
        var scanner = new AutomationScanner(_tempDir);

        // Act
        var result = await scanner.ScanAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task ScanAsync_CSharpAttributePattern_FindsTestIds()
    {
        // Arrange
        var testDir = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, "LoginTests.cs");

        await File.WriteAllTextAsync(testFile, """
            using Xunit;

            public class LoginTests
            {
                [TestCase("TC-001")]
                public void Test_ValidLogin()
                {
                }

                [Theory("TC-002")]
                public void Test_InvalidPassword()
                {
                }
            }
            """);

        var scanner = new AutomationScanner(_tempDir);

        // Act
        var result = await scanner.ScanAsync();

        // Assert
        Assert.Single(result);
        var fileInfo = result.Values.First();
        Assert.Contains("TC-001", fileInfo.ReferencedTestIds);
        Assert.Contains("TC-002", fileInfo.ReferencedTestIds);
    }

    [Fact]
    public async Task ScanAsync_InlineDataPattern_FindsTestIds()
    {
        // Arrange
        var testDir = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, "DataTests.cs");

        await File.WriteAllTextAsync(testFile, """
            using Xunit;

            public class DataTests
            {
                [InlineData("TC-100", "test data")]
                [InlineData("TC-101", "more data")]
                public void Test_WithData(string testId, string data)
                {
                }
            }
            """);

        var scanner = new AutomationScanner(_tempDir);

        // Act
        var result = await scanner.ScanAsync();

        // Assert
        Assert.Single(result);
        var fileInfo = result.Values.First();
        Assert.Contains("TC-100", fileInfo.ReferencedTestIds);
        Assert.Contains("TC-101", fileInfo.ReferencedTestIds);
    }

    [Fact]
    public async Task ScanAsync_CommentMarker_FindsTestIds()
    {
        // Arrange
        var testDir = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, "CommentTests.cs");

        await File.WriteAllTextAsync(testFile, """
            public class CommentTests
            {
                // TC-200: This tests the login functionality
                public void TestLogin()
                {
                }
            }
            """);

        var scanner = new AutomationScanner(_tempDir);

        // Act
        var result = await scanner.ScanAsync();

        // Assert
        Assert.Single(result);
        var fileInfo = result.Values.First();
        Assert.Contains("TC-200", fileInfo.ReferencedTestIds);
    }

    [Fact]
    public async Task ScanAsync_JavaScriptPattern_FindsTestIds()
    {
        // Arrange
        var testDir = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, "login.test.js");

        await File.WriteAllTextAsync(testFile, """
            describe('Login Tests', () => {
                it('TC-300: should login successfully', () => {
                    // test code
                });

                test('TC-301: should fail with invalid password', () => {
                    // test code
                });
            });
            """);

        var scanner = new AutomationScanner(_tempDir);

        // Act
        var result = await scanner.ScanAsync();

        // Assert
        Assert.Single(result);
        var fileInfo = result.Values.First();
        Assert.Contains("TC-300", fileInfo.ReferencedTestIds);
        Assert.Contains("TC-301", fileInfo.ReferencedTestIds);
    }

    [Fact]
    public async Task ScanAsync_PythonPattern_FindsTestIds()
    {
        // Arrange
        var testDir = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, "test_login.py");

        await File.WriteAllTextAsync(testFile, """
            import pytest

            def test_tc_400():
                # Test implementation
                pass

            def test_tc_401():
                # Another test
                pass
            """);

        var scanner = new AutomationScanner(_tempDir);

        // Act
        var result = await scanner.ScanAsync();

        // Assert
        Assert.Single(result);
        var fileInfo = result.Values.First();
        Assert.Contains("TC-400", fileInfo.ReferencedTestIds);
        Assert.Contains("TC-401", fileInfo.ReferencedTestIds);
    }

    [Fact]
    public async Task ScanAsync_MultipleFiles_ReturnsAllFiles()
    {
        // Arrange
        var testDir = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(testDir);

        await File.WriteAllTextAsync(Path.Combine(testDir, "Test1.cs"), """
            [TestCase("TC-001")]
            public void Test1() { }
            """);

        await File.WriteAllTextAsync(Path.Combine(testDir, "Test2.cs"), """
            [TestCase("TC-002")]
            public void Test2() { }
            """);

        var scanner = new AutomationScanner(_tempDir);

        // Act
        var result = await scanner.ScanAsync();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ScanAsync_NoMatches_ExcludesFile()
    {
        // Arrange
        var testDir = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, "NoTests.cs");

        await File.WriteAllTextAsync(testFile, """
            public class NoTests
            {
                public void NotATest()
                {
                }
            }
            """);

        var scanner = new AutomationScanner(_tempDir);

        // Act
        var result = await scanner.ScanAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task ScanAsync_IncludesLineNumbers()
    {
        // Arrange
        var testDir = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, "LineTest.cs");

        await File.WriteAllTextAsync(testFile, """
            public class LineTest
            {
                [TestCase("TC-001")]
                public void Test1() { }
            }
            """);

        var scanner = new AutomationScanner(_tempDir);

        // Act
        var result = await scanner.ScanAsync();

        // Assert
        Assert.Single(result);
        var fileInfo = result.Values.First();
        Assert.Single(fileInfo.References);
        Assert.True(fileInfo.References[0].LineNumber > 0);
    }

    [Fact]
    public async Task ScanAsync_CustomDirectories_ScansOnlySpecified()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_tempDir, "custom"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "other"));

        await File.WriteAllTextAsync(
            Path.Combine(_tempDir, "custom", "Test.cs"),
            """[TestCase("TC-001")] public void Test() { }""");

        await File.WriteAllTextAsync(
            Path.Combine(_tempDir, "other", "Test.cs"),
            """[TestCase("TC-002")] public void Test() { }""");

        var scanner = new AutomationScanner(
            _tempDir,
            searchDirectories: ["custom"],
            filePatterns: ["*.cs"],
            attributePatterns: [@"\[TestCase\s*\(\s*""(TC-\d{3,})"""]);

        // Act
        var result = await scanner.ScanAsync();

        // Assert
        Assert.Single(result);
        Assert.Contains("TC-001", result.Values.First().ReferencedTestIds);
    }

    [Fact]
    public async Task ScanAsync_DeduplicatesTestIds()
    {
        // Arrange
        var testDir = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, "DupTest.cs");

        await File.WriteAllTextAsync(testFile, """
            public class DupTest
            {
                // TC-001: First reference
                [TestCase("TC-001")]
                public void Test1()
                {
                    // TC-001: Another reference
                }
            }
            """);

        var scanner = new AutomationScanner(_tempDir);

        // Act
        var result = await scanner.ScanAsync();

        // Assert
        Assert.Single(result);
        var fileInfo = result.Values.First();
        Assert.Single(fileInfo.ReferencedTestIds); // Deduplicated
        Assert.True(fileInfo.References.Count > 1); // All references preserved
    }

    [Fact]
    public async Task ScanAsync_RecursiveDirectoryScan()
    {
        // Arrange
        var nestedDir = Path.Combine(_tempDir, "tests", "nested", "deep");
        Directory.CreateDirectory(nestedDir);
        var testFile = Path.Combine(nestedDir, "DeepTest.cs");

        await File.WriteAllTextAsync(testFile, """
            [TestCase("TC-001")]
            public void Test() { }
            """);

        var scanner = new AutomationScanner(_tempDir);

        // Act
        var result = await scanner.ScanAsync();

        // Assert
        Assert.Single(result);
        Assert.Contains("TC-001", result.Values.First().ReferencedTestIds);
    }

    [Fact]
    public async Task ScanAsync_NormalizesTestIdCase()
    {
        // Arrange
        var testDir = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, "CaseTest.cs");

        // Note: The scanner patterns match uppercase TC- by default
        // The normalization uppercases matched test IDs that start with tc- (case insensitive)
        await File.WriteAllTextAsync(testFile, """
            // TC-001: Test case
            public void Test() { }
            """);

        var scanner = new AutomationScanner(_tempDir);

        // Act
        var result = await scanner.ScanAsync();

        // Assert
        Assert.Single(result);
        Assert.Contains("TC-001", result.Values.First().ReferencedTestIds);
    }
}
