using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RazorRenderCli;

/// <summary>
/// Renders Razor <c>.cshtml</c> templates by hosting the ASP.NET Core Razor view engine with
/// runtime compilation. A service provider is built per template directory (so the view engine's
/// file provider is rooted there) and cached for reuse.
/// </summary>
public sealed class RazorRenderer : IRazorRenderer, IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, Lazy<ServiceProvider>> _providersByDirectory =
        new(StringComparer.OrdinalIgnoreCase);

    public RazorRenderer(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    public async Task<string> RenderAsync(string templatePath, object? model)
    {
        var fullPath = Path.GetFullPath(templatePath);
        if (!File.Exists(fullPath))
        {
            throw new RenderException($"Template file not found: '{templatePath}'");
        }

        var directory = Path.GetDirectoryName(fullPath)!;
        var fileName = Path.GetFileName(fullPath);
        // Lazy ensures BuildServiceProvider runs exactly once per directory even under
        // concurrent first-access; GetOrAdd's factory itself offers no such guarantee.
        var provider = _providersByDirectory
            .GetOrAdd(directory, dir => new Lazy<ServiceProvider>(() => BuildServiceProvider(dir)))
            .Value;

        var viewEngine = provider.GetRequiredService<IRazorViewEngine>();
        var tempDataProvider = provider.GetRequiredService<ITempDataProvider>();
        var metadataProvider = provider.GetRequiredService<IModelMetadataProvider>();

        var viewResult = viewEngine.GetView(executingFilePath: null, viewPath: "/" + fileName, isMainPage: true);
        if (!viewResult.Success)
        {
            var searched = string.Join(", ", viewResult.SearchedLocations);
            throw new RenderException(
                $"Could not load template '{templatePath}'. Searched: {searched}");
        }

        var view = viewResult.View;
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        var viewData = new ViewDataDictionary(metadataProvider, new ModelStateDictionary())
        {
            Model = model,
        };
        var tempData = new TempDataDictionary(httpContext, tempDataProvider);

        await using var writer = new StringWriter();
        var viewContext = new ViewContext(
            actionContext, view, viewData, tempData, writer, new HtmlHelperOptions());

        try
        {
            await view.RenderAsync(viewContext);
        }
        catch (Exception ex) when (ex is not RenderException)
        {
            throw new RenderException(
                $"Error rendering template '{templatePath}': {ex.Message}", ex);
        }

        return writer.ToString();
    }

    private ServiceProvider BuildServiceProvider(string directory)
    {
        var fileProvider = new PhysicalFileProvider(directory);
        var environment = new RenderingEnvironment(directory, fileProvider);

        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(environment);
        services.AddSingleton<IHostEnvironment>(environment);
        var diagnosticListener = new DiagnosticListener("RazorRenderCli");
        services.AddSingleton<DiagnosticSource>(diagnosticListener);
        services.AddSingleton(diagnosticListener);
        services.AddSingleton(_loggerFactory);
        services.AddLogging();
        services.AddHttpContextAccessor();

        services
            .AddMvcCore()
            .AddViews()
            .AddRazorViewEngine()
            .AddRazorRuntimeCompilation();

        services.Configure<MvcRazorRuntimeCompilationOptions>(options =>
            options.FileProviders.Add(fileProvider));

        return services.BuildServiceProvider();
    }

    public void Dispose()
    {
        foreach (var provider in _providersByDirectory.Values)
        {
            if (provider.IsValueCreated)
            {
                provider.Value.Dispose();
            }
        }

        _providersByDirectory.Clear();
    }

    /// <summary>Minimal hosting environment pointing the view engine at the template directory.</summary>
    private sealed class RenderingEnvironment : IWebHostEnvironment
    {
        public RenderingEnvironment(string contentRoot, IFileProvider fileProvider)
        {
            ContentRootPath = contentRoot;
            ContentRootFileProvider = fileProvider;
            WebRootPath = contentRoot;
            WebRootFileProvider = fileProvider;
        }

        // Must name a loadable assembly: MVC scans it for application parts at startup.
        public string ApplicationName { get; set; } =
            typeof(RazorRenderer).Assembly.GetName().Name!;
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
    }
}
