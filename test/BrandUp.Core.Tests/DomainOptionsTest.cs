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
    }
}
