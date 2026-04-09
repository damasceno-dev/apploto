using server.Application.Services;

namespace CommonTestUtilities.Services;

public static class PasswordEncryptionBuilder
{
    public static PasswordEncryption Build()
    {
        return new PasswordEncryption();
    }
}
