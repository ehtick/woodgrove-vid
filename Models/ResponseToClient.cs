

using System.Text.Json;
using System.Text.Json.Serialization;

namespace WoodgroveDemo.Models;

public class ResponseToClient
{
    public string RequestPayload { get; set; } = string.Empty;
    public string ResponseBody { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorUserMessage { get; set; } = string.Empty;
    public string PinCode { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;


    /// <summary>
    /// Serialize this object into a string
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Serialize this object into HTML string
    /// </summary>
    /// <returns></returns>
    public string ToHtml()
    {
        return this.ToString().Replace("\r\n", "<br>").Replace(" ", "&nbsp;");
    }

    /// <summary>
    /// Deserialize a JSON string into PresentationResponse object
    /// </summary>
    /// <param name="JsonString">The JSON string to be loaded</param>
    /// <returns></returns>
    public static ResponseToClient Parse(string JsonString)
    {
        return JsonSerializer.Deserialize<ResponseToClient>(JsonString);
    }
}
