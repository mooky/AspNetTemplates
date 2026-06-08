using RazorRenderCli;

namespace RazorRenderCli.Tests;

[TestFixture]
public class JsonModelLoaderTests
{
    private static IDictionary<string, object?> LoadObject(string json)
    {
        var model = JsonModelLoader.Load(json);
        Assert.That(model, Is.InstanceOf<IDictionary<string, object?>>());
        return (IDictionary<string, object?>)model!;
    }

    [Test]
    public void Load_FlatScalars_ConvertsToClrTypes()
    {
        var model = LoadObject(
            """{ "text": "hi", "count": 3, "ratio": 1.5, "flag": true, "nothing": null }""");

        Assert.Multiple(() =>
        {
            Assert.That(model["text"], Is.EqualTo("hi"));
            Assert.That(model["count"], Is.EqualTo(3L));
            Assert.That(model["ratio"], Is.EqualTo(1.5d));
            Assert.That(model["flag"], Is.EqualTo(true));
            Assert.That(model["nothing"], Is.Null);
        });
    }

    [Test]
    public void Load_NestedObject_BecomesNestedDictionary()
    {
        var model = LoadObject("""{ "user": { "address": { "city": "Seattle" } } }""");

        var user = (IDictionary<string, object?>)model["user"]!;
        var address = (IDictionary<string, object?>)user["address"]!;
        Assert.That(address["city"], Is.EqualTo("Seattle"));
    }

    [Test]
    public void Load_ArrayOfScalars_BecomesList()
    {
        var model = LoadObject("""{ "items": [1, 2, 3] }""");

        Assert.That(model["items"], Is.InstanceOf<List<object?>>());
        Assert.That((List<object?>)model["items"]!, Is.EqualTo(new object?[] { 1L, 2L, 3L }));
    }

    [Test]
    public void Load_ArrayOfObjects_BecomesListOfDictionaries()
    {
        var model = LoadObject("""{ "people": [ { "name": "Ann" }, { "name": "Bob" } ] }""");

        var people = (List<object?>)model["people"]!;
        Assert.That(people, Has.Count.EqualTo(2));
        Assert.That(((IDictionary<string, object?>)people[0]!)["name"], Is.EqualTo("Ann"));
        Assert.That(((IDictionary<string, object?>)people[1]!)["name"], Is.EqualTo("Bob"));
    }

    [Test]
    public void Load_EmptyObject_ReturnsEmptyDictionary()
    {
        var model = LoadObject("{}");
        Assert.That(model, Is.Empty);
    }

    [Test]
    public void Load_InvalidJson_ThrowsRenderException()
    {
        Assert.Throws<RenderException>(() => JsonModelLoader.Load("{ not valid json "));
    }

    [Test]
    public void LoadFile_MissingFile_ThrowsRenderException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.json");
        var ex = Assert.Throws<RenderException>(() => JsonModelLoader.LoadFile(path));
        Assert.That(ex!.Message, Does.Contain(path));
    }
}
