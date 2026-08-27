namespace GRD.SpChn.Identity.Application.Abstractions;

public interface IPasswordVerifier
{
    bool Verify(string password, string encodedHash);
}
