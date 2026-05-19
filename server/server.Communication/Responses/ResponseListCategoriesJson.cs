namespace server.Communication.Responses;

public class ResponseListCategoriesJson
{
    public IReadOnlyList<ResponseCategoryJson> Items { get; set; } = [];
}
