using FluentValidation;

namespace GRD.SpChn.Inventory.Application.Stock.SetStock;

public sealed class SetStockCommandValidator : AbstractValidator<SetStockCommand>
{
    public SetStockCommandValidator()
    {
        RuleFor(command => command.ProductId).NotEmpty();
        RuleFor(command => command.AvailableQuantity).GreaterThanOrEqualTo(0);
    }
}
