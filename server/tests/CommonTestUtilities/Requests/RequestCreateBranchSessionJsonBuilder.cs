using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestCreateBranchSessionJsonBuilder
{
    private Guid _branchId = Guid.NewGuid();

    public RequestCreateBranchSessionJsonBuilder WithBranchId(Guid branchId)
    {
        _branchId = branchId;
        return this;
    }

    public RequestCreateBranchSessionJson Build()
    {
        return new RequestCreateBranchSessionJson
        {
            BranchId = _branchId
        };
    }
}
