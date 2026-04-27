using Bogus;
using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestCancelTransactionJsonBuilder
{
    private readonly Faker _faker = new("pt_BR");
    private string _cancellationReason;

    public RequestCancelTransactionJsonBuilder()
    {
        _cancellationReason = _faker.Lorem.Sentence(8);
    }

    public RequestCancelTransactionJsonBuilder WithCancellationReason(string cancellationReason)
    {
        _cancellationReason = cancellationReason;
        return this;
    }

    public RequestCancelTransactionJson Build()
    {
        return new RequestCancelTransactionJson
        {
            CancellationReason = _cancellationReason
        };
    }
}
