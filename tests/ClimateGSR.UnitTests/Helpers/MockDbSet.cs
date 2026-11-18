using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Moq;

namespace ClimateGSR.UnitTests.Helpers
{
    /// <summary>
    /// Builds an in-memory EF6 DbSet backed by LINQ-to-Objects, so services that
    /// query ClimateGSRDbContext can be tested without a database.
    /// </summary>
    internal static class MockDbSet
    {
        public static DbSet<T> Of<T>(IEnumerable<T> items) where T : class
        {
            var data = items.ToList().AsQueryable();
            var mock = new Mock<DbSet<T>>();

            mock.As<IQueryable<T>>().Setup(m => m.Provider).Returns(data.Provider);
            mock.As<IQueryable<T>>().Setup(m => m.Expression).Returns(data.Expression);
            mock.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(data.ElementType);
            mock.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => data.GetEnumerator());

            // Include(...) and AsNoTracking() are no-ops in memory
            mock.Setup(m => m.Include(It.IsAny<string>())).Returns(mock.Object);
            mock.Setup(m => m.AsNoTracking()).Returns(mock.Object);

            return mock.Object;
        }
    }
}
