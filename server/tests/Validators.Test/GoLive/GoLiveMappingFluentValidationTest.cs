using server.Application.UseCases.GoLive.Imports;
using server.Communication.Requests;
using Shouldly;
using Xunit;

namespace Validators.Test.GoLive;

public class GoLiveMappingFluentValidationTest
{
    [Theory]
    [InlineData("TblUsuario","OpeningFiado",false)]
    [InlineData("TblCategoria","Transaction",false)]
    [InlineData("TblConfiguracao","Exclude",false)]
    [InlineData("TblRegistroPonto","HourOpening",false)]
    [InlineData("TblLancamentos","OpeningFiado",true)]
    public void A_catalog_or_attendance_row_cannot_silently_claim_an_unrelated_operational_action(string table,string action,bool valid)
    {
        var request=new RequestGoLiveMappingJson {Resolutions=[new(){Table=table,SourceId="1",Action=action,ExclusionDisposition="OutsideScope",Reason="Explicit synthetic reviewed scope"}]};
        new GoLiveMappingFluentValidation().Validate(request).IsValid.ShouldBe(valid);
    }
}
