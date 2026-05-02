using Bogus;
using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestRejectDailyCloseJsonBuilder
{
    private readonly Faker _faker = new("pt_BR");
    private string _rejectionReason;

    public RequestRejectDailyCloseJsonBuilder()
    {
        _rejectionReason = _faker.Lorem.Sentence(8);
    }

    public RequestRejectDailyCloseJsonBuilder WithRejectionReason(string rejectionReason)
    {
        _rejectionReason = rejectionReason;
        return this;
    }

    public RequestRejectDailyCloseJson Build()
    {
        return new RequestRejectDailyCloseJson
        {
            RejectionReason = _rejectionReason
        };
    }
}
