using RazorRenderCli;

namespace RazorRenderCli.Tests;

[TestFixture]
public class RazorRendererTests
{
    private RazorRenderer _renderer = null!;

    private static string Fixture(string name) =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "TestFiles", name);

    [SetUp]
    public void SetUp() => _renderer = new RazorRenderer();

    [TearDown]
    public void TearDown() => _renderer.Dispose();

    [Test]
    public async Task RenderAsync_SimpleSubstitution_ReplacesPlaceholder()
    {
        var model = JsonModelLoader.Load("""{ "name": "Razor" }""");
        var result = await _renderer.RenderAsync(Fixture("simple.cshtml"), model);
        Assert.That(result, Is.EqualTo("Hello Razor!"));
    }

    [Test]
    public async Task RenderAsync_NestedModel_ResolvesDottedMembers()
    {
        var model = JsonModelLoader.Load(
            """{ "user": { "name": "Ann", "address": { "city": "Seattle" } } }""");
        var result = await _renderer.RenderAsync(Fixture("nested.cshtml"), model);
        Assert.That(result, Is.EqualTo("Ann lives in Seattle."));
    }

    [Test]
    public async Task RenderAsync_Loop_IteratesArray()
    {
        var model = JsonModelLoader.Load("""{ "items": ["a", "b", "c"] }""");
        var result = await _renderer.RenderAsync(Fixture("loop.cshtml"), model);
        Assert.That(result, Is.EqualTo("[a][b][c]"));
    }

    [Test]
    public async Task RenderAsync_Conditional_SelectsBranch()
    {
        var adminResult = await _renderer.RenderAsync(
            Fixture("conditional.cshtml"), JsonModelLoader.Load("""{ "admin": true }"""));
        var userResult = await _renderer.RenderAsync(
            Fixture("conditional.cshtml"), JsonModelLoader.Load("""{ "admin": false }"""));

        Assert.Multiple(() =>
        {
            Assert.That(adminResult, Is.EqualTo("ADMIN"));
            Assert.That(userResult, Is.EqualTo("USER"));
        });
    }

    [Test]
    public async Task RenderAsync_PlainOutput_IsHtmlEncoded_RawIsNot()
    {
        var model = JsonModelLoader.Load("""{ "html": "<b>hi</b>" }""");
        var result = await _renderer.RenderAsync(Fixture("encoding.cshtml"), model);
        Assert.That(result, Is.EqualTo("&lt;b&gt;hi&lt;/b&gt; | <b>hi</b>"));
    }

    [Test]
    public void RenderAsync_CancelledToken_ThrowsBeforeRendering()
    {
        var model = JsonModelLoader.Load("""{ "name": "Razor" }""");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(
            () => _renderer.RenderAsync(Fixture("simple.cshtml"), model, cts.Token));
    }

    [Test]
    public void RenderAsync_MissingMember_ThrowsRenderException()
    {
        var model = JsonModelLoader.Load("""{ "present": 1 }""");
        var ex = Assert.ThrowsAsync<RenderException>(
            () => _renderer.RenderAsync(Fixture("missing.cshtml"), model));
        Assert.That(ex!.Message, Does.Contain("missing.cshtml"));
    }

    [Test]
    public void RenderAsync_TemplateNotFound_ThrowsRenderException()
    {
        var path = Fixture($"no-such-template-{Guid.NewGuid():N}.cshtml");
        var ex = Assert.ThrowsAsync<RenderException>(
            () => _renderer.RenderAsync(path, JsonModelLoader.Load("{}")));
        Assert.That(ex!.Message, Does.Contain("not found"));
    }
}
