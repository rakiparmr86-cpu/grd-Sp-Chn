using FluentValidation;

namespace GRD.SpChn.Inventory.Application.Stock.ReserveStock;

public sealed class ReserveStockCommandValidator : AbstractValidator<ReserveStockCommand>
{
    public ReserveStockCommandValidator()
    {
        RuleFor(command => command.EventId).NotEmpty();
        RuleFor(command => command.OrderId).NotEmpty();
        RuleFor(command => command.Items).NotEmpty();
        RuleForEach(command => command.Items).ChildRules(item =>
        {
            item.RuleFor(value => value.ProductId).NotEmpty();
            item.RuleFor(value => value.Quantity).GreaterThan(0);
        });
        RuleFor(command => command.Items)
            .Must(items => items is not null &&
                items.Select(item => item.ProductId).Distinct().Count() == items.Count)
            .WithMessage("A reservation cannot contain duplicate product ids.");
    }
}
