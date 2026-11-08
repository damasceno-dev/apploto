using server.Application.Services.Reports;
using server.Communication.Responses;
using Shouldly;
using Xunit;
namespace UseCases.Test.Services.Reports;

public class EmployeeStatementPrintRendererTest
{
    [Fact]
    public void Print_EncodesEmployeeAndEvidence_AndKeepsUnknownDistinctFromZero()
    {
        var statement = new ResponseEmployeeStatementJson
        {
            OperatorName = "Pessoa <script>alert('x')</script>",
            DateFrom = new DateTime(2026, 9, 1),
            DateTo = new DateTime(2026, 9, 30),
            AsOfDate = new DateTime(2026, 9, 30),
            PayItems = [new ResponsePayItemJson { Reason = "<img src=x onerror=alert(1)>", SourceDate = new DateTime(2026, 9, 1), PostedOn = new DateTime(2026, 9, 1) }]
        };
        var html = EmployeeStatementPrintRenderer.Render(statement);
        html.ShouldNotContain("<script>"); html.ShouldNotContain("<img src=x"); html.ShouldContain("&lt;script&gt;");
        html.ShouldContain("Não comprovado"); html.ShouldContain("0,00"); html.ShouldContain("Não comprova pagamento");
        html.ShouldContain("lang=\"pt-BR\"");
    }
    [Fact]
    public void Print_RefusesIncompletePersonalDifferenceDetail()
    {
        var statement = new ResponseEmployeeStatementJson { Differences = new ResponsePersonalDifferencesJson { TotalCount = 10001 } };
        Should.Throw<server.Exceptions.Exceptions.ConflictException>(() => EmployeeStatementPrintRenderer.Render(statement));
    }
    [Fact]
    public void Print_ExplainsNegativeUndistributedPostingMovement()
    {
        var statement = new ResponseEmployeeStatementJson { SpecialDraws = new ResponseSpecialDrawTotalsJson { SourceFeeTotal = 0, AllocatedFeeTotal = 5, UnallocatedFeeTotal = -5 } };
        var html = EmployeeStatementPrintRenderer.Render(statement);
        html.ShouldContain("Saldo sem distribuição negativo");
        html.ShouldContain("tarifas publicadas em outro mês");
        html.ShouldContain("-5,00");
    }
}
