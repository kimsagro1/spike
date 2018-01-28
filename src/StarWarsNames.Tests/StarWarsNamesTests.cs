using System.Linq;
using Xunit;
using StarWarsNames;

namespace StarWarsNames.Tests
{
    public class StarWarsNamesTests
    {
        [Fact]
        public void AllShouldContainLukeSkywalker()
        {
            Assert.Contains("Luke Skywalker", StarWarsNames.All());
        }
    }
}
