# BrandUp.Core

Base framework for .NET development.

* Universal result structure
* CQRS infrastructure

## Commands

```

public class SignUpCommandHandler : ICommandHandler<SignUpCommand, SignUpResult>
{
    readonly IUserRepository userRepository;

    public SignUpCommandHandler(IUserRepository userRepository)
    {
        this.userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<SignUpResult> HandleAsync(SignUpCommand command, CancellationToken cancelationToken = default)
    {
        var user = new User
            {
                Id = System.Guid.NewGuid(),
                Phone = command.Phone
            };

        await userRepository.CreateAsync(user, cancelationToken);

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

serviceCollection.AddCQRS(options =>
    {
        options.AddCommand<SignUpCommandHandler>();
        options.AddCommand<VisitUserCommandHandler>();
        options.AddCommand<ChangeUserPhoneCommandHandler>();
    })
    .AddValidator<ComponentModelCommandValidator>();

var domain = serviceProvider.GetRequiredService<IDomain>();
var signUpResult = await domain.SendAsync(new Example.Commands.SignUpCommandHandler { Phone = "+79231145449" });

// signUpResult.IsSuccess
// signUpResult.Errors
// signUpResult.Data

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