using server.Application.UseCases.People;
using server.Communication.Requests;
using Shouldly;
using Xunit;
namespace Validators.Test.People;

public class PeopleSourceFluentValidationTest
{
    private static readonly DateTime Day=new(2026,9,1);
    private static RequestSpecialDrawJson Draw()=>new(){Name="Sorteio",Modality="Modalidade",Contest="Concurso 1",Prize=0,PeriodFrom=Day,PeriodTo=Day,PostedOn=Day,Reason="Configuração explícita",Evidence="Documento sintético"};
    [Fact]
    public void EvidencedDraw_WithExplicitZeroPrize_IsValid()=>new SpecialDrawFluentValidation().Validate(Draw()).IsValid.ShouldBeTrue();
    [Theory]
    [InlineData("Name")][InlineData("Modality")][InlineData("Contest")]
    public void Draw_MissingIdentityField_IsRejected(string field)
    {
        var request=Draw();switch(field){case "Name":request.Name=" ";break;case "Modality":request.Modality=" ";break;default:request.Contest=" ";break;}
        var result=new SpecialDrawFluentValidation().Validate(request);result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe(field);
    }
    [Fact]
    public void Draw_UnsupportedMoneyPrecision_IsRejected()
    {
        var request=Draw();request.Prize=.001m;new SpecialDrawFluentValidation().Validate(request).IsValid.ShouldBeFalse();
    }
    [Fact]
    public void ExplicitCommissionSource_RequiresSupportedSourceRole()
    {
        var request=new RequestCommissionSourceJson{OperatorId=Guid.NewGuid(),SourceDate=Day,PostedOn=Day,SignedAmount=0,SourceRole="EnteredPrize",ExternalIdentity="fixture-zero",Reason="Zero explicitamente comprovado",Evidence="Documento sintético"};
        new CommissionSourceFluentValidation().Validate(request).IsValid.ShouldBeTrue();
        request.SourceRole="product-display-name";
        var result=new CommissionSourceFluentValidation().Validate(request);result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe("SourceRole");
    }
    [Fact]
    public void Allocation_RequiresExactSourceRevisionAndPositivePrecision()
    {
        var request=new RequestSpecialDrawAllocationJson{DrawId=Guid.NewGuid(),SessionId=Guid.NewGuid(),SessionFieldEffectId=Guid.NewGuid(),SourceRevision=1,SourceDate=Day,Amount=1,PostedOn=Day,Reason="Origem explícita",Evidence="Documento sintético"};
        new SpecialDrawAllocationFluentValidation().Validate(request).IsValid.ShouldBeTrue();
        request.SourceRevision=0;new SpecialDrawAllocationFluentValidation().Validate(request).Errors.ShouldHaveSingleItem().PropertyName.ShouldBe("SourceRevision");
        request.SourceRevision=1;request.Amount=.001m;new SpecialDrawAllocationFluentValidation().Validate(request).IsValid.ShouldBeFalse();
    }
}
