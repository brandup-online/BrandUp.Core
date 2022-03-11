using System;
using Xunit;

namespace BrandUp
{
    public class DomainOptionsTest
    {
        [Fact]
        public void AddQuery()
        {
            var options = new DomainOptions();

            Assert.Throws<InvalidOperationException>(() => {
                options.AddQuery<string>();
            });
        }

        [Fact]
        public void AddCommand()
        {
            var options = new DomainOptions();

            Assert.Throws<InvalidOperationException>(() => {
                options.AddCommand<string>();
            });
        }
    }
}
