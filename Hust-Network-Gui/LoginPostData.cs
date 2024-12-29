using System.Text.Json.Serialization;

namespace HustNetworkGui;

[JsonSerializable(typeof(LoginPostData))]
internal partial class SourceGenerationContext : JsonSerializerContext;

public class LoginPostData
{
    public string service { get; set; } = "";
    public string operatorPwd { get; set; } = "";
    public string operatorUserId { get; set; } = "";
    public string validcode { get; set; } = "";
    public bool passwordEncrypt { get; set; } = true;
    public string queryString { get; set; } = "";
    public string userId { get; set; } = "";
    public string password { get; set; } = "";
}