using BrandUp.Example.Items;
using BrandUp.Queries;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace BrandUp.Example.Queries
{
    public class UserByPhoneQuery : IQuery<User>
    {
        [Required]
        public string Phone { get; set; }
    }

    public class UserByPhoneQueryHandler : IQueryHandler<UserByPhoneQuery, User>
    {
        public Task<IList<User>> HandleAsync(UserByPhoneQuery query, CancellationToken cancelationToken = default)
        {
            var result = new List<User>
            {
                new User
                {
                    Id = System.Guid.NewGuid(),
                    Phone = query.Phone
                }
            };

            return Task.FromResult<IList<User>>(result);
        }
    }
}