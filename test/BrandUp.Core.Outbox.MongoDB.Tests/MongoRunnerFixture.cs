using System;
using System.Threading;
using EphemeralMongo;
using MongoDB.Driver;
using Xunit;

namespace BrandUp
{
    /// <summary>
    /// One mongod for the whole test collection, started as a single-node replica set so
    /// transactions work; each test takes its own database. Tests skip instead of failing when
    /// EphemeralMongo cannot start mongod in this environment (e.g. a CI agent whose CPU lacks
    /// AVX, required by MongoDB 5.0+, or no binary download access).
    /// </summary>
    public sealed class MongoRunnerFixture : IDisposable
    {
        readonly IMongoRunner runner;
        readonly MongoClient client;
        readonly Exception startException;
        int databaseIndex;

        public MongoRunnerFixture()
        {
            try
            {
                runner = MongoRunner.Run(new MongoRunnerOptions
                {
                    UseSingleNodeReplicaSet = true
                });
                client = new MongoClient(runner.ConnectionString);
            }
            catch (Exception exception)
            {
                startException = exception;
            }
        }

        public MongoClient ClientOrSkip()
        {
            SkipIfUnavailable();
            return client;
        }

        public IMongoDatabase GetDatabaseOrSkip()
        {
            SkipIfUnavailable();
            return client.GetDatabase($"outbox_tests_{Interlocked.Increment(ref databaseIndex)}");
        }

        void SkipIfUnavailable()
        {
            if (startException != null)
                Assert.Skip($"EphemeralMongo could not start mongod in this environment: {startException.Message}");
        }

        public void Dispose()
        {
            runner?.Dispose();
        }
    }

    [CollectionDefinition("mongo")]
    public sealed class MongoCollection : ICollectionFixture<MongoRunnerFixture>
    {
    }
}
