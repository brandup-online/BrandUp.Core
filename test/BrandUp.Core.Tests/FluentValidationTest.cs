using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrandUp.Commands;
using BrandUp.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrandUp
{
    public class FluentValidationTest
    {
        static ServiceProvider BuildServices()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddScoped<FluentValidation.IValidator<SignCommand>, SignCommandValidator>();

            serviceCollection.AddDomain(options => options.AddCommand<SignCommandHandler>())
                .AddFluentValidation();

            return serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public async Task FluentValidator_FailsDispatchWithMemberNames()
        {
            using var serviceProvider = BuildServices();
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            var result = await domain.SendAsync(new SignCommand { Phone = "" }, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            var error = Assert.IsType<ValidationError>(result.Errors.Single(), exactMatch: false);
            Assert.Equal(ErrorKind.Validation, error.Kind);
            Assert.Equal("Phone is required.", error.Message);
            Assert.Contains("Phone", error.MemberNames);
        }

        [Fact]
        public async Task ValidRequest_Passes()
        {
            using var serviceProvider = BuildServices();
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            var result = await domain.SendAsync(new SignCommand { Phone = "+1" }, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
        }

        public class SignCommand : ICommand
        {
            public string Phone { get; set; }
        }

        public class SignCommandHandler : ICommandHandler<SignCommand>
        {
            public Task<Result> HandleAsync(SignCommand command, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Result.Success());
            }
        }

        public class SignCommandValidator : AbstractValidator<SignCommand>
        {
            public SignCommandValidator()
            {
                RuleFor(command => command.Phone).NotEmpty().WithMessage("Phone is required.");
            }
        }
    }
}
