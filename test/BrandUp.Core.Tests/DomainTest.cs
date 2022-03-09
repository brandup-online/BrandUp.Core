using BrandUp;
using BrandUp.Commands.Validation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrandUp.Tests
{
    public class DomainTest
    {
        [Fact]
        public async void SendAsync_NotResult()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddCQRS(builder =>
            {
                builder.AddCommand<Example.Commands.VisitUserCommandHandler>();
            })
                .AddValidator<ComponentModelCommandValidator>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var joinUserResult = await domain.SendAsync(new Example.Commands.VisitUserCommand { Phone = "+79231145449" });

            Assert.True(joinUserResult.IsSuccess);
        }

        [Fact]
        public async void SendAsync_WithResult()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddCQRS(builder =>
                {
                    builder.AddCommand<Example.Commands.JoinUserCommandHandler>();
                })
                .AddValidator<ComponentModelCommandValidator>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var joinUserResult = await domain.SendAsync(new Example.Commands.JoinUserCommand { Phone = "+79231145449" });

            Assert.True(joinUserResult.IsSuccess);
            Assert.NotNull(joinUserResult.Data.User);
            Assert.Equal("+79231145449", joinUserResult.Data.User.Phone);
        }

        [Fact]
        public async void SendAsync_CommandInvalid()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddCQRS(builder =>
            {
                builder.AddCommand<Example.Commands.JoinUserCommandHandler>();
            })
                .AddValidator<ComponentModelCommandValidator>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var joinUserResult = await domain.SendAsync(new Example.Commands.JoinUserCommand());

            Assert.False(joinUserResult.IsSuccess);
        }
    }
}