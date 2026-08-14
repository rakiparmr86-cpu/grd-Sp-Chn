using FluentValidation;

namespace GRD.SpChn.OrderManagement.Application.Orders.CreateOrder;

public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(command => command.CustomerId).NotEmpty();
        RuleFor(command => command.Items).NotEmpty();
        RuleForEach(command => command.Items).ChildRules(item =>
        {
            item.RuleFor(value => value.ProductId).NotEmpty();
            item.RuleFor(value => value.Quantity).GreaterThan(0);
        });
        RuleFor(command => command.Items)
            .Must(items => items is not null &&
                items.Select(item => item.ProductId).Distinct().Count() == items.Count)
            .WithMessage("An order cannot contain duplicate product ids.");
    }
}
