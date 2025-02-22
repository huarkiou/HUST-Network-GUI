using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Serilog;

namespace HustNetworkGui;

public partial class HustNetworkController(string? username, string? password)
{
    private readonly HttpClient _client = new(new SocketsHttpHandler
        { AllowAutoRedirect = true, MaxAutomaticRedirections = 3, UseCookies = true, UseProxy = false });

    public string? Username { get; set; } = username;
    public string? Password { get; set; } = password;

    public Uri? GetVerificationUrl()
    {
        return GetVerificationUrlAsync().Result;
    }

    private async Task<Uri?> GetVerificationUrlAsync()
    {
        Uri[] urls =
        [
            new("http://connect.rom.miui.com/generate_204"),
            new("http://connectivitycheck.platform.hicloud.com/generate_204"),
            new("http://wifi.vivo.com.cn/generate_204")
        ];

        var tasks = urls.Select(url => _client.GetAsync(url)).ToList();

        List<Exception> exceptions = [];

        while (tasks.Count > 0)
        {
            var task = await Task.WhenAny(tasks);
            tasks.Remove(task);

            if (task.IsCompletedSuccessfully)
            {
                var responseMessage = await task;
                if (responseMessage.IsSuccessStatusCode)
                {
                    var content = await responseMessage.Content.ReadAsStringAsync();
                    content = content.Trim();

                    if (content.Length > 0)
                    {
                        var resMatch = RegexMatchVerificationUrl().Match(content);
                        if (resMatch.Success)
                        {
                            Log.Debug("Get from {}. The response is {}",
                                responseMessage.RequestMessage?.RequestUri?.AbsoluteUri,
                                resMatch.Groups[1].Value);
                            return new Uri(resMatch.Groups[1].Value);
                        }
                    }
                }
            }
            else if (task.Exception != null)
            {
                exceptions.Add(task.Exception);
            }
        }

        if (tasks.Count == 0 && exceptions.Count > 0)
        {
            Log.Information(new AggregateException(exceptions), "Cannot fetching verification URL");
        }

        return null;
    }

    private (BigInteger, BigInteger) GetModExpFromPageinfo(Uri originalUrl)
    {
        _client.DefaultRequestHeaders.Add("Accept", "*/*");
        _client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
        _client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36 Edg/123.0.0.0");
        _client.DefaultRequestHeaders.Add("Origin", originalUrl.GetLeftPart(UriPartial.Authority));
        _client.DefaultRequestHeaders.Referrer = originalUrl;

        var pageInfoUrl = originalUrl.GetLeftPart(UriPartial.Authority);
        pageInfoUrl += "/eportal/InterFace.do" + "?method=pageInfo";
        Log.Debug(pageInfoUrl);

        HttpContent content =
            new FormUrlEncodedContent([new KeyValuePair<string, string>("queryString", "method=pageInfo")]);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        var task = _client.PostAsync(new Uri(pageInfoUrl), content);
        task.Wait();
        var response = task.Result;
        response.EnsureSuccessStatusCode();
        var resultJsonString = response.Content.ReadAsStringAsync().Result;
        Log.Debug(resultJsonString);
        var node = JsonNode.Parse(resultJsonString)!;
        var mod = BigInteger.Parse("0" + node["publicKeyModulus"]!, NumberStyles.HexNumber);
        var exp = BigInteger.Parse("0" + node["publicKeyExponent"]!, NumberStyles.HexNumber);
        return (mod, exp);
    }

    public bool SendLoginRequest(Uri originalUrl)
    {
        if (Username == null || Password == null)
        {
            return false;
        }

        // 加密密码
        var (modulus, exponent) = GetModExpFromPageinfo(originalUrl);
        var macstringMatch = RegexMatchMacString().Match(originalUrl.ToString());
        string macstring = macstringMatch.Success ? macstringMatch.Groups[1].Value : "111111111";
        string passwordEncode = Password.Trim() + ">" + macstring;
        string passwordEncrypt = RsaNoPadding(passwordEncode, modulus, exponent);

        // 发送请求
        var loginUrl = originalUrl.GetLeftPart(UriPartial.Authority);
        loginUrl = loginUrl.TrimEnd('/') + "/eportal/InterFace.do?method=login";

        Log.Debug(loginUrl);

        HttpContent content =
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "service", "" },
                { "operatorPwd", "" },
                { "operatorUserId", "" },
                { "validcode", "" },
                { "passwordEncrypt", "true" },
                { "queryString", HttpUtility.UrlEncode(originalUrl.Query.TrimStart('?')) },
                { "userId", Username.Trim() },
                { "password", passwordEncrypt.Trim() },
            });
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        var task = _client.PostAsync(new Uri(loginUrl), content);
        task.Wait();
        var response = task.Result;
        response.EnsureSuccessStatusCode();
        var resultJsonString = response.Content.ReadAsStringAsync().Result;
        Log.Debug(resultJsonString);
        var node = JsonNode.Parse(resultJsonString)!;
        return node["result"]!.ToString() == "success";
    }

    public static bool CheckInternetAccess()
    {
        return Ping("hust.edu.cn") || Ping("8.8.8.8");
    }

    private static bool Ping(string host)
    {
        var p1 = new Ping();
        var reply = p1.Send(System.Net.Dns.GetHostEntry(host).AddressList.First(), TimeSpan.FromMilliseconds(1000),
            null,
            null);
        p1.Dispose();
        Log.Debug($"Ping {reply.Address} with RTT {reply.RoundtripTime} ms");
        return reply.Status == IPStatus.Success;
    }

    ~HustNetworkController()
    {
        _client.Dispose();
    }

    private static string RsaNoPadding(string text, BigInteger modulus, BigInteger exponent)
    {
        // 字符串转换为bytes
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        // 将字节转化成数字
        var inputNr = new BigInteger(bytes, isBigEndian: true, isUnsigned: true);
        // 计算x的y次方，如果z在存在，则再对结果进行取模，其结果等效于pow(x,y) %z
        var cryptNr = BigInteger.ModPow(inputNr, exponent, modulus);
        // 取模数的比特长度(二进制长度)，除以8将比特转为字节
        var length = (long)Math.Ceiling(modulus.GetBitLength() / 8.0);
        // 将密文转换为bytes存储，尾部补0
        var cryptData = cryptNr.ToByteArray(isBigEndian: true, isUnsigned: true);
        var cryptDataPad = new byte[length];
        cryptData.CopyTo(cryptDataPad, 0);
        // 转换为十六进制字符串
        return Convert.ToHexString(cryptDataPad).ToLower();
    }

    [GeneratedRegex("top.self.location.href='(.*)'")]
    private static partial Regex RegexMatchVerificationUrl();

    [GeneratedRegex(@"mac=(\w+)&")]
    private static partial Regex RegexMatchMacString();
}