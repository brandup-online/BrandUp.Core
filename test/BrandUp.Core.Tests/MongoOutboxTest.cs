using System;
using System.Linq;
using BrandUp.Events;
using BrandUp.Events.MongoDB;
using BrandUp.Example.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace BrandUp
{
    public class MongoOutboxTest
    {
        [Fact]
        public void Serializer_RoundTripsEventWithRuntimeType()
        {
            var serializer = new JsonOutboxEventSerializer();
            var @event = new UserJoined { Phone = "+79231145449" };

            var typeName = serializer.GetTypeName(@event);
            var payload = serializer.Serialize(@event);
            var restored = serializer.Deserialize(typeName, payload);

            var joined = Assert.IsType<UserJoined>(restored);
            Assert.Equal("+79231145449", joined.Phone);
        }

        [Fact]
        public void Serializer_UnresolvableType_ReturnsNull()
        {
            var serializer = new JsonOutboxEventSerializer();

            Assert.Null(serializer.Deserialize("No.Such.Type, No.Such.Assembly", "{}"));
        }

        [Fact]
        public void AddMongoEventOutbox_RegistersStoreProcessorAndOptIn()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain()
                .AddMongoEventOutbox(options => options.CollectionName = "events.outbox");

            Assert.Contains(serviceCollection, descriptor => descriptor.ServiceType == typeof(MongoEventOutbox) && descriptor.Lifetime == ServiceLifetime.Scoped);
            Assert.Contains(serviceCollection, descriptor => descriptor.ServiceType == typeof(IEventOutbox) && descriptor.Lifetime == ServiceLifetime.Scoped);
            Assert.Contains(serviceCollection, descriptor => descriptor.ServiceType == typeof(IOutboxEventSerializer));
            Assert.Contains(serviceCollection, descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(MongoOutboxProcessor));

            using var serviceProvider = serviceCollection.BuildServiceProvider();
            Assert.Equal("events.outbox", serviceProvider.GetRequiredService<IOptions<MongoOutboxOptions>>().Value.CollectionName);
        }
    }
}
