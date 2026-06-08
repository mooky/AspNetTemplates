using RazorRenderCli;

namespace RazorRenderCli.Tests;

[TestFixture]
[NonParallelizable]
public class CliEndToEndTests
{
    private string _tempDir = null!;

    private static string Fixture(string name) =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "TestFiles", name);

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"rrcli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static async Task<(int Code, string Out, string Err)> Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await CliApp.RunAsync(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    [Test]
    public async Task Render_ToStdout_WritesResultAndSucceeds()
    {
        var (code, stdout, stderr) = await Run(Fixture("simple.cshtml"), Fixture("data.json"));

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(CliApp.ExitSuccess));
            Assert.That(stdout, Is.EqualTo("Hello World!"));
            Assert.That(stderr, Is.Empty);
        });
    }

    [Test]
    public async Task Render_WithOutputOption_WritesFileAndLeavesStdoutEmpty()
    {
        var outFile = Path.Combine(_tempDir, "result.txt");

        var (code, stdout, _) = await Run(
            Fixture("simple.cshtml"), Fixture("data.json"), "--output", outFile);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(CliApp.ExitSuccess));
            Assert.That(stdout, Is.Empty);
            Assert.That(File.Exists(outFile), Is.True);
            Assert.That(File.ReadAllText(outFile), Is.EqualTo("Hello World!"));
        });
    }

    [Test]
    public async Task Render_NonexistentTemplate_FailsWithError()
    {
        var (code, _, stderr) = await Run(
            Fixture("no-such.cshtml"), Fixture("data.json"));

        Assert.That(code, Is.EqualTo(CliApp.ExitRenderError));
        Assert.That(stderr, Does.Contain("error:"));
    }

    [Test]
    public async Task Render_NonexistentData_FailsWithError()
    {
        var (code, _, stderr) = await Run(
            Fixture("simple.cshtml"), Path.Combine(_tempDir, "no-such.json"));

        Assert.That(code, Is.EqualTo(CliApp.ExitRenderError));
        Assert.That(stderr, Does.Contain("error:"));
    }

    [Test]
    public async Task Render_InvalidJson_FailsWithError()
    {
        var badJson = Path.Combine(_tempDir, "bad.json");
        await File.WriteAllTextAsync(badJson, "{ not json");

        var (code, _, stderr) = await Run(Fixture("simple.cshtml"), badJson);

        Assert.That(code, Is.EqualTo(CliApp.ExitRenderError));
        Assert.That(stderr, Does.Contain("Invalid JSON"));
    }

    [Test]
    public async Task Render_MissingModelKey_FailsWithError()
    {
        var (code, _, stderr) = await Run(Fixture("missing.cshtml"), Fixture("data.json"));

        Assert.That(code, Is.EqualTo(CliApp.ExitRenderError));
        Assert.That(stderr, Does.Contain("error:"));
    }

    [Test]
    public async Task Help_ShowsUsageAndSucceeds()
    {
        var (code, stdout, _) = await Run("--help");

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(CliApp.ExitSuccess));
            Assert.That(stdout, Does.Contain("template"));
            Assert.That(stdout, Does.Contain("--output"));
        });
    }

    [Test]
    public async Task MissingRequiredArgument_ReturnsUsageError()
    {
        var (code, _, stderr) = await Run(Fixture("simple.cshtml"));

        Assert.That(code, Is.EqualTo(CliApp.ExitUsageError));
        Assert.That(stderr, Does.Contain("error:"));
    }

    [Test]
    public async Task Verbose_KeepsStdoutCleanOfLogs()
    {
        var (code, stdout, _) = await Run(
            Fixture("simple.cshtml"), Fixture("data.json"), "--verbose");

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(CliApp.ExitSuccess));
            Assert.That(stdout, Is.EqualTo("Hello World!"));
        });
    }
}
