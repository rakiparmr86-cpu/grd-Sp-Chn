namespace GRD.SpChn.Identity.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
}
