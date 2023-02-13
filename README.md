# BrandUp.Core

Base framework for .NET development.

* Universal result structure
* CQRS infrastructure

## Qieries

```

public class UserByPhoneQuery : IQuery<User>
{
    [Required]
    public string Phone { get; set; }
}

public class UserByPhoneQueryHandler : IQueryHandler<UserByPhoneQuery, User>
{
    public Task<IList<User>> HandleAsync(UserByPhoneQuery query, CancellationToken cancellationToken = default)
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

serviceCollection.AddDomain(options =>
    {
        options.AddQuery<UserByPhoneQuery>();
    })
    .AddValidator<ComponentModelValidator>();

var userByPhoneResult = await domain.ReadAsync(new UserByPhoneQuery { Phone = "89232229022" });

// userByPhoneResult.IsSuccess
// userByPhoneResult.Errors
// userByPhoneResult.Data
// userByPhoneResult.Data[0]

```

## Commands

```

public class SignUpCommandHandler : ICommandHandler<SignUpCommand, SignUpResult>
{
    readonly IUserRepository userRepository;

    public SignUpCommandHandler(IUserRepository userRepository)
    {
        this.userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<SignUpResult> HandleAsync(SignUpCommand command, CancellationToken cancellationToken = default)
    {
        var user = new User
            {
                Id = System.Guid.NewGuid(),
                Phone = command.Phone
            };

        await userRepository.CreateAsync(user, cancellationToken);

        var result = new SignUpResult
        {
            User = user
        };

        return result;
    }
}

public class SignUpCommand : ICommand<SignUpResult>
{
    [Required]
    public string Phone { get; set; }
}

public class SignUpResult
{
    public User User { get; set; }
}

serviceCollection.AddDomain(options =>
    {
        options.AddCommand<SignUpCommandHandler>();
        options.AddCommand<VisitUserCommandHandler>();
        options.AddCommand<ChangeUserPhoneCommandHandler>();
    })
    .AddValidator<ComponentModelValidator>();

var domain = serviceProvider.GetRequiredService<IDomain>();
var signUpResult = await domain.SendAsync(new Example.Commands.SignUpCommandHandler { Phone = "+79231145449" });

// signUpResult.IsSuccess
// signUpResult.Errors
// signUpResult.Data

```

## Items

```

public class User : IItem<Guid>
{
    public Guid Id { get; set; }
    public string Phone { get; set; }
}

public class UserProvider : IItemProvider<Guid, User>
{
    public Task<User> FindByIdASync(Guid itemId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new User { Id = new Guid("0b1eb946-c07c-406c-a7ba-d56b007c830a"), Phone = "79232229022" });
    }
}

public class VisitUserCommand : IItemCommand<Items.User> { }

public class VisitUserCommandHandler : IItemCommandHandler<Items.User, VisitUserCommand>
{
    public Task<Result> HandleAsync(Items.User item, VisitUserCommand command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }
}

serviceCollection.AddDomain(options =>
    {
        options.AddCommand<Example.Commands.VisitUserCommandHandler>();
    })
    .AddItemProvider<UserProvider>();

var domain = serviceProvider.GetRequiredService<IDomain>();
var item = await domain.FindItem<Guid, User>(new Guid("0b1eb946-c07c-406c-a7ba-d56b007c830a"));

var userProvider1 = serviceProvider.GetService<UserProvider>();
var userProvider2 = serviceProvider.GetService<IItemProvider<Guid, User>>();

var result = await domain.SendItemAsync(new Guid("0b1eb946-c07c-406c-a7ba-d56b007c830a"), new Example.Commands.VisitUserCommand());
var result = await domain.SendItemAsync(item, new Example.Commands.VisitUserCommand());

```

## Results

```

Result.Success()
Result.Success<TData>(data)
Result.Error("code", "message")
Result.Error<TData>("code", "message")
Result.Error(...errors)
Result.Error<TData>(...errors)

// result.IsSuccess
// result.Errors
// result.Data

```