using System;
using Xunit;

namespace BrandUp
{
    public class DomainOptionsTest
    {
        [Fact]
        public void AddQuery_Invalid()
        {
            var options = new DomainOptions();

            Assert.Throws<InvalidOperationException>(() => {
                options.AddQuery<string>();
            });
        }

        [Fact]
        public void AddCommand_Invalid()
        {
            var options = new DomainOptions();

            Assert.Throws<InvalidOperationException>(() => {
                options.AddCommand<string>();
            });
        }

        [Fact]
        public void AddCommand_DuplicateCommandType_Throws()
        {
            var options = new DomainOptions();
            options.AddCommand<Example.Commands.CounterCommandHandler>();

            Assert.Throws<InvalidOperationException>(() => {
                options.AddCommand<Example.Commands.AnotherCounterCommandHandler>();
            });
        }

        [Fact]
        public void AddQuery_DuplicateQueryType_Throws()
        {
            var options = new DomainOptions();
            options.AddQuery<Example.Queries.UserByPhoneQueryHandler>();

            Assert.Throws<InvalidOperationException>(() => {
                options.AddQuery<Example.Queries.AnotherUserByPhoneQueryHandler>();
            });
        }
    }
}
