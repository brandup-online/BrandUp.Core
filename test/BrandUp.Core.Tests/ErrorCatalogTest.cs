using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using BrandUp.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace BrandUp
{
    public static class ExampleErrors
    {
        public static readonly ErrorDescriptor OrderNotFound = new(
            "order-not-found", ErrorKind.NotFound,
            "Order {0} not found.",
            "The order id does not exist or belongs to another project.");

        public static readonly ErrorDescriptor OrderAlreadyPaid = new(
            "order-already-paid", ErrorKind.Conflict, "Order {0} is already paid.");
    }

    public class ErrorCatalogTest
    {
        // Dictionary-backed IErrorLocalizer: culture name -> code -> template.
        sealed class FakeErrorLocalizer(Dictionary<string, Dictionary<string, string>> resources) : IErrorLocalizer
        {
            public string Localize(IError error, CultureInfo culture)
            {
                if (!resources.TryGetValue(culture.Name, out var templates) || !templates.TryGetValue(error.Code, out var template))
                    return null;

                return error.Arguments.Count > 0 ? string.Format(culture, template, error.Arguments.ToArray()) : template;
            }
        }

        [Fact]
        public void ResultError_FromDescriptor_FormatsInvariantAndKeepsArguments()
        {
            var result = Result.Error(ExampleErrors.OrderNotFound, 42);

            var error = Assert.Single(result.Errors);
            Assert.Equal("order-not-found", error.Code);
            Assert.Equal(ErrorKind.NotFound, error.Kind);
            Assert.Equal("Order 42 not found.", error.Message);
            Assert.Equal(42, Assert.Single(error.Arguments));
        }

        [Fact]
        public void Catalog_AddFrom_CollectsAndRejectsDuplicates()
        {
            var catalog = new ErrorCatalog().AddFrom(typeof(ExampleErrors));

            Assert.Equal(2, catalog.All.Count);
            Assert.True(catalog.TryGet("order-not-found", out var descriptor));
            Assert.Same(ExampleErrors.OrderNotFound, descriptor);

            Assert.Throws<InvalidOperationException>(() => catalog.AddFrom(typeof(ExampleErrors)));
            Assert.Throws<InvalidOperationException>(() => new ErrorCatalog().AddFrom(typeof(ErrorCatalogTest)));
        }

        [Fact]
        public void Catalog_RegisteredViaBuilder_ResolvesAsSingleton()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDomain()
                .AddErrorCatalog(catalog => catalog.Add(ExampleErrors.OrderNotFound))
                .AddErrorCatalog(catalog => catalog.Add(ExampleErrors.OrderAlreadyPaid));
            using var serviceProvider = serviceCollection.BuildServiceProvider();

            // Repeated calls configure one catalog.
            var catalog = serviceProvider.GetRequiredService<ErrorCatalog>();
            Assert.Equal(2, catalog.All.Count);
        }

        [Fact]
        public void ToProblemDetails_LocalizesByCulture_FallsBackToInvariant()
        {
            var localizer = new FakeErrorLocalizer(new Dictionary<string, Dictionary<string, string>>
            {
                ["ru"] = new() { ["order-not-found"] = "Заказ {0} не найден." }
            });
            var result = Result.Error(ExampleErrors.OrderNotFound, 42);

            var russian = result.ToProblemDetails(localizer, CultureInfo.GetCultureInfo("ru"));
            var russianError = Assert.Single(Assert.IsType<object[]>(russian.Extensions["errors"]));
            Assert.Contains("Заказ 42 не найден.", russianError.ToString());
            Assert.Equal(404, russian.Status);

            // No English resource: the invariant developer message is served.
            var english = result.ToProblemDetails(localizer, CultureInfo.GetCultureInfo("en"));
            var englishError = Assert.Single(Assert.IsType<object[]>(english.Extensions["errors"]));
            Assert.Contains("Order 42 not found.", englishError.ToString());
        }

        [Fact]
        public void ErrorCatalogEndpoint_BuildsLocalizedModel()
        {
            var catalog = new ErrorCatalog().AddFrom(typeof(ExampleErrors));
            var localizer = new FakeErrorLocalizer(new Dictionary<string, Dictionary<string, string>>
            {
                ["ru"] = new()
                {
                    ["order-not-found"] = "Заказ {0} не найден.",
                    ["order-already-paid"] = "Заказ {0} уже оплачен."
                }
            });

            var model = ErrorCatalogEndpoint.BuildModel(catalog, localizer, CultureInfo.GetCultureInfo("ru"));

            Assert.Equal(2, model.Length);
            var notFound = Assert.Single(model, entry => entry.Code == "order-not-found");
            Assert.Equal("NotFound", notFound.Kind);
            Assert.Equal(404, notFound.HttpStatus);
            Assert.Equal("Заказ {0} не найден.", notFound.Message);
            Assert.NotNull(notFound.Description);
        }

        [Fact]
        public void AssertAllLocalized_ListsMissingCodes()
        {
            var catalog = new ErrorCatalog().AddFrom(typeof(ExampleErrors));
            var localizer = new FakeErrorLocalizer(new Dictionary<string, Dictionary<string, string>>
            {
                ["ru"] = new()
                {
                    ["order-not-found"] = "Заказ {0} не найден."
                    // order-already-paid deliberately missing
                }
            });

            var exception = Assert.ThrowsAny<DomainAssertException>(
                () => ErrorCatalogAssert.AssertAllLocalized(catalog, localizer, "ru"));

            Assert.Contains("order-already-paid", exception.Message);
            Assert.DoesNotContain("order-not-found;", exception.Message);
        }

        // IStringLocalizer fake resolving by CurrentUICulture, exact culture sets only
        // (GetAllStrings(false) semantics), like ResourceManagerStringLocalizer.
        sealed class FakeStringLocalizer(Dictionary<string, Dictionary<string, string>> resourcesByCulture) : IStringLocalizer
        {
            public LocalizedString this[string name] => throw new NotSupportedException();
            public LocalizedString this[string name, params object[] arguments] => throw new NotSupportedException();

            public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            {
                if (!resourcesByCulture.TryGetValue(CultureInfo.CurrentUICulture.Name, out var resources))
                    throw new MissingManifestResourceException();

                return resources.Select(pair => new LocalizedString(pair.Key, pair.Value));
            }
        }

        [Fact]
        public void StringLocalizer_ParentChainWithoutNeutralFallback()
        {
            var localizer = new StringLocalizerErrorLocalizer(new FakeStringLocalizer(new Dictionary<string, Dictionary<string, string>>
            {
                // neutral resources are the untranslated default - must NOT count as ru
                [""] = new() { ["order-not-found"] = "Order {0} not found.", ["order-already-paid"] = "Order {0} is already paid." },
                ["ru"] = new() { ["order-not-found"] = "Заказ {0} не найден." }
            }));
            var error = ExampleErrors.OrderNotFound.CreateError(42);

            // ru-RU falls back to ru - a legitimate parent-chain hit.
            Assert.Equal("Заказ 42 не найден.", localizer.Localize(error, CultureInfo.GetCultureInfo("ru-RU")));

            // Present only in the neutral resources: reported as untranslated, not served as ru.
            Assert.Null(localizer.Localize(ExampleErrors.OrderAlreadyPaid.CreateError(7), CultureInfo.GetCultureInfo("ru")));
        }

        [Fact]
        public void StringLocalizer_MalformedTranslation_FallsBackInsteadOfThrowing()
        {
            var localizer = new StringLocalizerErrorLocalizer(new FakeStringLocalizer(new Dictionary<string, Dictionary<string, string>>
            {
                ["ru"] = new() { ["order-not-found"] = "Заказ {5} не найден." }
            }));

            // A broken translation must not become a FormatException on the response path.
            Assert.Null(localizer.Localize(ExampleErrors.OrderNotFound.CreateError(42), CultureInfo.GetCultureInfo("ru")));

            var problemDetails = Result.Error(ExampleErrors.OrderNotFound, 42).ToProblemDetails(localizer, CultureInfo.GetCultureInfo("ru"));
            Assert.Contains("Order 42 not found.", Assert.Single(Assert.IsType<object[]>(problemDetails.Extensions["errors"])).ToString());
        }

        [Fact]
        public void AssertAllLocalized_CatchesMissingAndBrokenTranslations()
        {
            var catalog = new ErrorCatalog().AddFrom(typeof(ExampleErrors));
            var localizer = new StringLocalizerErrorLocalizer(new FakeStringLocalizer(new Dictionary<string, Dictionary<string, string>>
            {
                [""] = new() { ["order-not-found"] = "Order {0} not found.", ["order-already-paid"] = "Order {0} is already paid." },
                ["ru"] = new() { ["order-not-found"] = "Заказ {5} не найден." } // broken; already-paid missing
            }));

            var exception = Assert.ThrowsAny<DomainAssertException>(
                () => ErrorCatalogAssert.AssertAllLocalized(catalog, localizer, "ru"));

            Assert.Contains("order-not-found", exception.Message);   // broken template
            Assert.Contains("order-already-paid", exception.Message); // neutral-only = untranslated
        }

        [Fact]
        public void AddErrorCatalog_CustomRegistration_ThrowsLoudly()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(provider => new ErrorCatalog());
            var builder = serviceCollection.AddDomain();

            Assert.Throws<InvalidOperationException>(
                () => builder.AddErrorCatalog(catalog => catalog.Add(ExampleErrors.OrderNotFound)));
        }

        [Fact]
        public void AssertError_ByDescriptor()
        {
            var result = Result.Error(ExampleErrors.OrderAlreadyPaid, 7);

            var error = result.AssertError(ExampleErrors.OrderAlreadyPaid);
            Assert.Equal(ErrorKind.Conflict, error.Kind);

            Assert.ThrowsAny<DomainAssertException>(() => result.AssertError(ExampleErrors.OrderNotFound));
        }
    }
}
